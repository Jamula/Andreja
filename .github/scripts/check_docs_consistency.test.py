#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import importlib.util
import unittest
from pathlib import Path

SCRIPT_PATH = Path(__file__).with_name("check_docs_consistency.py")
SPEC = importlib.util.spec_from_file_location("check_docs_consistency", SCRIPT_PATH)
assert SPEC and SPEC.loader
DOCS_CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DOCS_CHECK)


def plan_table(artifacts: dict[str, str]) -> str:
    rows = [
        "#### Phase 0 policy and governance artifact classification",
        "",
        "| Artifact | Source and current SHA-256 | Current authority |",
        "|---|---|---|",
    ]
    for path, digest in artifacts.items():
        if path in DOCS_CHECK.CANONICAL_BASELINE_REQUIREMENTS:
            challenger = DOCS_CHECK.CANONICAL_BASELINE_REQUIREMENTS[path]
            rows.append(
                f"| [`{path}`]({path.removeprefix('docs/')}) | "
                f"Issue [#116](issue), PR [#117](pr); `{digest}` | "
                "**Canonical descriptive baseline; not ratified.** "
                f"{challenger} challenge and Cyrus residual-risk acceptance remain pending. |"
            )
        else:
            rows.append(
                f"| [`{path}`]({path.removeprefix('docs/')}) | "
                f"`{digest}` | Draft |"
            )
    return "\n".join(rows)


class StatusArtifactHashTests(unittest.TestCase):
    def setUp(self) -> None:
        self.expected = set(DOCS_CHECK.EXPECTED_STATUS_ARTIFACTS)
        self.actual = {
            path: hashlib.sha256(path.encode("utf-8")).hexdigest()
            for path in self.expected
        }

    def digest(self, path: str) -> str:
        return hashlib.sha256(path.encode("utf-8")).hexdigest()

    def test_complete_current_inventory_passes(self) -> None:
        plan = plan_table(self.actual)
        parsed = DOCS_CHECK.extract_status_artifact_hashes(plan)
        DOCS_CHECK.validate_status_artifact_hashes(parsed, self.expected, self.digest)
        DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_requires_issue_and_pr(self) -> None:
        plan = plan_table(self.actual).replace("PR [#117](pr)", "PR pending", 1)
        with self.assertRaisesRegex(ValueError, "authority drifted"):
            DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_requires_not_ratified_authority(self) -> None:
        plan = plan_table(self.actual).replace(
            "Canonical descriptive baseline; not ratified",
            "Canonical descriptive baseline",
            1,
        )
        with self.assertRaisesRegex(ValueError, "authority drifted"):
            DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_requires_challenge_and_residual_acceptance(self) -> None:
        plan = plan_table(self.actual).replace(
            "Deanna Troi challenge and Cyrus residual-risk acceptance",
            "Review complete",
            1,
        )
        with self.assertRaisesRegex(ValueError, "authority drifted"):
            DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_missing_artifact_is_rejected(self) -> None:
        artifacts = dict(self.actual)
        artifacts.pop(next(iter(artifacts)))
        with self.assertRaisesRegex(ValueError, "inventory drifted"):
            DOCS_CHECK.validate_status_artifact_hashes(
                artifacts, self.expected, self.digest
            )

    def test_stale_hash_is_rejected(self) -> None:
        artifacts = dict(self.actual)
        path = next(iter(artifacts))
        artifacts[path] = "0" * 64
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            DOCS_CHECK.validate_status_artifact_hashes(
                artifacts, self.expected, self.digest
            )

    def test_malformed_hash_is_rejected(self) -> None:
        malformed = plan_table(self.actual).replace(
            next(iter(self.actual.values())), "not-a-sha256", 1
        )
        with self.assertRaisesRegex(ValueError, "Malformed status-artifact row"):
            DOCS_CHECK.extract_status_artifact_hashes(malformed)


if __name__ == "__main__":
    unittest.main()
