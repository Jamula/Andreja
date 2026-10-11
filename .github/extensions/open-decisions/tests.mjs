import test from "node:test";
import assert from "node:assert/strict";
import { readFile, rm } from "node:fs/promises";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { DecisionStore, DraftStore, extractDecisions, fetchIssues, decisionIdentity, safeLink, filterDecisions } from "./model.mjs";
import * as browser from "./browser-model.mjs";
import { startServer } from "./server.mjs";
import { createDecisionCanvas } from "./lifecycle.mjs";
import { request as httpRequest } from "node:http";

const issue = { number: 73, state: "open", title: "[Decision]: governance", body: "Original packet",
    html_url: "https://github.com/Jamula/Andreja/issues/73", updated_at: "2026-10-11T00:00:00Z" };
const comment = (id, body) => ({ id, body, html_url: `${issue.html_url}#issuecomment-${id}`, updated_at: "2026-10-11T01:00:00Z" });
const first = comment(123, "### Assessment question: plan\n**Please help me decide: retain Proposed, or amend?** Retaining avoids invented approval.\nRecommendation: retain Proposed.\n### Assessment question: cost\n**Please help me decide: ratify controls, leave Draft, or amend?** Ratification does not authorize spend.");
// Exact issue #73 body retrieved 2026-10-11; no live GitHub dependency in regressions.
const structured73 = `### Decision question

Which merged Phase 0 documents are authoritative decisions versus counsel-ready/review-ready drafts, and how must status be recorded consistently across document headers, issues, PR titles, and roadmap claims?

### Why this matters now

The fleet found merged operating/cost/support artifacts whose headers remain draft/proposed while issue or PR language can be read as ratified/completed. Issue closure, merge, agent statement, and document title must not substitute for Cyrus’s explicit human decision.

### Options and tradeoffs

1. Explicitly accept each artifact with approver/date/content hash/source issue and update its status.
2. Keep it draft/advisory and correct issue/roadmap language that implies approval.
3. Split factual framework content from policy decisions that still need human/counsel approval.

### Required evidence and sources

\`docs/operating-model.md\`, \`docs/cost-model.md\`, feedback/support framework, charter/license/regulatory documents, their source issues/PRs, current hashes, and recorded human decisions.

### Required artifact gates

Governance/architecture, security/privacy, legal, cost, quality, operations, public claims, and charter impact. Qualified counsel remains authoritative where required.

### Charter, ethics, and sustainability impact

Consistent status protects integrity, accountability, protected dissent, truthful public claims, and human authority. It prevents agent-generated or merge-generated false approval.

### Reversibility, rollback, and stop criteria

Status corrections are content-addressed and reversible through reviewed amendments. Stop publication, launch claims, spending, or policy enforcement when the authoritative status cannot be proven.`;
const structuredOptions = `1. Explicitly accept each artifact with approver/date/content hash/source issue and update its status.
2. Keep it draft/advisory and correct issue/roadmap language that implies approval.
3. Split factual framework content from policy decisions that still need human/counsel approval.`;

test("actual structured #73 retains options, tradeoffs, evidence, gates and stop boundaries in its focused card", () => {
    const [card] = extractDecisions({ ...issue, body: structured73 });
    assert.equal(card.body, structured73);
    assert.equal(card.options, structuredOptions);
    assert.match(card.question, /^Which merged Phase 0 documents/);
    assert.notEqual(card.options, card.question);
    assert.match(card.body, /current hashes, and recorded human decisions/);
    assert.match(card.body, /Qualified counsel remains authoritative/);
    assert.match(card.body, /Stop publication, launch claims, spending, or policy enforcement/);
    assert.equal(card.sourceId, "body");
    assert.equal(card.sourceUrl, issue.html_url);
});
test("associated context follows each distinct question without alternatives or boundaries bleeding across cards", () => {
    const body = `${structured73}\n\n### Assessment question: independent gate\nShould the later gate remain deferred?\n### Alternatives\n1. Defer until evidence exists.\n2. Request an amendment.\n### Boundary\nLater gate, not an automatic Phase 0 blocker.`;
    const cards = extractDecisions({ ...issue, body });
    assert.equal(cards.length, 2);
    assert.equal(cards[0].body, structured73);
    assert.equal(cards[0].options, structuredOptions);
    assert.doesNotMatch(cards[0].body, /independent gate|remain deferred/);
    assert.equal(cards[0].group, "Phase 0");
    assert.equal(cards[1].options, "1. Defer until evidence exists.\n2. Request an amendment.");
    assert.match(cards[1].body, /Later gate, not an automatic Phase 0 blocker/);
    assert.equal(cards[1].group, "Later / gated");
    assert.notEqual(cards[0].id, cards[1].id);
    const [noOptions] = extractDecisions(issue, [comment(8, "### Assessment question: evidence\nIs the evidence complete?\n### Sources\nRead the packet.")]);
    assert.equal(noOptions.options, null);
});

