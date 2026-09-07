#!/usr/bin/env python3
"""Documentation CI check for catalog/hash drift.

Fails the build when:
  1. The current or current-proposed plan SHA-256 content hash recorded in
     docs/adr/0000-plan-ratification.md no longer matches docs/plan.md. An
     accepted hash remains separate while a proposed amendment awaits approval.
  2. ADR 0006 and the charter/template ratification state are inconsistent, or
     an Accepted ADR's charter SHA-256 no longer matches docs/charter.md. A
     Proposed ADR does not enforce its candidate hash.
  3. The seed skill names in docs/plan.md's "Initial first-party skill
     catalog" table drift from the authoritative
     docs/roadmap/first-party-skills.md catalog.
  4. The seed connector categories in docs/plan.md's "Connector catalog and
     release bands" table drift from the authoritative
     docs/roadmap/channel-connectors.md catalog.
  5. A status-artifact row in docs/plan.md is missing, unexpected, malformed,
     or has a SHA-256 value that does not match the referenced file.
  6. The feedback framework permits recoverable tracking-secret persistence or
     omits required raw-secret exfiltration boundaries.

Per docs/plan.md and docs/frameworks/prioritization-launch.md, the roadmap
catalogs are authoritative and the plan's seed tables must stay in sync.
"""
from __future__ import annotations

import hashlib
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PLAN_PATH = REPO_ROOT / "docs" / "plan.md"
ADR_PATH = REPO_ROOT / "docs" / "adr" / "0000-plan-ratification.md"
CHARTER_PATH = REPO_ROOT / "docs" / "charter.md"
CHARTER_ADR_PATH = REPO_ROOT / "docs" / "adr" / "0006-charter-ratification.md"
README_PATH = REPO_ROOT / "README.md"
DECISION_TEMPLATE_PATH = REPO_ROOT / ".github" / "ISSUE_TEMPLATE" / "decision.yml"
PR_TEMPLATE_PATH = REPO_ROOT / ".github" / "pull_request_template.md"
SKILLS_PATH = REPO_ROOT / "docs" / "roadmap" / "first-party-skills.md"
CONNECTORS_PATH = REPO_ROOT / "docs" / "roadmap" / "channel-connectors.md"
FEEDBACK_SUPPORT_PATH = REPO_ROOT / "docs" / "frameworks" / "feedback-support.md"
EXPECTED_STATUS_ARTIFACTS = {
    "docs/operating-model.md",
    "docs/cost-model.md",
    "docs/privacy.md",
    "docs/threat-model.md",
    "docs/frameworks/feedback-support.md",
    "docs/frameworks/prioritization-launch.md",
    "docs/charter.md",
    "docs/legal/license-evaluation.md",
    "docs/legal/regulatory-applicability.md",
}
CANONICAL_BASELINE_REQUIREMENTS = {
    "docs/privacy.md": ("Deanna Troi", "Tuvok", "Rai (AI safety); pending"),
    "docs/threat-model.md": ("Tuvok", "Deanna Troi", "Rai (AI safety); pending"),
}
CANONICAL_OPEN_ASSESSMENT = (
    "The classification/impact assessment remains open unless explicitly approved "
    "with cited evidence."
)
CANONICAL_DOCUMENT_OPEN_ASSESSMENTS = {
    "docs/privacy.md": (
        "Open; this inventory does not satisfy "
        "that gate without an explicitly approved assessment and cited evidence"
    ),
    "docs/threat-model.md": (
        "Open; this model does not satisfy "
        "that gate without an explicitly approved assessment and cited evidence"
    ),
}
CANONICAL_ISSUE_SOURCE = (
    "Issue [#116](https://github.com/Jamula/Andreja/issues/116)"
)
CANONICAL_PR_SOURCE = "PR [#117](https://github.com/Jamula/Andreja/pull/117)"


def fail(message: str) -> None:
    print(f"::error::{message}")
    sys.exit(1)


def read(path: Path) -> str:
    if not path.exists():
        fail(f"Required file not found: {path.relative_to(REPO_ROOT)}")
    return path.read_text(encoding="utf-8")


def read_git_lf_bytes(path: Path) -> bytes:
    """Return text bytes in the LF form stored and hashed by Git."""
    if not path.exists():
        fail(f"Required file not found: {path.relative_to(REPO_ROOT)}")
    return path.read_bytes().replace(b"\r\n", b"\n")


