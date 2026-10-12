import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import { extractDecisions } from "./model.mjs";
import { filterDecisions, safeLink } from "./browser-model.mjs";

class Element {
    constructor(tagName) {
        this.tagName = tagName;
        this.children = [];
        this.textContent = "";
        this.value = "";
    }
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children = children; }
    setAttribute() {}
    focus() {}
}

const issue = {
    number: 73, state: "open", title: "[Decision]: governance",
    html_url: "https://github.com/Jamula/Andreja/issues/73",
    updated_at: "2026-10-11T00:00:00Z",
};

async function focusedDetail(card) {
    const elements = new Map(["search", "group", "refresh", "error", "freshness", "count", "cards", "detail"]
        .map(id => [id, new Element("div")]));
    elements.get("group").value = "All";
    const requests = [];
    const client = await readFile(new URL("client.mjs", import.meta.url), "utf8");
    const source = client.replace(/^import .* from "\/model\.mjs";\r?\n/, "");
    await runInNewContext(`(async () => { ${source}\n })()`, {
        document: { getElementById: id => elements.get(id), createElement: tag => new Element(tag) },
        window: { addEventListener() {}, confirm: () => true },
        setInterval() {},
        filterDecisions, safeLink,
        fetch: async path => {
            requests.push(path);
            assert.ok(path === "/state" || path === `/draft?id=${encodeURIComponent(card.id)}`);
            return { ok: true, json: async () => path === "/state"
                ? { cards: [card], csrf: "test", lastRefresh: "fixture" }
                : { text: "" } };
        },
    });
    const button = elements.get("cards").children[0].children[0];
    await button.onclick();
    assert.deepEqual(requests, ["/state", `/draft?id=${encodeURIComponent(card.id)}`]);
    return elements.get("detail");
}

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
