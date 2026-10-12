import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import { extractDecisions } from "./model.mjs";
import { filterDecisions, safeLink } from "./browser-model.mjs";

class Element {
    constructor(tagName, document) {
        this.tagName = tagName;
        this.document = document;
        this.children = [];
        this.textContent = "";
        this.value = "";
    }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) {
        if (this.document && this.children.some(child => child.contains(this.document.activeElement))) {
            this.document.activeElement = null;
        }
        this.children = children;
    }
    contains(element) { return this === element || this.children.some(child => child.contains(element)); }
    setAttribute() {}
    focus() { if (this.document && !this.disabled) this.document.activeElement = this; }
}

const issue = {
    number: 73, state: "open", title: "[Decision]: governance",
    html_url: "https://github.com/Jamula/Andreja/issues/73",
    updated_at: "2026-10-11T00:00:00Z",
};

async function focusedDetail(card, exercise) {
    const document = { activeElement: null };
    const elements = new Map(["search", "group", "refresh", "error", "freshness", "count", "cards", "detail"]
        .map(id => [id, new Element("div", document)]));
    document.getElementById = id => elements.get(id);
    document.createElement = tag => new Element(tag, document);
    elements.get("group").value = "All";
    const requests = [];
    const events = new Map();
    let poll;
    let snapshot = { cards: [card], csrf: "test", lastRefresh: "fixture" };
    const controls = {
        document, events,
        poll: () => poll(),
        update: cards => { snapshot = { ...snapshot, cards, lastRefresh: `${snapshot.lastRefresh}-next` }; },
    };
    const capability = "a".repeat(64);
    const client = await readFile(new URL("client.mjs", import.meta.url), "utf8");
    const source = client.replace(/^import .* from "\/model\.mjs";\r?\n/, "");
    await runInNewContext(`(async () => { ${source}\n })()`, {
        document,
        window: { location: { hash: `#capability=${capability}` },
            addEventListener: (name, handler) => events.set(name, handler), confirm: () => true },
        URLSearchParams,
        setInterval(handler, interval) { assert.equal(interval, 3000); poll = handler; },
        filterDecisions, safeLink,
        fetch: async (path, options) => {
            requests.push(path);
            assert.equal(options.headers.Authorization, `Bearer ${capability}`);
            assert.equal(options.redirect, "error");
            assert.equal(options.referrerPolicy, "no-referrer");
            assert.ok(["/state", "/refresh", "/draft", `/draft?id=${encodeURIComponent(card.id)}`].includes(path));
            if (options.method === "POST") {
                assert.equal(options.headers["Content-Type"], "application/json");
                assert.equal(options.headers["X-Canvas-CSRF"], "test");
                assert.ok(!options.body.includes(capability));
            }
            return { ok: true, json: async () => path === "/state"
                ? snapshot
                : path === "/refresh" ? { ...snapshot, lastRefresh: `${snapshot.lastRefresh}-refreshed` }
                    : path === "/draft" ? { text: JSON.parse(options.body).text, updatedAt: "saved" } : { text: "" } };
        },
    });
    const button = elements.get("cards").children[0].children[0];
    await button.onclick();
    assert.deepEqual(requests, ["/state", `/draft?id=${encodeURIComponent(card.id)}`]);
    assert.equal(document.activeElement, elements.get("detail").children[0], "Explicit selection focuses its heading");
    if (exercise) await exercise(elements, requests, capability, controls);
    return elements.get("detail");
}

test("client sends the panel bearer on draft saves and refreshes but not in draft data or GitHub links", async () => {
    const [card] = extractDecisions({ ...issue, body: "### Decision question\nRetain Proposed?" });
    await focusedDetail(card, async (elements, requests, capability) => {
        const detail = elements.get("detail");
        const label = detail.children.find(element => element.tagName === "label");
        const textarea = label.children[0];
        textarea.value = "private response";
        textarea.oninput();
        const actions = detail.children.find(element => element.className === "actions");
        await actions.children[0].onclick();
        assert.equal(requests.at(-1), "/draft");
        elements.get("refresh").focus();
        await elements.get("refresh").onclick();
        assert.deepEqual(requests.slice(-2), ["/refresh", `/draft?id=${encodeURIComponent(card.id)}`]);
        for (const element of detail.children.filter(element => element.tagName === "a")) {
            assert.equal(new URL(element.href).hostname, "github.com");
            assert.ok(!element.href.includes(capability));
            assert.equal(element.rel, "noopener noreferrer");
        }
    });
});

for (const update of ["poll", "refresh"]) {
    for (const control of ["search", "group"]) {
        test(`${update} updates selected source without stealing ${control} focus`, async () => {
            const [card] = extractDecisions({ ...issue, body: "### Decision question\nRetain Proposed?" });
            await focusedDetail(card, async (elements, requests, capability, controls) => {
                const input = elements.get(control);
                input.focus();
                input.value = control === "search" ? "Retain" : card.group;
                (control === "search" ? input.oninput : input.onchange)();
                const updated = { ...card, question: "Retain after checking newer evidence?" };
                controls.update([updated]);
                await (update === "poll" ? controls.poll() : elements.get("refresh").onclick());
                assert.equal(controls.document.activeElement, input);
                assert.equal(input.value, control === "search" ? "Retain" : card.group);
                assert.equal(questionSection(elements.get("detail"))[0].textContent, updated.question);
                assert.equal(elements.get("cards").children.length, 1);
                assert.equal(requests.at(-1), `/draft?id=${encodeURIComponent(card.id)}`);
            });
        });
    }
}

