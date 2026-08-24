import { X509Certificate, createHash } from "node:crypto";
import { once } from "node:events";
import { readFile, rm } from "node:fs/promises";
import { spawn } from "node:child_process";
import path from "node:path";

const root = process.cwd();
const baseUrl = process.env.ANDREJA_EVIDENCE_URL ?? "https://localhost:8443";
const healthUrl = process.env.ANDREJA_EVIDENCE_HEALTH_URL
    ?? "http://127.0.0.1:18080/health/ready";
const edgePath = process.env.EDGE_PATH
    ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const certificatePath = process.env.ANDREJA_TLS_CERTIFICATE_FILE
    ?? path.join(root, ".andreja", "localhost.pem");
const bootstrapTokenPath = process.env.ANDREJA_BOOTSTRAP_TOKEN_FILE
    ?? path.join(root, "deploy", "secrets", "bootstrap_token");
const userDataPath = path.join(root, ".andreja", "edge-evidence-44");
const debuggingPort = 43000 + Math.floor(Math.random() * 1000);

class Cdp {
    constructor(url) {
        this.nextId = 1;
        this.pending = new Map();
        this.listeners = new Map();
        this.socket = new WebSocket(url);
        this.opened = new Promise((resolve, reject) => {
            this.socket.addEventListener("open", resolve, { once: true });
            this.socket.addEventListener("error", reject, { once: true });
        });
        this.socket.addEventListener("message", event => {
            const message = JSON.parse(event.data);
            if (!message.id) {
                for (const listener of this.listeners.get(message.method) ?? []) {
                    listener(message.params);
                }
                return;
            }
            const pending = this.pending.get(message.id);
            if (!pending) return;
            this.pending.delete(message.id);
            if (message.error) pending.reject(new Error(message.error.message));
            else pending.resolve(message.result);
        });
    }

    async send(method, params = {}) {
        await this.opened;
        const id = this.nextId++;
        const result = new Promise((resolve, reject) => {
            this.pending.set(id, { resolve, reject });
        });
        this.socket.send(JSON.stringify({ id, method, params }));
        return result;
    }

    close() {
        this.socket.close();
    }

    on(method, listener) {
        const listeners = this.listeners.get(method) ?? [];
        listeners.push(listener);
        this.listeners.set(method, listeners);
    }
}

const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function waitFor(expression, timeoutMilliseconds = 30000) {
    const deadline = Date.now() + timeoutMilliseconds;
    let last;
    while (Date.now() < deadline) {
        last = await evaluate(expression);
        if (last) return last;
        await sleep(100);
    }
    throw new Error(`Timed out waiting for browser condition; last=${JSON.stringify(last)}`);
}

let cdp;
const identityResponses = [];
async function evaluate(expression) {
    const result = await cdp.send("Runtime.evaluate", {
        expression,
        awaitPromise: true,
        returnByValue: true,
    });
    if (result.exceptionDetails) {
        throw new Error(result.exceptionDetails.text ?? "Browser evaluation failed.");
    }
    return result.result.value;
}

async function navigate(relativePath) {
    await cdp.send("Page.navigate", { url: new URL(relativePath, baseUrl).href });
    await waitFor("document.readyState === 'complete'");
}

async function setValue(selector, value) {
    await evaluate(`(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element) return false;
        const prototype = element instanceof HTMLTextAreaElement
            ? HTMLTextAreaElement.prototype
            : HTMLInputElement.prototype;
        Object.getOwnPropertyDescriptor(prototype, "value").set.call(
            element, ${JSON.stringify(value)});
        element.dispatchEvent(new InputEvent("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
        return true;
    })()`);
}

async function click(selector) {
    const clicked = await evaluate(`(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element || element.disabled) return false;
        element.click();
        return true;
    })()`);
    if (!clicked) throw new Error(`Unable to click ${selector}.`);
}

async function waitForIdentitySuccess(successExpression) {
    const outcome = await waitFor(`(() => {
        if (${successExpression}) return { success: true };
        const status = document.querySelector("[data-identity-status]");
        if (status?.classList.contains("error-message")) {
            return { success: false, error: status.textContent.trim() };
        }
        return null;
    })()`);
    if (!outcome.success) {
        throw new Error(
            `Identity flow failed: ${outcome.error}; responses=${JSON.stringify(identityResponses)}`);
    }
}

async function waitForHealth() {
    const deadline = Date.now() + 60000;
    while (Date.now() < deadline) {
        try {
            const response = await fetch(healthUrl);
            if (response.ok) return;
        } catch {
            // Expected while the application container is restarting.
        }
        await sleep(250);
    }
    throw new Error("Application readiness did not recover after restart.");
}

async function restartApplication() {
    const child = spawn(
        "docker",
        [
            "compose",
            "--file", "compose.yaml",
            "--file", "deploy\\compose.evidence.yaml",
            "restart", "app",
        ],
        { cwd: root, windowsHide: true, stdio: "ignore" });

    let reconnectObserved = false;
    while (child.exitCode === null) {
        reconnectObserved ||= Boolean(await evaluate(
            "document.querySelector('#components-reconnect-modal')?.open"));
        await sleep(100);
    }
    if (child.exitCode !== 0) throw new Error("Compose application restart failed.");
    await waitForHealth();
    await waitFor("!document.querySelector('#components-reconnect-modal')?.open", 60000);
    return reconnectObserved;
}

