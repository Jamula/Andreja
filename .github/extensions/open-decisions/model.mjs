import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { createHash, randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile, rename } from "node:fs/promises";
import { join } from "node:path";
import { homedir } from "node:os";

export const REPO = "Jamula/Andreja";
export const PRIORITY = [2, 73, 3, 6, 93, 155, 67];
const run = promisify(execFile);
export async function ghJson(args) {
    const { stdout } = await run("gh", args, { timeout: 20000, maxBuffer: 8 * 1024 * 1024, windowsHide: true });
    return JSON.parse(stdout);
}
export function safeLink(value) {
    try {
        const url = new URL(value);
        return url.protocol === "https:" && url.hostname === "github.com" && !url.username && !url.password && !url.port ? url.href : null;
    } catch { return null; }
}
function digest(value) { return createHash("sha256").update(value).digest("hex"); }
export function decisionIdentity(number, heading) {
    return `${REPO}:${number}:${digest(heading.trim().toLowerCase()).slice(0, 24)}`;
}
export function extractDecisions(issue, comments = []) {
    if (issue.state.toLowerCase() !== "open") return [];
    const found = new Map();
    const sources = [{ id: "body", body: issue.body, html_url: issue.html_url, updated_at: issue.updated_at }, ...comments];
    for (const source of sources) {
        const body = source.body || "";
        const headings = [...body.matchAll(/^#{1,6}\s+([^\n]+)$/gm)];
        const questions = headings.filter((match, i) => /\bquestion\b/i.test(match[1])
            && /\?|please help me decide/i.test(body.slice(match.index, headings[i + 1]?.index ?? body.length)));
        for (let i = 0; i < questions.length; i++) {
            const heading = questions[i][1].trim();
            const section = body.slice(questions[i].index, questions[i + 1]?.index ?? body.length).trim();
            const id = decisionIdentity(issue.number, heading);
            const question = section.match(/\*\*(Please help me decide[\s\S]*?)\*\*/i)?.[1]
                || section.split("\n").find(line => line.includes("?")) || heading;
            found.set(id, makeCard(issue, source, id, heading, section, question));
        }
    }
    if (!found.size) {
        const latest = comments.at(-1) || sources[0];
        found.set(decisionIdentity(issue.number, "source-overview"), makeCard(issue, latest,
            decisionIdentity(issue.number, "source-overview"), issue.title,
            `${issue.body || "No issue text supplied."}\n\nLatest discussion:\n${latest.body || "No discussion supplied."}`, null));
    }
    return [...found.values()].map(card => ({
        ...card,
        discussion: comments.slice(-5).map(comment => ({
            id: comment.id, body: comment.body, url: safeLink(comment.html_url), updatedAt: comment.updated_at,
        })),
    }));
}
function extractOptions(body, question) {
    const headings = [...body.matchAll(/^(#{1,6})\s+([^\n]+)$/gm)];
    const index = headings.findIndex(match => /^(?:options|alternatives)\b/i.test(match[2]));
    if (index >= 0) {
        const heading = headings[index];
        const end = headings.slice(index + 1).find(match => match[1].length <= heading[1].length)?.index ?? body.length;
        const options = body.slice(heading.index + heading[0].length, end).trim();
        if (options) return options;
    }
    if (!question) return null;
    const alternatives = question.replace(/^Please help me decide\b[^:]*:\s*/i, "")
        .replace(/^Please help me decide\s*/i, "").replace(/\?\s*$/, "").trim();
    return /\bor\b/i.test(alternatives) ? alternatives : null;
}
function makeCard(issue, source, id, heading, body, question) {
    const later = /\blater gate\b|\bpreactivation\b|not automatically a phase 0|not an automatic.*phase 0/i.test(body)
        || [93, 155, 156, 157, 158].includes(issue.number);
    const group = later ? "Later / gated" : [2, 73, 3, 6].includes(issue.number) ? "Phase 0" : "Evidence / work updates";
    return {
        id, issueNumber: issue.number, issueTitle: issue.title, heading, body, question,
        // Options remain an exact source quotation, not an invented menu.
        options: extractOptions(body, question),
        recommendation: body.match(/(?:Recommendation:|[\w '’-]+ recommends?)[^\n]+/i)?.[0] || null,
        sourceId: String(source.id), sourceUrl: safeLink(source.html_url),
        issueUrl: safeLink(issue.html_url), updatedAt: source.updated_at || issue.updated_at,
        group, status: "Unknown — read current source dialogue; no local approval",
        kind: question ? "Human question" : "Source overview — question not extracted",
        priority: PRIORITY.indexOf(issue.number) < 0 ? 99 : PRIORITY.indexOf(issue.number),
    };
}
export function filterDecisions(cards, query = "", group = "All") {
    const needle = query.trim().toLowerCase();
    return cards.filter(card => (group === "All" || card.group === group)
        && `${card.issueNumber} ${card.issueTitle} ${card.heading} ${card.body}`.toLowerCase().includes(needle));
}
export async function fetchIssues(gh = ghJson) {
    const list = await gh(["issue", "list", "--repo", REPO, "--state", "open", "--limit", "500",
        "--json", "number,title,labels"]);
    if (list.length === 500) throw new Error("Open issue listing reached its limit; refresh cannot claim completeness.");
    const candidates = list.filter(issue => PRIORITY.includes(issue.number) || [156, 157, 158].includes(issue.number)
        || /^\[Decision\]/i.test(issue.title) || issue.labels.some(label => /^decision$/i.test(label.name)));
    const results = [];
    // Bounded concurrency avoids flooding the CLI or GitHub.
    for (let offset = 0; offset < candidates.length; offset += 3) {
        const batch = await Promise.all(candidates.slice(offset, offset + 3).map(async ({ number }) => {
            const issue = await gh(["api", `repos/${REPO}/issues/${number}`]);
            if (issue.state !== "open" || issue.pull_request) return [];
            const comments = [];
            for (let page = 1; page <= 10; page++) {
                const chunk = await gh(["api", `repos/${REPO}/issues/${number}/comments?per_page=100&page=${page}`,
                    "--jq", "map({id,body,html_url,updated_at})"]);
                comments.push(...chunk);
                if (chunk.length < 100) break;
                if (page === 10) throw new Error(`Issue #${number} dialogue exceeded the bounded fetch limit.`);
            }
            return extractDecisions(issue, comments);
        }));
        results.push(...batch.flat());
    }
    return results.sort((a, b) => a.priority - b.priority || a.issueNumber - b.issueNumber);
}
export class DecisionStore {
    constructor(loader = fetchIssues, clock = () => new Date().toISOString()) {
        this.loader = loader; this.clock = clock; this.cards = []; this.lastRefresh = null;
        this.attemptedAt = null; this.error = null; this.pending = null;
    }
    async refresh() {
        if (this.pending) return this.pending;
        this.attemptedAt = this.clock();
        this.pending = (async () => {
            try {
                const cards = await this.loader();
                this.cards = cards; this.lastRefresh = this.clock(); this.error = null;
            } catch (error) {
                this.error = `GitHub refresh failed: ${error.code || error.message || "unknown error"}. Check gh authentication/network and retry.`;
            } finally { this.pending = null; }
        })();
        return this.pending;
    }
    snapshot() {
        return { repo: REPO, cards: this.cards, lastRefresh: this.lastRefresh, attemptedAt: this.attemptedAt,
            error: this.error, stale: !!this.error || !this.lastRefresh, refreshing: !!this.pending };
    }
}
export class DraftStore {
    constructor(root = join(process.env.COPILOT_HOME || join(homedir(), ".copilot"), "extensions", "open-decisions", "artifacts")) {
        this.root = root;
    }
    path(id) {
        if (typeof id !== "string" || !id.startsWith(`${REPO}:`) || id.length > 160) throw new Error("Invalid draft identity");
        return join(this.root, `${digest(id)}.json`);
    }
    async get(id) {
        try {
            const data = JSON.parse(await readFile(this.path(id), "utf8"));
            if (data.id !== id || typeof data.text !== "string") throw new Error("Invalid saved draft");
            return data;
        } catch (error) {
            if (error.code === "ENOENT") return { id, text: "", updatedAt: null };
            throw error;
        }
    }
    async save(id, text) {
        const path = this.path(id);
        if (typeof text !== "string" || Buffer.byteLength(text) > 16000) throw new Error("Draft must be at most 16 KB");
        await mkdir(this.root, { recursive: true });
        const data = { id, text, updatedAt: new Date().toISOString() };
        const staging = `${path}.${randomUUID()}.new`;
        await writeFile(staging, JSON.stringify(data), { mode: 0o600 });
        await rename(staging, path);
        return data;
    }
}
