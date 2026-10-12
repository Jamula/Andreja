import test from "node:test";
import assert from "node:assert/strict";
import { rm } from "node:fs/promises";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { DraftStore, REPO } from "./model.mjs";
import { startServer } from "./server.mjs";

test("HTTP draft transport accommodates escaping while DraftStore enforces UTF-8 bytes", async () => {
    const root = join(process.cwd(), `.open-decisions-server-review-${randomUUID()}`);
    const id = `${REPO}:`.padEnd(160, "\u0000");
    const drafts = new DraftStore(root);
    const store = { cards: [{ id }], snapshot() { return { cards: this.cards }; } };
    let server;
    try {
        const started = await startServer(store, drafts);
        server = started.server;
        const { url } = started;
        const { csrf } = await (await fetch(`${url}state`)).json();
        const post = (body, route = "draft") => fetch(`${url}${route}`, {
            method: "POST",
            headers: { "Content-Type": "application/json", Origin: url.slice(0, -1), "X-Canvas-CSRF": csrf },
            body,
        });
        const accepted = [
            '"'.repeat(12000),
            "\u0000".repeat(16000),
            "é".repeat(8000),
            "😀".repeat(4000),
        ];
        for (const text of accepted) {
            assert.ok(Buffer.byteLength(text) <= 16000);
            const response = await post(JSON.stringify({ id, text }));
            assert.equal(response.status, 200);
            assert.equal((await response.json()).text, text);
            assert.equal((await new DraftStore(root).get(id)).text, text);
            const loaded = await fetch(`${url}draft?id=${encodeURIComponent(id)}`);
            assert.equal(loaded.status, 200);
            assert.equal((await loaded.json()).text, text);
        }
        const escapedUnicode = await post(`{"id":${JSON.stringify(id)},"text":"${"\\u00e9".repeat(8000)}"}`);
        assert.equal(escapedUnicode.status, 200);
        assert.equal((await escapedUnicode.json()).text, "é".repeat(8000));
        const savedText = (await drafts.get(id)).text;
        for (const text of ["x".repeat(16001), `${"é".repeat(8000)}x`, `${"😀".repeat(4000)}x`]) {
            assert.equal(Buffer.byteLength(text), 16001);
            const response = await post(JSON.stringify({ id, text }));
            assert.equal(response.status, 413);
            assert.match((await response.json()).error, /16 KB/);
            assert.equal((await drafts.get(id)).text, savedText);
        }
        const limit = 6 * (16000 + 160) + Buffer.byteLength(JSON.stringify({ id: "", text: "" }));
        const oversized = JSON.stringify({ id, text: "small", padding: "x".repeat(limit) });
        assert.ok(Buffer.byteLength(oversized) > limit);
        const response = await post(oversized);
        assert.equal(response.status, 413);
        assert.equal((await response.json()).error, "Request too large");
        assert.equal((await drafts.get(id)).text, savedText);
        assert.equal((await post(JSON.stringify({ padding: "x".repeat(21000) }), "refresh")).status, 413);
    } finally {
        if (server) await new Promise(resolve => server.close(resolve));
        await rm(root, { recursive: true, force: true });
    }
});
