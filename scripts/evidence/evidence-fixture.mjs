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

function isWindowsAbsolutePath(value) {
    return typeof value === "string" && /^[a-z]:[\\/]/i.test(value);
}

function normalizedWindowsPath(value) {
    return path.win32.normalize(value).replace(/[\\/]+$/, "").toLowerCase();
}

function existingRealPath(value) {
    try {
        return realpathSync(value);
    } catch {
        return null;
    }
}

export function sameMountSource(expected, observed) {
    if (typeof expected !== "string" || typeof observed !== "string") return false;
    if (isWindowsAbsolutePath(expected)) {
        const expectedRealPath = existingRealPath(expected) ?? expected;
        if (isWindowsAbsolutePath(observed)) {
            return normalizedWindowsPath(expectedRealPath)
                === normalizedWindowsPath(existingRealPath(observed) ?? observed);
        }
        const desktopPath = /^\/run\/desktop\/mnt\/host\/([a-z])(?:\/(.*))?$/i.exec(observed);
        if (!desktopPath) return false;
        const mappedPath = `${desktopPath[1]}:\\${(desktopPath[2] ?? "").replaceAll("/", "\\")}`;
        return normalizedWindowsPath(expectedRealPath) === normalizedWindowsPath(mappedPath);
    }
    if (isWindowsAbsolutePath(observed)) return false;
    const expectedRealPath = existingRealPath(expected);
    const observedRealPath = existingRealPath(observed);
    return expectedRealPath !== null && expectedRealPath === observedRealPath;
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

function inspectJson(command, args) {
    const output = execFileSync(command, args,
        { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
    return output ? output.split(/\r?\n/).map(line => JSON.parse(line)) : [];
}

function verifyRuntimeFixture(config, compose, model) {
    const reportedIds = execFileSync("docker", [...compose, "ps", "--all", "--quiet"],
        { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] })
        .split(/\r?\n/).map(value => value.trim()).filter(Boolean);
    requireThat(reportedIds.length === services.length && new Set(reportedIds).size === services.length,
        "running fixture must contain exactly one container for every approved service");

    const inspected = inspectJson("docker", ["--context", config.dockerContext, "inspect",
        ...reportedIds, "--format", "{{json .}}"]);
    requireThat(inspected.length === reportedIds.length, "Docker inspect did not return every project container");
    const byService = new Map();
    for (const container of inspected) {
        const labels = container.Config?.Labels ?? {};
        const project = labels["com.docker.compose.project"];
        const service = labels["com.docker.compose.service"];
        requireThat(project === config.projectName && services.includes(service),
            "runtime container has the wrong Compose project/service labels");
        requireThat(!byService.has(service), `runtime project has multiple ${service} containers`);
        requireThat(container.State?.Running === true && container.State?.Status === "running",
            `runtime ${service} container is not running`);
        requireThat(container.Config?.Image === config.approvedImages[service],
            `runtime ${service} image differs from its exact approved immutable reference`);
        byService.set(service, container);
    }
    requireThat(services.every(service => byService.has(service)),
        "runtime project is missing an approved service container");

    const expectedNetworkMembers = new Map(Object.keys(model.networks ?? {}).map(name => [name, new Set()]));
    const networkInfo = new Map();
    for (const service of services) {
        const serviceNetworks = model.services[service].networks ?? {};
        for (const name of Object.keys(serviceNetworks)) {
            requireThat(expectedNetworkMembers.has(name),
                `validated Compose model references an unknown network for ${service}`);
            expectedNetworkMembers.get(name).add(byService.get(service).Id);
        }
        const container = byService.get(service);
        requireThat(!container.HostConfig?.ExtraHosts?.length,
            `runtime ${service} must not override service hostname resolution`);
        const runtimeNetworks = container.NetworkSettings?.Networks ?? {};
        const expectedNames = Object.keys(serviceNetworks).map(name => model.networks[name].name).sort();
        requireThat(JSON.stringify(Object.keys(runtimeNetworks).sort()) === JSON.stringify(expectedNames),
            `runtime ${service} network membership differs from the validated Compose model`);
        for (const name of Object.keys(serviceNetworks)) {
            const networkName = model.networks[name].name;
            const network = runtimeNetworks[networkName];
            requireThat(typeof network?.NetworkID === "string" && network.NetworkID.length > 0,
                `runtime ${service} is missing its ${name} network identity`);
            if (name === "backend") {
                const aliases = network.Aliases ?? [];
                requireThat(aliases.includes("postgres") === (service === "postgres"),
                    "the PostgreSQL service alias must belong only to the authorized PostgreSQL container");
            } else {
                requireThat(!(network.Aliases ?? []).includes("postgres"),
                    "the PostgreSQL service alias is present outside the authorized backend");
            }
        }
    }
    for (const [name, members] of expectedNetworkMembers) {
        requireThat(members.size > 0, `validated Compose network ${name} has no approved service members`);
        const expectedName = model.networks[name].name;
        const network = inspectJson("docker", ["--context", config.dockerContext, "network", "inspect",
            expectedName, "--format", "{{json .}}"])[0];
        requireThat(typeof network?.Id === "string" && network.Id.length > 0
            && network.Name === expectedName
            && network.Labels?.["com.docker.compose.project"] === config.projectName
            && network.Labels?.["com.docker.compose.network"] === name,
        `runtime network ${name} identity or Compose ownership differs from the validated model`);
        const containers = network.Containers ?? {};
        requireThat(JSON.stringify(Object.keys(containers).sort()) === JSON.stringify([...members].sort()),
            `runtime network ${name} has unexpected or missing service members`);
        networkInfo.set(name, network);
    }
    for (const service of services) {
        const container = byService.get(service);
        const serviceNetworks = model.services[service].networks ?? {};
        for (const name of Object.keys(serviceNetworks)) {
            const runtimeNetwork = container.NetworkSettings.Networks[model.networks[name].name];
            requireThat(runtimeNetwork.NetworkID === networkInfo.get(name).Id,
                `runtime ${service} is attached to an unapproved ${name} network`);
        }
    }

    for (const service of services) {
        const container = byService.get(service);
        const serviceModel = model.services[service];
        const runtimeMounts = container.Mounts ?? [];
        const expectedVolumes = (serviceModel.volumes ?? []).filter(mount => mount.type === "volume");
        const runtimeVolumes = runtimeMounts.filter(mount => mount.Type === "volume");
        requireThat(runtimeVolumes.length === expectedVolumes.length,
            `runtime ${service} volume mounts differ from the validated Compose model`);
        for (const expected of expectedVolumes) {
            const expectedName = model.volumes[expected.source]?.name;
            const matching = runtimeVolumes.filter(mount => mount.Destination === expected.target);
            requireThat(typeof expectedName === "string" && matching.length === 1
                && matching[0].Name === expectedName
                && matching[0].RW === (expected.read_only !== true),
            `runtime ${service} volume identity or destination differs from the validated Compose model`);
        }
        const expectedBinds = (serviceModel.volumes ?? []).filter(mount => mount.type === "bind");
        const expectedSecrets = (serviceModel.secrets ?? []).map(secret => {
            const source = typeof secret === "string" ? secret : secret.source;
            const target = typeof secret === "string" ? secret : secret.target ?? source;
            requireThat(typeof source === "string" && typeof target === "string" && target.length > 0
                && typeof model.secrets[source]?.file === "string",
            `validated Compose model has an invalid ${service} secret mount`);
            return { source: model.secrets[source]?.file, target: target.startsWith("/")
                ? target : `/run/secrets/${target}` };
        });
        const secretTargets = new Set(expectedSecrets.map(secret => secret.target));
        const runtimeBinds = runtimeMounts.filter(mount => mount.Type === "bind"
            && !secretTargets.has(mount.Destination));
        requireThat(runtimeBinds.length === expectedBinds.length,
            `runtime ${service} bind mounts differ from the validated Compose model`);
        for (const expected of expectedBinds) {
            const matching = runtimeBinds.filter(mount => mount.Destination === expected.target);
            requireThat(matching.length === 1 && matching[0].RW === (expected.read_only !== true)
                && typeof matching[0].Source === "string"
                && sameMountSource(expected.source, matching[0].Source),
            `runtime ${service} bind source, destination, or access mode differs from the validated Compose model`);
        }
        for (const expected of expectedSecrets) {
            const matching = runtimeMounts.filter(mount => mount.Destination === expected.target);
            requireThat(typeof expected.source === "string" && matching.length === 1
                && ["bind", "tmpfs"].includes(matching[0].Type) && matching[0].RW === false
                && (matching[0].Type !== "bind" || (typeof matching[0].Source === "string"
                    && sameMountSource(expected.source, matching[0].Source))),
            `runtime ${service} secret mount differs from the validated Compose model`);
        }
        const image = inspectJson("docker", ["--context", config.dockerContext, "image", "inspect",
            config.approvedImages[service], "--format", "{{json .}}"])[0];
        requireThat(typeof image?.Id === "string" && image.Id.length > 0
            && container.Image === image.Id,
        `runtime ${service} image identity differs from the approved image`);
        requireThat(image.Config && typeof image.Config === "object",
            `approved ${service} image is missing its runtime defaults`);
        const normalizeArguments = value => value === null || value === undefined
            ? [] : Array.isArray(value) ? value : [value];
        for (const [property, composeProperty] of [["Entrypoint", "entrypoint"], ["Cmd", "command"]]) {
            const expected = serviceModel[composeProperty] ?? image.Config[property];
            requireThat(JSON.stringify(normalizeArguments(container.Config?.[property]))
                === JSON.stringify(normalizeArguments(expected)),
            `runtime ${service} ${property === "Cmd" ? "command" : "entrypoint"} differs from the validated Compose model and approved image`);
        }
        const hasHostOverrides = value => value === null || value === undefined
            ? false : Array.isArray(value) || typeof value === "object"
                ? Object.keys(value).length > 0 : true;
        requireThat(container.HostConfig && typeof container.HostConfig === "object"
            && !hasHostOverrides(serviceModel.extra_hosts)
            && !hasHostOverrides(container.HostConfig.ExtraHosts),
        `runtime ${service} contains unapproved extra host mappings`);
    }

    const appEnvironment = byService.get("app").Config.Env ?? [];
    const environmentValue = name => {
        const entries = appEnvironment.filter(entry => entry.slice(0, entry.indexOf("=")) === name);
        requireThat(entries.length === 1, `runtime application must define ${name} exactly once`);
        return entries[0].slice(entries[0].indexOf("=") + 1);
    };
    requireThat(environmentValue("Andreja__OpenLoops__AssistantProvider") === "deterministic",
        "runtime application provider is not deterministic");
    requireThat(environmentValue("Andreja__Identity__AllowedOrigins__0") === new URL(config.baseUrl).origin,
        "runtime application WebAuthn origin differs from the explicit browser endpoint");
    const databaseSettings = [
        "Enabled", "Host", "Port", "Name", "Username", "PasswordFile",
    ].map(setting => `Andreja__Operations__Database__${setting}`);
    for (const setting of databaseSettings) {
        requireThat(environmentValue(setting) === model.services.app.environment[setting],
            `runtime application ${setting} differs from the validated Compose model`);
    }

    const publishedPorts = [
        ["tls-proxy", 8443, config.baseUrl], ["app", 8080, config.healthUrl],
        ["otel-collector", 8888, config.collectorMetricsUrl], ["prometheus", 9090, config.prometheusUrl],
    ];
    for (const [service, target, endpoint] of publishedPorts) {
        const container = byService.get(service);
        const published = String(new URL(endpoint).port);
        const mappings = container.NetworkSettings?.Ports ?? {};
        const bindings = Object.values(mappings).flatMap(value => value ?? []);
        const targetBindings = mappings[`${target}/tcp`] ?? [];
        requireThat(targetBindings.length === 1 && targetBindings[0].HostIp === "127.0.0.1"
            && String(targetBindings[0].HostPort) === published && bindings.length === 1,
        `runtime ${service} published binding differs from its explicit loopback endpoint`);

        const owners = execFileSync("docker", ["--context", config.dockerContext, "ps", "--all", "--quiet",
            "--no-trunc", "--filter", `publish=${published}`],
        { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] })
            .split(/\r?\n/).map(value => value.trim()).filter(Boolean);
        const foreignOwners = owners.filter(id => !reportedIds.includes(id));
        if (foreignOwners.length) {
            const foreign = inspectJson("docker", ["--context", config.dockerContext, "inspect",
                ...foreignOwners, "--format", "{{json .}}"]);
            requireThat(foreign.every(owner =>
                owner.Config?.Labels?.["com.docker.compose.project"] === config.projectName
                && services.includes(owner.Config?.Labels?.["com.docker.compose.service"])),
            `configured ${service} host port is owned by a container outside the authorized project`);
            requireThat(false, `configured ${service} host port has an additional runtime owner`);
        }
    }

    const postgresBindings = Object.values(byService.get("postgres").NetworkSettings?.Ports ?? {})
        .flatMap(value => value ?? []);
    requireThat(postgresBindings.length === 0, "runtime PostgreSQL container must not publish host ports");
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
    requireThat(["browser", "browser-static", "offline"].includes(mode), "unknown evidence mode");
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
    const database = app.environment;
    const postgres = model.services.postgres.environment;
    requireThat(database.Andreja__Operations__Database__Enabled === "true"
        && database.Andreja__Operations__Database__Host === "postgres"
        && database.Andreja__Operations__Database__Port === "5432"
        && database.Andreja__Operations__Database__Name === postgres.POSTGRES_DB
        && database.Andreja__Operations__Database__Username === postgres.POSTGRES_USER
        && database.Andreja__Operations__Database__PasswordFile === "/run/secrets/postgres_password",
    "resolved database settings must target the fixture PostgreSQL service and its credentials");
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
    } else if (mode === "browser") {
        verifyRuntimeFixture(config, compose, model);
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
            "usage: evidence-fixture.mjs --config <path> <browser|browser-static|offline>");
        console.log(JSON.stringify(prepareFixture(process.argv[3], process.argv[4])));
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