async function signOut() {
    if (!await evaluate("Boolean(document.querySelector(\"form[action='/Account/Logout']\"))")) {
        await navigate("/Account/Passkeys");
        await waitFor("document.querySelector(\"form[action='/Account/Logout']\")");
    }
    await click("form[action='/Account/Logout'] button");
    await waitFor("location.pathname === '/Account/Login'");
    await navigate("/Account/Login");
}

async function signIn() {
    await click("[data-passkey-signin] button[type='submit']");
    await waitForIdentitySuccess("location.pathname === '/'");
    await waitFor("document.querySelector('#page-title')");
}

async function viewportEvidence() {
    const results = [];
    for (const width of [320, 768, 1280]) {
        await cdp.send("Emulation.setDeviceMetricsOverride", {
            width,
            height: 900,
            deviceScaleFactor: 1,
            mobile: width === 320,
        });
        const result = await evaluate(`(() => {
            const main = document.querySelector("main[aria-labelledby]");
            const labelTarget = main
                ? document.getElementById(main.getAttribute("aria-labelledby"))
                : null;
            const unnamed = [...document.querySelectorAll("button, a[href], input, textarea")]
                .filter(element => {
                    const label = element.id
                        ? document.querySelector(\`label[for="\${CSS.escape(element.id)}"]\`)
                        : null;
                    return !element.textContent.trim()
                        && !element.getAttribute("aria-label")
                        && !label?.textContent.trim()
                        && element.type !== "hidden";
                }).length;
            return {
                width: innerWidth,
                noHorizontalOverflow: document.documentElement.scrollWidth <= innerWidth,
                labelledMain: Boolean(labelTarget),
                unnamedInteractiveControls: unnamed
            };
        })()`);
        if (!result.noHorizontalOverflow || !result.labelledMain
            || result.unnamedInteractiveControls !== 0) {
            throw new Error(`Viewport/accessibility check failed at ${width}px.`);
        }
        results.push(result);
    }
    await cdp.send("Emulation.clearDeviceMetricsOverride");
    return results;
}

async function keyboardEvidence() {
    await evaluate("document.body.focus()");
    const focusOrder = [];
    for (let index = 0; index < 6; index++) {
        await cdp.send("Input.dispatchKeyEvent", {
            type: "keyDown",
            key: "Tab",
            code: "Tab",
            windowsVirtualKeyCode: 9,
        });
        await cdp.send("Input.dispatchKeyEvent", {
            type: "keyUp",
            key: "Tab",
            code: "Tab",
            windowsVirtualKeyCode: 9,
        });
        focusOrder.push(await evaluate(
            "document.activeElement?.tagName + ':' + (document.activeElement?.type ?? '')"));
    }
    if (new Set(focusOrder).size < 3) {
        throw new Error("Keyboard focus did not traverse at least three controls.");
    }
    return focusOrder.length;
}

