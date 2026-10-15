import { test, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync } from "node:fs";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { root } from "../evidence-fixture.mjs";

const projectName = `andreja-evidence-test-${process.pid}`;
const directory = path.join(root, ".andreja", "evidence", projectName);
mkdirSync(directory, { recursive: true });
after(() => rmSync(directory, { recursive: true, force: true }));
const file = name => path.join(directory, name);
for (const name of ["fixture.env", "certificate.pem", "bootstrap_token", "password", "key.pem"]) {
    writeFileSync(file(name), "synthetic-not-a-secret");
}
const config = {
    projectName, dockerContext: "synthetic-local", disposable: true, cleanupProjects: [projectName],
    cleanupProfiles: [file("browser")], fixtureDirectory: directory,
    environmentFile: file("fixture.env"), browserExecutable: process.execPath,
    certificateFile: file("certificate.pem"), bootstrapTokenFile: file("bootstrap_token"),
    baseUrl: "https://localhost:28443", healthUrl: "http://127.0.0.1:28080/health/ready",
    collectorMetricsUrl: "http://127.0.0.1:28888/metrics", prometheusUrl: "http://127.0.0.1:29090",
    approvedImages: Object.fromEntries(["app", "postgres", "otel-collector", "prometheus", "tls-proxy"]
        .map(service => [service, `${service === "tls-proxy" ? "caddy" : service}@sha256:${"a".repeat(64)}`])),
};
const model = {
    name: projectName,
    services: Object.fromEntries(Object.entries(config.approvedImages).map(([service, image]) =>
        [service, { image, volumes: [] }])),
    networks: { edge: { name: `${projectName}_edge`, internal: true },
        backend: { name: `${projectName}_backend`, internal: true } },
    volumes: { "postgres-data": { name: `${projectName}_postgres-data` } },
    secrets: { tls_certificate: { file: config.certificateFile },
        bootstrap_token: { file: config.bootstrapTokenFile }, postgres_password: { file: file("password") } },
};
model.services.app.environment = {
    Andreja__OpenLoops__AssistantProvider: "deterministic",
    Andreja__Identity__AllowedOrigins__0: new URL(config.baseUrl).origin,
};
for (const [service, target, published] of [
    ["app", 8080, "28080"], ["tls-proxy", 8443, "28443"],
    ["otel-collector", 8888, "28888"], ["prometheus", 9090, "29090"],
]) model.services[service].ports = [{ host_ip: "127.0.0.1", target, published }];
const preload = pathToFileURL(path.join(root, "scripts", "evidence", "tests", "mock-docker.mjs")).href;
const env = { ...process.env, NODE_OPTIONS: `--import=${preload}`,
    EVIDENCE_TEST_MODEL: file("model.json"), EVIDENCE_TEST_LOG: file("commands.jsonl") };
