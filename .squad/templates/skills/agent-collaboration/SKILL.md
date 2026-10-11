---
name: "agent-collaboration"
description: "Standard collaboration patterns for all squad agents — worktree awareness, decisions, cross-agent communication"
domain: "team-workflow"
confidence: "high"
source: "extracted from charter boilerplate — identical content in 18+ agent charters"
---

## Context

Every agent on the team follows identical collaboration patterns for worktree awareness, decision recording, and cross-agent communication. These were previously duplicated in every charter's Collaboration section (~300 bytes × 18 agents = ~5.4KB of redundant context). Now centralized here.

The coordinator's spawn prompt already instructs agents to read decisions.md and their history.md. This skill adds the patterns for WRITING decisions and requesting help.

## Patterns

### Worktree Awareness
Use the `TEAM ROOT` path provided in your spawn prompt. All `.squad/` paths are relative to this root. If TEAM ROOT is not provided (rare), run `git rev-parse --show-toplevel` as fallback. Never assume CWD is the repo root.

### Start-of-Work Sync
Before editing, require a clean worktree, fetch/prune the remote, and rebase the
issue branch onto the verified live `origin/main` or stacked parent branch.
Stop on dirty state, conflict, changed remote tip, or lease failure; never
overwrite another worktree.

### Pre-PR Validation
Before opening or marking a PR ready, run the smallest complete existing local
build/test/lint/type/docs/config/scenario checks required by the issue. Record
commands and results in the issue and PR. Required failures block readiness.

### Decision Recording
After making a decision that affects other team members, write it to:
`.squad/decisions/inbox/{your-name}-{brief-slug}.md`

Format:
```
### {date}: {decision title}
**By:** {Your Name}
**What:** {the decision}
**Why:** {rationale}
```

### Cross-Agent Communication
If you need another team member's input, say so in your response. The coordinator will bring them in. Don't try to do work outside your domain.

### Human Decision Dialogue
When work needs Cyrus's decision, synthesize the blocker into a conversation, not a raw status dump. Ask for one clear decision at a time:

- Lead with "Please help me decide [specific decision]." Give only the context needed to answer, link the current issue/PR and evidence, and distinguish verified facts from missing evidence.
- Present a small set of concrete, genuinely available choices with the consequence or tradeoff of each. Recommend one and explain why the evidence supports it. Use fewer than four choices when appropriate; never invent options, facts, estimates, or an unsupported recommendation.
- State the exact authorization requested, its scope and limits, and what remains blocked or unauthorized afterward. Distinguish advice, document review, ordinary-code sign-off, and a recorded human decision; never treat silence as approval.
- For an interactive decision, use `ask_user` with one focused question and choices whose labels make the consequences clear. Present the recommendation and evidence before the picker. Do not bundle unrelated approvals into a single choice. If `ask_user` is unavailable, present the same focused question, recommendation, and labeled choices with their consequences in your response, then wait for an explicit answer before acting.
- For a complex document or decision packet, ask "Please review this document (as a PR) and add comments so we can work through the decision." Link the draft PR, identify the specific section/question and tradeoffs, and work through comments one decision at a time. Review comments alone do not ratify the document or authorize execution.

Use only the record designated by the applicable governed workflow for an authorization decision. A PR may host document review and discussion, but it does not substitute for that record, including when merged. Do not create a parallel backlog or rewrite unresolved decisions; record an explicit response through the designated workflow where required.

This guidance changes communication, not authority or merge gates. An independent, favorable Copilot or separately dispatched team-lead verdict must be explicit and bound to the exact current PR head under the [ordinary-code sign-off procedure](../../../../docs/operations/default-branch-protection.md#proposed-ordinary-code-sign-off-procedure); `COMMENTED` alone is not sign-off. It does not satisfy separate human legal/counsel, spend, ratification, activation, launch, or residual-risk gates. Preserve [reviewer lockout](../reviewer-protocol/SKILL.md): after rejection, a different agent owns the revision; the rejected author cannot contribute. The coordinator retains the serialized merge slot, and PR-level auto-merge remains subject to the existing sign-off, resolved-findings/threads, and exact-head required-check gates; this dialogue grants no merge or settings authorization.

### Reviewer Protocol
If you have reviewer authority and reject work: the original author is locked out from revising that artifact. A different agent must own the revision. State who should revise in your rejection response.

## Anti-Patterns
- Don't read all agent charters — you only need your own context + decisions.md
- Don't write directly to `.squad/decisions.md` — always use the inbox drop-box
- Don't modify other agents' history.md files — that's Scribe's job
- Don't assume CWD is the repo root — always use TEAM ROOT
- Don't start from stale local `main` or an unverified stacked parent
- Don't post a PR before running applicable local validation
