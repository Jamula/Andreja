import { spawnSync } from "node:child_process";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

export async function startLocalSite(directory, now = () => new Date()) {
  const root = path.resolve(directory);
  const verify = spawnSync("dotnet", [
    "run", "--project", path.join("src", "Andreja.PublicSite"), "--configuration", "Release",
    "--no-build", "--", "verify", root,
  ], { encoding: "utf8", windowsHide: true });
  if (verify.status !== 0) throw new Error(`Artifact verification failed: ${verify.stderr || verify.error}`);
  const manifest = JSON.parse(await readFile(path.join(root, "manifest.json"), "utf8"));
  const headers = JSON.parse(await readFile(path.join(root, "headers.json"), "utf8"));
  const metadata = JSON.parse(await readFile(path.join(root, "site-metadata.json"), "utf8"));
  const earliestExpiry = metadata.pages.map(page => page.expires).sort()[0];
  if (!/^\d{4}-\d{2}-\d{2}$/.test(earliestExpiry ?? "")) {
    throw new Error("Verified artifact has no valid page expiry.");
  }
  const base = new URL(manifest.baseUrl);
  const types = { ".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8", ".json": "application/json", ".txt": "text/plain; charset=utf-8" };
  const server = createServer(async (request, response) => {
    for (const [key, value] of Object.entries(headers)) response.setHeader(key, value);
    if (now().toISOString().slice(0, 10) >= earliestExpiry) {
      response.writeHead(410).end();
      return;
    }
    if (request.headers.host !== base.host || request.method !== "GET") {
      response.writeHead(403).end();
      return;
    }
    let pathname;
    try {
      if (!request.url?.startsWith("/") || request.url.startsWith("//")) throw new TypeError();
      pathname = new URL(request.url, base).pathname;
      decodeURI(request.url);
    } catch {
      response.writeHead(400).end();
      return;
    }
    const name = pathname === "/" ? "index.html" : pathname.slice(1);
    if (!Object.hasOwn(manifest.files, name) || name.includes("/") || name.includes("\\")) {
      response.writeHead(404).end();
      return;
    }
    try {
      const bytes = await readFile(path.join(root, name));
      const { createHash } = await import("node:crypto");
      if (createHash("sha256").update(bytes).digest("hex") !== manifest.files[name]) {
        response.writeHead(409).end("Artifact changed; stop and rebuild into a new directory.");
        return;
      }
      response.setHeader("Content-Type", types[path.extname(name)] ?? "application/octet-stream");
      response.writeHead(200).end(bytes);
    } catch (error) {
      if (error.code === "ENOENT" || error.code === "ENOTDIR") {
        response.writeHead(404).end();
      } else {
        console.error("Local preview: artifact read failed.");
        response.writeHead(500).end();
      }
    }
  });
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(Number(base.port), "127.0.0.1", resolve);
  });
  return { server, baseUrl: base.origin };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const { server, baseUrl } = await startLocalSite(process.argv[2] ?? path.join("artifacts", "public-site"));
  console.log(`Private loopback review only: ${baseUrl}. Noindex is not authorization. Ctrl+C stops.`);
  process.on("SIGINT", () => server.close());
}
