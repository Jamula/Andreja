# Squad two-layer state bridge

This runbook verifies the repository half of the Squad state contract and the
runtime bridge loaded by an app-created child session. The static check is
necessary but cannot prove that a running app session loaded MCP tools.

## Static contract

From the repository root, run:

```text
node .github/scripts/validate-squad-state-bridge.js
node --test .github/scripts/validate-squad-state-bridge.test.js
```

The check fails closed unless all of these remain true:

- `.squad/config.json` selects `two-layer`.
- `.mcp.json` declares only `squad_state`, backed by
  `@bradygaster/squad-cli@0.12.0 state-mcp`.
- the checked-in coordinator has its ordered HEAD/EOF canaries and matching
  version stamp;
- coordinator ownership rules keep static team configuration on disk and
  mutable state behind governed tools; and
- runtime-owned state is effectively ignored, every tracked repository
  `.gitignore` contains no active negation rules, and runtime state is absent
  from Git tracking. Local validation also checks untracked, non-excluded
  `.gitignore` files. This repository-wide fail-closed policy avoids
  interpreting gitignore glob syntax: exceptions must be expressed without `!`
  rules.

The check does not call MCP, inspect private state, or claim runtime success.
GitHub Actions validates only this repository static contract; it cannot create
app child sessions.

## Fresh-child acceptance

Start a fresh app child session with `agent: Squad` in a clean worktree. Record
the app version and child-session ID, then require this evidence:

1. The loaded coordinator payload reports `Squad v0.12.0` and observes both
   canaries.
2. `squad_state_health` returns `StateBackendStorageAdapter`.
3. Static `team.md`, `routing.md`, and the assigned charter are read from disk.
   Mutable `decisions.md` is read only through `squad_state_read`.
4. With a unique, non-sensitive `log/state-bridge-probe-<session-id>.md` key,
   use `squad_state_write`, read back the exact value, delete it with
   `squad_state_delete`, and confirm a final read reports key-not-found.
5. Attempt a non-sensitive write under disallowed `verification/`. The adapter
   must reject it. Do not use file operations or another namespace to retry.
6. Confirm `git status --short` is unchanged from before the probes.
7. In a second fresh Squad child, request: `Run required Squad preflight, make
   no repository or mutable-state changes, dispatch no agents, and report no-op
   completion.` It must complete without a standard-agent fallback, and the
   worktree must remain clean.

Parent and child sessions follow the same boundary: read static configuration
from the worktree; use `squad_state_*` or governed memory tools for mutable
state. Never enumerate or expose another agent's private history as evidence.

Before closing the linked issue or PR, attach the fresh-child results (including
session IDs, tested commit, probe outcomes, and clean-status evidence) to its
acceptance record. Runtime evidence must accompany closure; do not represent
the static GitHub Actions job as an automated runtime gate.

## Failure and recovery

| Failure | Required response |
| --- | --- |
| Static validator fails | Stop. Review the reported tracked-file drift; do not change `stateBackend` or add MCP servers to make the check pass. |
| `squad_state_health` is missing or errors | Stop before state mutation. End the child, restart the app/session so project `.mcp.json` is loaded, and rerun fresh-child acceptance. Do not fall back to raw files or a standard agent. |
| Probe write outcome is uncertain | Read the exact unique probe key through `squad_state_read`. If present and exact, delete that key through `squad_state_delete`; then verify key-not-found. Never overwrite unknown content. |
| Disallowed `verification/` write succeeds | Stop all probes and escalate as a state-boundary failure. Do not broaden access or invent success. |
| No-op child mutates or falls back | Treat acceptance as failed, preserve the clean baseline evidence, and investigate the app/session bridge before retrying in another fresh child. |

Only governed state tools may recover probe state. Do not edit runtime-owned
`.squad` paths, use git-notes choreography, or expose provider/private state.
