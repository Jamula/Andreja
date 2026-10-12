import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { randomBytes, timingSafeEqual } from "node:crypto";

// JSON can escape each draft byte and identity character as six ASCII bytes.
const maxDraftRequestBytes = 6 * (16000 + 160) + Buffer.byteLength(JSON.stringify({ id: "", text: "" }));
const assets = new Map([
    ["/", ["index.html", "text/html; charset=utf-8"]],
    ["/client.mjs", ["client.mjs", "text/javascript; charset=utf-8"]],
    ["/style.css", ["style.css", "text/css; charset=utf-8"]],
    ["/model.mjs", ["browser-model.mjs", "text/javascript; charset=utf-8"]],
]);
export async function startServer(store, drafts) {
    const capability = randomBytes(32).toString("hex");
    const credential = Buffer.from(`Bearer ${capability}`);
    const csrf = randomBytes(32).toString("hex");
    let origin;
    const server = createServer(async (req, res) => {
        const reply = (status, data) => {
            res.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
            res.end(JSON.stringify(data));
        };
        res.setHeader("Cache-Control", "no-store");
        res.setHeader("X-Content-Type-Options", "nosniff");
        res.setHeader("Referrer-Policy", "no-referrer");
        res.setHeader("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'none'; base-uri 'none'; form-action 'none'");
        try {
            if (req.headers.host !== new URL(origin).host) return reply(403, { error: "Invalid host" });
            if (req.headers.origin && req.headers.origin !== origin) return reply(403, { error: "Foreign origin" });
            if (req.headers["sec-fetch-site"] === "cross-site" && req.url !== "/") return reply(403, { error: "Cross-site request" });
            const url = new URL(req.url, origin);
            if (req.method === "GET" && assets.has(url.pathname)) {
                const [file, type] = assets.get(url.pathname);
                res.writeHead(200, { "Content-Type": type });
                res.end(await readFile(new URL(file, import.meta.url)));
                return;
            }
            const authorization = Buffer.from(req.headers.authorization || "");
            if (authorization.length !== credential.length || !timingSafeEqual(authorization, credential))
                return reply(403, { error: "Panel authorization required" });
            if (req.method === "GET" && url.pathname === "/state") return reply(200, { ...store.snapshot(), csrf });
            if (req.method === "GET" && url.pathname === "/draft") {
                const id = url.searchParams.get("id");
                if (!store.cards.some(card => card.id === id)) return reply(404, { error: "Unknown decision" });
                return reply(200, await drafts.get(id));
            }
            if (req.method !== "POST" || !["/refresh", "/draft"].includes(url.pathname)) return reply(404, { error: "Not found" });
            if (req.headers.origin !== origin || req.headers["x-canvas-csrf"] !== csrf
                || req.headers["content-type"] !== "application/json") return reply(403, { error: "Same-origin JSON request required" });
            const chunks = [];
            let size = 0;
            const limit = url.pathname === "/draft" ? maxDraftRequestBytes : 20000;
            for await (const chunk of req) {
                size += chunk.length;
                if (size > limit) { reply(413, { error: "Request too large" }); req.resume(); return; }
                chunks.push(chunk);
            }
            const data = JSON.parse(Buffer.concat(chunks).toString("utf8") || "{}");
            if (url.pathname === "/refresh") {
                await store.refresh();
                return reply(store.error ? 502 : 200, store.snapshot());
            }
            if (!store.cards.some(card => card.id === data.id)) return reply(404, { error: "Unknown decision" });
            try {
                return reply(200, await drafts.save(data.id, data.text));
            } catch (error) {
                if (error.message === "Draft must be at most 16 KB") return reply(413, { error: error.message });
                throw error;
            }
        } catch { reply(500, { error: "Request failed. No GitHub write was performed; retry or check local artifact access." }); }
    });
    server.requestTimeout = 30000;
    server.headersTimeout = 10000;
    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(0, "127.0.0.1", resolve);
    });
    origin = `http://127.0.0.1:${server.address().port}`;
    return { server, url: `${origin}/#capability=${capability}` };
}
