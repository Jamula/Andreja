#!/usr/bin/env python3
from __future__ import annotations

import copy
import importlib.util
import unittest
from pathlib import Path

SCRIPT_PATH = Path(__file__).with_name("check_squad_child_session.py")
SPEC = importlib.util.spec_from_file_location("check_squad_child_session", SCRIPT_PATH)
assert SPEC and SPEC.loader
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)


def valid_contract() -> tuple[dict, dict, str, str, str]:
    squad_config = {"version": 1, "stateBackend": "two-layer"}
    mcp_config = {
        "mcpServers": {
            "squad_state": {
                "command": "npx",
                "args": list(CHECK.SQUAD_STATE_ARGS),
                "env": {},
                "tools": list(CHECK.SQUAD_STATE_TOOLS),
            }
        }
    }
    agent_template = (
        f"---\n---\n<!-- {CHECK.HEAD_CANARY} -->\n"
        f"Coordinator\n<!-- {CHECK.EOF_CANARY} -->\n"
    )
    copilot_instructions = f"Validate {CHECK.HEAD_CANARY} and {CHECK.EOF_CANARY}."
    gitignore = "\n".join(CHECK.RUNTIME_STATE_EXCLUSIONS)
    return (
        squad_config,
        mcp_config,
        agent_template,
        copilot_instructions,
        gitignore,
    )


class SquadChildSessionContractTests(unittest.TestCase):
    def test_complete_contract_passes(self) -> None:
        CHECK.validate_contract(*valid_contract())

    def test_local_backend_is_rejected(self) -> None:
        contract = list(valid_contract())
        contract[0]["stateBackend"] = "local"

        with self.assertRaisesRegex(ValueError, "retain the two-layer"):
            CHECK.validate_contract(*contract)

    def test_wildcard_tool_grant_is_rejected(self) -> None:
        contract = list(valid_contract())
        contract[1]["mcpServers"]["squad_state"]["tools"] = ["*"]

        with self.assertRaisesRegex(ValueError, "wildcard"):
            CHECK.validate_contract(*contract)

    def test_malformed_command_arguments_are_rejected(self) -> None:
        contract = list(valid_contract())
        contract[1]["mcpServers"]["squad_state"]["args"] = None

        with self.assertRaisesRegex(ValueError, "pinned Squad CLI"):
            CHECK.validate_contract(*contract)

    def test_missing_or_unexpected_tool_is_rejected(self) -> None:
        for tools in (
            list(CHECK.SQUAD_STATE_TOOLS[:-1]),
            [*CHECK.SQUAD_STATE_TOOLS, "future_unreviewed_tool"],
        ):
            with self.subTest(tools=tools):
                contract = list(valid_contract())
                contract[1] = copy.deepcopy(contract[1])
                contract[1]["mcpServers"]["squad_state"]["tools"] = tools
                with self.assertRaisesRegex(ValueError, "allowlist drifted"):
                    CHECK.validate_contract(*contract)

    def test_truncated_agent_template_is_rejected(self) -> None:
        contract = list(valid_contract())
        contract[2] = contract[2].replace(
            f"<!-- {CHECK.EOF_CANARY} -->\n",
            "",
        )

        with self.assertRaisesRegex(ValueError, "EOF canary"):
            CHECK.validate_contract(*contract)

    def test_missing_canary_instruction_is_rejected(self) -> None:
        contract = list(valid_contract())
        contract[3] = contract[3].replace(CHECK.HEAD_CANARY, "missing")

        with self.assertRaisesRegex(ValueError, CHECK.HEAD_CANARY):
            CHECK.validate_contract(*contract)

    def test_unexcluded_runtime_state_is_rejected(self) -> None:
        contract = list(valid_contract())
        contract[4] = contract[4].replace(".squad/memory/", "")

        with self.assertRaisesRegex(ValueError, "not fully excluded"):
            CHECK.validate_contract(*contract)


if __name__ == "__main__":
    unittest.main()
