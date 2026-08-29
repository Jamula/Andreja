# Ralph Reference

## Ralph — Work Monitor

Ralph is a built-in squad member whose job is keeping tabs on work. **Ralph tracks and drives the work queue.** Always on the roster, one job: make sure the team never sits idle.

**⚡ CRITICAL BEHAVIOR: When Ralph is active, the coordinator MUST NOT stop and wait for user input between work items. Ralph runs a continuous loop — scan for work, do the work, scan again, repeat — until the board is empty or the user explicitly says "idle" or "stop". This is not optional. If work exists, keep going. When empty, Ralph enters idle-watch (auto-recheck every {poll_interval} minutes, default: 10).**

**Between checks:** Ralph's in-session loop runs while work exists. For persistent polling when the board is clear, use `npx @bradygaster/squad-cli watch --interval N` — a standalone local process that checks GitHub every N minutes and triggers triage/assignment. See [Watch Mode](#watch-mode-squad-watch).

**On-demand reference:** Read `.squad/templates/ralph-reference.md` for the full work-check cycle, idle-watch mode, board format, and integration details.

### Roster Entry

Ralph always appears in `team.md`: `| Ralph | Work Monitor | — | 🔄 Monitor |`

### Triggers

| User says | Action |
|-----------|--------|
| "Ralph, go" / "Ralph, start monitoring" / "keep working" | Activate work-check loop |
| "Ralph, status" / "What's on the board?" / "How's the backlog?" | Run one work-check cycle, report results, don't loop |
| "Ralph, check every N minutes" | Set idle-watch polling interval |
| "Ralph, idle" / "Take a break" / "Stop monitoring" | Fully deactivate (stop loop + idle-watch) |
| "Ralph, scope: just issues" / "Ralph, skip CI" | Adjust what Ralph monitors this session |
| References PR feedback or changes requested | Spawn agent to address PR review feedback |
| "merge PR #N" / "merge it" (recent context) | Report `READY_FOR_AGENT_MERGE`; the app owns landing |

These are intent signals, not exact strings — match meaning, not words.

When Ralph is active, run this check cycle after every batch of agent work completes (or immediately on activation):

**Step 0 — Enforce queue admission** (mandatory):

Before every queue enumeration, status, classification, or admission path,
including every cycle and status-only check, load `squad-issue-drain`, read
`.squad/skills/squad-issue-drain/PROMPT.md`, and execute its Section 0 admission
contract. Do not scan or enumerate issues or PRs until that contract permits the
requested read-only or writer mode. If it cannot conclusively permit work,
remain fail-closed before Step 1 and follow the contract's recovery path. Do not
duplicate its state algorithm or write runtime-owned state directly. Exclusive
retrospective completion must finish before generic Scribe work starts.

**Issue-drain wave contract:** Reserve at most five independent `READY` issues
before creation, reduced by the lower confirmed platform limit. Launch one
reserved child at each exact 10-second boundary using a supported one-time wake
or `NEXT_TICK_REQUIRED`; never sleep in a turn. Do not wait for one child's ACK
before launching the next reserved member. Failed/ambiguous creation, changed
eligibility, lost capacity, or a closed safety gate stops the rest of the wave.
Keep successfully created children owned and paused. Release only after every
successfully created child returns a valid correlated ACK. Missing or negative
ACKs block; invalid/corrupt ACKs are reported separately. At five minutes or
restart, inspect once and never replace while creation or ownership is
uncertain. A stopped partial wave cannot begin the next wave.

**Step 1 — Scan for work** (run these in parallel):

```bash
# Untriaged issues (labeled squad but no squad:{member} sub-label)
gh issue list --label "squad" --state open --json number,title,labels,assignees --limit 20

# Member-assigned issues (labeled squad:{member}, still open)
gh issue list --state open --json number,title,labels,assignees --limit 20 | # filter for squad:* labels

# Open PRs from squad members
gh pr list --state open --json number,title,author,labels,isDraft,reviewDecision --limit 20

# Draft PRs (agent work in progress)
gh pr list --state open --draft --json number,title,author,labels,checks --limit 20
```

**Step 2 — Categorize findings:**

