import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { readFile, rm, mkdir } from "node:fs/promises";
import path from "node:path";
import { startLocalSite } from "./local-preview.mjs";

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const directory = process.argv[2] ?? "artifacts\\public-site";
const profile = path.resolve("artifacts", `site-browser-${process.pid}`);
let browser;
let socket;
let server;
try {
  const site = await startLocalSite(directory);
  server = site.server;
  const response = await fetch(site.baseUrl);
  assert.equal(response.status, 200, "loopback preview must be responsive");
  assert.match(response.headers.get("content-security-policy"), /connect-src 'none'/);
  assert.equal(response.headers.get("x-robots-tag"), "noindex, nofollow, noarchive");
  assert.equal((await fetch(`${site.baseUrl}/Account/Login`)).status, 404);
  assert.equal((await fetch(`${site.baseUrl}/.well-known/security.txt`)).status, 404);
  assert.equal((await fetch(site.baseUrl, { method: "POST" })).status, 403);
  await mkdir(profile, { recursive: true });
  browser = spawn(process.env.EDGE_PATH ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    ["--headless=new", "--disable-gpu", "--no-first-run", "--disable-background-networking",
      "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank"],
    { stdio: "ignore", windowsHide: true });
  browser.on("error", error => console.error(error.message));
  let port;
  for (let i = 0; i < 100; i++) {
    try { port = Number((await readFile(path.join(profile, "DevToolsActivePort"), "utf8")).split("\n")[0]); break; }
    catch { await sleep(100); }
  }
  assert.ok(port, "Edge must start its local CDP endpoint");
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  socket = new WebSocket(targets.find(target => target.type === "page").webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  let next = 0;
  const pending = new Map();
  const requests = [];
  const errors = [];
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (message.method === "Network.requestWillBeSent") requests.push(message.params.request);
    if (message.method === "Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
    if (message.id) {
      const callback = pending.get(message.id);
      if (!callback) return;
      pending.delete(message.id);
      clearTimeout(callback.timeout);
      if (message.error) callback.reject(new Error(message.error.message));
      else callback.resolve(message.result);
    }
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++next;
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 15000);
    pending.set(id, { resolve, reject, timeout });
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await send("Runtime.evaluate", { expression, returnByValue: true });
    assert.equal(result.exceptionDetails, undefined, expression);
    return result.result.value;
  };
  const until = async expression => {
    for (let i = 0; i < 100; i++) {
      if (await evaluate(expression)) return;
      await sleep(50);
    }
    throw new Error(`Browser state timed out: ${expression}`);
  };
  const key = async (name, code, vk) => {
    await send("Input.dispatchKeyEvent", { type: "keyDown", key: name, code, windowsVirtualKeyCode: vk });
    await send("Input.dispatchKeyEvent", { type: "keyUp", key: name, code, windowsVirtualKeyCode: vk });
  };
  await send("Page.enable");
  await send("Runtime.enable");
  await send("Network.enable");
  const metadata = JSON.parse(await readFile(path.join(directory, "site-metadata.json"), "utf8"));
  for (const width of [320, 768, 1280]) {
    await send("Emulation.setDeviceMetricsOverride", { width, height: 900, deviceScaleFactor: 1, mobile: false });
    for (const page of metadata.pages) {
      await send("Page.navigate", { url: `${site.baseUrl}/${page.slug}.html` });
      await until(`document.readyState === "complete" && document.querySelector("h1")?.textContent === ${JSON.stringify(page.title)}`);
      const state = await evaluate(`({
        overflow: document.documentElement.scrollWidth > innerWidth,
        h1: document.querySelectorAll("h1").length,
        main: !!document.querySelector("main[aria-labelledby='page-title']"),
        language: document.documentElement.lang,
        unnamed: [...document.querySelectorAll("a, button, input")].filter(element =>
          !(element.textContent.trim() || element.getAttribute("aria-label") || element.labels?.length)).length
      })`);
      assert.deepEqual(state, { overflow: false, h1: 1, main: true, language: "en", unnamed: 0 }, `${page.slug} at ${width}px`);
    }
  }
  await send("Page.navigate", { url: `${site.baseUrl}/index.html` });
  await until(`document.readyState === "complete" && !!window.siteIndex`);
  await key("Tab", "Tab", 9);
  assert.equal(await evaluate("document.activeElement.textContent"), "Skip to content");
  await key("Enter", "Enter", 13);
  await until(`document.activeElement.id === "main-content"`);
  await key("Tab", "Tab", 9);
  assert.equal(await evaluate("document.activeElement.id"), "site-search");
  await send("Input.insertText", { text: "recovery" });
  await until(`document.querySelectorAll("#search-results a").length > 0`);
  assert.match(await evaluate('document.getElementById("search-status").textContent'), /matching chapter/);
  await key("Tab", "Tab", 9);
  assert.equal(await evaluate('document.activeElement.closest("#search-results") !== null'), true);
  const target = await evaluate("document.activeElement.href");
  await key("Enter", "Enter", 13);
  await until(`location.href === ${JSON.stringify(target)} && document.readyState === "complete"`);
  await evaluate('document.getElementById("site-search").focus()');
  await send("Input.insertText", { text: "zzznomatchzz" });
  await until(`document.getElementById("search-status").textContent.startsWith("No matching")`);
  await key("Escape", "Escape", 27);
  assert.equal(await evaluate('document.getElementById("site-search").value'), "");
  assert.equal(await evaluate('document.querySelectorAll("#search-results a").length'), 0);
  await send("Input.insertText", { text: "<img src=x onerror=alert(1)>" });
  assert.equal(await evaluate('document.querySelectorAll("#search-results img").length'), 0);
  assert.deepEqual(await evaluate("({cookies: document.cookie, local: localStorage.length, session: sessionStorage.length})"),
    { cookies: "", local: 0, session: 0 });
  for (const request of requests) {
    assert.equal(new URL(request.url).origin, site.baseUrl, request.url);
    assert.equal(request.method, "GET");
  }
  assert.deepEqual(errors, []);
  await send("Emulation.setScriptExecutionDisabled", { value: true });
  await send("Page.navigate", { url: `${site.baseUrl}/identity.html` });
  await sleep(500);
  assert.match(await (await fetch(`${site.baseUrl}/identity.html`)).text(), /<noscript>Search needs JavaScript/);
  console.log(`PASS: ${metadata.pages.length} pages at 320/768/1280px; keyboard skip/search/result activation/Escape; live/no-result states; CSP headers; no app routes, remote requests, cookies or persistence. Manual screen-reader/WCAG conformance unverified.`);
} finally {
  socket?.close();
  if (browser && browser.exitCode === null) {
    browser.kill();
    await new Promise(resolve => {
      browser.once("exit", resolve);
      setTimeout(resolve, 3000).unref();
    });
  }
  if (server) await new Promise(resolve => server.close(resolve));
  await rm(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 300 });
}
