import { spawn } from "node:child_process";
import { once } from "node:events";
import { createReadStream } from "node:fs";
import { readFile, rm } from "node:fs/promises";
import http from "node:http";
import path from "node:path";

const root = process.cwd();
const publicWebsiteRoot = path.join(root, "docs", "public-website");
const prototypeRoot = path.join(publicWebsiteRoot, "prototype");
const indexPath = path.join(prototypeRoot, "index.html");
const edgePath = process.env.EDGE_PATH
  ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const userDataPath = path.join(root, ".andreja", "edge-public-website");
const debuggingPort = 44000 + Math.floor(Math.random() * 1000);
const mimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".md", "text/markdown; charset=utf-8"],
]);

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
    const response = new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
    });
    this.socket.send(JSON.stringify({ id, method, params }));
    return response;
  }

  on(method, listener) {
    const listeners = this.listeners.get(method) ?? [];
    listeners.push(listener);
    this.listeners.set(method, listeners);
  }

  close() {
    this.socket.close();
  }
}

const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function waitForValue(action, timeoutMilliseconds = 15000) {
  const deadline = Date.now() + timeoutMilliseconds;
  let lastError;
  while (Date.now() < deadline) {
    try {
      const value = await action();
      if (value) return value;
    } catch (error) {
      lastError = error;
    }
    await sleep(100);
  }
  throw new Error(`Timed out waiting for browser state${lastError ? `: ${lastError.message}` : ""}.`);
}

function startServer() {
  const server = http.createServer((request, response) => {
    const url = new URL(request.url, "http://127.0.0.1");
    const relative = url.pathname.endsWith("/")
      ? `${url.pathname}index.html`
      : url.pathname;
    const requestedPath = path.resolve(publicWebsiteRoot, relative.replace(/^\/+/, ""));
    if (!requestedPath.startsWith(`${path.resolve(publicWebsiteRoot)}${path.sep}`)) {
      response.writeHead(403).end();
      return;
    }
    const extension = path.extname(requestedPath);
    response.setHeader("Content-Type", mimeTypes.get(extension) ?? "application/octet-stream");
    response.setHeader("X-Robots-Tag", "noindex, nofollow, noarchive");
    const stream = createReadStream(requestedPath);
    stream.on("error", () => response.writeHead(404).end());
    stream.pipe(response);
  });
  server.listen(0, "127.0.0.1");
  return once(server, "listening").then(() => server);
}

async function staticChecks() {
  const html = await readFile(indexPath, "utf8");
  const required = [
    '<html lang="en">',
    '<meta name="viewport"',
    '<meta name="robots" content="noindex, nofollow, noarchive">',
    'id="main-content"',
    'aria-labelledby="page-title"',
    'class="skip-link"',
    'id="site-search"',
    'role="status"',
    'prefers-reduced-motion',
    'forced-colors: active',
  ];
  for (const text of required) {
    if (!html.includes(text)) throw new Error(`Prototype is missing required markup: ${text}`);
  }
  if ((html.match(/<h1\b/g) ?? []).length !== 1) {
    throw new Error("Prototype must contain exactly one h1.");
  }
  if (/https?:\/\//i.test(html)) {
    throw new Error("Prototype must not contain remote HTTP(S) dependencies or links.");
  }
  if (/<form\b/i.test(html)) {
    throw new Error("Prototype must not contain a feedback or submission form.");
  }
}

