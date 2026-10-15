import { execFileSync, spawn } from "node:child_process";
import { readFileSync, realpathSync, lstatSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const fields = [
    "projectName", "dockerContext", "disposable", "cleanupProjects", "cleanupProfiles", "fixtureDirectory",
    "environmentFile", "browserExecutable", "certificateFile", "bootstrapTokenFile",
    "baseUrl", "healthUrl", "collectorMetricsUrl", "prometheusUrl", "approvedImages",
];
const services = ["app", "postgres", "otel-collector", "prometheus", "tls-proxy"];
const immutable = /^[^\s@]+@sha256:[0-9a-f]{64}$/;

function requireThat(condition, message) {
    if (!condition) throw new Error(`Evidence fixture: ${message}`);
}

function inside(parent, child) {
    const relative = path.relative(parent, child);
    return relative !== "" && !relative.startsWith(`..${path.sep}`)
        && relative !== ".." && !path.isAbsolute(relative);
}

function localUrl(value, protocol, pathname) {
    const url = new URL(value);
    requireThat(url.protocol === protocol && ["localhost", "127.0.0.1"].includes(url.hostname)
        && !url.username && !url.password && !url.search && !url.hash
        && url.pathname === pathname && url.port, `invalid explicit loopback endpoint: ${value}`);
    return url;
}

export function loadFixture(configPath) {
    requireThat(typeof configPath === "string" && configPath.length > 0, "--config / -FixtureConfig is required");
    const config = JSON.parse(readFileSync(path.resolve(configPath), "utf8"));
    requireThat(config && !Array.isArray(config) && typeof config === "object", "configuration must be an object");
    requireThat(Object.keys(config).length === fields.length
        && fields.every(key => Object.hasOwn(config, key)), "missing or unknown configuration fields");
    requireThat(config.disposable === true, "disposable must explicitly be true");
    requireThat(typeof config.dockerContext === "string" && /^[a-zA-Z0-9][a-zA-Z0-9_.-]*$/.test(config.dockerContext),
        "dockerContext must explicitly name the authorized same-host runtime");
    requireThat(/^andreja-evidence-[a-z0-9][a-z0-9-]{2,39}$/.test(config.projectName),
        "projectName must be an explicitly disposable andreja-evidence-* project, never andreja");
    requireThat(Array.isArray(config.cleanupProjects) && config.cleanupProjects.length === 1
        && config.cleanupProjects[0] === config.projectName, "cleanupProjects must authorize exactly projectName");
    const expectedDirectory = path.join(root, ".andreja", "evidence", config.projectName);
    requireThat(path.resolve(config.fixtureDirectory) === expectedDirectory,
        "fixtureDirectory must be .andreja/evidence/<projectName> in this checkout");
    // Resolve existing ancestors too: a missing browser directory must not conceal a linked parent.
    for (let directory = expectedDirectory; inside(root, directory); directory = path.dirname(directory)) {
        requireThat(!lstatSync(directory).isSymbolicLink(), "fixture directories must not be links");
    }
    requireThat(realpathSync(expectedDirectory) === expectedDirectory, "fixtureDirectory must not traverse links");
    config.fixtureDirectory = expectedDirectory;
    config.userDataPath = path.join(expectedDirectory, "browser");
    requireThat(!existsSync(config.userDataPath) || (lstatSync(config.userDataPath).isDirectory()
        && !lstatSync(config.userDataPath).isSymbolicLink()
        && realpathSync(config.userDataPath) === config.userDataPath), "browser profile must not traverse links");
    requireThat(Array.isArray(config.cleanupProfiles) && config.cleanupProfiles.length === 1
        && path.resolve(config.cleanupProfiles[0]) === config.userDataPath,
    "cleanupProfiles must authorize exactly the fixture browser directory");
    for (const field of ["environmentFile", "certificateFile", "bootstrapTokenFile"]) {
        requireThat(typeof config[field] === "string", `${field} is required`);
        config[field] = realpathSync(path.resolve(config[field]));
        requireThat(inside(expectedDirectory, config[field]), `${field} must be inside fixtureDirectory`);
        requireThat(lstatSync(config[field]).isFile(), `${field} must be a regular file`);
    }
    requireThat(typeof config.browserExecutable === "string" && path.isAbsolute(config.browserExecutable),
        "browserExecutable must be an explicit absolute path");
    requireThat(lstatSync(config.browserExecutable).isFile(), "browserExecutable must exist");
    localUrl(config.baseUrl, "https:", "/");
    localUrl(config.healthUrl, "http:", "/health/ready");
    localUrl(config.collectorMetricsUrl, "http:", "/metrics");
    config.prometheusUrl = localUrl(config.prometheusUrl, "http:", "/").origin;
    requireThat(config.approvedImages && Object.keys(config.approvedImages).length === services.length
        && services.every(service => immutable.test(config.approvedImages[service])),
    "approvedImages must approve exactly the five evidence services by immutable digest");
    return config;
}

export function composeArguments(config, mode) {
    requireThat(["browser", "offline"].includes(mode), "unknown evidence mode");
    const files = ["compose.yaml", "deploy/compose.evidence.yaml"];
    if (mode === "offline") files.push("deploy/compose.offline-evidence.yaml");
    return ["--context", config.dockerContext, "compose", "--project-name", config.projectName, "--env-file", config.environmentFile,
        "--profile", "evidence", ...files.flatMap(file => ["--file", path.join(root, file)])];
}

export function prepareFixture(configPath, mode) {
    const config = loadFixture(configPath);
    const compose = composeArguments(config, mode);
    const files = compose.filter((_, index) => compose[index - 1] === "--file");
    const variables = new Set(files.flatMap(file =>
        [...readFileSync(file, "utf8").matchAll(/\$\{([A-Z][A-Z0-9_]*)/g)].map(match => match[1])));
    requireThat(!Object.keys(process.env).some(key => variables.has(key.toUpperCase())
        || key.toUpperCase().startsWith("COMPOSE_") || key.toUpperCase().startsWith("DOCKER_")),
    "unset ambient Compose/interpolation/Docker variables; use the fixture configuration");
    const contextEndpoint = JSON.parse(execFileSync("docker",
        ["context", "inspect", config.dockerContext, "--format", "{{json .Endpoints.docker.Host}}"],
        { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }));
    requireThat(typeof contextEndpoint === "string" && /^(unix:\/\/\/|npipe:\/\/\/\/\.\/pipe\/)/.test(contextEndpoint),
        "dockerContext must use a same-host Unix socket or named pipe, not a remote endpoint");
    // Read-only model expansion precedes all HTTP, secret reads, browser/profile changes and Docker mutations.
    const model = JSON.parse(execFileSync("docker", [...compose, "config", "--format", "json"],
        { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }));
    requireThat(model.name === config.projectName, "resolved project does not match authorization");
    requireThat(Object.keys(model.services).length === services.length
        && services.every(service => model.services[service]?.image === config.approvedImages[service]),
    "resolved services/images differ from approvedImages");
    for (const section of ["volumes", "networks"]) {
        for (const resource of Object.values(model[section] ?? {})) {
            requireThat(!resource.external && resource.name?.startsWith(`${config.projectName}_`),
                `external or unscoped ${section} are forbidden`);
        }
    }
    for (const secret of Object.values(model.secrets ?? {})) {
        requireThat(!secret.external && typeof secret.file === "string"
            && inside(config.fixtureDirectory, realpathSync(secret.file)), "secret source is outside the fixture");
    }
    requireThat(realpathSync(model.secrets.tls_certificate.file) === config.certificateFile
        && realpathSync(model.secrets.bootstrap_token.file) === config.bootstrapTokenFile,
    "browser secret/certificate paths do not match Compose");
    const readOnlyBinds = ["deploy/Caddyfile.evidence", "deploy/otel-collector.yaml", "deploy/prometheus.yaml"]
        .map(file => realpathSync(path.join(root, file)));
    for (const service of Object.values(model.services)) {
        for (const mount of service.volumes ?? []) {
            requireThat(mount.type === "volume"
                ? Object.hasOwn(model.volumes, mount.source)
                : mount.type === "bind" && mount.read_only === true
                    && readOnlyBinds.includes(realpathSync(mount.source)),
            "unapproved bind or volume mount");
        }
    }
    const app = model.services.app;
    requireThat(app.environment.Andreja__OpenLoops__AssistantProvider === "deterministic",
        "only the synthetic deterministic provider is allowed");
    requireThat(app.environment.Andreja__Identity__AllowedOrigins__0 === new URL(config.baseUrl).origin,
        "baseUrl differs from the resolved WebAuthn origin");
    const ports = [
        ["tls-proxy", 8443, config.baseUrl], ["app", 8080, config.healthUrl],
        ["otel-collector", 8888, config.collectorMetricsUrl], ["prometheus", 9090, config.prometheusUrl],
    ];
    for (const [service, target, endpoint] of ports) {
        const port = model.services[service].ports;
        requireThat(port?.length === 1 && port[0].host_ip === "127.0.0.1"
            && Number(port[0].target) === target && String(port[0].published) === new URL(endpoint).port,
        `${service} published port differs from the explicit endpoint`);
    }
    requireThat(!(model.services.postgres.ports?.length), "PostgreSQL must not publish host ports");
    if (mode === "offline") {
        requireThat(model.networks.edge.internal === true && model.networks.backend.internal === true,
            "offline networks must be internal");
    }
    return { ...config, compose };
}

export function restartApplication(config) {
    return spawn("docker", [...config.compose, "restart", "app"],
        { cwd: root, windowsHide: true, stdio: "ignore" });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        requireThat(process.argv.length === 5 && process.argv[2] === "--config",
            "usage: evidence-fixture.mjs --config <path> <browser|offline>");
        console.log(JSON.stringify(prepareFixture(process.argv[3], process.argv[4])));
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