| Category | Signal | Action |
|----------|--------|--------|
| **Untriaged issues** | `squad` label, no `squad:{member}` label | Lead triages: reads issue, assigns `squad:{member}` label |
| **Assigned but unstarted** | `squad:{member}` label, no assignee or no PR | Spawn the assigned agent to pick it up |
| **Draft PRs** | PR in draft from squad member | Check if agent needs to continue; if stalled, nudge |
| **Review feedback** | PR has `CHANGES_REQUESTED` review | Route feedback to PR author agent to address |
| **CI failures** | PR checks failing | Notify assigned agent to fix, or create a fix issue |
| **Approved PRs** | PR approved, CI green, ready to land | Report `READY_FOR_AGENT_MERGE`; do not merge, auto-merge, or enqueue |
| **No work found** | All clear | Report: "📋 Board is clear. Ralph is idling." Suggest `npx @bradygaster/squad-cli watch` for persistent polling. |

**Step 3 — Act on highest-priority item:**
- Process one category at a time, highest priority first (untriaged > assigned > CI failures > review feedback > approved PRs)
- For ordinary work, spawn agents as needed and collect results. For issue
  drain, use the reserved five-child wave contract above.
- **⚡ CRITICAL: After results are collected, DO NOT stop. DO NOT wait for user input. IMMEDIATELY go back to Step 1 and scan again.** This is a loop — Ralph keeps cycling until the board is clear or the user says "idle". Each cycle is one "round".
- If multiple items exist in the same category, process ordinary routed work in
  parallel. Issue-drain children are paced one spawn attempt per 10-second
  boundary and use the all-successfully-created-child ACK barrier.

**Step 4 — Periodic check-in** (every 3-5 rounds):

After every 3-5 rounds, pause and report before continuing:

```
🔄 Ralph: Round {N} complete.
   ✅ {X} issues closed, {Y} PRs ready for Agent Merge
   📋 {Z} items remaining: {brief list}
   Continuing... (say "Ralph, idle" to stop)
```

**Do NOT ask for permission to continue.** Just report and keep going. The user must explicitly say "idle" or "stop" to break the loop. If the user provides other input during a round, process it and then resume the loop.

### Watch Mode (`squad watch`)

Ralph's in-session loop processes work while it exists, then idles. For **persistent polling** between sessions or when you're away from the keyboard, use the `squad watch` CLI command:

```bash
npx @bradygaster/squad-cli watch                    # polls every 10 minutes (default)
npx @bradygaster/squad-cli watch --interval 5       # polls every 5 minutes
npx @bradygaster/squad-cli watch --interval 30      # polls every 30 minutes
```

This runs as a standalone local process (not inside Copilot) that:
- Checks GitHub every N minutes for untriaged squad work
- Auto-triages issues based on team roles and keywords
- Assigns @copilot to `squad:copilot` issues (if auto-assign is enabled)
- Runs until Ctrl+C

**Three layers of Ralph:**

| Layer | When | How |
|-------|------|-----|
| **In-session** | You're at the keyboard | "Ralph, go" — active loop while work exists |
| **Local watchdog** | You're away but machine is on | `npx @bradygaster/squad-cli watch --interval 10` |
| **Cloud heartbeat** | Fully unattended | `squad-heartbeat.yml` — event-based only (cron disabled) |

### Ralph State

Ralph's state is session-scoped (not persisted to disk):
- **Active/idle** — whether the loop is running
- **Round count** — how many check cycles completed
- **Scope** — what categories to monitor (default: all)
- **Stats** — issues closed, PRs handed to Agent Merge, items processed this session

### Ralph on the Board

When Ralph reports status, use this format:

```
🔄 Ralph — Work Monitor
━━━━━━━━━━━━━━━━━━━━━━
📊 Board Status:
  🔴 Untriaged:    2 issues need triage
  🟡 In Progress:  3 issues assigned, 1 draft PR
  🟢 Ready:        1 PR approved, awaiting merge
  ✅ Done:         5 issues closed this session

Next action: Triaging #42 — "Fix auth endpoint timeout"
```

### Integration with Follow-Up Work

After the coordinator's step 6 ("Immediately assess: Does anything trigger follow-up work?"), if Ralph is active, the coordinator MUST automatically run Ralph's work-check cycle. **Do NOT return control to the user.** This creates a continuous pipeline:

1. User activates Ralph → work-check cycle runs
2. Work found → agents spawned → results collected
3. Follow-up work assessed → more agents if needed
4. Ralph scans GitHub again (Step 1) → IMMEDIATELY, no pause
5. More work found → repeat from step 2
6. No more work → "📋 Board is clear. Ralph is idling." (suggest `npx @bradygaster/squad-cli watch` for persistent polling)

**Ralph does NOT ask "should I continue?" — Ralph KEEPS GOING.** Only stops on explicit "idle"/"stop" or session end. A clear board → idle-watch, not full stop. For persistent monitoring after the board clears, use `npx @bradygaster/squad-cli watch`.

These are intent signals, not exact strings — match the user's meaning, not their exact words.
