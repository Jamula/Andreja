import test from "node:test";
import assert from "node:assert/strict";
import { rm } from "node:fs/promises";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { decisionIdentity, DraftStore, extractDecisions } from "./model.mjs";

const issue = {
    number: 73, state: "open", title: "[Decision]: governance",
    body: "Original packet", html_url: "https://github.com/Jamula/Andreja/issues/73",
    updated_at: "2026-10-11T00:00:00Z",
};
const comment = (id, body) => ({
    id, body, html_url: `${issue.html_url}#issuecomment-${id}`,
    updated_at: "2026-10-11T01:00:00Z",
});
const repeated = "### Decision question\nShould the plan remain Proposed?\n"
    + "### Assessment question: evidence\nIs the evidence complete?\n"
    + "### DECISION QUESTION\nShould the cost controls remain Draft?";

test("repeated normalized headings retain separate cards and durable private drafts", async () => {
    const cards = extractDecisions({ ...issue, body: repeated });
    assert.equal(cards.length, 3);
    assert.equal(new Set(cards.map(card => card.id)).size, 3);
    assert.equal(cards[0].id, decisionIdentity(issue.number, "Decision question"));
    assert.equal(cards[2].id, decisionIdentity(issue.number, "decision question", 2));
    assert.equal(cards[0].question, "Should the plan remain Proposed?");
    assert.equal(cards[2].question, "Should the cost controls remain Draft?");
    assert.doesNotMatch(cards[0].body, /cost controls|evidence complete/);
    assert.doesNotMatch(cards[2].body, /plan remain/);
    const root = join(process.cwd(), `.open-decisions-model-review-${randomUUID()}`);
    try {
        const drafts = new DraftStore(root);
        assert.notEqual(drafts.path(cards[0].id), drafts.path(cards[2].id));
        await drafts.save(cards[0].id, "Plan draft");
        await drafts.save(cards[2].id, "Cost draft");
        const reopened = new DraftStore(root);
        assert.equal((await reopened.get(cards[0].id)).text, "Plan draft");
        assert.equal((await reopened.get(cards[2].id)).text, "Cost draft");
    } finally { await rm(root, { recursive: true, force: true }); }
});

test("later comments update matching heading occurrences without changing their identities", () => {
    const original = extractDecisions({ ...issue, body: repeated });
    const update = comment(123, "### decision question\nShould the plan be amended?\n"
        + "### Decision question\nShould the cost controls be ratified?");
    const cards = extractDecisions({ ...issue, body: repeated }, [update]);
    assert.deepEqual(cards.map(card => card.id), original.map(card => card.id));
    assert.equal(cards[0].question, "Should the plan be amended?");
    assert.equal(cards[0].sourceId, "123");
    assert.equal(cards[0].sourceUrl, update.html_url);
    assert.equal(cards[1].sourceId, "body");
    assert.equal(cards[2].question, "Should the cost controls be ratified?");
    assert.equal(cards[2].sourceId, "123");
});

test("a single-heading follow-up preserves existing first-occurrence update semantics", () => {
    const cards = extractDecisions(issue, [
        comment(123, repeated),
        comment(124, "### Decision question\nShould the plan be deferred?"),
    ]);
    assert.equal(cards.length, 3);
    assert.equal(cards[0].id, decisionIdentity(issue.number, "Decision question"));
    assert.equal(cards[0].sourceId, "124");
    assert.equal(cards[0].question, "Should the plan be deferred?");
    assert.equal(cards[2].sourceId, "123");
    assert.equal(cards[2].question, "Should the cost controls remain Draft?");
});

test("non-question sections with repeated headings do not consume question occurrence slots", () => {
    const cards = extractDecisions({
        ...issue, body: "### Decision question\nNo question supplied.\n" + repeated,
    });
    assert.equal(cards.length, 3);
    assert.equal(cards[0].id, decisionIdentity(issue.number, "Decision question"));
    assert.equal(cards[2].id, decisionIdentity(issue.number, "Decision question", 2));
});

test("overview without comments quotes the issue body once and keeps issue provenance", () => {
    const [card] = extractDecisions(issue);
    assert.equal(card.body, issue.body);
    assert.equal(card.question, null);
    assert.equal(card.sourceId, "body");
    assert.equal(card.sourceUrl, issue.html_url);
    assert.equal(card.updatedAt, issue.updated_at);
    assert.deepEqual(card.discussion, []);
    assert.equal(extractDecisions({ ...issue, body: "" })[0].body, "No issue text supplied.");
});

test("overview with comments includes only latest discussion and its actual provenance", () => {
    const latest = comment(124, "Latest evidence update.");
    const [card] = extractDecisions(issue, [comment(123, "Earlier evidence update."), latest]);
    assert.equal(card.body, `${issue.body}\n\nLatest discussion:\n${latest.body}`);
    assert.equal(card.sourceId, "124");
    assert.equal(card.sourceUrl, latest.html_url);
    assert.equal(card.updatedAt, latest.updated_at);
    assert.deepEqual(card.discussion.map(entry => entry.id), [123, 124]);
    assert.equal(extractDecisions(issue, [comment(125, "")])[0].body,
        `${issue.body}\n\nLatest discussion:\nNo discussion supplied.`);
});