def parse_plan_hash_metadata(adr_text: str) -> tuple[str, str]:
    metadata = adr_text.split("## Decision", 1)[0]
    patterns = {
        "proposed": r"\*\*Current proposed Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`",
        "current": r"\*\*Current Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`",
        "legacy": r"\*\*Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`",
    }
    matches = {
        state: re.search(pattern, metadata)
        for state, pattern in patterns.items()
    }
    present = [(state, match) for state, match in matches.items() if match]
    if len(present) != 1:
        raise ValueError(
            "ADR 0000 must contain exactly one current, current-proposed, "
            "or legacy plan SHA-256 metadata line."
        )

    state, match = present[0]
    assert match is not None
    amendment_sections = re.split(r"(?m)^### ", adr_text)[1:]
    hashed_sections = [
        (
            section,
            hash_match.group(1).lower(),
            approver_match.group(1) if approver_match else "",
            classification_match.group(1) if classification_match else "",
        )
        for section in amendment_sections
        if (hash_match := re.search(
            r"(?m)^- \*\*Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`",
            section,
        ))
        for approver_match in [
            re.search(r"(?m)^- \*\*Approver:\*\*\s*(.+)$", section)
        ]
        for classification_match in [
            re.search(r"(?m)^- \*\*Classification:\*\*\s*(.+)$", section)
        ]
    ]
    if not hashed_sections:
        raise ValueError("ADR 0000 has no hashed amendment record.")

    classified_sections = []
    for section, digest, approver, classification in hashed_sections:
        if not approver or not classification:
            raise ValueError(
                "Every hashed ADR 0000 amendment requires Approver and "
                "Classification fields."
            )
        is_proposed = classification.lower().startswith("proposed")
        has_pending = "pending" in approver.lower()
        has_pending_grammar = re.fullmatch(
            r".+;\s*\*\*pending\*\*\s*",
            approver,
            flags=re.IGNORECASE,
        )
        if is_proposed and not has_pending_grammar:
            raise ValueError(
                "A Proposed amendment Approver must use '<name>; **pending**'."
            )
        if not is_proposed and has_pending:
            raise ValueError(
                "A pending Approver requires a Classification beginning "
                "with 'Proposed'."
            )
        classified_sections.append(
            (section, digest, approver, classification, is_proposed)
        )

    if state == "proposed":
        accepted_match = re.search(
            r"\*\*Accepted Plan SHA-256:\*\*\s*`([0-9a-fA-F]{64})`",
            metadata,
        )
        if not accepted_match:
            raise ValueError(
                "A current-proposed plan hash requires a separate accepted hash."
            )
        proposed_sections = [
            record for record in classified_sections if record[4]
        ]
        if not proposed_sections:
            raise ValueError(
                "The current-proposed plan hash must be backed by the latest "
                "pending, Proposed amendment with the same hash."
            )
        _, proposed_hash, _, _, _ = proposed_sections[-1]
        if proposed_hash != match.group(1).lower():
            raise ValueError(
                "The current-proposed plan hash must be backed by the latest "
                "pending, Proposed amendment with the same hash."
            )
        accepted_sections = [
            (section, digest)
            for section, digest, _, _, is_proposed in classified_sections
            if not is_proposed
        ]
        if (
            not accepted_sections
            or accepted_sections[-1][1] != accepted_match.group(1).lower()
        ):
            raise ValueError(
                "The accepted plan hash must match the latest accepted "
                "amendment."
            )
    else:
        accepted_sections = [
            record for record in classified_sections if not record[4]
        ]
        if not accepted_sections or accepted_sections[-1][1] != match.group(1).lower():
            raise ValueError(
                "The current/legacy plan hash must match the latest explicitly "
                "approved amendment."
            )
    return match.group(1).lower(), state


def check_plan_hash() -> None:
    plan_text = read(PLAN_PATH)
    actual_hash = hashlib.sha256(plan_text.encode("utf-8")).hexdigest()

    adr_text = read(ADR_PATH)
    try:
        recorded_hash, hash_state = parse_plan_hash_metadata(adr_text)
    except ValueError as exc:
        fail(str(exc))

    if recorded_hash != actual_hash:
        fail(
            "docs/plan.md has changed since ADR 0000 was ratified.\n"
            f"  Recorded hash: {recorded_hash}\n"
            f"  Actual hash:   {actual_hash}\n"
            "Log a proposed/accepted amendment (or re-ratify) in "
            f"{ADR_PATH.relative_to(REPO_ROOT)} with the new hash."
        )
    print(
        "OK: docs/plan.md content hash matches ADR 0000 "
        f"({hash_state}; hash state does not imply approval)."
    )