let edge;
let cdp;
let server;
try {
  await staticChecks();
  server = await startServer();
  const { port } = server.address();
  const baseUrl = `http://127.0.0.1:${port}`;
  const pageUrl = `${baseUrl}/prototype/`;
  const requests = [];
  const linkedDocuments = [
    "/design-hosting-matrix.md",
    "/claims-inventory.md",
  ];
  for (const documentPath of linkedDocuments) {
    const response = await fetch(`${baseUrl}${documentPath}`);
    if (!response.ok) {
      throw new Error(`Prototype governance link failed: ${documentPath} returned ${response.status}.`);
    }
  }

  await rm(userDataPath, {
    recursive: true,
    force: true,
    maxRetries: 20,
    retryDelay: 100,
  });
  edge = spawn(edgePath, [
    "--headless=new",
    `--remote-debugging-port=${debuggingPort}`,
    `--user-data-dir=${userDataPath}`,
    "--disable-background-networking",
    "--disable-component-update",
    "--disable-sync",
    "--metrics-recording-only",
    "--no-first-run",
    `${pageUrl}?scoutTheme=dark`,
  ], { windowsHide: true, stdio: "ignore" });

  const target = await waitForValue(async () => {
    const response = await fetch(`http://127.0.0.1:${debuggingPort}/json/list`);
    if (!response.ok) return null;
    const targets = await response.json();
    return targets.find(item => item.type === "page" && item.url.startsWith(pageUrl));
  });

  cdp = new Cdp(target.webSocketDebuggerUrl);
  await cdp.send("Page.enable");
  await cdp.send("Runtime.enable");
  await cdp.send("Network.enable");
  cdp.on("Network.requestWillBeSent", event => requests.push(event.request.url));

  const evaluate = async expression => {
    const result = await cdp.send("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true,
    });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  };

  await waitForValue(() => evaluate("document.querySelector('#page-title') !== null"));
  const landmarks = await evaluate(`(() => ({
    theme: document.documentElement.dataset.theme,
    title: document.title,
    h1: document.querySelectorAll("h1").length,
    labelledMain: Boolean(document.querySelector("main[aria-labelledby='page-title']")),
    namedNavigation: Boolean(document.querySelector("nav[aria-label='Primary']")),
    searchLabel: document.querySelector("label[for='site-search']")?.textContent.trim(),
  }))()`);
  if (landmarks.theme !== "dark" || landmarks.h1 !== 1 || !landmarks.labelledMain
    || !landmarks.namedNavigation || !landmarks.searchLabel) {
    throw new Error(`Semantic landmark check failed: ${JSON.stringify(landmarks)}`);
  }

  const viewports = [];
  for (const width of [320, 768, 1280]) {
    await cdp.send("Emulation.setDeviceMetricsOverride", {
      width,
      height: 900,
      deviceScaleFactor: 1,
      mobile: width === 320,
    });
    const result = await evaluate(`(() => {
      const unnamed = [...document.querySelectorAll("button, a[href], input")]
        .filter(element => {
          const label = element.id
            ? document.querySelector(\`label[for="\${CSS.escape(element.id)}"]\`)
            : null;
          return !element.textContent.trim()
            && !element.getAttribute("aria-label")
            && !label?.textContent.trim();
        }).length;
      const undersizedControls = [...document.querySelectorAll("button, input")]
        .filter(element => {
          const box = element.getBoundingClientRect();
          return box.width > 0 && box.height > 0 && (box.width < 44 || box.height < 44);
        }).length;
      return {
        width: innerWidth,
        noHorizontalOverflow: document.documentElement.scrollWidth <= innerWidth,
        unnamedInteractiveControls: unnamed,
        undersizedControls,
      };
    })()`);
    if (!result.noHorizontalOverflow || result.unnamedInteractiveControls !== 0
      || result.undersizedControls !== 0) {
      throw new Error(`Viewport/accessibility check failed: ${JSON.stringify(result)}`);
    }
    viewports.push(result);
  }
  await cdp.send("Emulation.clearDeviceMetricsOverride");

  await sleep(250);
  const searchRequestStart = requests.length;
  const searchResult = await evaluate(`(() => {
    const input = document.querySelector("#site-search");
    input.value = "privacy";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
    return {
      count: document.querySelectorAll("#search-results li").length,
      status: document.querySelector("#search-status").textContent,
      first: document.querySelector("#search-results a")?.textContent,
    };
  })()`);
  if (searchResult.count < 1 || !searchResult.status.includes("result")) {
    throw new Error(`In-artifact search failed: ${JSON.stringify(searchResult)}`);
  }
  await sleep(250);
  const searchRequests = requests.slice(searchRequestStart);
  if (searchRequests.length > 0) {
    throw new Error(`Search caused network requests: ${JSON.stringify(searchRequests)}`);
  }

  await cdp.send("Page.navigate", { url: `${pageUrl}?scoutTheme=dark` });
  await waitForValue(() => evaluate("document.querySelector('#page-title') !== null"));
  await cdp.send("Input.dispatchKeyEvent", { type: "keyDown", key: "Tab", code: "Tab" });
  await cdp.send("Input.dispatchKeyEvent", { type: "keyUp", key: "Tab", code: "Tab" });
  const firstFocus = await evaluate("document.activeElement?.className");
  if (firstFocus !== "skip-link") {
    throw new Error(`Expected skip link to be first keyboard target; got ${firstFocus}.`);
  }

  const storage = await evaluate(`(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDatabases: typeof indexedDB.databases === "function"
      ? (await indexedDB.databases()).length
      : null,
    cacheEntries: "caches" in globalThis ? (await caches.keys()).length : null,
    serviceWorkerRegistrations: "serviceWorker" in navigator
      ? (await navigator.serviceWorker.getRegistrations()).length
      : null,
    serviceWorkerControlled: Boolean(navigator.serviceWorker?.controller),
  }))()`);
  const cookies = await cdp.send("Network.getCookies", { urls: [baseUrl] });
  if (storage.localStorage !== 0 || storage.sessionStorage !== 0
    || (storage.indexedDatabases ?? 0) !== 0
    || (storage.cacheEntries ?? 0) !== 0
    || (storage.serviceWorkerRegistrations ?? 0) !== 0
    || storage.serviceWorkerControlled || cookies.cookies.length !== 0) {
    throw new Error(`Browser storage check failed: ${JSON.stringify({ storage, cookies: cookies.cookies.length })}`);
  }

  await sleep(250);
  const unexpectedRequests = requests.filter(requestUrl => {
    const parsed = new URL(requestUrl);
    return parsed.origin !== baseUrl;
  });
  if (unexpectedRequests.length > 0) {
    throw new Error(`Cross-origin requests detected: ${JSON.stringify(unexpectedRequests)}`);
  }

  console.log(JSON.stringify({
    result: "PASS",
    staticChecks: true,
    linkedDocuments,
    landmarks,
    viewports,
    searchResult,
    storage,
    searchRequests: 0,
    crossOriginRequests: 0,
  }, null, 2));
} finally {
  cdp?.close();
  if (edge && edge.exitCode === null) edge.kill();
  if (edge) await Promise.race([once(edge, "exit"), sleep(3000)]);
  if (server) await new Promise(resolve => server.close(resolve));
  await rm(userDataPath, {
    recursive: true,
    force: true,
    maxRetries: 20,
    retryDelay: 100,
  });
}
