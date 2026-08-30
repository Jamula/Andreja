#!/usr/bin/env python3
"""Validate the repository contract required by fresh Squad child sessions."""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
HEAD_CANARY = "SQUAD_COORDINATOR_CANARY_HEAD_b7d2"
EOF_CANARY = "SQUAD_COORDINATOR_CANARY_a8f3"
SQUAD_STATE_ARGS = (
    "-y",
    "@bradygaster/squad-cli@0.12.0",
    "state-mcp",
)
SQUAD_STATE_TOOLS = (
    "squad_decide",
    "squad_state_read",
    "squad_state_write",
    "squad_state_append",
    "squad_state_delete",
    "squad_state_list",
    "squad_state_health",
    "memory.classify",
    "memory.write",
    "memory.search",
    "memory.promote",
    "memory.delete",
    "memory.audit",
)
RUNTIME_STATE_EXCLUSIONS = (
    ".squad/orchestration-log/",
    ".squad/log/",
    ".squad/decisions/inbox/",
    ".squad/sessions/",
    ".squad/decisions.md",
    ".squad/agents/*/history.md",
    ".squad/casting/history.json",
    ".squad/identity/",
    ".squad/memory/",
    ".squad/rai/audit-trail.md",
    ".squad/fact-checker/audit-trail.md",
)


def parse_object(document: str, name: str) -> dict[str, Any]:
    try:
        value = json.loads(document)
    except json.JSONDecodeError as error:
        raise ValueError(f"{name} is not valid JSON: {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{name} must contain a JSON object.")
    return value


def validate_contract(
    squad_config: dict[str, Any],
    mcp_config: dict[str, Any],
    agent_template: str,
    copilot_instructions: str,
    gitignore: str,
) -> None:
    if squad_config.get("stateBackend") != "two-layer":
        raise ValueError(".squad/config.json must retain the two-layer state backend.")

    servers = mcp_config.get("mcpServers")
    server = servers.get("squad_state") if isinstance(servers, dict) else None
    if not isinstance(server, dict):
        raise ValueError(".mcp.json must declare the squad_state MCP server.")
    args = server.get("args")
    if (
        server.get("command") != "npx"
        or not isinstance(args, list)
        or tuple(args) != SQUAD_STATE_ARGS
    ):
        raise ValueError("squad_state must use the pinned Squad CLI state-mcp command.")
    env = server.get("env", {})
    if not isinstance(env, dict) or env:
        raise ValueError("squad_state must not receive environment credentials.")

    tools = server.get("tools")
    if not isinstance(tools, list) or not all(
        isinstance(tool, str) for tool in tools
    ):
        raise ValueError("squad_state tools must be an explicit allowlist.")
    if "*" in tools:
        raise ValueError("squad_state must not grant wildcard MCP capabilities.")
    if len(tools) != len(set(tools)):
        raise ValueError("squad_state tools must not contain duplicate selectors.")
    missing = sorted(set(SQUAD_STATE_TOOLS) - set(tools))
    unexpected = sorted(set(tools) - set(SQUAD_STATE_TOOLS))
    if missing or unexpected:
        raise ValueError(
            "squad_state tool allowlist drifted.\n"
            f"  Missing:    {missing}\n"
            f"  Unexpected: {unexpected}"
        )

    if agent_template.count(HEAD_CANARY) != 1:
        raise ValueError("Squad agent template must contain one HEAD canary.")
    if agent_template.count(EOF_CANARY) != 1 or not agent_template.rstrip().endswith(
        f"<!-- {EOF_CANARY} -->"
    ):
        raise ValueError("Squad agent template must end with one EOF canary.")
    for canary in (HEAD_CANARY, EOF_CANARY):
        if canary not in copilot_instructions:
            raise ValueError(f"Copilot instructions must validate {canary}.")

    ignore_lines = {
        line.strip()
        for line in gitignore.splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    }
    missing_exclusions = sorted(set(RUNTIME_STATE_EXCLUSIONS) - ignore_lines)
    if missing_exclusions:
        raise ValueError(
            "Runtime-owned Squad state is not fully excluded from commits.\n"
            f"  Missing: {missing_exclusions}"
        )


def check_repository(root: Path = REPO_ROOT) -> None:
    validate_contract(
        parse_object(
            (root / ".squad" / "config.json").read_text(encoding="utf-8"),
            ".squad/config.json",
        ),
        parse_object(
            (root / ".mcp.json").read_text(encoding="utf-8"),
            ".mcp.json",
        ),
        (root / ".squad" / "templates" / "squad.agent.md.template").read_text(
            encoding="utf-8"
        ),
        (root / ".github" / "copilot-instructions.md").read_text(encoding="utf-8"),
        (root / ".gitignore").read_text(encoding="utf-8"),
    )


def main() -> None:
    try:
        check_repository()
    except (OSError, ValueError) as error:
        print(f"::error::{error}")
        sys.exit(1)
    print(
        "OK: Squad child-session bridge, canaries, and two-layer ownership "
        "contract are consistent."
    )


if __name__ == "__main__":
    main()
