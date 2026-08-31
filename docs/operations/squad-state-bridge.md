# Squad two-layer state bridge

This runbook verifies the repository half of the Squad state contract and the
runtime bridge loaded by an app-created child session. The static check is
necessary but cannot prove that a running app session loaded MCP tools.

## Static contract

From the repository root, run:

```text
node .github/scripts/validate-squad-state-bridge.js
node --test .github/scripts/validate-squad-state-bridge.test.js
node --test .github/scripts/squad-state-protected-manifest.test.js
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
GitHub Actions validates the repository static contract and the manifest
utility's isolated tests only; it cannot create app child sessions or prove
runtime bridge behavior.

## Fresh-child acceptance

Start a fresh app child session with `agent: Squad` in a clean worktree. Record
the app version and child-session ID, then require this evidence:

1. Before starting the state probes, capture the protected worktree state to a
   new, explicitly supplied absolute path outside the repository:
   ```text
   node .github/scripts/squad-state-protected-manifest.js capture --baseline "<absolute-external-temp-path>"
   git status --short
   ```
   Keep the baseline private and delete it after acceptance. The script records
   only protected path identities and `lstat` metadata, never file contents. It
   refuses an in-repository or existing baseline and fails closed on unreadable,
   symbolic-link/reparse-point, or non-regular protected paths.
2. The loaded coordinator payload reports `Squad v0.12.0` and observes both
   canaries.
3. `squad_state_health` returns `StateBackendStorageAdapter`.
4. Static `team.md`, `routing.md`, and the assigned charter are read from disk.
   Mutable `decisions.md` is read only through `squad_state_read`.
5. With a unique, non-sensitive `log/state-bridge-probe-<session-id>.md` key,
   use `squad_state_write`, read back the exact value, delete it with
   `squad_state_delete`, and confirm a final read reports key-not-found.
6. Attempt a non-sensitive write under disallowed `verification/`. The adapter
   must reject it. Do not use file operations or another namespace to retry.
7. After the probes, compare the same baseline and retain the Git check:
   ```text
   node .github/scripts/squad-state-protected-manifest.js compare --baseline "<absolute-external-temp-path>"
   git status --short
   ```
   Success prints only `UNCHANGED`. A changed worktree returns nonzero and
   prints only `CHANGED <protected-path-identity>` lines; it never prints
   contents or metadata values. `git status --short` must also be unchanged.
8. In a second fresh Squad child, request: `Run required Squad preflight, make
   no repository or mutable-state changes, dispatch no agents, and report no-op
   completion.` It must complete without a standard-agent fallback. Compare the
   same baseline again and run `git status --short`; both checks must remain
   unchanged.

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
| Protected-state manifest capture or comparison errors, or reports `CHANGED` | Stop and treat acceptance as failed. Do not inspect protected file contents to diagnose it; retain only the reported path identities and clean up the external baseline after recording the result. |
| `squad_state_health` is missing or errors | Stop before repository or mutable-state mutation and end the child. Restart the app/session so project `.mcp.json` is loaded, then rerun fresh-child acceptance. For ordinary task recovery, a standard/general-purpose fallback may proceed only after that Squad-first child stops cleanly; fallback work is recovery only and cannot count as successful Squad bridge acceptance. Never fall back to raw state files. |
| Probe write outcome is uncertain | Read the exact unique probe key through `squad_state_read`. If present and exact, delete that key through `squad_state_delete`; then verify key-not-found. Never overwrite unknown content. |
| Disallowed `verification/` write succeeds | Stop all probes and escalate as a state-boundary failure. Do not broaden access or invent success. |
| No-op child mutates or falls back | Treat acceptance as failed, preserve the clean baseline evidence, and investigate the app/session bridge before retrying in another fresh child. |

Only governed state tools may recover probe state. Do not edit runtime-owned
`.squad` paths, use git-notes choreography, or expose provider/private state.
