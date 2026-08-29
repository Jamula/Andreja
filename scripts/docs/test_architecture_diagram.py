#!/usr/bin/env python3
"""Negative tests for the committed architecture PNG policy."""

from __future__ import annotations

import hashlib
import importlib.util
import struct
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("generate_architecture_diagram.py")
SPEC = importlib.util.spec_from_file_location("architecture_diagram", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load {SCRIPT_PATH}.")
diagram = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(diagram)


class DiagramModelTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.model = diagram.build_diagram()
        cls.text_elements = [
            element for element in cls.model.elements if element["type"] == "text"
        ]
        cls.visible_text = "\n".join(
            str(element["text"]) for element in cls.text_elements
        )
        cls.normalized_visible_text = " ".join(cls.visible_text.split())

    def find_text(self, prefix: str) -> dict[str, object]:
        return next(
            element
            for element in self.text_elements
            if " ".join(str(element["text"]).split()).startswith(prefix)
        )

    def test_status_is_expressed_with_words_and_line_styles(self) -> None:
        for label in ("CURRENT PHASE 1A", "CURRENT CONTRACT-ONLY", "FUTURE / GATED"):
            self.assertIn(label, self.visible_text)
        rectangle_styles = {
            element["strokeStyle"]
            for element in self.model.elements
            if element["type"] == "rectangle"
        }
        self.assertTrue({"solid", "dashed", "dotted"}.issubset(rectangle_styles))

    def test_local_fake_and_external_byok_are_separate_boundaries(self) -> None:
        local_fake = self.find_text("Assistant adapter + local fake")
        external_provider = self.find_text("EXTERNAL BYOK MODEL PROVIDER")
        self.assertGreaterEqual(local_fake["x"], 1280)
        self.assertLess(local_fake["x"], 1600)
        self.assertGreaterEqual(external_provider["x"], 1600)
        local_text = " ".join(str(local_fake["text"]).split())
        provider_text = " ".join(str(external_provider["text"]).split())
        self.assertIn("Non-secret credential handle stays here", local_text)
        self.assertIn("Fake: no egress", local_text)
        self.assertIn("Owning-tenant policy authoritative", provider_text)

    def test_provider_credential_disclosure_is_truthful_and_narrow(self) -> None:
        local_fake = " ".join(
            str(self.find_text("Assistant adapter + local fake")["text"]).split()
        )
        external_provider = " ".join(
            str(self.find_text("EXTERNAL BYOK MODEL PROVIDER")["text"]).split()
        )
        self.assertIn("Secret value: TB5 custody", local_fake)
        self.assertIn(
            "Transport discloses provider credential only to "
            "operator configured allowlisted provider endpoint for authentication",
            local_fake,
        )
        self.assertIn(
            "Provider credential: authentication only",
            external_provider,
        )
        self.assertIn("other tenant secrets", external_provider)
        self.assertNotIn("NO passkeys / recovery / secrets", external_provider)

    def test_public_help_hosting_is_an_external_content_free_surface(self) -> None:
        public_help = self.find_text("PUBLIC / HELP HOSTING")
        self.assertGreaterEqual(public_help["x"], 1600)
        public_help_text = " ".join(str(public_help["text"]).split())
        self.assertIn("Receives NO app cookie, token", public_help_text)
        self.assertIn("or product / user data", public_help_text)

    def test_visible_flow_labels_capture_content_and_authority(self) -> None:
        flow_ids = {
            element.get("customData", {}).get("flowId")
            for element in self.model.elements
            if element["type"] == "arrow"
        }
        for flow in ("F2a", "F2b", "F2c", "F2d", "F7", "F10 ↔", "S1"):
            self.assertIn(flow, flow_ids)
        for wording in (
            "May receive current request +",
            "Disclosed content ceiling applies",
            "User-selected supported data +",
            "purpose-scoped minimum disclosure",
            "CURRENT: no content / traffic",
            "User-approved assertions +",
            "minimized source refs/digests",
            "No durable tables",
        ):
            self.assertIn(wording, self.normalized_visible_text)

    def test_f10_is_future_gated_and_explicitly_bidirectional(self) -> None:
        f10 = next(
            element
            for element in self.model.elements
            if element["type"] == "arrow"
            and element.get("customData", {}).get("flowId") == "F10 ↔"
        )
        self.assertEqual("dotted", f10["strokeStyle"])
        self.assertEqual("arrow", f10["startArrowhead"])
        self.assertEqual("arrow", f10["endArrowhead"])
        self.assertGreater(f10["points"][-1][0], f10["points"][0][0])
        peer = " ".join(
            str(self.find_text("INDEPENDENT PEER INSTANCE")["text"]).split()
        )
        self.assertIn("May receive user-approved purpose-scoped minimum disclosure", peer)
        self.assertIn("May submit a proposal; never an authoritative write", peer)
        self.assertIn("CURRENT: no content / traffic", peer)

    def test_runtime_and_recovery_labels_match_current_contracts(self) -> None:
        self.assertIn(
            "CompleteAsync → one AssistantResponse",
            self.normalized_visible_text,
        )
        self.assertIn("Passkey public credential data", self.normalized_visible_text)
        self.assertIn("Hashed recovery-code state", self.normalized_visible_text)
        self.assertIn(
            "LOGICAL DB includes all durable rows",
            self.normalized_visible_text,
        )
        self.assertIn(
            "token rows per ADR 0003",
            self.normalized_visible_text,
        )
        self.assertIn(
            "Raw recovery codes not stored in DB",
            self.normalized_visible_text,
        )
        self.assertIn(
            "outside DB and dump",
            self.normalized_visible_text,
        )

    def test_960px_readability_and_geometry_are_enforced(self) -> None:
        report = diagram.validate_diagram_readability(self.model)
        self.assertEqual(960, report["presentation_width"])
        self.assertEqual(0.5, report["scale"])
        self.assertGreaterEqual(report["minimum_source_font"], 22)
        self.assertGreaterEqual(report["minimum_effective_font"], 11)
        self.assertGreater(report["contained_labels"], 0)
        self.assertEqual(0, report["overflow_count"])

    def test_svg_and_png_bind_the_readability_scale(self) -> None:
        root = ET.fromstring(diagram.SVG_PATH.read_bytes())
        namespace = {"svg": "http://www.w3.org/2000/svg"}
        self.assertEqual(str(diagram.WIDTH), root.attrib["width"])
        self.assertEqual(str(diagram.HEIGHT), root.attrib["height"])
        self.assertEqual(
            f"0 0 {diagram.WIDTH} {diagram.HEIGHT}",
            root.attrib["viewBox"],
        )
        svg_text = root.findall("svg:text", namespace)
        self.assertEqual(len(self.text_elements), len(svg_text))
        minimum_svg_font = min(float(node.attrib["font-size"]) for node in svg_text)
        self.assertGreaterEqual(
            minimum_svg_font * diagram.GITHUB_PRESENTATION_WIDTH / diagram.WIDTH,
            diagram.MIN_EFFECTIVE_FONT_SIZE,
        )
        contained_svg_labels = [
            node for node in svg_text if "data-container-id" in node.attrib
        ]
        self.assertEqual(
            sum(1 for element in self.text_elements if element.get("containerId")),
            len(contained_svg_labels),
        )

        ihdr = diagram.png_chunks(diagram.PNG_PATH.read_bytes())[0]
        self.assertEqual(b"IHDR", ihdr[0])
        self.assertEqual(13, len(ihdr[1]))
        self.assertEqual(
            (diagram.WIDTH, diagram.HEIGHT),
            struct.unpack(">II", ihdr[1][:8]),
        )


class PngPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source_hash = hashlib.sha256(diagram.EXCALIDRAW_PATH.read_bytes()).hexdigest()
        cls.svg_hash = hashlib.sha256(diagram.SVG_PATH.read_bytes()).hexdigest()
        cls.base_png = diagram.PNG_PATH.read_bytes()

    def assert_rejected_with_regenerated_manifest(
        self,
        chunk_type: bytes,
        payload: bytes,
        expected_error: str = "Non-allowlisted PNG chunk",
    ) -> None:
        output = bytearray(diagram.PNG_SIGNATURE)
        for existing_type, existing_data in diagram.png_chunks(self.base_png):
            if existing_type == b"IEND":
                output.extend(diagram.png_chunk(chunk_type, payload))
            output.extend(diagram.png_chunk(existing_type, existing_data))
        mutated = bytes(output)
        regenerated_manifest = (
            f"{hashlib.sha256(mutated).hexdigest()}  {diagram.PNG_PATH.name}\n"
        )
        with self.assertRaisesRegex(ValueError, expected_error):
            diagram.verify_png_artifact(
                mutated,
                regenerated_manifest,
                self.source_hash,
                self.svg_hash,
            )

    def assert_chunks_rejected_with_regenerated_manifest(
        self,
        chunks: list[tuple[bytes, bytes]],
        expected_error: str,
    ) -> None:
        mutated = diagram.PNG_SIGNATURE + b"".join(
            diagram.png_chunk(chunk_type, payload) for chunk_type, payload in chunks
        )
        regenerated_manifest = (
            f"{hashlib.sha256(mutated).hexdigest()}  {diagram.PNG_PATH.name}\n"
        )
        with self.assertRaisesRegex(ValueError, expected_error):
            diagram.verify_png_artifact(
                mutated,
                regenerated_manifest,
                self.source_hash,
                self.svg_hash,
            )

    def test_rejects_oversized_ihdr_with_regenerated_provenance_and_manifest(
        self,
    ) -> None:
        chunks = [
            (chunk_type, payload + b"\0" if chunk_type == b"IHDR" else payload)
            for chunk_type, payload in diagram.png_chunks(self.base_png)
        ]
        raster_hash = diagram.png_raster_hash(chunks)
        replacements = {
            diagram.PNG_METADATA_KEYS["raster"]: raster_hash,
            diagram.PNG_METADATA_KEYS["binding"]: diagram.png_binding_hash(
                self.source_hash,
                self.svg_hash,
                raster_hash,
            ),
        }
        regenerated_chunks = []
        for chunk_type, payload in chunks:
            if chunk_type == b"tEXt":
                keyword, separator, _ = payload.partition(b"\0")
                replacement = replacements.get(keyword.decode("ascii"))
                if replacement is not None:
                    payload = keyword + separator + replacement.encode("ascii")
            regenerated_chunks.append((chunk_type, payload))
        self.assert_chunks_rejected_with_regenerated_manifest(
            regenerated_chunks,
            "IHDR payload to be exactly 13 bytes",
        )

    def test_rejects_nonempty_iend_with_regenerated_manifest(self) -> None:
        chunks = [
            (chunk_type, b"unexpected" if chunk_type == b"IEND" else payload)
            for chunk_type, payload in diagram.png_chunks(self.base_png)
        ]
        self.assert_chunks_rejected_with_regenerated_manifest(
            chunks,
            "IEND payload to be empty",
        )

    def test_rejects_extra_itxt_with_regenerated_manifest(self) -> None:
        self.assert_rejected_with_regenerated_manifest(
            b"iTXt", b"Review\0\0\0\0\0unexpected metadata"
        )

    def test_rejects_extra_exif_with_regenerated_manifest(self) -> None:
        self.assert_rejected_with_regenerated_manifest(
            b"eXIf", b"MM\0*\0\0\0\x08"
        )

    def test_rejects_apng_control_with_regenerated_manifest(self) -> None:
        self.assert_rejected_with_regenerated_manifest(
            b"acTL", struct.pack(">II", 1, 0)
        )

    def test_rejects_apng_frame_data_with_regenerated_manifest(self) -> None:
        self.assert_rejected_with_regenerated_manifest(
            b"fdAT", struct.pack(">I", 0) + b"unexpected frame"
        )

    def test_rejects_unknown_text_keyword_with_regenerated_manifest(self) -> None:
        self.assert_rejected_with_regenerated_manifest(
            b"tEXt",
            b"Unexpected-Keyword\0" + b"0" * 64,
            "Non-allowlisted PNG tEXt keyword",
        )

    def test_rejects_duplicate_provenance_with_regenerated_manifest(self) -> None:
        provenance = next(
            data
            for chunk_type, data in diagram.png_chunks(self.base_png)
            if chunk_type == b"tEXt"
        )
        self.assert_rejected_with_regenerated_manifest(
            b"tEXt",
            provenance,
            "Duplicate PNG provenance tEXt keyword",
        )



class ArrowGeometryTests(unittest.TestCase):
    """Negative tests: out-of-canvas arrow points are rejected by the validator."""

    def _diagram_with_arrow(
        self, x1: int, y1: int, x2: int, y2: int
    ) -> diagram.Diagram:
        d = diagram.Diagram()
        d.arrow(x1, y1, x2, y2, "T", color="gray", status="current")
        return d

    def test_rejects_arrow_point_left_of_canvas(self) -> None:
        d = self._diagram_with_arrow(-1, 100, 100, 100)
        with self.assertRaisesRegex(ValueError, "outside the"):
            diagram.validate_diagram_readability(d)

    def test_rejects_arrow_point_right_of_canvas(self) -> None:
        d = self._diagram_with_arrow(100, 100, diagram.WIDTH + 1, 100)
        with self.assertRaisesRegex(ValueError, "outside the"):
            diagram.validate_diagram_readability(d)

    def test_rejects_arrow_point_above_canvas(self) -> None:
        d = self._diagram_with_arrow(100, -1, 100, 100)
        with self.assertRaisesRegex(ValueError, "outside the"):
            diagram.validate_diagram_readability(d)

    def test_rejects_arrow_point_below_canvas(self) -> None:
        d = self._diagram_with_arrow(100, 100, 100, diagram.HEIGHT + 1)
        with self.assertRaisesRegex(ValueError, "outside the"):
            diagram.validate_diagram_readability(d)

if __name__ == "__main__":
    unittest.main()
