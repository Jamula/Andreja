import { spawn } from "node:child_process";
import { once } from "node:events";
import { createReadStream } from "node:fs";
import { readFile, rm } from "node:fs/promises";
import http from "node:http";
import path from "node:path";

const root = process.cwd();
const publicWebsiteRoot = path.join(root, "docs", "public-website");
const prototypeRoot = path.join(publicWebsiteRoot, "prototype");
const indexPath = path.join(prototypeRoot, "index.html");
const edgePath = process.env.EDGE_PATH
  ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const userDataPath = path.join(root, ".andreja", "edge-public-website");
const debuggingPort = 44000 + Math.floor(Math.random() * 1000);
const mimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".md", "text/markdown; charset=utf-8"],
]);

class Cdp {
  constructor(url) {
    this.nextId = 1;
    this.pending = new Map();
    this.listeners = new Map();
    this.socket = new WebSocket(url);
    this.opened = new Promise((resolve, reject) => {
      this.socket.addEventListener("open", resolve, { once: true });
      this.socket.addEventListener("error", reject, { once: true });
    });
    this.socket.addEventListener("message", event => {
      const message = JSON.parse(event.data);
      if (!message.id) {
        for (const listener of this.listeners.get(message.method) ?? []) {
          listener(message.params);
        }
        return;
      }
      const pending = this.pending.get(message.id);
      if (!pending) return;
      this.pending.delete(message.id);
      if (message.error) pending.reject(new Error(message.error.message));
      else pending.resolve(message.result);
    });
  }

  async send(method, params = {}) {
    await this.opened;
    const id = this.nextId++;
    const response = new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
    });
    this.socket.send(JSON.stringify({ id, method, params }));
    return response;
  }

  on(method, listener) {
    const listeners = this.listeners.get(method) ?? [];
    listeners.push(listener);
    this.listeners.set(method, listeners);
    return () => {
      const current = this.listeners.get(method) ?? [];
      this.listeners.set(method, current.filter(candidate => candidate !== listener));
    };
  }

  waitForEvent(method, timeoutMilliseconds = 15000) {
    return new Promise((resolve, reject) => {
      let unsubscribe;
      const timeout = setTimeout(() => {
        unsubscribe?.();
        reject(new Error(`Timed out waiting for CDP event ${method}.`));
      }, timeoutMilliseconds);
      unsubscribe = this.on(method, params => {
        clearTimeout(timeout);
        unsubscribe();
        resolve(params);
      });
    });
  }

  close() {
    this.socket.close();
  }
}

const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function waitForValue(action, timeoutMilliseconds = 15000) {
  const deadline = Date.now() + timeoutMilliseconds;
  let lastError;
  while (Date.now() < deadline) {
    try {
      const value = await action();
      if (value) return value;
    } catch (error) {
      lastError = error;
    }
    await sleep(100);
  }
  throw new Error(`Timed out waiting for browser state${lastError ? `: ${lastError.message}` : ""}.`);
}

function startServer() {
  const server = http.createServer((request, response) => {
    const url = new URL(request.url, "http://127.0.0.1");
    const relative = url.pathname.endsWith("/")
      ? `${url.pathname}index.html`
      : url.pathname;
    const requestedPath = path.resolve(publicWebsiteRoot, relative.replace(/^\/+/, ""));
    if (!requestedPath.startsWith(`${path.resolve(publicWebsiteRoot)}${path.sep}`)) {
      response.writeHead(403).end();
      return;
    }
    const extension = path.extname(requestedPath);
    response.setHeader("Content-Type", mimeTypes.get(extension) ?? "application/octet-stream");
    response.setHeader("X-Robots-Tag", "noindex, nofollow, noarchive");
    const stream = createReadStream(requestedPath);
    stream.on("error", () => response.writeHead(404).end());
    stream.pipe(response);
  });
  server.listen(0, "127.0.0.1");
  return once(server, "listening").then(() => server);
}