let edge;
try {
    const certificate = new X509Certificate(await readFile(certificatePath));
    const spki = certificate.publicKey.export({ type: "spki", format: "der" });
    const spkiPin = createHash("sha256").update(spki).digest("base64");
    await rm(userDataPath, { recursive: true, force: true });

    edge = spawn(edgePath, [
        "--headless=new",
        "--disable-background-networking",
        "--disable-component-update",
        "--disable-default-apps",
        "--disable-sync",
        "--metrics-recording-only",
        "--no-first-run",
        `--remote-debugging-port=${debuggingPort}`,
        `--user-data-dir=${userDataPath}`,
        `--ignore-certificate-errors-spki-list=${spkiPin}`,
        "about:blank",
    ], { windowsHide: true, stdio: "ignore" });

    let target;
    const deadline = Date.now() + 30000;
    while (Date.now() < deadline) {
        try {
            const response = await fetch(
                `http://127.0.0.1:${debuggingPort}/json/new?about:blank`,
                { method: "PUT" });
            if (response.ok) {
                target = await response.json();
                break;
            }
        } catch {
            // Browser has not opened the debugging socket yet.
        }
        await sleep(100);
    }
    if (!target) throw new Error("Edge DevTools endpoint did not become ready.");

    cdp = new Cdp(target.webSocketDebuggerUrl);
    await Promise.all([
        cdp.send("Page.enable"),
        cdp.send("Runtime.enable"),
        cdp.send("Network.enable"),
        cdp.send("WebAuthn.enable"),
    ]);
    cdp.on("Network.responseReceived", ({ response }) => {
        const url = new URL(response.url);
        if (url.pathname.startsWith("/Account/")) {
            identityResponses.push({ path: url.pathname, status: response.status });
        }
    });
    const { authenticatorId } = await cdp.send("WebAuthn.addVirtualAuthenticator", {
        options: {
            protocol: "ctap2",
            transport: "internal",
            hasResidentKey: true,
            hasUserVerification: true,
            isUserVerified: true,
            automaticPresenceSimulation: true,
        },
    });

    const bootstrapToken = (await readFile(bootstrapTokenPath, "utf8")).trim();
    await navigate("/Account/Bootstrap");
    await setValue("#tenant-name", "Evidence 44 workspace");
    await setValue("#user-display-name", "Evidence operator");
    await setValue("#bootstrap-token", bootstrapToken);
    await click("[data-passkey-bootstrap] button[type='submit']");
    await waitForIdentitySuccess(
        "!document.querySelector('[data-recovery-codes]').hidden");
    const recoveryCodes = await evaluate(
        "[...document.querySelectorAll('[data-recovery-code-list] code')].map(x => x.textContent)");
    if (!Array.isArray(recoveryCodes) || recoveryCodes.length === 0) {
        throw new Error("Bootstrap did not produce recovery codes.");
    }
    const bootstrapCredentials =
        (await cdp.send("WebAuthn.getCredentials", { authenticatorId })).credentials;
    await click("[data-recovery-confirm]");
    await click("[data-recovery-continue]");
    await waitFor("location.pathname === '/'");
    await waitFor("document.querySelector('#page-title')");

    await setValue("#task-request", "Evidence task 44 synthetic");
    await waitFor("!document.querySelector('.composer form button[type=\"submit\"]')?.disabled");
    await click(".composer form button[type='submit']");
    const proposalOutcome = await waitFor(`(() => {
        if (document.querySelector("#proposal-heading")) return { success: true };
        const error = document.querySelector(".error-message");
        return error ? { success: false, error: error.textContent.trim() } : null;
    })()`);
    if (!proposalOutcome.success) {
        throw new Error(`Task proposal failed: ${proposalOutcome.error}`);
    }
    await click(".proposal .button.primary");
    await waitFor("document.querySelectorAll('.task-item').length === 1");
    await click(".task-item .button.primary");
    await waitFor(
        "document.querySelector('.task-item .state-chip')?.textContent.trim() === 'Completed'");
    await click(".task-list > .section-heading .button.secondary");
    const exportCount = await waitFor(`(() => {
        const link = document.querySelector(".download-callout a[download]");
        if (!link) return 0;
        const payload = JSON.parse(decodeURIComponent(link.href.split(",")[1]));
        const tasks = payload.Tasks ?? payload.tasks;
        return Array.isArray(tasks) ? tasks.length : 0;
    })()`);
    if (exportCount !== 1) throw new Error("Task export did not contain one task.");

    const reconnectObserved = await restartApplication();
    await navigate("/");
    await waitFor(
        "document.querySelector('.task-item .state-chip')?.textContent.trim() === 'Completed'");
    await click(".task-item .button.secondary");
    await waitFor(`(() => {
        const button = document.querySelector(".task-item .button.danger");
        return Boolean(button && !button.disabled);
    })()`);
    await click(".task-item .button.danger");
    await waitFor("document.querySelectorAll('.task-item').length === 0");

    await signOut();
    await signIn();
    await signOut();

    for (const credential of bootstrapCredentials) {
        await cdp.send("WebAuthn.removeCredential", {
            authenticatorId,
            credentialId: credential.credentialId,
        });
    }
    await navigate("/Account/Recovery");
    await setValue("#recovery-code", recoveryCodes[0]);
    await click("[data-passkey-recovery] button[type='submit']");
    await waitForIdentitySuccess(
        "!document.querySelector('[data-recovery-codes]').hidden");
    const recoveredCredentials =
        (await cdp.send("WebAuthn.getCredentials", { authenticatorId })).credentials;
    await click("[data-recovery-confirm]");
    await click("[data-recovery-continue]");
    await waitFor("location.pathname === '/Account/Login'");
    await signIn();
    await signOut();

    await navigate("/Account/Recovery");
    await setValue("#recovery-code", recoveryCodes[0]);
    await click("[data-passkey-recovery] button[type='submit']");
    await waitFor(
        "document.querySelector('[data-identity-status]')?.classList.contains('error-message')");
    await navigate("/Account/Login");
    await signIn();

    const viewports = await viewportEvidence();
    const keyboardTabs = await keyboardEvidence();

    console.log(JSON.stringify({
        status: "passed",
        bootstrap: true,
        signIn: true,
        recovery: true,
        recoveryReplayRejected: true,
        taskLifecycle: {
            proposal: true,
            confirm: true,
            list: true,
            complete: true,
            export: true,
            delete: true,
        },
        restartPersistence: true,
        reconnectObserved,
        credentials: {
            afterBootstrap: bootstrapCredentials.length,
            afterRecovery: recoveredCredentials.length,
        },
        viewports,
        keyboardTabs,
    }));
} finally {
    cdp?.close();
    if (edge && edge.exitCode === null) {
        edge.kill();
        await Promise.race([once(edge, "exit"), sleep(5000)]);
    }
    for (let attempt = 0; attempt < 20; attempt++) {
        try {
            await rm(userDataPath, { recursive: true, force: true });
            break;
        } catch (error) {
            if (error.code !== "EBUSY" || attempt === 19) throw error;
            await sleep(250);
        }
    }
}
