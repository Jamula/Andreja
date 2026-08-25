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

    def test_duplicate_adr_status_lines_are_rejected(self) -> None:
        charter = "candidate charter\n"
        with redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit):
                CHECKS.check_charter_hash_content(
                    charter,
                    "- **Status:** Proposed\n"
                    "- **Status:** Accepted\n"
                    f"- **Charter SHA-256:** `{'0' * 64}`\n",
                )


class CharterAtomicityTests(unittest.TestCase):
    PROPOSED_ADR = "- **Status:** Proposed\n"
    ACCEPTED_ADR = "- **Status:** Accepted\n"
    PROPOSED_CHARTER = (
        "- **Status:** Proposed for explicit ratification; not yet authoritative\n"
    )
    ACCEPTED_CHARTER = "- **Status:** Ratified and authoritative\n"
    REQUIRED_CHARTER_FIELD = """
body:
  - type: textarea
    id: evidence
    validations:
      required: false
  - type: textarea
    id: charter
    attributes:
      label: Charter impact
    validations:
      required: true
"""
    PROPOSED_PR_TEMPLATE = """
## Charter impact

<!-- charter-impact-state: proposed -->
<!--
This section is optional while ADR 0006 is Proposed and required after it is Accepted.
-->

## Gates

- [ ] Company charter impact recorded or not applicable (required after ADR 0006
      acceptance)
"""
    REQUIRED_PR_TEMPLATE = """
## Charter impact

<!-- charter-impact-state: required -->
<!-- This section is required. -->

## Gates

- [ ] Company charter impact recorded or not applicable (required)
"""
    HALF_ACCEPTED_PR_TEMPLATE = """
## Charter impact

<!-- charter-impact-state: required -->
<!-- This section is required. -->

## Gates

- [ ] Company charter impact recorded or not applicable (required after ADR 0006
      acceptance)
"""

    def assert_fails(
        self, charter: str, adr: str, form: str, pr_template: str
    ) -> None:
        with redirect_stdout(io.StringIO()):
            with self.assertRaises(SystemExit):
                CHECKS.check_charter_atomicity_content(
                    charter, adr, form, pr_template
                )

    def test_proposed_state_accepts_non_authoritative_charter(self) -> None:
        CHECKS.check_charter_atomicity_content(
            self.PROPOSED_CHARTER,
            self.PROPOSED_ADR,
            "body: []\n",
            self.PROPOSED_PR_TEMPLATE,
        )

    def test_proposed_state_rejects_authoritative_charter(self) -> None:
        self.assert_fails(
            self.ACCEPTED_CHARTER,
            self.PROPOSED_ADR,
            self.REQUIRED_CHARTER_FIELD,
            self.PROPOSED_PR_TEMPLATE,
        )

    def test_proposed_state_rejects_required_pr_template(self) -> None:
        self.assert_fails(
            self.PROPOSED_CHARTER,
            self.PROPOSED_ADR,
            "body: []\n",
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_proposed_state_rejects_required_charter_field(self) -> None:
        self.assert_fails(
            self.PROPOSED_CHARTER,
            self.PROPOSED_ADR,
            self.REQUIRED_CHARTER_FIELD,
            self.PROPOSED_PR_TEMPLATE,
        )

    def test_proposed_state_rejects_duplicate_adr_status_lines(self) -> None:
        self.assert_fails(
            self.PROPOSED_CHARTER,
            self.PROPOSED_ADR + self.ACCEPTED_ADR,
            "body: []\n",
            self.PROPOSED_PR_TEMPLATE,
        )

    def test_accepted_state_accepts_all_atomic_requirements(self) -> None:
        CHECKS.check_charter_atomicity_content(
            self.ACCEPTED_CHARTER,
            self.ACCEPTED_ADR,
            self.REQUIRED_CHARTER_FIELD,
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_accepted_state_rejects_proposed_charter(self) -> None:
        self.assert_fails(
            self.PROPOSED_CHARTER,
            self.ACCEPTED_ADR,
            self.REQUIRED_CHARTER_FIELD,
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_accepted_state_rejects_optional_charter_field(self) -> None:
        optional_field = """
body:
  - type: textarea
    id: charter
    attributes:
      label: Charter impact
    validations:
      required: false
"""
        self.assert_fails(
            self.ACCEPTED_CHARTER,
            self.ACCEPTED_ADR,
            optional_field,
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_other_required_field_does_not_satisfy_charter_gate(self) -> None:
        form = """
body:
  - type: textarea
    id: evidence
    validations:
      required: true
  - type: textarea
    id: charter
"""
        self.assert_fails(
            self.ACCEPTED_CHARTER,
            self.ACCEPTED_ADR,
            form,
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_nested_required_text_does_not_satisfy_charter_gate(self) -> None:
        form = """
body:
  - type: textarea
    id: charter
    attributes:
      validations:
        required: true
"""
        self.assert_fails(
            self.ACCEPTED_CHARTER,
            self.ACCEPTED_ADR,
            form,
            self.REQUIRED_PR_TEMPLATE,
        )

    def test_accepted_state_rejects_half_accepted_pr_template(self) -> None:
        self.assert_fails(
            self.ACCEPTED_CHARTER,
            self.ACCEPTED_ADR,
            self.REQUIRED_CHARTER_FIELD,
            self.HALF_ACCEPTED_PR_TEMPLATE,
        )


if __name__ == "__main__":
    unittest.main()
