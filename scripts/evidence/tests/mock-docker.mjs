// Preloaded only by the synthetic caller tests. Never contacts a runtime.
import childProcess from "node:child_process";
import { syncBuiltinESMExports } from "node:module";
import fs, { appendFileSync, readFileSync } from "node:fs";
import fsPromises from "node:fs/promises";

const log = entry => appendFileSync(process.env.EVIDENCE_TEST_LOG, JSON.stringify(entry) + "\n");
const runtime = () => JSON.parse(readFileSync(process.env.EVIDENCE_TEST_RUNTIME, "utf8"));

childProcess.execFileSync = (command, args) => {
    if (command === "docker" && args[0] === "context") {
        log({ command, args });
        return JSON.stringify(process.env.EVIDENCE_TEST_CONTEXT ?? "unix:///synthetic/docker.sock");
    }
    if (command !== "docker") {
        throw new Error("Unexpected command in synthetic preflight");
    }
    log({ command, args });
    if (args.at(-3) === "config" && args.at(-2) === "--format" && args.at(-1) === "json") {
        return readFileSync(process.env.EVIDENCE_TEST_MODEL, "utf8");
    }
    const fixture = runtime();
    if (args.includes("ps") && args.includes("--quiet")) {
        if (args.includes("--filter")) {
            const port = args[args.indexOf("--filter") + 1].replace("publish=", "");
            return (fixture.portOwners[port] ?? []).join("\n");
        }
        return fixture.projectContainerIds.join("\n");
    }
    if (args.includes("inspect")) {
        if (args.includes("image")) {
            const image = args[args.indexOf("inspect") + 1];
            return JSON.stringify(fixture.images[image] ?? {});
        }
        const ids = args.slice(args.indexOf("inspect") + 1, args.indexOf("--format"));
        return ids.map(id => JSON.stringify(fixture.containers.find(container => container.Id === id) ?? {}))
            .join("\n");
    }
    throw new Error("Unexpected command in synthetic preflight");
};
childProcess.spawn = (command, args) => {
    log({ command, args });
    throw new Error("SYNTHETIC_SCOPED_SPAWN");
};
const originalReadFileSync = fs.readFileSync;
fs.readFileSync = function (path, ...args) {
    if ([process.env.EVIDENCE_TEST_CERT, process.env.EVIDENCE_TEST_TOKEN].includes(String(path))) {
        log({ command: "credential-read", args: [String(path)] });
    }
    return originalReadFileSync.call(this, path, ...args);
};
const originalReadFile = fsPromises.readFile;
fsPromises.readFile = async function (path, ...args) {
    if ([process.env.EVIDENCE_TEST_CERT, process.env.EVIDENCE_TEST_TOKEN].includes(String(path))) {
        log({ command: "credential-read", args: [String(path)] });
    }
    return originalReadFile.call(this, path, ...args);
};
const originalRm = fsPromises.rm;
fsPromises.rm = async function (path, ...args) {
    if (String(path) === process.env.EVIDENCE_TEST_PROFILE) log({ command: "profile-mutation", args: [String(path)] });
    return originalRm.call(this, path, ...args);
};
globalThis.fetch = async (...args) => {
    log({ command: "http", args: args.map(String) });
    throw new Error("SYNTHETIC_HTTP");
};
syncBuiltinESMExports();
