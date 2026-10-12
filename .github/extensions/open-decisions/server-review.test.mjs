import test from "node:test";
import assert from "node:assert/strict";
import { readFile, rm } from "node:fs/promises";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { DraftStore, REPO } from "./model.mjs";
import { startServer } from "./server.mjs";

test("panel capability gates every API despite forged Host/Origin and known CSRF; public assets reveal no secrets", async () => {
    const root = join(process.cwd(), `.open-decisions-capability-${randomUUID()}`);
    const id = `${REPO}:private-question`;
    const calls = { snapshot: 0, refresh: 0, get: 0, save: 0 };
    const persisted = new DraftStore(root);
    const drafts = {
        get: async id => { calls.get++; return persisted.get(id); },
        save: async (id, text) => { calls.save++; return persisted.save(id, text); },
    };
    const store = {
        cards: [{ id, body: "PRIVATE_SOURCE_SENTINEL" }],
        snapshot() { calls.snapshot++; return { cards: this.cards }; },
        async refresh() { calls.refresh++; },
    };
    const panels = [];
    try {
        await persisted.save(id, "PRIVATE_DRAFT_SENTINEL");
        const first = await startServer(store, drafts);
        const other = await startServer(store, drafts);
        panels.push(first, other);
        const url = new URL(first.url);
        const capability = new URLSearchParams(url.hash.slice(1)).get("capability");
        const otherCapability = new URLSearchParams(new URL(other.url).hash.slice(1)).get("capability");
        assert.match(url.hash, /^#capability=[a-f0-9]{64}$/);
        assert.notEqual(capability, otherCapability);
        assert.equal(url.search, "");
        const bearer = { Authorization: `Bearer ${capability}` };
        const state = await (await fetch(new URL("state", url), { headers: bearer })).json();
        assert.equal(state.cards[0].id, id);
        assert.equal("capability" in state, false);
        const before = { ...calls };
        for (const route of ["state", `draft?id=${encodeURIComponent(id)}`, `state?capability=${capability}`]) {
            const response = await fetch(new URL(route, url));
            assert.equal(response.status, 403);
            assert.deepEqual(await response.json(), { error: "Panel authorization required" });
        }
        for (const authorization of [undefined, "Bearer wrong", `Bearer ${"0".repeat(64)}`, `Bearer ${otherCapability}`]) {
            for (const [method, route] of [["GET", "state"], ["GET", `draft?id=${encodeURIComponent(id)}`],
                ["POST", "refresh"], ["POST", "draft"]]) {
                const response = await fetch(new URL(route, url), {
                    method,
                    headers: { Host: url.host, Origin: url.origin, "Content-Type": "application/json",
                        "X-Canvas-CSRF": state.csrf, ...(authorization ? { Authorization: authorization } : {}) },
                    ...(method === "POST" ? { body: JSON.stringify({ id, text: "forged draft" }) } : {}),
                });
                assert.equal(response.status, 403, `${method} ${route} must reject an invalid capability`);
                assert.deepEqual(await response.json(), { error: "Panel authorization required" });
            }
        }
        assert.deepEqual(calls, before, "Unauthorized requests must not access stores or trigger refresh");
        assert.equal((await persisted.get(id)).text, "PRIVATE_DRAFT_SENTINEL");
        for (const route of ["", "client.mjs", "model.mjs", "style.css"]) {
            const response = await fetch(new URL(route, url));
            assert.equal(response.status, 200);
            assert.equal(response.headers.get("referrer-policy"), "no-referrer");
            assert.equal(response.headers.get("cache-control"), "no-store");
            const text = await response.text();
            for (const secret of [capability, otherCapability, state.csrf, id, "PRIVATE_SOURCE_SENTINEL", "PRIVATE_DRAFT_SENTINEL"])
                assert.ok(!text.includes(secret), `${route || "HTML"} must contain no private data or credential`);
        }
        const get = await fetch(new URL(`draft?id=${encodeURIComponent(id)}`, url), { headers: bearer });
        assert.equal(get.status, 200);
        assert.equal((await get.json()).text, "PRIVATE_DRAFT_SENTINEL");
        const post = route => fetch(new URL(route, url), { method: "POST",
            headers: { ...bearer, Origin: url.origin, "Content-Type": "application/json", "X-Canvas-CSRF": state.csrf },
            body: JSON.stringify({ id, text: "authorized private draft" }) });
        assert.equal((await post("draft")).status, 200);
        assert.equal((await post("refresh")).status, 200);
        assert.equal(calls.save, 1);
        assert.equal(calls.refresh, 1);
        const rehydrated = await startServer(store, new DraftStore(root));
        panels.push(rehydrated);
        const freshURL = new URL(rehydrated.url);
        const freshCapability = new URLSearchParams(freshURL.hash.slice(1)).get("capability");
        const draftURL = new URL(`draft?id=${encodeURIComponent(id)}`, freshURL);
        assert.equal((await fetch(draftURL, { headers: bearer })).status, 403);
        const recovered = await fetch(draftURL, { headers: { Authorization: `Bearer ${freshCapability}` } });
        assert.equal(recovered.status, 200);
        assert.equal((await recovered.json()).text, "authorized private draft");
        const files = await readFile(persisted.path(id), "utf8");
        assert.ok(!files.includes(capability) && !files.includes(freshCapability));
    } finally {
        await Promise.all(panels.map(({ server }) => new Promise(resolve => server.close(resolve))));
        await rm(root, { recursive: true, force: true });
    }
});

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
        const capability = new URLSearchParams(new URL(url).hash.slice(1)).get("capability");
        const headers = { Authorization: `Bearer ${capability}` };
        const { csrf } = await (await fetch(new URL("state", url), { headers })).json();
        const post = (body, route = "draft") => fetch(new URL(route, url), {
            method: "POST",
            headers: { ...headers, "Content-Type": "application/json", Origin: new URL(url).origin, "X-Canvas-CSRF": csrf },
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
            const loaded = await fetch(new URL(`draft?id=${encodeURIComponent(id)}`, url), { headers });
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
