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
    rows.extend(
        f"| [`{path}`]({path.removeprefix('docs/')}) | `{digest}` | Draft |"
        for path, digest in artifacts.items()
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
        parsed = DOCS_CHECK.extract_status_artifact_hashes(plan_table(self.actual))
        DOCS_CHECK.validate_status_artifact_hashes(parsed, self.expected, self.digest)

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
