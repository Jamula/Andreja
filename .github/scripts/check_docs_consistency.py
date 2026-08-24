#!/usr/bin/env python3
"""Documentation CI check for catalog/hash drift.

Fails the build when:
  1. The plan SHA-256 hash recorded in docs/adr/0000-plan-ratification.md no
     longer matches the merged docs/plan.md content.
  2. ADR 0006 and the charter/template ratification state are inconsistent, or
     an Accepted ADR's charter SHA-256 no longer matches docs/charter.md. A
     Proposed ADR does not enforce its candidate hash.
  3. The seed skill names in docs/plan.md's "Initial first-party skill
     catalog" table drift from the authoritative
     docs/roadmap/first-party-skills.md catalog.
  4. The seed connector categories in docs/plan.md's "Connector catalog and
     release bands" table drift from the authoritative
     docs/roadmap/channel-connectors.md catalog.

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
DECISION_TEMPLATE_PATH = REPO_ROOT / ".github" / "ISSUE_TEMPLATE" / "decision.yml"
SKILLS_PATH = REPO_ROOT / "docs" / "roadmap" / "first-party-skills.md"
CONNECTORS_PATH = REPO_ROOT / "docs" / "roadmap" / "channel-connectors.md"


def fail(message: str) -> None:
    print(f"::error::{message}")
    sys.exit(1)


def read(path: Path) -> str:
    if not path.exists():
        fail(f"Required file not found: {path.relative_to(REPO_ROOT)}")
    return path.read_text(encoding="utf-8")


def check_plan_hash() -> None:
    plan_text = read(PLAN_PATH)
    actual_hash = hashlib.sha256(plan_text.encode("utf-8")).hexdigest()

    adr_text = read(ADR_PATH)
    match = re.search(r"\*\*Plan SHA-256:\*\*\s*`([0-9a-fA-F]+)`", adr_text)
    if not match:
        fail(
            "Could not find a '**Plan SHA-256:** `<hash>`' line in "
            f"{ADR_PATH.relative_to(REPO_ROOT)}."
        )
    recorded_hash = match.group(1).lower()

    if recorded_hash != actual_hash:
        fail(
            "docs/plan.md has changed since ADR 0000 was ratified.\n"
            f"  Recorded hash: {recorded_hash}\n"
            f"  Actual hash:   {actual_hash}\n"
            "Log an amendment (or re-ratify) in "
            f"{ADR_PATH.relative_to(REPO_ROOT)} with the new hash."
        )
    print("OK: docs/plan.md hash matches ADR 0000.")


def charter_adr_status(adr_text: str) -> str:
    status_match = re.search(
        r"^- \*\*Status:\*\*\s*(Proposed|Accepted)\s*$", adr_text, re.MULTILINE
    )
    if not status_match:
        fail(
            "Could not find a '- **Status:** Proposed' or "
            "'- **Status:** Accepted' line in "
            f"{CHARTER_ADR_PATH.relative_to(REPO_ROOT)}."
        )
    return status_match.group(1)


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


def check_charter_atomicity_content(
    charter_text: str, adr_text: str, decision_form_text: str
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
        print("OK: Proposed ADR 0006 keeps the charter non-authoritative.")
        return

    if says_proposed or says_not_authoritative or not re.search(
        r"\b(?:accepted|ratified)\b", status_text, re.IGNORECASE
    ):
        fail(
            "When ADR 0006 is Accepted, docs/charter.md status must say Accepted "
            "or ratified and must no longer say Proposed or not authoritative."
        )
    if not issue_form_field_is_required(decision_form_text, "charter"):
        fail(
            "When ADR 0006 is Accepted, the 'charter' field in "
            ".github/ISSUE_TEMPLATE/decision.yml must have "
            "'validations: required: true'."
        )
    print("OK: Accepted ADR 0006 has atomic charter and decision-form state.")


def check_charter_hash_content(charter_text: str, adr_text: str) -> None:
    if charter_adr_status(adr_text) == "Proposed":
        print("OK: ADR 0006 is Proposed; charter hash enforcement is deferred.")
        return

    actual_hash = hashlib.sha256(charter_text.encode("utf-8")).hexdigest()
    hash_match = re.search(
        r"\*\*Charter SHA-256:\*\*\s*`([0-9a-fA-F]{64})`", adr_text
    )
    if not hash_match:
        fail(
            "Accepted ADR 0006 must contain a "
            "'**Charter SHA-256:** `<64-character hash>`' line."
        )

    recorded_hash = hash_match.group(1).lower()
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
        charter_text, adr_text, read(DECISION_TEMPLATE_PATH)
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
    check_skill_catalog()
    check_connector_catalog()
    print("All documentation consistency checks passed.")


if __name__ == "__main__":
    main()
