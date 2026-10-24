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
    volumes: {
        "data-protection-keys": { name: `${projectName}_data-protection-keys` },
        attachments: { name: `${projectName}_attachments` },
        "evidence-bootstrap": { name: `${projectName}_evidence-bootstrap` },
        "postgres-data": { name: `${projectName}_postgres-data` },
        "prometheus-data": { name: `${projectName}_prometheus-data` },
    },
    secrets: { tls_certificate: { file: config.certificateFile },
        bootstrap_token: { file: config.bootstrapTokenFile }, postgres_password: { file: file("password") } },
};
model.services.app.environment = {
    Andreja__OpenLoops__AssistantProvider: "deterministic",
    Andreja__Identity__AllowedOrigins__0: new URL(config.baseUrl).origin,
    Andreja__Operations__Database__Enabled: "true",
    Andreja__Operations__Database__Host: "postgres",
    Andreja__Operations__Database__Port: "5432",
    Andreja__Operations__Database__Name: "andreja_evidence",
    Andreja__Operations__Database__Username: "andreja_evidence",
    Andreja__Operations__Database__PasswordFile: "/run/secrets/postgres_password",
};
model.services.postgres.environment = {
    POSTGRES_DB: "andreja_evidence",
    POSTGRES_USER: "andreja_evidence",
};
model.services.app.volumes = [
    { type: "volume", source: "data-protection-keys", target: "/var/lib/andreja/keys" },
    { type: "volume", source: "attachments", target: "/var/lib/andreja/attachments" },
    { type: "volume", source: "evidence-bootstrap", target: "/run/evidence-bootstrap", read_only: true },
];
model.services.postgres.volumes = [
    { type: "volume", source: "postgres-data", target: "/var/lib/postgresql/data" },
];
model.services.prometheus.volumes = [
    { type: "volume", source: "prometheus-data", target: "/prometheus" },
];
for (const [service, target, published] of [
    ["app", 8080, "28080"], ["tls-proxy", 8443, "28443"],
    ["otel-collector", 8888, "28888"], ["prometheus", 9090, "29090"],
]) model.services[service].ports = [{ host_ip: "127.0.0.1", target, published }];
const preload = pathToFileURL(path.join(root, "scripts", "evidence", "tests", "mock-docker.mjs")).href;
const env = { ...process.env, NODE_OPTIONS: `--import=${preload}`,
    EVIDENCE_TEST_MODEL: file("model.json"), EVIDENCE_TEST_RUNTIME: file("runtime.json"),
    EVIDENCE_TEST_LOG: file("commands.jsonl"), EVIDENCE_TEST_CERT: file("certificate.pem"),
    EVIDENCE_TEST_TOKEN: file("bootstrap_token"), EVIDENCE_TEST_PROFILE: file("browser") };