def charter_adr_status(adr_text: str) -> str:
    matches = re.findall(
        r"^- \*\*Status:\*\*\s*(Proposed|Accepted)\s*$", adr_text, re.MULTILINE
    )
    if len(matches) != 1:
        fail(
            f"{CHARTER_ADR_PATH.relative_to(REPO_ROOT)} must contain exactly one "
            "'- **Status:** Proposed' or '- **Status:** Accepted' metadata line."
        )
    return matches[0]


def charter_status(charter_text: str) -> str:
    matches = re.findall(
        r"^- \*\*Status:\*\*\s*(.+?)\s*$", charter_text, re.MULTILINE
    )
    if len(matches) != 1:
        fail(
            f"{CHARTER_PATH.relative_to(REPO_ROOT)} must contain exactly one "
            "'- **Status:** <status>' metadata line."
        )
    return matches[0]


def issue_form_field_is_required(form_text: str, field_id: str) -> bool:
    """Inspect one issue-form body entry without matching another field's validation."""
    lines = form_text.splitlines()
    matching_entries: list[list[str]] = []

    for index, line in enumerate(lines):
        entry_match = re.fullmatch(r"(\s*)-\s+type:\s*\S+\s*", line)
        if not entry_match:
            continue

        entry_indent = len(entry_match.group(1))
        end = index + 1
        while end < len(lines):
            sibling = re.match(r"^(\s*)-\s+type:", lines[end])
            if sibling and len(sibling.group(1)) == entry_indent:
                break
            end += 1

        entry = lines[index:end]
        if any(
            re.fullmatch(rf"\s*id:\s*{re.escape(field_id)}\s*", item)
            for item in entry
        ):
            matching_entries.append(entry)

    if len(matching_entries) != 1:
        return False

    entry = matching_entries[0]
    id_line = next(
        item
        for item in entry
        if re.fullmatch(rf"\s*id:\s*{re.escape(field_id)}\s*", item)
    )
    field_property_indent = len(id_line) - len(id_line.lstrip())
    for index, line in enumerate(entry):
        validations_match = re.fullmatch(r"(\s*)validations:\s*", line)
        if (
            not validations_match
            or len(validations_match.group(1)) != field_property_indent
        ):
            continue
        validations_indent = len(validations_match.group(1))
        for item in entry[index + 1 :]:
            item_indent = len(item) - len(item.lstrip())
            if item.strip() and item_indent <= validations_indent:
                break
            if re.fullmatch(r"\s*required:\s*true\s*", item):
                return True
    return False


def markdown_h2_section(text: str, heading: str) -> str:
    matches = list(
        re.finditer(rf"^## {re.escape(heading)}\s*$", text, re.MULTILINE)
    )
    if len(matches) != 1:
        fail(
            f"{PR_TEMPLATE_PATH.relative_to(REPO_ROOT)} must contain exactly one "
            f"'## {heading}' section."
        )
    start = matches[0].end()
    next_heading = re.search(r"^##\s+", text[start:], re.MULTILINE)
    end = start + next_heading.start() if next_heading else len(text)
    return text[start:end]


def markdown_checkbox_text(section: str, label_prefix: str) -> str | None:
    lines = section.splitlines()
    matches: list[str] = []
    index = 0
    while index < len(lines):
        item = re.match(r"^- \[ \]\s+(.+)$", lines[index])
        if not item:
            index += 1
            continue

        content = [item.group(1).strip()]
        index += 1
        while index < len(lines) and not re.match(r"^- \[ \]\s+", lines[index]):
            if lines[index].strip():
                content.append(lines[index].strip())
            index += 1
        normalized = " ".join(content)
        if normalized.startswith(label_prefix):
            matches.append(normalized)

    return matches[0] if len(matches) == 1 else None