for (const key of Object.keys(env)) {
    if (/^(ANDREJA_|COMPOSE_|POSTGRES_|PROMETHEUS_|OTEL_|DOCKER_)/.test(key)) delete env[key];
}
function run(caller, changed = config, resolved = model, extraEnv = {}, fromFixtureDirectory = false) {
    writeFileSync(file("config.json"), JSON.stringify(changed));
    writeFileSync(file("model.json"), JSON.stringify(resolved));
    writeFileSync(file("commands.jsonl"), "");
    const configArgument = fromFixtureDirectory ? "config.json" : file("config.json");
    const args = caller === "browser"
        ? [path.join(root, "scripts", "evidence", "browser-e2e.mjs"), "--config",
            changed === null ? "" : configArgument]
        : ["-NoProfile", "-File", path.join(root, "scripts", "evidence", "tests", "invoke-caller.ps1"),
            "-Caller", caller, "-Config", changed === null ? "" : configArgument, "-Log", file("commands.jsonl")];
    const result = spawnSync(caller === "browser" ? process.execPath : "pwsh", args,
        { cwd: fromFixtureDirectory ? directory : root, env: { ...env, ...extraEnv }, encoding: "utf8" });
    assert.ifError(result.error);
    const commands = readFileSync(file("commands.jsonl"), "utf8").trim().split("\n")
        .filter(Boolean).map(line => JSON.parse(line));
    return { ...result, commands, output: result.stdout + result.stderr };
}
const invalid = [
    ["missing", null],
    ["unknown", { ...config, typo: true }],
    ["missing-context", { ...config, dockerContext: "" }],
    ["default-project", { ...config, projectName: "andreja", cleanupProjects: ["andreja"] }],
    ["not-disposable", { ...config, disposable: false }],
    ["cleanup-denied", { ...config, cleanupProjects: [] }],
    ["profile-cleanup-denied", { ...config, cleanupProfiles: [root] }],
    ["remote-endpoint", { ...config, baseUrl: "https://example.com:8443" }],
    ["mutable-image", { ...config, approvedImages: { ...config.approvedImages, app: "app:latest" } }],
    ["default-secret", { ...config, bootstrapTokenFile: path.join(root, "deploy", "secrets", "bootstrap_token") }],
];
for (const caller of ["browser", "offline", "telemetry"]) {
    for (const [name, changed] of invalid) {
        test(`${caller} rejects ${name} before any command or HTTP/profile mutation`, () => {
            mkdirSync(file("browser"), { recursive: true });
            const sentinel = path.join(file("browser"), "keep-synthetic.txt");
            writeFileSync(sentinel, "must-not-delete");
            try {
                const result = run(caller, changed);
                assert.notEqual(result.status, 0, result.output);
                assert.match(result.output, /fixture|Usage:/i);
                assert.deepEqual(result.commands, []);
                assert.equal(readFileSync(sentinel, "utf8"), "must-not-delete");
            } finally {
                rmSync(file("browser"), { recursive: true, force: true });
            }
        });
    }
    for (const [name, mutate] of [
        ["image drift", value => { value.services.app.image = "app:latest"; }],
        ["external volume", value => { value.volumes["postgres-data"].external = true; }],
        ["default volume", value => { value.volumes["postgres-data"].name = "andreja_postgres-data"; }],
        ["external network", value => { value.networks.edge.external = true; }],
        ["wrong port", value => { value.services.app.ports[0].published = "8080"; }],
        ["wrong origin", value => { value.services.app.environment.Andreja__Identity__AllowedOrigins__0 = "https://localhost:8443"; }],
        ["live provider", value => { value.services.app.environment.Andreja__OpenLoops__AssistantProvider = "byok"; }],
        ["writable bind", value => { value.services.app.volumes = [{ type: "bind", source: root }]; }],
        ["unscoped secret", value => { value.secrets.postgres_password.file = path.join(root, "compose.yaml"); }],
    ]) {
        test(`${caller} rejects resolved ${name} before mutation`, () => {
            const changed = structuredClone(model);
            mutate(changed);
            const result = run(caller, config, changed);
            assert.notEqual(result.status, 0, result.output);
            assert.match(result.output, /Evidence fixture/);
            assert.equal(result.commands.length, 2);
            assert.deepEqual(result.commands[1].args.slice(-3), ["config", "--format", "json"]);
        });
    }
    test(`${caller} rejects ambient fallback before invoking Docker`, () => {
        const result = run(caller, config, model, { ANDREJA_IMAGE: config.approvedImages.app });
        assert.notEqual(result.status, 0);
        assert.match(result.output, /ambient Compose/);
        assert.deepEqual(result.commands, []);
    });
    test(`${caller} rejects case-insensitive ambient fallback before invoking Docker`, () => {
        const result = run(caller, config, model, { andreja_image: config.approvedImages.app });
        assert.notEqual(result.status, 0);
        assert.match(result.output, /ambient Compose/);
        assert.deepEqual(result.commands, []);
    });
    test(`${caller} rejects a remote Docker context before model expansion or mutation`, () => {
        const result = run(caller, config, model, { EVIDENCE_TEST_CONTEXT: "ssh://remote-host" });
        assert.notEqual(result.status, 0);
        assert.match(result.output, /same-host Unix socket or named pipe/);
        assert.equal(result.commands.length, 1);
        assert.equal(result.commands[0].args[0], "context");
    });
    test(`${caller} rejects a remote named pipe before model expansion or mutation`, () => {
        const result = run(caller, config, model, { EVIDENCE_TEST_CONTEXT: "npipe:////remote-host/pipe/docker_engine" });
        assert.notEqual(result.status, 0);
        assert.match(result.output, /same-host Unix socket or named pipe/);
        assert.equal(result.commands.length, 1);
    });
}
function assertScope(args, offline = false) {
    assert.deepEqual(args.slice(0, 2), ["--context", config.dockerContext]);
    assert.equal(args[args.indexOf("--project-name") + 1], projectName);
    assert.equal(args[args.indexOf("--env-file") + 1], config.environmentFile);
    assert.equal(args[args.indexOf("--profile") + 1], "evidence");
    const files = args.filter((_, index) => args[index - 1] === "--file");
    assert.deepEqual(files, [
        path.join(root, "compose.yaml"), path.join(root, "deploy", "compose.evidence.yaml"),
        ...(offline ? [path.join(root, "deploy", "compose.offline-evidence.yaml")] : []),
    ]);
}
test("offline production caller scopes startup, restart, probe and cleanup without deleting volumes by default", () => {
    const result = run("offline");
    assert.equal(result.status, 0, result.output);
    const output = JSON.parse(result.stdout);
    assert.equal(output.status, "passed");
    const compose = result.commands.filter(command => command.args.includes("compose"));
    compose.forEach(command => assertScope(command.args, true));
    assert.equal(compose.filter(command => command.args.includes("up")).length, 1);
    assert.equal(compose.filter(command => command.args.includes("restart")).length, 1);
    const downs = compose.filter(command => command.args.includes("down"));
    assert.equal(downs.length, 1, "must not tear down a prior stack before startup");
    assert.ok(!downs[0].args.includes("--volumes"));
    assert.deepEqual(result.commands.filter(command => command.args.includes("network"))
        .map(command => command.args[4]), [`${projectName}_edge`, `${projectName}_backend`]);
    const probe = result.commands.find(command => command.args.includes("run"));
    assert.ok(probe.args.includes("container:synthetic-app-container"));
    assert.ok(probe.args.includes(config.approvedImages["tls-proxy"]));
});
test("offline deletes allowlisted volumes only with explicit opt-in", () => {
    const result = run("offline", config, model, { EVIDENCE_TEST_DESTROY_VOLUMES: "true" });
    assert.equal(result.status, 0, result.output);
    const down = result.commands.find(command => command.args.includes("down"));
    assertScope(down.args, true);
    assert.ok(down.args.includes("--volumes"));
});
test("offline missing preloaded image never starts or cleans a project", () => {
    const result = run("offline", config, model, { EVIDENCE_TEST_DOCKER_FAIL: "image" });
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Required preloaded image is unavailable/);
    assert.equal(result.commands.filter(command => command.args.includes("up") || command.args.includes("down")).length, 0);
});
test("offline failed startup still cleans only the approved project", () => {
    const result = run("offline", config, model, { EVIDENCE_TEST_DOCKER_FAIL: "up" });
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Offline Compose startup failed/);
    const down = result.commands.filter(command => command.args.includes("down"));
    assert.equal(down.length, 1);
    assertScope(down[0].args, true);
});
test("offline cleanup failure cannot report a pass", () => {
    const result = run("offline", config, model, { EVIDENCE_TEST_DOCKER_FAIL: "down" });
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Evidence cleanup failed/);
    assert.doesNotMatch(result.stdout, /"status":"passed"/);
});
test("telemetry production caller forwards the same config and uses only its endpoints", () => {
    const result = run("telemetry");
    assert.equal(result.status, 0, result.output);
    assert.equal(JSON.parse(result.stdout).acceptedDelta.spans, 99);
    assertScope(result.commands[1].args);
    const browser = result.commands.find(command => command.command === "browser");
    assert.deepEqual(browser.args.slice(-2), ["--config", file("config.json")]);
    const urls = result.commands.filter(command => command.command === "http").map(command => command.args[0]);
    assert.equal(urls.filter(url => url === config.collectorMetricsUrl).length, 6);
    assert.equal(urls.filter(url => url.startsWith(`${config.prometheusUrl}/api/v1/query?`)).length, 3);
});
test("telemetry forwards an absolute config from a different working directory and accepts a trailing URL slash", () => {
    const result = run("telemetry", { ...config, prometheusUrl: `${config.prometheusUrl}/` }, model, {}, true);
    assert.equal(result.status, 0, result.output);
    const browser = result.commands.find(command => command.command === "browser");
    assert.equal(browser.args.at(-1), file("config.json"));
    const urls = result.commands.filter(command => command.command === "http").map(command => command.args[0]);
    assert.ok(urls.some(url => url.startsWith(`${config.prometheusUrl}/api/`)));
    assert.ok(!urls.some(url => url.includes("//api/")));
});
test("browser production caller validates before certificate/profile access; browser and restart use scoped args", () => {
    const result = run("browser");
    assert.notEqual(result.status, 0);
    assert.match(result.output, /PEM|certificate|decoder/i);
    assertScope(result.commands[1].args);
    assert.equal(result.commands.length, 2);
    // Export only a short-lived synthetic public certificate; no private key is written.
    const certificate = spawnSync("pwsh", ["-NoProfile", "-Command",
        `$rsa = [Security.Cryptography.RSA]::Create(2048);
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=synthetic-evidence', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1);
        $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddMinutes(5));
        [IO.File]::WriteAllBytes('${config.certificateFile.replaceAll("'", "''")}',
            $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert));
        $cert.Dispose(); $rsa.Dispose();`], { encoding: "utf8" });
    assert.equal(certificate.status, 0, certificate.stderr);
    const validBrowser = run("browser");
    assert.match(validBrowser.output, /SYNTHETIC_SCOPED_SPAWN/);
    const launch = validBrowser.commands.at(-1);
    assert.equal(launch.command, config.browserExecutable);
    assert.ok(launch.args.includes(`--user-data-dir=${file("browser")}`));
    assert.ok(launch.args.some(arg => arg.startsWith("--ignore-certificate-errors-spki-list=")));
    assert.equal(existsSync(file("browser")), false);
    const result2 = spawnSync(process.execPath, ["--input-type=module", "-e",
        `import {prepareFixture,restartApplication} from ${JSON.stringify(pathToFileURL(path.join(root, "scripts", "evidence", "evidence-fixture.mjs")).href)};
        restartApplication(prepareFixture(${JSON.stringify(file("config.json"))},"browser"));`],
    { cwd: root, env, encoding: "utf8" });
    assert.match(result2.stderr, /SYNTHETIC_SCOPED_SPAWN/);
    const commands = readFileSync(file("commands.jsonl"), "utf8").trim().split("\n").map(JSON.parse);
    assertScope(commands.at(-1).args);
    assert.deepEqual(commands.at(-1).args.slice(-2), ["restart", "app"]);
});
test("runbook documents every fixture field and the checked-in helper digests", () => {
    const docs = readFileSync(path.join(root, "docs", "operations", "self-hosting.md"), "utf8");
    for (const field of Object.keys(config)) {
        assert.match(docs, new RegExp(`\\b${field}\\b`), `undocumented fixture field ${field}`);
    }
    const compose = readFileSync(path.join(root, "compose.yaml"), "utf8")
        + readFileSync(path.join(root, "deploy", "compose.evidence.yaml"), "utf8");
    for (const image of compose.matchAll(/[a-z0-9][a-z0-9/.-]*(?::[a-zA-Z0-9.-]+)?@sha256:[0-9a-f]{64}/g)) {
        assert.ok(docs.includes(image[0]), `runbook image drift: ${image[0]}`);
    }
});