async function staticChecks() {
  const html = await readFile(indexPath, "utf8");
  const required = [
    '<html lang="en">',
    '<meta name="viewport"',
    '<meta name="robots" content="noindex, nofollow, noarchive">',
    'id="main-content"',
    'aria-labelledby="page-title"',
    'class="skip-link"',
    'id="site-search"',
    'aria-live="polite"',
    '<ul class="route-strip"',
    'prefers-reduced-motion',
    'forced-colors: active',
  ];
  for (const text of required) {
    if (!html.includes(text)) throw new Error(`Prototype is missing required markup: ${text}`);
  }
  if ((html.match(/<h1\b/g) ?? []).length !== 1) {
    throw new Error("Prototype must contain exactly one h1.");
  }
  if (/https?:\/\//i.test(html)) {
    throw new Error("Prototype must not contain remote HTTP(S) dependencies or links.");
  }
  if (/<form\b/i.test(html)) {
    throw new Error("Prototype must not contain a feedback or submission form.");
  }
  const prohibitedMarkers = [
    "scoutTheme",
    "Impeccable",
    "Clawpilot",
    "direction-seed",
  ];
  for (const marker of prohibitedMarkers) {
    if (html.toLowerCase().includes(marker.toLowerCase())) {
      throw new Error(`Prototype contains an unexplained provenance marker: ${marker}`);
    }
  }
  const definedTokens = new Set(
    [...html.matchAll(/(--[a-z0-9-]+)\s*:/gi)].map(match => match[1]),
  );
  const usedTokens = new Set(
    [...html.matchAll(/var\((--[a-z0-9-]+)\)/gi)].map(match => match[1]),
  );
  const unusedTokens = [...definedTokens].filter(token => !usedTokens.has(token));
  if (unusedTokens.length > 0) {
    throw new Error(`Prototype defines unused design tokens: ${unusedTokens.join(", ")}`);
  }
}