def check_pr_template_state(pr_template_text: str, adr_status: str) -> None:
    impact_section = markdown_h2_section(pr_template_text, "Charter impact")
    gates_section = markdown_h2_section(pr_template_text, "Gates")
    markers = re.findall(
        r"<!--\s*charter-impact-state:\s*(proposed|required)\s*-->",
        impact_section,
    )
    marker = markers[0] if len(markers) == 1 else None
    impact_text = " ".join(impact_section.split())
    gate_text = markdown_checkbox_text(
        gates_section, "Company charter impact recorded or not applicable"
    )

    if adr_status == "Proposed":
        required_sentence = (
            "This section is optional while ADR 0006 is Proposed and required "
            "after it is Accepted."
        )
        required_gate = (
            "Company charter impact recorded or not applicable "
            "(required after ADR 0006 acceptance)"
        )
        if (
            marker != "proposed"
            or required_sentence not in impact_text
            or gate_text != required_gate
        ):
            fail(
                "While ADR 0006 is Proposed, .github/pull_request_template.md "
                "must retain the 'charter-impact-state: proposed' marker, the "
                "explicit optional-before/required-after wording, and the "
                "pre-ratification charter gate."
            )
        return

    required_gate = "Company charter impact recorded or not applicable (required)"
    if (
        marker != "required"
        or "This section is required." not in impact_text
        or "optional while ADR 0006 is Proposed" in impact_text
        or gate_text != required_gate
    ):
        fail(
            "When ADR 0006 is Accepted, .github/pull_request_template.md must "
            "use the 'charter-impact-state: required' marker, state that the "
            "Charter impact section is required, and make its checklist gate "
            "unconditionally required."
        )


def check_readme_atomicity_content(readme_text: str, adr_status: str) -> None:
    charter_pending = re.search(r"charter[^.]*\bpending\b", readme_text, re.IGNORECASE)
    charter_ratified = re.search(
        r"charter[^.]*\bratified\b", readme_text, re.IGNORECASE
    )

    if adr_status == "Proposed":
        if not charter_pending or charter_ratified:
            fail(
                "While ADR 0006 is Proposed, README.md must describe the "
                "charter as pending ratification and must not yet call it "
                "ratified."
            )
        return

    if charter_pending or not charter_ratified:
        fail(
            "When ADR 0006 is Accepted, README.md must describe the charter "
            "as ratified and must no longer call it pending."
        )


def check_charter_atomicity_content(
    charter_text: str,
    adr_text: str,
    decision_form_text: str,
    pr_template_text: str,
    readme_text: str,
) -> None:
    adr_status = charter_adr_status(adr_text)
    status_text = charter_status(charter_text)
    says_proposed = re.search(r"\bproposed\b", status_text, re.IGNORECASE)
    says_not_authoritative = re.search(
        r"\bnot\s+(?:yet\s+)?authoritative\b", status_text, re.IGNORECASE
    )

    if adr_status == "Proposed":
        if not says_proposed or not says_not_authoritative:
            fail(
                "While ADR 0006 is Proposed, docs/charter.md must remain "
                "explicitly Proposed and not authoritative."
            )
        if issue_form_field_is_required(decision_form_text, "charter"):
            fail(
                "While ADR 0006 is Proposed, the 'charter' field in "
                ".github/ISSUE_TEMPLATE/decision.yml must not be required; "
                "that change belongs to the atomic acceptance pull request."
            )
        check_pr_template_state(pr_template_text, adr_status)
        check_readme_atomicity_content(readme_text, adr_status)
        print(
            "OK: Proposed ADR 0006 keeps the charter non-authoritative, the "
            "decision form optional, README pending, and the PR template in "
            "its pre-ratification state."
        )
        return

    if (
        says_proposed
        or says_not_authoritative
        or not re.match(r"(?i)^(?:accepted|ratified)\b", status_text)
    ):
        fail(
            "When ADR 0006 is Accepted, docs/charter.md status must start "
            "with Accepted or Ratified and must no longer say Proposed or "
            "not authoritative."
        )
    if not issue_form_field_is_required(decision_form_text, "charter"):
        fail(
            "When ADR 0006 is Accepted, the 'charter' field in "
            ".github/ISSUE_TEMPLATE/decision.yml must have "
            "'validations: required: true'."
        )
    check_pr_template_state(pr_template_text, adr_status)
    check_readme_atomicity_content(readme_text, adr_status)
    print(
        "OK: Accepted ADR 0006 has atomic charter, decision-form, "
        "README, and PR-template state."
    )


