"""Offline document contracts, not feedback implementation or exercise evidence."""

from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[1]
PACKET = Path("docs/frameworks/feedback-readiness.md")
SCENARIOS = Path("docs/frameworks/feedback-readiness-scenarios.md")
RUNBOOKS = Path("docs/runbooks/feedback-operations.md")
HELP = Path("docs/help/feedback.md")
PATHS = (PACKET, SCENARIOS, RUNBOOKS, HELP)


def documents():
    return {path: (ROOT / path).read_text(encoding="utf-8") for path in PATHS}


def normalized(text):
    return " ".join(text.split())


def framework_scenarios():
    source = (ROOT / "docs/frameworks/feedback-support.md").read_text(
        encoding="utf-8"
    )
    section = source.split("## Phase 1B acceptance evidence\n", 1)[1].split(
        "\nGuinan, Deanna Troi", 1
    )[0]
    return [
        normalized(item)
        for item in re.findall(
            r"^\d+\. (.*?)(?=^\d+\. |\Z)", section, flags=re.M | re.S
        )
    ]


def contract_errors(docs):
    errors = []
    scenario_text = docs[SCENARIOS]
    sections = re.findall(
        r"^## S(\d{2}) ([^\n]+)\n(.*?)(?=^## S\d{2} |\Z)",
        scenario_text,
        flags=re.M | re.S,
    )
    if [int(number) for number, _, _ in sections] != list(range(1, 15)):
        errors.append("scenario inventory must be exactly S01-S14 in order")
    expected = framework_scenarios()
    if len(expected) != 14:
        errors.append("framework scenario inventory changed; review mapping")
    for number, _, body in sections:
        index = int(number) - 1
        quote = normalized(
            "\n".join(re.findall(r"^> ?(.*)$", body, flags=re.M))
        )
        if index >= len(expected) or quote != expected[index]:
            errors.append(f"S{number}: complete framework requirement differs")
        for field in (
            "**Fixture/setup:**", "**Action:**",
            "**Observable pass assertions:**", "**Negative/edge:**",
            "**Failure evidence:**", "**Owner:**", "**Status:** NOT RUN.",
        ):
            if field not in body:
                errors.append(f"S{number}: missing {field}")
        if "../runbooks/feedback-operations.md" not in body:
            errors.append(f"S{number}: missing runbook link")

    runbooks = re.findall(
        r"^## R(\d{2}) [^\n]+\n(.*?)(?=^## R\d{2} |\Z)",
        docs[RUNBOOKS],
        flags=re.M | re.S,
    )
    if [int(number) for number, _ in runbooks] != list(range(1, 12)):
        errors.append("runbook inventory must be exactly R01-R11 in order")
    for number, body in runbooks:
        for field in ("**Prerequisites:**", "1. ", "**Evidence:**", "**Fail/stop:**",
                      "**Recovery:**"):
            if field not in body:
                errors.append(f"R{number}: missing {field}")
    register = re.findall(
        r"^\| R\d{2} .+\| REQUIRED / NOT RUN \|$", docs[RUNBOOKS], flags=re.M
    )
    if len(register) != 11:
        errors.append("all 11 runbooks need cadence and NOT RUN exercise register")

    required = {
        PACKET: (
            "disabled/off/no-collection/deny",
            "UNSATISFIED BY TEMPLATES ALONE",
            "Current record: MISSING. No exercise is authorized.",
            "NEW explicit authorization",
            "no later than 14 days after issuance",
            "qualified-counsel",
            "documented binding",
            "proceed / extend-learning / de-scope / stop",
        ),
        HELP: (
            "not active",
            "operational/accessibility review: NOT RUN",
            "Silence is never verification.",
            "non-user subjects",
        ),
    }
    for path, phrases in required.items():
        for phrase in phrases:
            if phrase.casefold() not in docs[path].casefold():
                errors.append(f"{path}: missing safety statement {phrase}")

    for path, text in docs.items():
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", text):
            if re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", link):
                if not link.startswith("https://github.com/Jamula/Andreja/"):
                    errors.append(f"{path}: unreviewed external link {link}")
                continue
            filename, _, fragment = link.partition("#")
            target = (ROOT / path.parent / filename).resolve()
            if not target.is_relative_to(ROOT) or not target.is_file():
                errors.append(f"{path}: broken local link {link}")
                continue
            if fragment:
                headings = re.findall(
                    r"^#{1,6} (.+)$",
                    target.read_text(encoding="utf-8"),
                    flags=re.M,
                )
                anchors = {
                    re.sub(r"[^\w\- ]", "", heading.lower()).replace(" ", "-")
                    for heading in headings
                }
                if fragment not in anchors:
                    errors.append(f"{path}: broken anchor {link}")
    return errors


class FeedbackReadinessDocuments(unittest.TestCase):
    def test_packet_contract(self):
        self.assertEqual([], contract_errors(documents()))

    def test_missing_scenario_is_rejected(self):
        docs = documents()
        docs[SCENARIOS] = re.sub(
            r"^## S04 .*?(?=^## S05 )", "", docs[SCENARIOS], flags=re.M | re.S
        )
        self.assertIn(
            "scenario inventory must be exactly S01-S14 in order",
            contract_errors(docs),
        )

    def test_reduced_requirement_is_rejected(self):
        docs = documents()
        # Mutate a real clause rather than a separate hand-built test fixture.
        docs[SCENARIOS] = docs[SCENARIOS].replace(
            "> proportionate identity/relationship proof, conflicting instructions,",
            "> proportionate identity/relationship proof,",
            1,
        )
        self.assertIn(
            "S04: complete framework requirement differs", contract_errors(docs)
        )

    def test_unsafe_default_is_rejected(self):
        docs = documents()
        docs[PACKET] = docs[PACKET].replace(
            "disabled/off/no-collection/deny", "enabled/on/collect/allow", 1
        )
        self.assertTrue(
            any("missing safety statement disabled/off/no-collection/deny" in error
                for error in contract_errors(docs))
        )

    def test_broken_link_is_rejected(self):
        docs = documents()
        docs[HELP] = docs[HELP].replace(
            "../frameworks/feedback-readiness.md",
            "../frameworks/feedback-missing.md",
            1,
        )
        self.assertTrue(
            any("broken local link" in error and "feedback-missing.md" in error
                for error in contract_errors(docs))
        )

    def test_broken_anchor_is_rejected(self):
        docs = documents()
        docs[SCENARIOS] = docs[SCENARIOS].replace(
            "#r01-intake-health-backpressure-and-recovery",
            "#r01-missing",
            1,
        )
        self.assertTrue(
            any("broken anchor" in error and "r01-missing" in error
                for error in contract_errors(docs))
        )


if __name__ == "__main__":
    unittest.main()
