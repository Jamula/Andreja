import { filterDecisions, safeLink } from "/model.mjs";

const $ = id => document.getElementById(id);
let state = { cards: [] };
let selected = null;
let csrf;
let dirty = false;
let draftGeneration = 0;
function node(tag, text, className) {
    const element = document.createElement(tag);
    if (text != null) element.textContent = text;
    if (className) element.className = className;
    return element;
}
function link(text, url) {
    const href = safeLink(url);
    const element = node(href ? "a" : "span", text);
    if (href) { element.href = href; element.target = "_blank"; element.rel = "noopener noreferrer"; }
    return element;
}
async function request(path, data) {
    const response = await fetch(path, data === undefined ? {} : {
        method: "POST", headers: { "Content-Type": "application/json", "X-Canvas-CSRF": csrf },
        body: JSON.stringify(data),
    });
    const result = await response.json();
    if (!response.ok) {
        if (path === "/refresh" && result.cards) { state = result; render(); }
        throw new Error(result.error || "Local request failed");
    }
    return result;
}
function error(message) { $("error").textContent = message || ""; $("error").hidden = !message; }
function render() {
    error(state.error);
    $("freshness").textContent = `${state.stale ? "STALE / unverified. " : ""}Last successful refresh: ${state.lastRefresh || "never"}. Last attempt: ${state.attemptedAt || "not yet"}.${dirty ? " Unsaved draft: focused source retained. Save or copy, then reselect to inspect refreshed dialogue." : ""}`;
    const cards = filterDecisions(state.cards, $("search").value, $("group").value);
    $("count").textContent = `${cards.length} source questions / overviews`;
    $("cards").replaceChildren();
    for (const card of cards) {
        const item = node("li");
        const button = node("button", null, "card");
        button.type = "button"; button.setAttribute("aria-current", String(card.id === selected));
        button.append(node("strong", `#${card.issueNumber} · ${card.heading}`),
            node("span", `${card.group} · ${card.kind}`, "meta"), node("span", `Source updated ${card.updatedAt}`, "meta"));
        button.onclick = () => focusCard(card);
        item.append(button); $("cards").append(item);
    }
    if (!cards.length) $("cards").append(node("li", state.error ? "No verified source available. Retry refresh." : "No matching open decisions."));
}
async function focusCard(card, force = false) {
    if (dirty && !force && !window.confirm("Discard unsaved local draft changes? Save the draft first to keep it.")) return;
    selected = card.id; dirty = false; render();
    const generation = ++draftGeneration;
    const detail = $("detail"); detail.replaceChildren();
    const heading = node("h2", card.heading); heading.tabIndex = -1;
    detail.append(heading, node("p", card.status, "meta"),
        node("p", `${card.kind} · ${card.group} · source ${card.sourceId} · updated ${card.updatedAt}`, "meta"),
        link("Read / discuss on GitHub (no approval here)", card.sourceUrl || card.issueUrl));
    if (card.question) {
        detail.append(node("h3", "Question and available alternatives — exact source"), node("p", card.question, "source"));
        if (card.options) detail.append(node("p", card.options, "source"));
    }
    if (card.recommendation) detail.append(node("h3", "Source recommendation — not your decision"), node("p", card.recommendation, "source"));
    detail.append(node("h3", "Context, tradeoffs and boundaries — source text"), node("div", card.body, "source"));
    const dialogue = node("details"); dialogue.append(node("summary", "Latest discussion (may supersede the question)"));
    for (const comment of card.discussion) dialogue.append(link(`Comment ${comment.id} · ${comment.updatedAt}`, comment.url), node("p", comment.body, "source"));
    detail.append(dialogue, link(`Full issue #${card.issueNumber} discussion`, card.issueUrl));
    const draftLabel = node("label", "Private draft — NOT submitted / approved");
    const textarea = node("textarea"); textarea.maxLength = 16000; textarea.disabled = true;
    draftLabel.append(textarea);
    const status = node("p", "Loading saved local draft…"); status.setAttribute("role", "status");
    const actions = node("div", null, "actions");
    const save = node("button", "Save local draft"); save.disabled = true;
    const copy = node("button", "Copy draft"); copy.disabled = true;
    const undo = node("button", "Undo unsaved changes"); undo.disabled = true;
    actions.append(save, copy, undo); detail.append(draftLabel, actions, status);
    let saved = "";
    textarea.oninput = () => { dirty = true; status.textContent = "Unsaved local changes — nothing submitted."; };
    save.onclick = async () => {
        save.disabled = true;
        const text = textarea.value;
        try {
            const result = await request("/draft", { id: card.id, text });
            saved = result.text;
            if (generation !== draftGeneration) return;
            dirty = textarea.value !== saved;
            status.textContent = `Draft saved locally ${result.updatedAt}. NOT submitted / approved.`;
        } catch (e) { status.textContent = `Draft not saved: ${e.message}`; }
        finally { save.disabled = false; }
    };
    copy.onclick = async () => {
        try { await navigator.clipboard.writeText(textarea.value); status.textContent = "Copied draft. Submit only through the genuine GitHub discussion."; }
        catch { status.textContent = "Copy unavailable. Select the draft text and copy manually."; textarea.select(); }
    };
    undo.onclick = () => { textarea.value = saved; dirty = false; status.textContent = "Restored last saved local draft."; };
    heading.focus();
    try {
        const data = await request(`/draft?id=${encodeURIComponent(card.id)}`);
        if (generation !== draftGeneration) return;
        saved = data.text; textarea.value = saved; textarea.disabled = false;
        save.disabled = false; copy.disabled = false; undo.disabled = false;
        status.textContent = data.updatedAt ? `Local draft saved ${data.updatedAt}. NOT submitted / approved.` : "No saved draft. Stored under your Copilot user artifacts, never in the repository.";
    } catch (e) { status.textContent = `Saved draft unavailable: ${e.message}. Editing disabled to avoid overwriting unread data.`; }
}
$("search").oninput = render;
$("group").onchange = render;
$("refresh").onclick = async () => {
    $("refresh").disabled = true;
    $("freshness").textContent = "Refreshing authoritative GitHub source…";
    try {
        state = await request("/refresh", {}); render();
        const card = state.cards.find(item => item.id === selected);
        if (!dirty && card) await focusCard(card, true);
        else if (!dirty && selected && !card) { selected = null; $("detail").replaceChildren(node("p", "This question is no longer in the open source set.")); }
    } catch (e) { error(e.message); }
    finally { $("refresh").disabled = false; }
};
window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });
async function sync() {
    try {
        const next = await request("/state"); csrf = next.csrf;
        const changed = next.lastRefresh !== state.lastRefresh || next.error !== state.error;
        state = next; render();
        if (changed && selected && !dirty) {
            const card = state.cards.find(item => item.id === selected);
            if (card) await focusCard(card, true);
            else { selected = null; $("detail").replaceChildren(node("p", "This question is no longer in the open source set.")); }
        }
    } catch (e) { error(`Local provider unavailable: ${e.message}. Displayed data may be stale.`); }
}
await sync();
setInterval(sync, 3000);