test("multiple source questions retain exact alternatives, source IDs and separate stable draft identities", () => {
    const cards = extractDecisions(issue, [first]);
    assert.equal(cards.length, 2);
    assert.equal(cards[0].options, "retain Proposed, or amend");
    assert.equal(cards[1].options, "ratify controls, leave Draft, or amend");
    assert.equal(cards[0].sourceId, "123");
    assert.equal(cards[0].sourceUrl, `${issue.html_url}#issuecomment-123`);
    assert.match(cards[0].body, /Retaining avoids invented approval/);
    assert.equal(cards[0].recommendation, "Recommendation: retain Proposed.");
    assert.notEqual(cards[0].id, cards[1].id);
    const updated = extractDecisions(issue, [first, comment(124, "### Assessment question: plan\n**Please help me decide: amend or defer?**")]);
    assert.equal(updated.length, 2);
    assert.equal(updated[0].id, cards[0].id);
    assert.equal(updated[0].sourceId, "124");
    assert.match(updated[0].options, /amend or defer/);
    assert.match(updated[0].status, /Unknown/);
    assert.equal(updated[0].discussion.at(-1).id, 124);
});
test("closed issues excluded; gated source separated from Phase 0 and task/evidence updates", () => {
    assert.deepEqual(extractDecisions({ ...issue, state: "closed" }, [first]), []);
    assert.equal(extractDecisions(issue, [first])[0].group, "Phase 0");
    const gated = extractDecisions(issue, [comment(5, "### Assessment question: later\n**Please help me decide: defer?** Later gate, not an automatic Phase 0 blocker.")]);
    assert.equal(gated[0].group, "Later / gated");
    assert.equal(extractDecisions({ ...issue, number: 67 }, [first])[0].group, "Evidence / work updates");
    assert.equal(extractDecisions({ ...issue, number: 158 }, [])[0].question, null);
});
test("browser and provider filtering match actual source text", () => {
    const cards = extractDecisions(issue, [first]);
    for (const filter of [filterDecisions, browser.filterDecisions]) {
        assert.equal(filter(cards, "  RATIFY ", "Phase 0")[0].heading, "Assessment question: cost");
        assert.equal(filter(cards, "", "Later / gated").length, 0);
        assert.equal(filter(cards, "73").length, 2);
    }
});
test("refresh failures retain last complete data explicitly stale; first failure yields no fabricated data", async () => {
    let fail = false;
    const store = new DecisionStore(async () => { if (fail) throw new Error("offline"); return extractDecisions(issue, [first]); }, () => "fixed-time");
    await store.refresh(); assert.equal(store.snapshot().stale, false);
    fail = true; await store.refresh();
    assert.equal(store.snapshot().cards.length, 2);
    assert.equal(store.snapshot().lastRefresh, "fixed-time");
    assert.equal(store.snapshot().stale, true);
    assert.match(store.snapshot().error, /offline/);
    const empty = new DecisionStore(async () => { throw new Error("offline"); });
    await empty.refresh(); assert.deepEqual(empty.snapshot().cards, []);
    assert.equal(empty.snapshot().lastRefresh, null);
});
test("production canvas reopen refreshes source, preserves stale snapshot on failure and recovers", async () => {
    let calls = 0;
    let fail = false;
    const store = new DecisionStore(async () => {
        calls++;
        if (fail) throw new Error("reopen offline");
        return extractDecisions({ ...issue, body: structured73, title: `Revision ${calls}` });
    }, () => `refresh-${calls}`);
    const canvas = createDecisionCanvas(store, {});
    const ctx = { instanceId: "reopen" };
    try {
        const initial = await canvas.open(ctx);
        assert.equal(calls, 1);
        const reopened = await canvas.open(ctx);
        assert.equal(reopened.url, initial.url);
        assert.equal(calls, 2);
        const current = await (await fetch(`${reopened.url}state`)).json();
        assert.equal(current.cards[0].issueTitle, "Revision 2");
        assert.equal(current.cards[0].body, structured73);
        assert.equal(current.lastRefresh, "refresh-2");
        fail = true;
        const failed = await canvas.open(ctx);
        assert.equal(calls, 3);
        assert.equal(failed.status, "Source fetch failed");
        const stale = await (await fetch(`${failed.url}state`)).json();
        assert.deepEqual(stale.cards, current.cards);
        assert.equal(stale.lastRefresh, current.lastRefresh);
        assert.equal(stale.attemptedAt, "refresh-2");
        assert.equal(stale.stale, true);
        assert.match(stale.error, /reopen offline/);
        await canvas.onClose(ctx);
        await assert.rejects(fetch(`${failed.url}state`));
        fail = false;
        const recreated = await canvas.open(ctx);
        assert.equal(calls, 4);
        const recovered = await (await fetch(`${recreated.url}state`)).json();
        assert.equal(recovered.cards[0].issueTitle, "Revision 4");
        assert.equal(recovered.lastRefresh, "refresh-4");
        assert.equal(recovered.stale, false);
        assert.equal(recovered.error, null);
    } finally { await canvas.onClose(ctx); }
});
test("concurrent canvas opens and agent refresh coalesce; compact agent results leave full iframe state intact", async () => {
    let calls = 0;
    let gate;
    const store = new DecisionStore(async () => {
        calls++;
        if (gate) await gate.promise;
        return extractDecisions({ ...issue, body: structured73 }, [first]);
    }, () => `refresh-${calls}`);
    const canvas = createDecisionCanvas(store, {});
    const ctx = { instanceId: "coalesced" };
    try {
        const initial = await canvas.open(ctx);
        gate = Promise.withResolvers();
        const a = canvas.open(ctx);
        const b = canvas.open(ctx);
        const refresh = canvas.actions[0].handler();
        try {
            const pending = await (await fetch(`${initial.url}state`)).json();
            assert.equal(pending.refreshing, true);
            assert.equal(calls, 2);
        } finally { gate.resolve(); }
        const [opened, reopened, summary] = await Promise.all([a, b, refresh]);
        assert.equal(opened.url, reopened.url);
        assert.equal(calls, 2);
        assert.equal(summary.count, 3);
        assert.equal(summary.lastRefresh, "refresh-2");
        assert.equal(summary.error, null);
        assert.equal(summary.stale, false);
        assert.equal(summary.refreshing, false);
        assert.equal("cards" in summary, false);
        assert.ok(JSON.stringify(summary).length < 1000);
        const full = await (await fetch(`${initial.url}state`)).json();
        assert.equal(full.cards.length, summary.count);
        assert.equal(full.cards[0].body, structured73);
        assert.equal(full.cards[0].options, structuredOptions);
        store.loader = async () => { throw new Error("action offline"); };
        const failed = await canvas.actions[0].handler();
        assert.equal(failed.count, 3);
        assert.equal(failed.lastRefresh, summary.lastRefresh);
        assert.equal(failed.stale, true);
        assert.match(failed.error, /action offline/);
        assert.equal("cards" in failed, false);
        const stale = await (await fetch(`${initial.url}state`)).json();
        assert.deepEqual(stale.cards, full.cards);
        assert.equal(stale.error, failed.error);
    } finally { gate?.resolve(); await canvas.onClose(ctx); }
});
test("live loader reads comments with numeric identity; closed races excluded and failures reject whole fetch", async () => {
    const calls = [];
    const fake = async args => {
        calls.push(args);
        if (args[0] === "issue") return [{ number: 73, title: issue.title, labels: [] }, { number: 3, title: "charter", labels: [] }];
        if (args[1].endsWith("/3")) return { ...issue, number: 3, state: "closed" };
        if (args[1].includes("/comments?")) return [first];
        return issue;
    };
    assert.equal((await fetchIssues(fake)).length, 2);
    assert.ok(calls.some(args => args[1].includes("comments?per_page=100&page=1")));
    await assert.rejects(fetchIssues(async args => {
        if (args[0] === "issue") return [{ number: 73, title: issue.title, labels: [] }];
        throw new Error("fetch failed");
    }), /fetch failed/);
});
test("untrusted text is never interpolated into HTML and links reject script/foreign/authenticated URLs", async () => {
    const payload = '<img src=x onerror="alert(1)"><script>bad()</script>';
    const cards = extractDecisions(issue, [comment(1, `### Assessment question: escape\n**Please help me decide: ${payload}?**`)]);
    assert.ok(cards[0].body.includes(payload));
    const client = await readFile(new URL("client.mjs", import.meta.url), "utf8");
    assert.match(client, /element\.textContent = text/);
    assert.doesNotMatch(client, /innerHTML|outerHTML|insertAdjacentHTML|document\.write/);
    for (const validate of [safeLink, browser.safeLink]) {
        for (const unsafe of ["javascript:alert(1)", "https://evil.example/", "https://github.com.evil.example/", "https://user@github.com/", "http://github.com/", "https://github.com:123/"]) assert.equal(validate(unsafe), null);
        assert.equal(validate(issue.html_url), issue.html_url);
    }
});
test("durable private drafts rehydrate across stores and distinct panel IDs without repository writes", async () => {
    const root = join(process.cwd(), `.open-decisions-test-${randomUUID()}`);
    try {
        const id = decisionIdentity(73, "Assessment question: plan");
        const a = new DraftStore(root); await a.save(id, "Draft—not approved");
        const b = new DraftStore(root);
        assert.equal((await b.get(id)).text, "Draft—not approved");
        assert.equal((await b.get(decisionIdentity(73, "Assessment question: cost"))).text, "");
        assert.throws(() => a.path("../../escape"), /Invalid/);
        await assert.rejects(a.save(id, "x".repeat(16001)), /16 KB/);
        await a.save(id, ""); assert.equal((await b.get(id)).text, "");
    } finally { await rm(root, { recursive: true, force: true }); }
});
test("HTTP routes are loopback, CSRF protected, bounded, read-only toward GitHub and close cleanly", async () => {
    const store = new DecisionStore(async () => extractDecisions(issue, [first]));
    await store.refresh();
    const saved = new Map();
    const drafts = { get: async id => ({ id, text: saved.get(id) || "" }), save: async (id, text) => { saved.set(id, text); return { id, text }; } };
    const { server, url } = await startServer(store, drafts);
    try {
        assert.equal(server.address().address, "127.0.0.1");
        const source = await fetch(url); assert.equal(source.status, 200);
        assert.match(await source.text(), /NOT submitted|never submits/);
        assert.match(source.headers.get("content-security-policy"), /script-src 'self'/);
        const state = await (await fetch(`${url}state`)).json();
        const id = state.cards[0].id;
        const post = (path, data, headers = {}) => fetch(`${url}${path}`, { method: "POST",
            headers: { "Content-Type": "application/json", Origin: url.slice(0, -1), "X-Canvas-CSRF": state.csrf, ...headers }, body: JSON.stringify(data) });
        assert.equal((await post("draft", { id, text: "private" }, { Origin: "https://evil.example" })).status, 403);
        assert.equal((await post("draft", { id, text: "private" }, { "X-Canvas-CSRF": "wrong" })).status, 403);
        assert.equal((await post("draft", { id, text: "private" })).status, 200);
        assert.equal((await (await fetch(`${url}draft?id=${encodeURIComponent(id)}`)).json()).text, "private");
        assert.equal((await post("draft", { id, text: "x".repeat(21000) })).status, 413);
        assert.equal((await post("approve", { id })).status, 404);
        assert.equal((await post("refresh", {})).status, 200);
        assert.equal((await fetch(`${url}state`, { headers: { Origin: "https://evil.example" } })).status, 403);
        const hostStatus = await new Promise((resolve, reject) => {
            const req = httpRequest(`${url}state`, { headers: { Host: "evil.example" } }, response => {
                response.resume(); resolve(response.statusCode);
            });
            req.on("error", reject); req.end();
        });
        assert.equal(hostStatus, 403);
        assert.equal((await fetch(`${url}client.mjs`)).headers.get("content-type"), "text/javascript; charset=utf-8");
    } finally { await new Promise(resolve => server.close(resolve)); }
    assert.equal(server.listening, false);
});
