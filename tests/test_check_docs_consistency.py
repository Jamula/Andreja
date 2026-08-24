from __future__ import annotations

import hashlib
import importlib.util
import io
import unittest
from contextlib import redirect_stdout
from pathlib import Path


SCRIPT_PATH = (
    Path(__file__).resolve().parents[1]
    / ".github"
    / "scripts"
    / "check_docs_consistency.py"
)
SPEC = importlib.util.spec_from_file_location("check_docs_consistency", SCRIPT_PATH)
assert SPEC and SPEC.loader
CHECKS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKS)


class CharterHashTests(unittest.TestCase):
    def test_proposed_charter_does_not_enforce_candidate_hash(self) -> None:
        CHECKS.check_charter_hash_content(
            "candidate charter\n",
            "- **Status:** Proposed\n"
            "- **Charter SHA-256:** `CANDIDATE_HASH_PENDING_FINAL_REVIEW`\n",
        )

    def test_accepted_charter_accepts_current_hash(self) -> None:
        charter = "ratified charter\n"
        digest = hashlib.sha256(charter.encode("utf-8")).hexdigest()

        CHECKS.check_charter_hash_content(
            charter,
            f"- **Status:** Accepted\n- **Charter SHA-256:** `{digest}`\n",
        )

    def test_accepted_charter_rejects_stale_hash(self) -> None:
        with redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit):
                CHECKS.check_charter_hash_content(
                    "changed charter\n",
                    f"- **Status:** Accepted\n- **Charter SHA-256:** `{'0' * 64}`\n",
                )


if __name__ == "__main__":
    unittest.main()
