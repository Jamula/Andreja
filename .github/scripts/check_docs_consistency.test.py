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
            challengers = DOCS_CHECK.CANONICAL_BASELINE_REQUIREMENTS[path]
            challenge_text = ", ".join(challengers[:-1])
            rows.append(
                f"| [`{path}`]({path.removeprefix('docs/')}) | "
                f"Issue [#116](issue), PR [#117](pr); `{digest}` | "
                "**Canonical descriptive baseline; not ratified.** "
                f"{challenge_text}, {challengers[-1]}. "
                "Cyrus residual-risk acceptance remains pending. "
                f"{DOCS_CHECK.CANONICAL_OPEN_ASSESSMENT} |"
            )
        else:
            rows.append(
                f"| [`{path}`]({path.removeprefix('docs/')}) | "
                f"`{digest}` | Draft |"
            )
    return "\n".join(rows)


def canonical_documents() -> dict[str, str]:
    return {
        path: "\n".join(
            (
                "# Baseline",
                "",
                "- **Status:** Canonical descriptive baseline; not ratified",
                f"- **Required challenge:** {', '.join(challengers)}",
                "- **Residual-risk acceptance:** Cyrus; pending",
                f"- {DOCS_CHECK.CANONICAL_DOCUMENT_OPEN_ASSESSMENTS[path]}",
                "",
                "Baseline body.",
            )
        )
        for path, challengers in DOCS_CHECK.CANONICAL_BASELINE_REQUIREMENTS.items()
    }


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
        DOCS_CHECK.validate_canonical_baseline_documents(canonical_documents())

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
        for requirement in (
            "Deanna Troi",
            "Tuvok",
            "Rai",
            "Cyrus",
            "residual-risk acceptance",
        ):
            with self.subTest(requirement=requirement):
                plan = plan_table(self.actual).replace(requirement, "omitted", 1)
                with self.assertRaisesRegex(ValueError, "authority drifted"):
                    DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_rejects_granted_residual_risk_acceptance(self) -> None:
        plan = plan_table(self.actual).replace(
            "Cyrus residual-risk acceptance remains pending",
            "Cyrus residual-risk acceptance is granted",
            1,
        )
        with self.assertRaisesRegex(ValueError, "authority drifted"):
            DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_requires_open_classification_assessment(self) -> None:
        for replacement in (
            "The classification/impact assessment is complete.",
            "",
        ):
            with self.subTest(replacement=replacement or "omitted"):
                plan = plan_table(self.actual).replace(
                    DOCS_CHECK.CANONICAL_OPEN_ASSESSMENT,
                    replacement,
                    1,
                )
                with self.assertRaisesRegex(ValueError, "authority drifted"):
                    DOCS_CHECK.validate_canonical_baseline_rows(plan)

    def test_canonical_baseline_header_requires_open_assessment(self) -> None:
        for path, required in DOCS_CHECK.CANONICAL_DOCUMENT_OPEN_ASSESSMENTS.items():
            for replacement in (
                "**Classification/impact assessment:** Completed",
                "",
            ):
                with self.subTest(path=path, replacement=replacement or "omitted"):
                    documents = canonical_documents()
                    if replacement:
                        documents[path] = documents[path].replace(
                            required,
                            replacement,
                            1,
                        )
                    else:
                        documents[path] = documents[path].replace(
                            f"- {required}\n",
                            "",
                            1,
                        )
                    with self.assertRaisesRegex(ValueError, "header drifted"):
                        DOCS_CHECK.validate_canonical_baseline_documents(documents)

    def test_canonical_baseline_header_requires_status_challenge_and_acceptance(
        self,
    ) -> None:
        for path, challengers in DOCS_CHECK.CANONICAL_BASELINE_REQUIREMENTS.items():
            required_parts = (
                "**Status:** Canonical descriptive baseline; not ratified",
                "**Required challenge:**",
                *challengers,
                "**Residual-risk acceptance:** Cyrus; pending",
            )
            for requirement in required_parts:
                with self.subTest(path=path, requirement=requirement):
                    documents = canonical_documents()
                    documents[path] = documents[path].replace(
                        requirement,
                        "omitted",
                        1,
                    )
                    with self.assertRaises(ValueError):
                        DOCS_CHECK.validate_canonical_baseline_documents(documents)

    def test_canonical_baseline_metadata_block_boundaries(self) -> None:
        path = "docs/privacy.md"
        required = DOCS_CHECK.CANONICAL_DOCUMENT_OPEN_ASSESSMENTS[path]

        documents = canonical_documents()
        documents[path] = documents[path].replace(
            "- **Residual-risk acceptance:** Cyrus; pending",
            "- **Additional context:** Legitimate metadata\n"
            "- **Residual-risk acceptance:** Cyrus; pending",
            1,
        )
        DOCS_CHECK.validate_canonical_baseline_documents(documents)

        for placement in ("missing", "outside"):
            with self.subTest(placement=placement):
                documents = canonical_documents()
                documents[path] = documents[path].replace(f"- {required}\n", "", 1)
                if placement == "outside":
                    documents[path] += f"\n- {required}\n"
                with self.assertRaisesRegex(ValueError, "header drifted"):
                    DOCS_CHECK.validate_canonical_baseline_documents(documents)

        for malformed in (
            "# Baseline\n\n\nBaseline body.",
            canonical_documents()[path].replace(
                "- **Residual-risk acceptance:** Cyrus; pending",
                "not metadata",
                1,
            ),
            canonical_documents()[path].replace("\n\nBaseline body.", ""),
        ):
            with self.subTest(malformed=malformed[-30:]):
                documents = canonical_documents()
                documents[path] = malformed
                with self.assertRaisesRegex(ValueError, "metadata block"):
                    DOCS_CHECK.validate_canonical_baseline_documents(documents)

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
