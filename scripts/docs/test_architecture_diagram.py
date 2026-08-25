#!/usr/bin/env python3
"""Negative tests for the committed architecture PNG policy."""

from __future__ import annotations

import hashlib
import importlib.util
import struct
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("generate_architecture_diagram.py")
SPEC = importlib.util.spec_from_file_location("architecture_diagram", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load {SCRIPT_PATH}.")
diagram = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(diagram)


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


if __name__ == "__main__":
    unittest.main()
