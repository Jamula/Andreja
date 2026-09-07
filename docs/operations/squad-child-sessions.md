# Squad child-session preflight

Andreja keeps Squad's `two-layer` state backend. A fresh child session started
with `agent: Squad` must load both the complete project agent payload and the
repository-root `.mcp.json` before it performs any mutation.

## Ownership boundary

- Static configuration, including `.squad/config.json`, `team.md`, `routing.md`,
  charters, templates, and skills, remains in the worktree.
- Mutable decisions, histories, identity, memory, and session or orchestration
  logs are owned by the runtime state bridge. Agents use `squad_state_*`,
  `squad_decide`, or the governed `memory.*` aliases; they do not edit those
  paths directly.
- Private task/profile content, prompts, credentials, and connector payloads
  remain excluded from committed Squad state and issue content.

The bridge is pinned in `.mcp.json` and grants only the 13 tools exported by
`@bradygaster/squad-cli@0.12.0 state-mcp`. Prefix patterns and `"*"` are not
permitted because future bridge tools must not become available without review.

## Required preflight

Run these checks once in every fresh Squad child session, before changing files,
Git state, issues, or runtime state:

1. Confirm the loaded agent instructions contain both
   `SQUAD_COORDINATOR_CANARY_HEAD_b7d2` and
   `SQUAD_COORDINATOR_CANARY_a8f3`. The EOF canary proves that a payload with a
   visible HEAD canary was not truncated.
2. Discover `squad_state_health` even when MCP tools are loaded lazily, then call
   it. Continue only when it reports the configured runtime-owned backend.
3. For a no-op task, finish after the health check; no state write is required.

MCP configuration is read when the session starts. Restart the child after
changing `.mcp.json`; an already-running session cannot validate a newly changed
configuration.

## Failure and recovery

Fail closed before mutation when either canary is incomplete, the bridge is not
discoverable, or `squad_state_health` fails. Do not downgrade `stateBackend` to
`local`, edit runtime-owned paths, or broaden the MCP allowlist.

After confirming the worktree is still clean, restart a Squad child so it loads
the project configuration. If the host still does not expose the project agent
or repository MCP server, record which preflight failed and use the documented
standard-agent fallback on that same clean worktree. The fallback does not gain
permission to mutate two-layer state directly.

## Repository validation

Run:

```sh
python3 .github/scripts/check_squad_child_session.test.py
python3 .github/scripts/check_squad_child_session.py
```

The check locks the backend, exact MCP command and allowlist, agent-template
canaries, Copilot canary instructions, and runtime-state exclusions together.
An end-to-end child-session check still requires a fresh host session: launch a
no-op task with `agent: Squad`, observe a successful `squad_state_health`, and
confirm that it completes without fallback or repository changes.