def check_charter_hash_content(charter_text: str, adr_text: str) -> None:
    if charter_adr_status(adr_text) == "Proposed":
        print("OK: ADR 0006 is Proposed; charter hash enforcement is deferred.")
        return

    actual_hash = hashlib.sha256(charter_text.encode("utf-8")).hexdigest()
    hash_matches = re.findall(
        r"^- \*\*Charter SHA-256:\*\*\s*`([0-9a-fA-F]{64})`\s*$",
        adr_text,
        re.MULTILINE,
    )
    if len(hash_matches) != 1:
        fail(
            "Accepted ADR 0006 must contain exactly one "
            "'- **Charter SHA-256:** `<64-character hash>`' line."
        )

    recorded_hash = hash_matches[0].lower()
    if recorded_hash != actual_hash:
        fail(
            "docs/charter.md has changed since ADR 0006 was accepted.\n"
            f"  Recorded hash: {recorded_hash}\n"
            f"  Actual hash:   {actual_hash}\n"
            "Log an amendment (or re-ratify) in "
            f"{CHARTER_ADR_PATH.relative_to(REPO_ROOT)} with the new hash."
        )
    print("OK: docs/charter.md hash matches Accepted ADR 0006.")


def check_charter_hash() -> None:
    charter_text = read(CHARTER_PATH)
    adr_text = read(CHARTER_ADR_PATH)
    check_charter_atomicity_content(
        charter_text,
        adr_text,
        read(DECISION_TEMPLATE_PATH),
        read(PR_TEMPLATE_PATH),
        read(README_PATH),
    )
    check_charter_hash_content(charter_text, adr_text)


def extract_table_rows(text: str, section_heading: str, header_prefix: str) -> list[list[str]]:
    """Return the cell lists for the first Markdown table whose header row
    starts with header_prefix, searched after section_heading."""
    heading_idx = text.find(section_heading)
    if heading_idx == -1:
        fail(f"Could not find section heading '{section_heading}'.")
    lines = text[heading_idx:].splitlines()

    header_idx = None
    for i, line in enumerate(lines):
        if line.strip().startswith(header_prefix):
            header_idx = i
            break
    if header_idx is None:
        fail(
            f"Could not find a table under '{section_heading}' with header "
            f"starting with '{header_prefix}'."
        )

    rows = []
    i = header_idx + 2  # skip header + separator row
    while i < len(lines) and lines[i].strip().startswith("|"):
        cells = [c.strip() for c in lines[i].strip().strip("|").split("|")]
        rows.append(cells)
        i += 1
    return rows


def first_segment(cell: str) -> str:
    """Strip Markdown bold/backticks/parentheticals and take the text before
    an em dash, so the same category can be compared across both tables."""
    cell = cell.strip().lstrip("*").rstrip("*").strip("`")
    cell = cell.split("—")[0].strip()
    cell = re.sub(r"\s*\([^)]*\)\s*$", "", cell).strip()
    return cell


def extract_status_artifact_hashes(plan_text: str) -> dict[str, str]:
    rows = extract_table_rows(
        plan_text,
        "#### Phase 0 policy and governance artifact classification",
        "| Artifact | Source and current SHA-256 | Current authority |",
    )
    artifacts: dict[str, str] = {}
    for row in rows:
        if len(row) < 2:
            raise ValueError("A status-artifact row has fewer than two columns.")
        path_match = re.search(r"\[`([^`]+)`\]\([^)]+\)", row[0])
        hash_match = re.search(r"`([0-9a-fA-F]{64})`", row[1])
        if not path_match or not hash_match:
            raise ValueError(f"Malformed status-artifact row: {' | '.join(row)}")
        path = path_match.group(1)
        if path in artifacts:
            raise ValueError(f"Duplicate status-artifact row: {path}")
        artifacts[path] = hash_match.group(1).lower()
    return artifacts


def extract_status_artifact_cells(plan_text: str) -> dict[str, list[str]]:
    rows = extract_table_rows(
        plan_text,
        "#### Phase 0 policy and governance artifact classification",
        "| Artifact | Source and current SHA-256 | Current authority |",
    )
    artifacts: dict[str, list[str]] = {}
    for row in rows:
        if len(row) != 3:
            raise ValueError(f"Malformed status-artifact row: {' | '.join(row)}")
        path_match = re.search(r"\[`([^`]+)`\]\([^)]+\)", row[0])
        if not path_match:
            raise ValueError(f"Malformed status-artifact row: {' | '.join(row)}")
        artifacts[path_match.group(1)] = row
    return artifacts