test("poll retains a screen-reader-focused heading, then applies pending detail once focus leaves", async () => {
    const [card] = extractDecisions({ ...issue, body: "### Decision question\nRetain Proposed?" });
    await focusedDetail(card, async (elements, requests, capability, controls) => {
        const heading = elements.get("detail").children[0];
        controls.update([{ ...card, question: "New evidence?" }]);
        await controls.poll();
        assert.equal(controls.document.activeElement, heading);
        assert.equal(elements.get("detail").children[0], heading);
        assert.equal(requests.filter(path => path.startsWith("/draft?")).length, 1);
        elements.get("search").focus();
        await controls.poll();
        assert.equal(controls.document.activeElement, elements.get("search"));
        assert.equal(questionSection(elements.get("detail"))[0].textContent, "New evidence?");
    });
});

for (const update of ["poll", "refresh"]) {
    test(`${update} retains focused private draft controls and dirty typing without saving or discarding`, async () => {
        const [card] = extractDecisions({ ...issue, body: "### Decision question\nRetain Proposed?" });
        await focusedDetail(card, async (elements, requests, capability, controls) => {
            const detail = elements.get("detail");
            const textarea = detail.children.find(element => element.tagName === "label").children[0];
            textarea.focus();
            textarea.selectionStart = 0; textarea.selectionEnd = 0;
            controls.update([{ ...card, question: "Updated evidence?" }]);
            await (update === "poll" ? controls.poll() : elements.get("refresh").onclick());
            assert.equal(controls.document.activeElement, textarea, "Even a clean focused control must not be rebuilt");
            assert.equal(detail.children.find(element => element.tagName === "label").children[0], textarea);
            textarea.value = "My private draft in progress";
            textarea.selectionStart = 8; textarea.selectionEnd = 8;
            textarea.oninput();
            controls.update([{ ...card, question: "Further evidence?" }]);
            await controls.poll();
            assert.equal(controls.document.activeElement, textarea);
            assert.equal(textarea.selectionStart, 8);
            assert.equal(textarea.selectionEnd, 8);
            elements.get("search").focus();
            elements.get("search").value = "Retain";
            elements.get("search").oninput();
            elements.get("refresh").focus();
            await elements.get("refresh").onclick();
            assert.equal(controls.document.activeElement, elements.get("refresh"));
            assert.equal(detail.children.find(element => element.tagName === "label").children[0], textarea);
            assert.equal(textarea.value, "My private draft in progress");
            assert.equal(questionSection(detail)[0].textContent, card.question);
            assert.match(elements.get("freshness").textContent, /Unsaved draft/);
            assert.ok(!requests.includes("/draft"), "Background work must not save the private draft");
            let prevented = false;
            const event = { preventDefault() { prevented = true; } };
            controls.events.get("beforeunload")(event);
            assert.ok(prevented, "Dirty navigation protection is still armed");
            assert.equal(event.returnValue, "");
        });
    });
}

test("client without a valid fragment cannot bootstrap state or send a bearer", async () => {
    const client = await readFile(new URL("client.mjs", import.meta.url), "utf8");
    const source = client.replace(/^import .* from "\/model\.mjs";\r?\n/, "");
    for (const hash of ["", "#capability=bad", `#capability=${"a".repeat(63)}`]) {
        const elements = new Map(["search", "group", "refresh", "error", "freshness", "count", "cards", "detail"]
            .map(id => [id, new Element("div")]));
        await runInNewContext(`(async () => { ${source}\n })()`, {
            document: { getElementById: id => elements.get(id), createElement: tag => new Element(tag) },
            window: { location: { hash }, addEventListener() {} }, URLSearchParams,
            setInterval() {}, filterDecisions, safeLink,
            fetch: async () => assert.fail("Invalid/missing capability must not issue HTTP requests"),
        });
        assert.match(elements.get("error").textContent, /Missing panel capability; reopen/);
        await elements.get("refresh").onclick();
        assert.match(elements.get("error").textContent, /Missing panel capability; reopen/);
    }
});

function questionSection(detail) {
    const start = detail.children.findIndex(element =>
        element.tagName === "h3" && element.textContent === "Question and available alternatives — exact source");
    assert.notEqual(start, -1, "Focused detail must contain the question section");
    const rest = detail.children.slice(start + 1);
    const end = rest.findIndex(element => element.tagName === "h3");
    return rest.slice(0, end === -1 ? rest.length : end);
}

test("focused no-options question renders its exact prompt without an empty alternatives paragraph", async () => {
    const [noOptions] = extractDecisions({ ...issue, body:
        "### Assessment question: evidence\nIs the evidence complete?\n### Sources\nRead the packet." });
    assert.equal(noOptions.options, null);
    const section = questionSection(await focusedDetail(noOptions));
    assert.deepEqual(section.map(element => [element.tagName, element.className, element.textContent]),
        [["p", "source", noOptions.question]]);
    assert.match(section[0].textContent, /Is the evidence complete\?/);
});

test("focused structured question quotes prompt and alternatives separately as safe source text", async () => {
    const prompt = 'Which <img src=x onerror="alert(1)"> evidence is authoritative?';
    const options = "1. Retain Proposed.\n2. Amend after review.";
    const [card] = extractDecisions({ ...issue, body:
        `### Decision question\n\n${prompt}\n\n### Options and tradeoffs\n\n${options}\n\n### Boundaries\nNo approval here.` });
    assert.equal(card.question, prompt);
    assert.equal(card.options, options);
    const section = questionSection(await focusedDetail(card));
    assert.deepEqual(section.map(element => [element.tagName, element.className, element.textContent]),
        [["p", "source", prompt], ["p", "source", options]]);
    assert.ok(section.every(element => element.children.length === 0), "Source content remains text, not HTML children");
});
