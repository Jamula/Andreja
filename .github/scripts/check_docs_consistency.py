#!/usr/bin/env python3
"""Documentation CI check for catalog/hash drift.

Fails the build when:
  1. The plan SHA-256 hash recorded in docs/adr/0000-plan-ratification.md no
     longer matches the merged docs/plan.md content.
  2. The seed skill names in docs/plan.md's "Initial first-party skill
     catalog" table drift from the authoritative
     docs/roadmap/first-party-skills.md catalog.
  3. The seed connector categories in docs/plan.md's "Connector catalog and
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
    plan_bytes = PLAN_PATH.read_bytes()
    actual_hash = hashlib.sha256(plan_bytes).hexdigest()

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
    check_skill_catalog()
    check_connector_catalog()
    print("All documentation consistency checks passed.")


if __name__ == "__main__":
    main()