for (const key of Object.keys(env)) {
    if (/^(ANDREJA_|COMPOSE_|POSTGRES_|PROMETHEUS_|OTEL_|DOCKER_)/.test(key)) delete env[key];
}
function makeRuntime(changed = config, resolved = model) {
    const images = {};
    const containers = Object.entries(changed.approvedImages).map(([service, image], index) => {
        const imageId = `sha256:${String(index + 1).repeat(64)}`;
        images[image] = { Id: imageId, RepoDigests: [image] };
        const source = resolved.services[service];
        const ports = Object.fromEntries((source.ports ?? []).map(binding => [
            `${binding.target}/tcp`, [{ HostIp: binding.host_ip, HostPort: String(binding.published) }],
        ]));
        const envEntries = Object.entries(source.environment ?? {}).map(([key, value]) => `${key}=${value}`);
        return {
            Id: String(index + 1).repeat(64),
            Image: imageId,
            Mounts: (source.volumes ?? []).map(mount => ({
                Type: mount.type,
                Name: mount.type === "volume" ? resolved.volumes[mount.source].name : undefined,
                Source: mount.type === "volume" ? `/synthetic/volumes/${resolved.volumes[mount.source].name}`
                    : mount.source,
                Destination: mount.target,
                RW: mount.read_only !== true,
            })),
            Config: {
                Image: source.image,
                Env: envEntries,
                Labels: {
                    "com.docker.compose.project": changed.projectName,
                    "com.docker.compose.service": service,
                },
            },
            State: { Running: true, Status: "running" },
            NetworkSettings: { Ports: ports },
        };
    });
    const portOwners = {};
    for (const container of containers) {
        for (const bindings of Object.values(container.NetworkSettings.Ports)) {
            for (const binding of bindings ?? []) {
                (portOwners[binding.HostPort] ??= []).push(container.Id);
            }
        }
    }
    return {
        projectContainerIds: containers.map(container => container.Id),
        containers, images, portOwners,
    };
}
function run(caller, changed = config, resolved = model, extraEnv = {}, fromFixtureDirectory = false,
    runtime = makeRuntime(changed ?? config, resolved ?? model), powerShellLocation = null) {
    writeFileSync(file("config.json"), JSON.stringify(changed));
    writeFileSync(file("model.json"), JSON.stringify(resolved));
    writeFileSync(file("runtime.json"), JSON.stringify(runtime));
    writeFileSync(file("commands.jsonl"), "");
    const configArgument = fromFixtureDirectory || powerShellLocation ? "config.json" : file("config.json");
    const args = caller === "browser"
        ? [path.join(root, "scripts", "evidence", "browser-e2e.mjs"), "--config",
            changed === null ? "" : configArgument]
        : ["-NoProfile", "-File", path.join(root, "scripts", "evidence", "tests", "invoke-caller.ps1"),
            "-Caller", caller, "-Config", changed === null ? "" : configArgument, "-Log", file("commands.jsonl"),
            ...(powerShellLocation ? ["-PowerShellLocation", powerShellLocation] : [])];
    const result = spawnSync(caller === "browser" ? process.execPath : "pwsh", args,
        { cwd: fromFixtureDirectory ? directory : root, env: { ...env, ...extraEnv }, encoding: "utf8" });
    assert.ifError(result.error);
    const commands = readFileSync(file("commands.jsonl"), "utf8").trim().split("\n")
        .filter(Boolean).map(line => JSON.parse(line));
    return { ...result, commands, output: result.stdout + result.stderr };
}
function runPreparation(mode, runtime = makeRuntime()) {
    writeFileSync(file("config.json"), JSON.stringify(config));
    writeFileSync(file("model.json"), JSON.stringify(model));
    writeFileSync(file("runtime.json"), JSON.stringify(runtime));
    writeFileSync(file("commands.jsonl"), "");
    const result = spawnSync(process.execPath, [
        path.join(root, "scripts", "evidence", "evidence-fixture.mjs"),
        "--config", file("config.json"), mode,
    ], { cwd: root, env, encoding: "utf8" });
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
function assertRuntimeFailureIsReadOnly(result) {
    assert.notEqual(result.status, 0, result.output);
    assert.deepEqual(result.commands.filter(command =>
        ["http", "browser", "credential-read", "profile-mutation"].includes(command.command)), []);
    for (const command of result.commands.filter(command => command.command === "docker")) {
        assert.ok(!["up", "down", "restart", "run"].some(mutation => command.args.includes(mutation)),
            `unexpected Docker mutation: ${JSON.stringify(command.args)}`);
    }
}
const runtimeDrift = [
    ["absent target", runtime => {
        runtime.projectContainerIds.pop();
        runtime.containers.pop();
    }],
    ["stopped target", runtime => {
        runtime.containers.find(container => container.Config.Labels["com.docker.compose.service"] === "app")
            .State = { Running: false, Status: "exited" };
    }],
    ["wrong project label", runtime => {
        runtime.containers[0].Config.Labels["com.docker.compose.project"] = "other-project";
    }],
    ["wrong service label", runtime => {
        runtime.containers[0].Config.Labels["com.docker.compose.service"] = "postgres";
    }],
    ["mutable unapproved runtime image", runtime => {
        runtime.containers[0].Config.Image = "app:latest";
    }],
    ["runtime image identity mismatch", runtime => {
        runtime.containers[0].Image = `sha256:${"f".repeat(64)}`;
    }],
    ["live provider drift", runtime => {
        runtime.containers[0].Config.Env[0] = "Andreja__OpenLoops__AssistantProvider=byok";
    }],
    ["WebAuthn origin drift", runtime => {
        runtime.containers[0].Config.Env[1] = "Andreja__Identity__AllowedOrigins__0=https://other.local";
    }],
    ["database target drift", runtime => {
        const app = runtime.containers.find(container =>
            container.Config.Labels["com.docker.compose.service"] === "app");
        const setting = "Andreja__Operations__Database__Host=";
        app.Config.Env[app.Config.Env.findIndex(entry => entry.startsWith(setting))] =
            `${setting}host.docker.internal`;
    }],
    ["database volume identity drift", runtime => {
        const postgres = runtime.containers.find(container =>
            container.Config.Labels["com.docker.compose.service"] === "postgres");
        postgres.Mounts[0].Name = "andreja_postgres-data";
    }],
    ["database volume destination drift", runtime => {
        const postgres = runtime.containers.find(container =>
            container.Config.Labels["com.docker.compose.service"] === "postgres");
        postgres.Mounts[0].Destination = "/var/lib/andreja/postgres";
    }],
    ["database volume type drift", runtime => {
        const postgres = runtime.containers.find(container =>
            container.Config.Labels["com.docker.compose.service"] === "postgres");
        postgres.Mounts[0].Type = "bind";
    }],
    ["wrong host port binding", runtime => {
        runtime.containers[0].NetworkSettings.Ports["8080/tcp"][0].HostPort = "18080";
    }],
    ["non-loopback host binding", runtime => {
        runtime.containers[0].NetworkSettings.Ports["8080/tcp"][0].HostIp = "0.0.0.0";
    }],
    ["missing host binding", runtime => {
        runtime.containers[0].NetworkSettings.Ports["8080/tcp"] = [];
    }],
    ["published PostgreSQL", runtime => {
        runtime.containers.find(container =>
            container.Config.Labels["com.docker.compose.service"] === "postgres")
            .NetworkSettings.Ports["5432/tcp"] = [{ HostIp: "127.0.0.1", HostPort: "25432" }];
    }],
    ["wrong-project container owns configured port", runtime => {
        const foreignId = "synthetic-foreign";
        runtime.containers.push({
            Id: foreignId,
            Config: { Labels: { "com.docker.compose.project": "other-project",
                "com.docker.compose.service": "app" } },
        });
        runtime.portOwners["28080"].push(foreignId);
    }],
];
for (const caller of ["browser", "telemetry"]) {
    for (const [name, mutate] of runtimeDrift) {
        test(`${caller} rejects runtime ${name} before requests, credentials, profile or mutation`, () => {
            const runtime = makeRuntime();
            mutate(runtime);
            const result = run(caller, config, model, {}, false, runtime);
            assertRuntimeFailureIsReadOnly(result);
            const projectList = result.commands.find(command => command.command === "docker"
                && command.args.includes("compose") && command.args.includes("ps")
                && command.args.includes("--all") && command.args.includes("--quiet"));
            assert.ok(projectList, "runtime preflight must enumerate the authorized Compose project");
            if (name === "wrong-project container owns configured port") {
                assert.match(result.output, /outside the authorized project/);
            }
            if (name !== "absent target") {
                assert.ok(result.commands.some(command => command.command === "docker"
                    && command.args[0] === "--context" && command.args[2] === "inspect"),
                "runtime drift must be rejected from scoped read-only container inspection");
            }
        });
    }
}
test("offline static preflight permits startup when the authorized project is absent", () => {
    const absent = makeRuntime();
    absent.projectContainerIds = [];
    absent.containers = [];
    const result = run("offline", config, model, {}, false, absent);
    assert.equal(result.status, 0, result.output);
    assert.ok(result.commands.some(command => command.command === "docker"
        && command.args.includes("up")), "offline caller must be able to create the absent fixture");
    assert.ok(!result.commands.some(command => command.command === "docker"
        && command.args.includes("ps") && command.args.includes("--all")
        && command.args.includes("--quiet")), "offline static preflight must not require pre-existing containers");
});
test("browser static preparation returns scoped Compose args for a fresh fixture without runtime checks", () => {
    const absent = makeRuntime();
    absent.projectContainerIds = [];
    absent.containers = [];
    absent.portOwners = {};
    const result = runPreparation("browser-static", absent);
    assert.equal(result.status, 0, result.output);
    const prepared = JSON.parse(result.stdout);
    assertScope(prepared.compose);
    assert.ok(!result.commands.some(command => command.args.includes("ps")),
    "static preparation must not require an existing runtime");
});
test("runtime inspect uses the explicit context and only project-reported containers", () => {
    const result = run("browser");
    assert.match(result.output, /PEM|certificate|decoder/i);
    const ps = result.commands.find(command => command.command === "docker"
        && command.args.includes("ps") && command.args.includes("--quiet")
        && !command.args.includes("--filter"));
    assert.ok(ps);
    assertScope(ps.args);
    const runtimeInspect = result.commands.find(command => command.command === "docker"
        && command.args[0] === "--context" && command.args[2] === "inspect");
    assert.deepEqual(runtimeInspect.args.slice(0, 2), ["--context", config.dockerContext]);
    assert.deepEqual(runtimeInspect.args.slice(3, -2), makeRuntime().projectContainerIds);
    const portOwners = result.commands.filter(command => command.command === "docker"
        && command.args.includes("ps") && command.args.includes("--filter"));
    assert.equal(portOwners.length, 4);
    assert.ok(portOwners.every(command => command.args.includes("--no-trunc")),
        "port-owner IDs must not be shortened before comparing with Compose IDs");
    assert.doesNotMatch(result.output, /additional runtime owner/);
    for (const command of result.commands.filter(command => command.command === "docker"
        && command.args.includes("image") && command.args.includes("inspect"))) {
        assert.deepEqual(command.args.slice(0, 2), ["--context", config.dockerContext]);
    }
});
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
    assert.equal(urls    .filter(url => url.startsWith(`${config.prometheusUrl}/api/v1/query?`)).length, 3);
    const runtimeInspect = result.commands.findIndex(command => command.command === "docker"
    && command.args[0] === "--context" && command.args[2] === "inspect");
    const firstRequest = result.commands.findIndex(command => command.command === "http");
    assert.ok(runtimeInspect >= 0 && runtimeInspect < firstRequest,
    "telemetry must inspect authorized containers before its first endpoint request");
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
for (const caller of ["offline", "telemetry"]) {
    test(`${caller} resolves relative fixture config from PowerShell location`, () => {
        const result = run(caller, config, model, {}, false, undefined, directory);
        assert.equal(result.status, 0, result.output);
        assert.match(result.stdout, /"status":"passed"/);
    });
}
test("browser production caller validates before certificate/profile access; browser and restart use scoped args", () => {
    const result = run("browser");
    assert.notEqual(result.status, 0);
    assert.match(result.output, /PEM|certificate|decoder/i);
    assertScope(result.commands[1].args);
    const certificateRead = result.commands.findIndex(command => command.command === "credential-read"
        && command.args[0] === file("certificate.pem"));
    assert.ok(certificateRead > 0, "synthetic certificate is read only after runtime preflight");
    assert.ok(result.commands.slice(2, certificateRead).some(command =>
        command.command === "docker" && command.args[0] === "--context" && command.args[2] === "inspect"));
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
    const launch = validBrowser.commands.find(command => command.command === config.browserExecutable);
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
    assert.match(docs, /read-only runtime\s+inspection must find exactly one running container/);
    assert.match(docs, /offline\s+evidence validates configuration only/);
    assert.match(docs, /A Compose restart does not apply changed configuration/);
    for (const field of Object.keys(config)) {
        assert.match(docs, new RegExp(`\\b${field}\\b`), `undocumented fixture field ${field}`);
    }
    const compose = readFileSync(path.join(root, "compose.yaml"), "utf8")
        + readFileSync(path.join(root, "deploy", "compose.evidence.yaml"), "utf8");
    for (const image of compose.matchAll(/[a-z0-9][a-z0-9/.-]*(?::[a-zA-Z0-9.-]+)?@sha256:[0-9a-f]{64}/g)) {
        assert.ok(docs.includes(image[0]), `runbook image drift: ${image[0]}`);
    }
});
