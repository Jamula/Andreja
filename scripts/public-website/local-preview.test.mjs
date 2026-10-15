import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { request } from "node:http";
import path from "node:path";
import { test } from "node:test";
import { startLocalSite } from "./local-preview.mjs";

test("loopback adapter fails closed and stays responsive after rejected requests", async t => {
  const directory = path.resolve("artifacts", `site-adapter-${process.pid}`);
  const root = path.join(directory, "site");
  let server;
  await mkdir(directory, { recursive: true });
  try {
    const build = spawnSync("dotnet", [
      "run", "--project", "src\\Andreja.PublicSite", "--configuration", "Release", "--no-build",
      "--", "build", "src\\Andreja.PublicSite\\Content",
      "src\\Andreja.PublicSite\\Content\\local.config.json", root,
    ], { encoding: "utf8", windowsHide: true });
    assert.equal(build.status, 0, `Fixture build failed: ${build.stderr || build.error}`);
    const site = await startLocalSite(root);
    server = site.server;
    const base = new URL(site.baseUrl);
    const original = await readFile(path.join(root, "index.html"));
    const get = (target = "/", options = {}) => new Promise((resolve, reject) => {
      const req = request({
        hostname: "127.0.0.1", port: base.port, path: target, method: "GET",
        ...options, headers: { Host: base.host, ...options.headers },
      }, response => {
        const chunks = [];
        response.on("data", chunk => chunks.push(chunk));
        response.on("end", () => resolve({
          status: response.statusCode, headers: response.headers,
          bytes: Buffer.concat(chunks),
        }));
        response.on("error", reject);
      });
      req.setTimeout(5000, () => req.destroy(new Error("Adapter request timed out")));
      req.on("error", reject);
      req.end();
    });
    const rejected = async (target, expected, options) => {
      const response = await get(target, options);
      assert.equal(response.status, expected, target);
      assert.equal(response.bytes.length, 0, "Rejected requests must not expose artifact/error data");
      assert.equal(response.headers["x-robots-tag"], "noindex, nofollow, noarchive");
      assert.match(response.headers["content-security-policy"], /connect-src 'none'/);
      const healthy = await get();
      assert.equal(healthy.status, 200, "Server must remain responsive after rejection");
      assert.deepEqual(healthy.bytes, original, "Healthy request must serve verified content");
    };

    await t.test("malformed targets return 400 without terminating the server", async () => {
      for (const target of ["http://[", "//[", "/%ZZ", "/%E0%A4%A"]) {
        await rejected(target, 400);
      }
    });
    await t.test("wrong Host and non-GET requests are denied", async () => {
      await rejected("/", 403, { headers: { Host: "localhost:" + base.port } });
      await rejected("/", 403, { method: "POST" });
    });
    await t.test("traversal and unlisted files never expose repository data", async () => {
      for (const target of [
        "/../manifest.json", "/%2e%2e/manifest.json", "/..%2findex.html",
        "/..%5cindex.html", "/manifest.json", "/Account/Login",
      ]) await rejected(target, 404);
    });
    await t.test("changed bytes return 409 instead of being served", async () => {
      try {
        await writeFile(path.join(root, "index.html"), "changed artifact");
        const response = await get();
        assert.equal(response.status, 409);
        assert.equal(response.bytes.toString(), "Artifact changed; stop and rebuild into a new directory.");
      } finally {
        await writeFile(path.join(root, "index.html"), original);
      }
    });
    await t.test("missing files return 404", async () => {
      try {
        await rm(path.join(root, "index.html"));
        const response = await get();
        assert.equal(response.status, 404);
        assert.equal(response.bytes.length, 0);
      } finally {
        await writeFile(path.join(root, "index.html"), original);
      }
    });
    await t.test("non-directory artifact root returns 404", async () => {
      const moved = path.join(directory, "moved-site");
      await rename(root, moved);
      try {
        await writeFile(root, "not a directory");
        const response = await get();
        assert.equal(response.status, 404);
        assert.equal(response.bytes.length, 0);
      } finally {
        await rm(root, { force: true });
        await rename(moved, root);
      }
    });
    await t.test("unexpected read errors return 500 with only a safe diagnostic", async () => {
      const messages = [];
      const logger = t.mock.method(console, "error", (...args) => messages.push(args));
      try {
        await rm(path.join(root, "index.html"));
        await mkdir(path.join(root, "index.html"));
        const response = await get();
        assert.equal(response.status, 500);
        assert.equal(response.bytes.length, 0);
        assert.deepEqual(messages, [["Local preview: artifact read failed."]]);
      } finally {
        logger.mock.restore();
        await rm(path.join(root, "index.html"), { recursive: true, force: true });
        await writeFile(path.join(root, "index.html"), original);
      }
      const healthy = await get();
      assert.equal(healthy.status, 200);
      assert.deepEqual(healthy.bytes, original);
    });
  } finally {
    if (server) await new Promise(resolve => server.close(resolve));
    await rm(directory, { recursive: true, force: true });
  }
});
