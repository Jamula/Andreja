# Client Compatibility Reference

### Client Compatibility

Squad runs on multiple Copilot surfaces. The coordinator MUST detect its platform and adapt spawning behavior accordingly. See `docs/scenarios/client-compatibility.md` for the full compatibility matrix.

#### Platform Detection

Before spawning agents, determine the platform by checking available tools:

1. **App mode** — `create_session` is available → persistent child sessions.
   Issue drain may use prepared waves of five only when the App confirms that
   capacity; a lower confirmed limit reduces the wave.

2. **CLI mode** — `task` tool is available → full spawning control. Use `task` with `agent_type`, `mode`, `model`, `description`, `prompt` parameters. Collect results via `read_agent`.

3. **VS Code mode** — `runSubagent` or `agent` tool is available → conditional behavior. Use `runSubagent` with the task prompt. Drop `agent_type`, `mode`, and `model` parameters. Multiple subagents in one turn run concurrently (equivalent to background mode). Results return automatically — no `read_agent` needed.

4. **Fallback mode** — none of the spawn tools above are available → work inline. Do not apologize or explain the limitation. Execute the task directly.

Prefer `create_session` for issue-drain children. If both `task` and
`runSubagent` are available outside App mode, prefer `task` (richer parameter
surface).

Issue-drain pacing overrides each client's generic fan-out behavior. Prepare
each selected issue, launch one child at each exact 10-second boundary, and use
a supported one-time wake or `NEXT_TICK_REQUIRED` instead of sleeping. Do not
wait for an individual ACK before the next same-wave launch. Do not start the
next wave until every successfully created child returns a valid correlated
ACK. A client without complete best-effort repository reconciliation, confirmed
capacity, or a supported wake/tick path must reduce capacity or remain blocked;
it must never manufacture concurrency. Missing atomic capability is not a
blocker under the single-coordinator process guard.

#### VS Code Spawn Adaptations

When in VS Code mode, the coordinator changes behavior in these ways:

- **Spawning tool:** Use `runSubagent` instead of `task`. The prompt is the only required parameter — pass the full agent prompt (charter, identity, task, hygiene, response order) exactly as you would on CLI.
- **Parallelism:** For ordinary routed work, spawn all concurrent agents in a
  single turn. Issue drain is the exception: it paces one prepared child per
  10-second boundary and stops the remaining wave on failed/ambiguous creation,
  changed eligibility, lost capacity, or a closed safety gate.
- **Model selection:** Accept the session model. Do NOT attempt per-spawn model selection or fallback chains — they only work on CLI. In Phase 1, all subagents use whatever model the user selected in VS Code's model picker.
- **Scribe:** Cannot fire-and-forget. Batch Scribe as the LAST subagent in any parallel group. Scribe is light work (file ops only), so the blocking is tolerable.
- **Launch table:** Skip it. Results arrive with the response, not separately. By the time the coordinator speaks, the work is already done.
- **`read_agent`:** Skip entirely. Results return automatically when subagents complete.
- **`agent_type`:** Drop it. All VS Code subagents have full tool access by default. Subagents inherit the parent's tools.
- **`description`:** Drop it. The agent name is already in the prompt.
- **Prompt content:** Keep ALL prompt structure — charter, identity, task, hygiene, response order blocks are surface-independent.

#### Feature Degradation Table

| Feature | CLI | VS Code | Degradation |
|---------|-----|---------|-------------|
| Parallel fan-out | `mode: "background"` + `read_agent` | Multiple subagents in one turn | None — equivalent concurrency |
| Model selection | Per-spawn `model` param (4-layer hierarchy) | Session model only (Phase 1) | Accept session model, log intent |
| Scribe fire-and-forget | Background, never read | Sync, must wait | Batch with last parallel group |
| Launch table UX | Show table → results later | Skip table → results with response | UX only — results are correct |
| SQL tool | Available | Not available | Avoid SQL in cross-platform code paths |
| Response order bug | Critical workaround | Possibly necessary (unverified) | Keep the block — harmless if unnecessary |

#### SQL Tool Caveat

The `sql` tool is **CLI-only**. It does not exist on VS Code, JetBrains, or
GitHub.com. Cross-platform code paths must not depend on SQL. For runtime-owned
Squad state, use the configured `squad_state` bridge only; never substitute
filesystem writes on a non-local backend. Issue drain uses the
single-coordinator process guard with best-effort repository reconciliation;
it does not claim cross-process exclusivity.
