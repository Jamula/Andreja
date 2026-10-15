// Preloaded only by the synthetic caller tests. Never contacts a runtime.
import childProcess from "node:child_process";
import { syncBuiltinESMExports } from "node:module";
import { appendFileSync, readFileSync } from "node:fs";

childProcess.execFileSync = (command, args) => {
    if (command === "docker" && args[0] === "context") {
        appendFileSync(process.env.EVIDENCE_TEST_LOG, JSON.stringify({ command, args }) + "\n");
        return JSON.stringify(process.env.EVIDENCE_TEST_CONTEXT ?? "unix:///synthetic/docker.sock");
    }
    if (command !== "docker" || args.at(-3) !== "config"
        || args.at(-2) !== "--format" || args.at(-1) !== "json") {
        throw new Error("Unexpected command in synthetic preflight");
    }
    appendFileSync(process.env.EVIDENCE_TEST_LOG, JSON.stringify({ command, args }) + "\n");
    return readFileSync(process.env.EVIDENCE_TEST_MODEL, "utf8");
};
childProcess.spawn = (command, args) => {
    appendFileSync(process.env.EVIDENCE_TEST_LOG, JSON.stringify({ command, args }) + "\n");
    throw new Error("SYNTHETIC_SCOPED_SPAWN");
};
syncBuiltinESMExports();