def validate_canonical_baseline_rows(plan_text: str) -> None:
    rows = extract_status_artifact_cells(plan_text)
    for path, challengers in CANONICAL_BASELINE_REQUIREMENTS.items():
        if path not in rows:
            raise ValueError(f"Missing canonical baseline status row: {path}")
        _, source, authority = rows[path]
        required_source_parts = (CANONICAL_ISSUE_SOURCE, CANONICAL_PR_SOURCE)
        missing_source = [part for part in required_source_parts if part not in source]
        required_authority_parts = (
            "Canonical descriptive baseline; not ratified",
            *challengers,
            "Cyrus residual-risk acceptance remains pending",
            CANONICAL_OPEN_ASSESSMENT,
        )
        missing_authority = [
            part for part in required_authority_parts if part not in authority
        ]
        if missing_source or missing_authority:
            raise ValueError(
                f"Canonical baseline authority drifted for {path}.\n"
                f"  Missing source text: {missing_source}\n"
                f"  Missing authority text: {missing_authority}"
            )


def parse_initial_metadata_block(document_text: str, path: str) -> dict[str, str]:
    lines = document_text.splitlines()
    if len(lines) < 4 or not lines[0].startswith("# ") or lines[1].strip():
        raise ValueError(f"Canonical baseline metadata block malformed for {path}.")

    metadata: dict[str, str] = {}
    current_field: str | None = None
    for line in lines[2:]:
        if not line.strip():
            if not metadata:
                raise ValueError(
                    f"Canonical baseline metadata block absent for {path}."
                )
            return metadata
        field_match = re.match(r"^- \*\*([^*]+):\*\*\s+(\S.*)$", line)
        if field_match:
            current_field, value = field_match.groups()
            if current_field in metadata:
                raise ValueError(
                    f"Duplicate canonical baseline metadata field "
                    f"{current_field!r} for {path}."
                )
            metadata[current_field] = value.strip()
        elif current_field and line.startswith(("  ", "\t")) and line.strip():
            metadata[current_field] = " ".join(
                f"{metadata[current_field]} {line.strip()}".split()
            )
        else:
            raise ValueError(
                f"Canonical baseline metadata block malformed for {path}."
            )

    raise ValueError(
        f"Canonical baseline metadata block has no terminating blank line for {path}."
    )


def validate_canonical_baseline_documents(document_texts: dict[str, str]) -> None:
    for path, challengers in CANONICAL_BASELINE_REQUIREMENTS.items():
        metadata = parse_initial_metadata_block(document_texts[path], path)
        required_fields = {
            "Status": "Canonical descriptive baseline; not ratified",
            "Residual-risk acceptance": "Cyrus; pending",
            "Classification/impact assessment": (
                CANONICAL_DOCUMENT_OPEN_ASSESSMENTS[path]
            ),
        }
        drifted_fields = [
            field
            for field, expected in required_fields.items()
            if metadata.get(field) != expected
        ]
        challenge = metadata.get("Required challenge", "")
        missing_challengers = [
            challenger for challenger in challengers if challenger not in challenge
        ]
        if drifted_fields or missing_challengers:
            raise ValueError(
                f"Canonical baseline header drifted for {path}.\n"
                f"  Missing or drifted fields: {drifted_fields}\n"
                f"  Missing required challengers: {missing_challengers}"
            )


def validate_status_artifact_hashes(
    artifacts: dict[str, str],
    expected_paths: set[str],
    digest_for_path,
) -> None:
    actual_paths = set(artifacts)
    if actual_paths != expected_paths:
        raise ValueError(
            "Status-artifact inventory drifted.\n"
            f"  Missing:    {sorted(expected_paths - actual_paths)}\n"
            f"  Unexpected: {sorted(actual_paths - expected_paths)}"
        )
    for path, recorded_hash in sorted(artifacts.items()):
        actual_hash = digest_for_path(path)
        if recorded_hash != actual_hash:
            raise ValueError(
                f"Status-artifact hash mismatch for {path}.\n"
                f"  Recorded hash: {recorded_hash}\n"
                f"  Actual hash:   {actual_hash}"
            )