let edge;
let cdp;
let server;
try {
  await staticChecks();
  server = await startServer();
  const { port } = server.address();
  const baseUrl = `http://127.0.0.1:${port}`;
  const pageUrl = `${baseUrl}/prototype/`;
  const requests = [];
  const linkedDocuments = [
    "/design-hosting-matrix.md",
    "/claims-inventory.md",
  ];
  for (const documentPath of linkedDocuments) {
    const response = await fetch(`${baseUrl}${documentPath}`);
    if (!response.ok) {
      throw new Error(`Prototype governance link failed: ${documentPath} returned ${response.status}.`);
    }
  }

  await rm(userDataPath, {
    recursive: true,
    force: true,
    maxRetries: 20,
    retryDelay: 100,
  });
  edge = spawn(edgePath, [
    "--headless=new",
    `--remote-debugging-port=${debuggingPort}`,
    `--user-data-dir=${userDataPath}`,
    "--disable-background-networking",
    "--disable-component-update",
    "--disable-sync",
    "--metrics-recording-only",
    "--no-first-run",
    `${pageUrl}?theme=dark`,
  ], { windowsHide: true, stdio: "ignore" });

  const target = await waitForValue(async () => {
    const response = await fetch(`http://127.0.0.1:${debuggingPort}/json/list`);
    if (!response.ok) return null;
    const targets = await response.json();
    return targets.find(item => item.type === "page" && item.url.startsWith(pageUrl));
  });

  cdp = new Cdp(target.webSocketDebuggerUrl);
  await cdp.send("Page.enable");
  await cdp.send("Runtime.enable");
  await cdp.send("Network.enable");
  cdp.on("Network.requestWillBeSent", event => requests.push(event.request.url));

  const evaluate = async expression => {
    const result = await cdp.send("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true,
    });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  };

  const navigateAndWait = async url => {
    const loaded = cdp.waitForEvent("Page.loadEventFired");
    await cdp.send("Page.navigate", { url });
    await loaded;
    await waitForValue(() => evaluate("document.querySelector('#page-title') !== null"));
  };

  const auditContrast = () => evaluate(`(() => {
    const visible = element => {
      const style = getComputedStyle(element);
      const box = element.getBoundingClientRect();
      return style.display !== "none" && style.visibility !== "hidden"
        && box.width > 0 && box.height > 0;
    };
    const parse = value => {
      const channels = value.match(/[\\d.]+/g)?.map(Number) ?? [];
      return {
        r: channels[0] ?? 0,
        g: channels[1] ?? 0,
        b: channels[2] ?? 0,
        a: channels[3] ?? 1,
      };
    };
    const composite = (top, bottom) => {
      const alpha = top.a + bottom.a * (1 - top.a);
      if (alpha === 0) return { r: 255, g: 255, b: 255, a: 1 };
      return {
        r: (top.r * top.a + bottom.r * bottom.a * (1 - top.a)) / alpha,
        g: (top.g * top.a + bottom.g * bottom.a * (1 - top.a)) / alpha,
        b: (top.b * top.a + bottom.b * bottom.a * (1 - top.a)) / alpha,
        a: alpha,
      };
    };
    const background = element => {
      const layers = [];
      for (let current = element; current; current = current.parentElement) {
        layers.push(parse(getComputedStyle(current).backgroundColor));
      }
      let result = { r: 255, g: 255, b: 255, a: 1 };
      for (let index = layers.length - 1; index >= 0; index--) {
        result = composite(layers[index], result);
      }
      return result;
    };
    const luminance = color => {
      const channels = [color.r, color.g, color.b].map(channel => {
        const normalized = channel / 255;
        return normalized <= 0.04045
          ? normalized / 12.92
          : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    };
    const contrast = (left, right) => {
      const values = [luminance(left), luminance(right)].sort((a, b) => b - a);
      return (values[0] + 0.05) / (values[1] + 0.05);
    };
    const links = [...document.querySelectorAll("a[href]")].filter(visible).map(element => ({
      text: element.textContent.trim().slice(0, 80),
      ratio: contrast(parse(getComputedStyle(element).color), background(element)),
    }));
    const controls = [...document.querySelectorAll("button, input")].filter(visible).map(element => ({
      name: element.getAttribute("aria-label") || element.id || element.tagName,
      ratio: contrast(parse(getComputedStyle(element).borderTopColor), background(element)),
    }));
    return {
      minimumLinkRatio: Math.min(...links.map(item => item.ratio)),
      minimumControlBoundaryRatio: Math.min(...controls.map(item => item.ratio)),
      linkFailures: links.filter(item => item.ratio < 4.5),
      controlFailures: controls.filter(item => item.ratio < 3),
    };
  })()`);

  const viewportAudit = () => evaluate(`(() => {
    const unnamed = [...document.querySelectorAll("button, a[href], input")]
      .filter(element => {
        const label = element.id
          ? document.querySelector(\`label[for="\${CSS.escape(element.id)}"]\`)
          : null;
        return !element.textContent.trim()
          && !element.getAttribute("aria-label")
          && !label?.textContent.trim();
      }).length;
    const undersizedControls = [...document.querySelectorAll("button, input")]
      .filter(element => {
        const box = element.getBoundingClientRect();
        return box.width > 0 && box.height > 0 && (box.width < 44 || box.height < 44);
      }).length;
    return {
      width: innerWidth,
      noHorizontalOverflow: document.documentElement.scrollWidth <= innerWidth,
      unnamedInteractiveControls: unnamed,
      undersizedControls,
    };
  })()`);

  const textResizeAudit = async width => {
    await cdp.send("Emulation.setDeviceMetricsOverride", {
      width,
      height: 900,
      deviceScaleFactor: 1,
      mobile: false,
    });
    const result = await evaluate(`(async () => {
      document.documentElement.style.fontSize = "200%";
      await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      const clippedControls = [...document.querySelectorAll(
        "button, input, .primary-link, .site-nav a")]
        .filter(element => {
          const box = element.getBoundingClientRect();
          return box.width > 0 && box.height > 0
            && (element.scrollWidth > element.clientWidth + 1
              || element.scrollHeight > element.clientHeight + 1);
        })
        .map(element => element.getAttribute("aria-label") || element.textContent.trim());
      const output = {
        width: innerWidth,
        bodyFontPixels: Number.parseFloat(getComputedStyle(document.body).fontSize),
        noHorizontalOverflow: document.documentElement.scrollWidth <= innerWidth,
        clippedControls,
        overflowingElements: [...document.querySelectorAll("body *")]
          .filter(element => {
            const box = element.getBoundingClientRect();
            return box.right > innerWidth + 1 || box.left < -1;
          })
          .slice(0, 10)
          .map(element => ({
            tag: element.tagName,
            className: element.className,
            id: element.id,
            left: element.getBoundingClientRect().left,
            right: element.getBoundingClientRect().right,
          })),
      };
      document.documentElement.style.removeProperty("font-size");
      return output;
    })()`);
    if (result.width !== width || result.bodyFontPixels < 31 || !result.noHorizontalOverflow
      || result.clippedControls.length > 0) {
      throw new Error(`200% text resize check failed: ${JSON.stringify(result)}`);
    }
    return result;
  };

  const themes = [];
  for (const theme of ["light", "dark"]) {
    await navigateAndWait(`${pageUrl}?theme=${theme}`);
    const landmarks = await evaluate(`(() => ({
      theme: document.documentElement.dataset.theme,
      title: document.title,
      h1: document.querySelectorAll("h1").length,
      labelledMainCount: document.querySelectorAll("[aria-labelledby='page-title']").length,
      namedNavigation: Boolean(document.querySelector("nav[aria-label='Primary']")),
      namedRouteList: document.querySelectorAll("ul.route-strip[aria-label] > li").length,
      staticStatusRoles: document.querySelectorAll(".prototype-banner[role='status']").length,
      searchLabel: document.querySelector("label[for='site-search']")?.textContent.trim(),
      scrollPaddingTop: Number.parseFloat(getComputedStyle(document.documentElement).scrollPaddingTop),
      headerHeight: document.querySelector(".site-header").getBoundingClientRect().height,
      searchScrollMargin: Number.parseFloat(getComputedStyle(document.querySelector("#search")).scrollMarginTop),
    }))()`);
    if (landmarks.theme !== theme || landmarks.h1 !== 1
      || landmarks.labelledMainCount !== 1 || !landmarks.namedNavigation
      || landmarks.namedRouteList !== 3 || landmarks.staticStatusRoles !== 0
      || !landmarks.searchLabel || landmarks.scrollPaddingTop < landmarks.headerHeight
      || landmarks.searchScrollMargin < landmarks.headerHeight) {
      throw new Error(`Semantic/scroll check failed: ${JSON.stringify(landmarks)}`);
    }

    const viewports = [];
    for (const width of [320, 768, 1280]) {
      await cdp.send("Emulation.setDeviceMetricsOverride", {
        width,
        height: 900,
        deviceScaleFactor: 1,
        mobile: false,
      });
      const viewport = await viewportAudit();
      const contrast = await auditContrast();
      if (!viewport.noHorizontalOverflow || viewport.unnamedInteractiveControls !== 0
        || viewport.undersizedControls !== 0 || contrast.linkFailures.length > 0
        || contrast.controlFailures.length > 0) {
        throw new Error(`Viewport/contrast check failed: ${JSON.stringify({ theme, viewport, contrast })}`);
      }
      viewports.push({ ...viewport, ...contrast });
    }

    await cdp.send("Emulation.setDeviceMetricsOverride", {
      width: 320,
      height: 900,
      deviceScaleFactor: 1,
      mobile: false,
    });
    const mobileMenu = await evaluate(`(() => {
      const button = document.querySelector(".menu-button");
      const navigation = document.querySelector(".site-nav");
      const initial = {
        expanded: button.getAttribute("aria-expanded"),
        display: getComputedStyle(navigation).display,
      };
      button.click();
      const opened = {
        expanded: button.getAttribute("aria-expanded"),
        display: getComputedStyle(navigation).display,
      };
      navigation.querySelector("a").click();
      const closed = {
        expanded: button.getAttribute("aria-expanded"),
        display: getComputedStyle(navigation).display,
      };
      return { initial, opened, closed };
    })()`);
    if (mobileMenu.initial.expanded !== "false" || mobileMenu.initial.display !== "none"
      || mobileMenu.opened.expanded !== "true" || mobileMenu.opened.display === "none"
      || mobileMenu.closed.expanded !== "false" || mobileMenu.closed.display !== "none") {
      throw new Error(`Mobile navigation check failed: ${JSON.stringify({ theme, mobileMenu })}`);
    }

    const textResize = [];
    for (const width of [320, 1280]) textResize.push(await textResizeAudit(width));

    await cdp.send("Emulation.clearDeviceMetricsOverride");
    await navigateAndWait(`${pageUrl}?theme=${theme}`);
    await cdp.send("Input.dispatchKeyEvent", { type: "keyDown", key: "Tab", code: "Tab" });
    await cdp.send("Input.dispatchKeyEvent", { type: "keyUp", key: "Tab", code: "Tab" });
    const firstFocus = await evaluate("document.activeElement?.className");
    if (firstFocus !== "skip-link") {
      throw new Error(`Expected skip link first in ${theme} theme; got ${firstFocus}.`);
    }

    themes.push({ theme, landmarks, viewports, mobileMenu, textResize, firstFocus });
  }

  await navigateAndWait(`${pageUrl}?theme=dark`);
  await sleep(250);
  const searchRequestStart = requests.length;
  const immediateSearchState = await evaluate(`(() => {
    const input = document.querySelector("#site-search");
    const initialStatus = document.querySelector("#search-status").textContent;
    input.value = "privacy";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
    return {
      initialStatus,
      immediateStatus: document.querySelector("#search-status").textContent,
      immediateCount: document.querySelectorAll("#search-results li").length,
    };
  })()`);
  const searchResult = await waitForValue(() => evaluate(`(() => {
    const count = document.querySelectorAll("#search-results li").length;
    if (count < 1) return null;
    return {
      count,
      status: document.querySelector("#search-status").textContent,
      first: document.querySelector("#search-results a")?.textContent,
    };
  })()`));
  if (immediateSearchState.initialStatus !== immediateSearchState.immediateStatus
    || immediateSearchState.immediateCount !== 0 || !searchResult.status.includes("result")) {
    throw new Error(`Debounced in-artifact search failed: ${JSON.stringify({
      immediateSearchState, searchResult,
    })}`);
  }
  await sleep(100);
  const searchRequests = requests.slice(searchRequestStart);
  if (searchRequests.length > 0) {
    throw new Error(`Search caused network requests: ${JSON.stringify(searchRequests)}`);
  }
  const searchContrast = await auditContrast();
  if (searchContrast.linkFailures.length > 0) {
    throw new Error(`Search result contrast failed: ${JSON.stringify(searchContrast)}`);
  }

  await navigateAndWait(`${pageUrl}?theme=dark`);
  await cdp.send("Emulation.setEmulatedMedia", {
    features: [{ name: "prefers-reduced-motion", value: "reduce" }],
  });
  const reducedMotion = await evaluate(`(() => {
    const durations = [...document.querySelectorAll("*")].flatMap(element => {
      const style = getComputedStyle(element);
      const milliseconds = value => value.split(",").map(item => {
        const trimmed = item.trim();
        return Number.parseFloat(trimmed) * (trimmed.endsWith("ms") ? 1 : 1000);
      });
      return [...milliseconds(style.animationDuration), ...milliseconds(style.transitionDuration)];
    });
    document.querySelector(".primary-link").click();
    return {
      mediaMatches: matchMedia("(prefers-reduced-motion: reduce)").matches,
      scrollBehavior: getComputedStyle(document.documentElement).scrollBehavior,
      maximumDurationMilliseconds: Math.max(...durations),
      targetHash: location.hash,
    };
  })()`);
  if (!reducedMotion.mediaMatches || reducedMotion.scrollBehavior !== "auto"
    || reducedMotion.maximumDurationMilliseconds > 0.011
    || reducedMotion.targetHash !== "#search") {
    throw new Error(`Reduced-motion behavior failed: ${JSON.stringify(reducedMotion)}`);
  }

  await cdp.send("Emulation.setEmulatedMedia", {
    features: [{ name: "forced-colors", value: "active" }],
  });
  const forcedColors = await evaluate(`(() => {
    const target = document.querySelector(".primary-link");
    target.focus();
    const style = getComputedStyle(target);
    return {
      mediaMatches: matchMedia("(forced-colors: active)").matches,
      focused: document.activeElement === target,
      outlineStyle: style.outlineStyle,
      outlineWidth: Number.parseFloat(style.outlineWidth),
      boxShadow: style.boxShadow,
    };
  })()`);
  if (!forcedColors.mediaMatches || !forcedColors.focused
    || forcedColors.outlineStyle === "none" || forcedColors.outlineWidth < 3
    || forcedColors.boxShadow !== "none") {
    throw new Error(`Forced-colors behavior failed: ${JSON.stringify(forcedColors)}`);
  }
  await cdp.send("Emulation.setEmulatedMedia", { features: [] });

  const storage = await evaluate(`(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDatabases: typeof indexedDB.databases === "function"
      ? (await indexedDB.databases()).length
      : null,
    cacheEntries: "caches" in globalThis ? (await caches.keys()).length : null,
    serviceWorkerRegistrations: "serviceWorker" in navigator
      ? (await navigator.serviceWorker.getRegistrations()).length
      : null,
    serviceWorkerControlled: Boolean(navigator.serviceWorker?.controller),
  }))()`);
  const cookies = await cdp.send("Network.getCookies", { urls: [pageUrl] });
  if (storage.localStorage !== 0 || storage.sessionStorage !== 0
    || (storage.indexedDatabases ?? 0) !== 0
    || (storage.cacheEntries ?? 0) !== 0
    || (storage.serviceWorkerRegistrations ?? 0) !== 0
    || storage.serviceWorkerControlled || cookies.cookies.length !== 0) {
    throw new Error(`Browser storage check failed: ${JSON.stringify({ storage, cookies: cookies.cookies.length })}`);
  }

  await sleep(250);
  const unexpectedRequests = requests.filter(requestUrl => {
    const parsed = new URL(requestUrl);
    return parsed.origin !== baseUrl;
  });
  if (unexpectedRequests.length > 0) {
    throw new Error(`Cross-origin requests detected: ${JSON.stringify(unexpectedRequests)}`);
  }

  console.log(JSON.stringify({
    result: "PASS",
    staticChecks: true,
    linkedDocuments,
    themes,
    reducedMotion,
    forcedColors,
    immediateSearchState,
    searchResult,
    searchContrast,
    storage,
    searchRequests: 0,
    crossOriginRequests: 0,
  }, null, 2));
} finally {
  cdp?.close();
  if (edge && edge.exitCode === null) edge.kill();
  if (edge) await Promise.race([once(edge, "exit"), sleep(3000)]);
  if (server) await new Promise(resolve => server.close(resolve));
  await rm(userDataPath, {
    recursive: true,
    force: true,
    maxRetries: 20,
    retryDelay: 100,
  });
}