def validate_feedback_tracking_secret_contract(document_text: str) -> None:
    normalized = " ".join(document_text.split())
    required_clauses = (
        "A raw tracking secret is disclosed only to the requester at issuance "
        "and is never persisted",
        "Only a one-way verifier, the non-secret `trackingRef`, and metadata "
        "strictly necessary for expiry, failed-attempt throttling, recovery, "
        "rotation, and revocation may be stored.",
        "The raw secret never appears in a URL path, query, fragment, browser "
        "history, referrer, log, trace, metric, alert, queue field, provider "
        "metadata, backup, replica, export, analytics dataset, or support tool.",
    )
    missing = [clause for clause in required_clauses if clause not in normalized]
    forbidden_clauses = (
        "tracking secrets are stored outside the envelope",
        "tracking credential as separately protected records",
    )
    present_forbidden = [
        clause for clause in forbidden_clauses if clause in normalized.casefold()
    ]
    if missing or present_forbidden:
        raise ValueError(
            "Feedback tracking-secret custody contract drifted.\n"
            f"  Missing required clauses: {missing}\n"
            f"  Forbidden persistence clauses: {present_forbidden}"
        )


def check_feedback_tracking_secret_contract() -> None:
    try:
        validate_feedback_tracking_secret_contract(read(FEEDBACK_SUPPORT_PATH))
    except (OSError, ValueError) as error:
        fail(str(error))
    print("OK: feedback tracking secrets use verifier-only custody.")


def check_status_artifact_hashes() -> None:
    try:
        plan_text = read(PLAN_PATH)
        artifacts = extract_status_artifact_hashes(plan_text)
        validate_status_artifact_hashes(
            artifacts,
            EXPECTED_STATUS_ARTIFACTS,
            lambda path: hashlib.sha256(
                read_git_lf_bytes(REPO_ROOT / path)
            ).hexdigest(),
        )
        validate_canonical_baseline_rows(plan_text)
        validate_canonical_baseline_documents(
            {
                path: read(REPO_ROOT / path)
                for path in CANONICAL_BASELINE_REQUIREMENTS
            }
        )
    except (OSError, ValueError) as error:
        fail(str(error))
    print(f"OK: {len(artifacts)} status-artifact hashes match.")


def check_skill_catalog() -> None:
    plan_text = read(PLAN_PATH)
    skills_text = read(SKILLS_PATH)

    plan_rows = extract_table_rows(
        plan_text, "## Initial first-party skill catalog", "| Skill | Initial capabilities |"
    )
    plan_skills = {first_segment(row[0]) for row in plan_rows if row and row[0]}

    catalog_rows = extract_table_rows(
        skills_text, "## First-party skill catalog", "| # | Skill | Capability |"
    )
    catalog_skills = {first_segment(row[1]) for row in catalog_rows if len(row) > 1}

    missing_in_catalog = plan_skills - catalog_skills
    missing_in_plan = catalog_skills - plan_skills
    if missing_in_catalog or missing_in_plan:
        fail(
            "docs/plan.md's seed skill table and "
            "docs/roadmap/first-party-skills.md have drifted.\n"
            f"  In plan.md but not in catalog: {sorted(missing_in_catalog)}\n"
            f"  In catalog but not in plan.md: {sorted(missing_in_plan)}"
        )
    print(f"OK: {len(catalog_skills)} first-party skills match between plan.md and the catalog.")


def check_connector_catalog() -> None:
    plan_text = read(PLAN_PATH)
    connectors_text = read(CONNECTORS_PATH)

    plan_rows = extract_table_rows(
        plan_text, "### Connector catalog and release bands", "| Category | Baseline connectors |"
    )
    plan_categories = {first_segment(row[0]) for row in plan_rows if row and row[0]}

    catalog_rows = extract_table_rows(
        connectors_text, "## Channel catalog", "| # | Category | Capability |"
    )
    catalog_categories = {first_segment(row[1]) for row in catalog_rows if len(row) > 1}

    missing_in_catalog = plan_categories - catalog_categories
    missing_in_plan = catalog_categories - plan_categories
    if missing_in_catalog or missing_in_plan:
        fail(
            "docs/plan.md's seed connector table and "
            "docs/roadmap/channel-connectors.md have drifted.\n"
            f"  In plan.md but not in catalog: {sorted(missing_in_catalog)}\n"
            f"  In catalog but not in plan.md: {sorted(missing_in_plan)}"
        )
    print(
        f"OK: {len(catalog_categories)} connector categories match between plan.md and the catalog."
    )


def main() -> None:
    check_plan_hash()
    check_charter_hash()
    check_feedback_tracking_secret_contract()
    check_status_artifact_hashes()
    check_skill_catalog()
    check_connector_catalog()
    print("All documentation consistency checks passed.")


if __name__ == "__main__":
    main()
