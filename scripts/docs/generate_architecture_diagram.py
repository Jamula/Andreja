#!/usr/bin/env python3
"""Generate Andreja's editable architecture diagram and GitHub renderings."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import struct
import subprocess
import sys
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "docs" / "architecture"
EXCALIDRAW_PATH = OUT_DIR / "andreja-high-level.excalidraw"
SVG_PATH = OUT_DIR / "andreja-high-level.svg"
PNG_PATH = OUT_DIR / "andreja-high-level.png"
CHECK_PNG_PATH = OUT_DIR / ".andreja-high-level.check.png"
WIDTH = 1920
HEIGHT = 1460
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_METADATA_KEYS = {
    "source": "Andreja-Source-SHA256",
    "svg": "Andreja-SVG-SHA256",
    "raster": "Andreja-Raster-SHA256",
    "binding": "Andreja-Binding-SHA256",
}

COLORS = {
    "blue": ("#1864ab", "#d0ebff"),
    "green": ("#2f9e44", "#d3f9d8"),
    "orange": ("#e67700", "#fff3bf"),
    "purple": ("#862e9c", "#f3d9fa"),
    "teal": ("#0c8599", "#c5f6fa"),
    "gray": ("#495057", "#f1f3f5"),
    "red": ("#c92a2a", "#ffe3e3"),
}


class Diagram:
    def __init__(self) -> None:
        self.elements: list[dict[str, object]] = []
        self.svg: list[str] = []
        self._counter = 0

    def _id(self, prefix: str) -> str:
        self._counter += 1
        return f"{prefix}-{self._counter:03d}"

    def _base(self, kind: str, element_id: str, x: int, y: int, width: int, height: int) -> dict[str, object]:
        return {
            "id": element_id,
            "type": kind,
            "x": x,
            "y": y,
            "width": width,
            "height": height,
            "angle": 0,
            "strokeWidth": 2,
            "strokeStyle": "solid",
            "roughness": 0,
            "opacity": 100,
            "groupIds": [],
            "frameId": None,
            "index": f"a{self._counter:03d}",
            "roundness": {"type": 3},
            "seed": 1000 + self._counter,
            "version": 1,
            "versionNonce": 2000 + self._counter,
            "isDeleted": False,
            "boundElements": [],
            "updated": 1,
            "link": None,
            "locked": False,
        }

    def text(
        self,
        x: int,
        y: int,
        text: str,
        *,
        size: int = 18,
        width: int = 300,
        align: str = "left",
        weight: str = "normal",
    ) -> None:
        lines = text.splitlines() or [""]
        height = max(size * 3, int(size * 2.5 * len(lines)))
        element_id = self._id("text")
        item = self._base("text", element_id, x, y, width, height)
        item.update(
            {
                "strokeColor": "#000000",
                "backgroundColor": "transparent",
                "fillStyle": "solid",
                "strokeWidth": 1,
                "text": text,
                "fontSize": size,
                "fontFamily": 2,
                "textAlign": align,
                "verticalAlign": "top",
                "containerId": None,
                "originalText": text,
                "autoResize": False,
                "lineHeight": 1.25,
            }
        )
        self.elements.append(item)

        anchor = {"left": "start", "center": "middle", "right": "end"}[align]
        svg_x = x if align == "left" else x + width / 2 if align == "center" else x + width
        tspans = []
        for index, line in enumerate(lines):
            dy = 0 if index == 0 else int(size * 1.25)
            tspans.append(
                f'<tspan x="{svg_x:g}" dy="{dy}">{html.escape(line)}</tspan>'
            )
        self.svg.append(
            f'<text x="{svg_x:g}" y="{y + size:g}" text-anchor="{anchor}" '
            f'font-size="{size}" font-weight="{weight}" fill="#000000">'
            + "".join(tspans)
            + "</text>"
        )

    def rectangle(
        self,
        x: int,
        y: int,
        width: int,
        height: int,
        label: str,
        *,
        color: str = "gray",
        status: str = "current",
        container: bool = False,
        font_size: int = 17,
        label_align: str = "center",
    ) -> None:
        stroke, fill = COLORS[color]
        if container:
            fill = "transparent"
        style = {"current": "solid", "contract": "dashed", "future": "dotted"}[status]
        element_id = self._id("rect")
        item = self._base("rectangle", element_id, x, y, width, height)
        item.update(
            {
                "strokeColor": stroke,
                "backgroundColor": fill,
                "fillStyle": "solid",
                "strokeStyle": style,
            }
        )
        self.elements.append(item)

        dash = {"solid": "", "dashed": ' stroke-dasharray="12 8"', "dotted": ' stroke-dasharray="3 8"'}[style]
        svg_fill = "none" if container else fill
        self.svg.append(
            f'<rect x="{x}" y="{y}" width="{width}" height="{height}" rx="12" '
            f'fill="{svg_fill}" stroke="{stroke}" stroke-width="2"{dash}/>'
        )
        if label:
            lines = label.splitlines()
            text_height = int(font_size * 1.25 * len(lines))
            text_y = y + 8 if container else y + max(10, (height - text_height) // 2 - 2)
            self.text(
                x + 10,
                text_y,
                label,
                size=font_size,
                width=width - 20,
                align=label_align,
                weight="bold" if container else "normal",
            )

    def arrow(
        self,
        x1: int,
        y1: int,
        x2: int,
        y2: int,
        label: str,
        *,
        color: str = "gray",
        status: str = "current",
        label_dx: int = 0,
        label_dy: int = -24,
        via: list[tuple[int, int]] | None = None,
    ) -> None:
        stroke, _ = COLORS[color]
        style = {"current": "solid", "contract": "dashed", "future": "dotted"}[status]
        absolute_points = [(x1, y1), *(via or []), (x2, y2)]
        local_points = [[x - x1, y - y1] for x, y in absolute_points]
        point_x = [point[0] for point in local_points]
        point_y = [point[1] for point in local_points]
        element_id = self._id("arrow")
        item = self._base(
            "arrow",
            element_id,
            x1,
            y1,
            max(point_x) - min(point_x),
            max(point_y) - min(point_y),
        )
        item.update(
            {
                "strokeColor": stroke,
                "backgroundColor": "transparent",
                "fillStyle": "solid",
                "strokeStyle": style,
                "roundness": {"type": 2},
                "points": local_points,
                "lastCommittedPoint": None,
                "startBinding": None,
                "endBinding": None,
                "startArrowhead": None,
                "endArrowhead": "arrow",
                "elbowed": False,
            }
        )
        self.elements.append(item)

        dash = {"solid": "", "dashed": ' stroke-dasharray="12 8"', "dotted": ' stroke-dasharray="3 8"'}[style]
        points = " ".join(f"{x},{y}" for x, y in absolute_points)
        self.svg.append(
            f'<polyline points="{points}" fill="none" stroke="{stroke}" '
            f'stroke-width="3"{dash} marker-end="url(#arrow-{color})"/>'
        )
        label_x = int((x1 + x2) / 2) + label_dx
        label_y = int((y1 + y2) / 2) + label_dy
        self.svg.append(
            f'<rect x="{label_x - 26}" y="{label_y - 16}" width="52" height="25" '
            'rx="8" fill="#ffffff" stroke="#495057" stroke-width="1"/>'
        )
        self.text(label_x - 24, label_y - 14, label, size=15, width=48, align="center", weight="bold")


def build_diagram() -> Diagram:
    d = Diagram()

    d.text(35, 20, "Andreja high-level architecture and data flows", size=34, width=1200, weight="bold")
    d.text(
        35,
        70,
        "Phase 1A evidence view • one modular-monolith deployable • future capability is explicitly gated",
        size=19,
        width=1260,
    )
    d.rectangle(
        1320,
        15,
        565,
        80,
        "CONTENT RULE\nUser content follows policy-labelled flows only.\nNever telemetry, public/help, or secret files.",
        color="red",
        font_size=14,
    )

    d.rectangle(35, 110, 245, 58, "CURRENT PHASE 1A\nsolid border / flow", color="green", status="current", font_size=14)
    d.rectangle(300, 110, 285, 58, "CURRENT CONTRACT-ONLY\nlocal conformance; dashed", color="orange", status="contract", font_size=14)
    d.rectangle(605, 110, 245, 58, "FUTURE / GATED\nnot deployed; dotted", color="purple", status="future", font_size=14)
    d.rectangle(870, 110, 1015, 58, "Trust boundaries are labelled TB1–TB6. Flow numbers F1–F10 map to the companion document.", color="gray", font_size=15)

    # Trust boundary containers. They are intentionally transparent.
    d.rectangle(25, 195, 290, 850, "TB1 • CLIENT / BROWSER\nTRUST BOUNDARY", color="blue", container=True, font_size=15)
    d.rectangle(335, 195, 925, 850, "TB2 • AUTHENTICATED APP TRUST BOUNDARY", color="green", container=True, font_size=16)
    d.rectangle(365, 405, 865, 610, "TB3 • TENANT + PRINCIPAL + PURPOSE ISOLATION", color="orange", container=True, font_size=15)
    d.rectangle(1280, 195, 300, 850, "TB4 • ADAPTER / PROVIDER\nBOUNDARY", color="purple", container=True, font_size=15)
    d.rectangle(335, 1070, 1245, 350, "TB5 • LOCAL OPERATOR CUSTODY BOUNDARY", color="teal", container=True, font_size=16)
    d.rectangle(1600, 195, 295, 1225, "TB6 • EXTERNAL / PEER\nBOUNDARY", color="red", container=True, font_size=15)

    # Clients and separately gated public surface.
    d.rectangle(50, 270, 240, 110, "Responsive Blazor web\nTyped API client", color="blue", font_size=18)
    d.rectangle(50, 440, 240, 110, "Future native +\nthird-party clients", color="purple", status="future", font_size=18)
    d.rectangle(50, 820, 240, 155, "Public / help site\nSeparately deployed\nNo app cookies or user data", color="purple", status="future", font_size=16)

    # Composition and API boundary.
    d.rectangle(365, 270, 245, 105, "AppHost\nComposition + Blazor host", color="green", font_size=18)
    d.rectangle(645, 270, 255, 105, "Versioned HTTP API\nTyped contracts / clients", color="green", font_size=18)
    d.rectangle(935, 270, 285, 105, "Adapters\nIdentity • PostgreSQL • OTel\nOpenAI-compatible BYOK", color="green", font_size=16)

    # Capability modules: current implementation or explicit local contracts.
    d.rectangle(390, 475, 240, 100, "Identity + Tenancy\nPasskeys / scoped context", color="green", font_size=16)
    d.rectangle(655, 475, 240, 100, "Assistant Runtime\nProvider-neutral sessions", color="green", font_size=16)
    d.rectangle(920, 475, 280, 100, "Skills\nISkillHost + manifests", color="green", font_size=16)

    d.rectangle(390, 605, 240, 105, "Policy evaluator\nTenant • principal • purpose", color="green", font_size=16)
    d.rectangle(
        655,
        605,
        240,
        105,
        "Proposal / Control Plane\nDurable proposal state\nConfirm before task write",
        color="green",
        font_size=14,
    )
    d.rectangle(920, 605, 280, 105, "Open Loops / Tasks\nAccess-scoped domain write", color="green", font_size=16)

    d.rectangle(390, 740, 240, 110, "Sharing / Federation\nGrant • consent • IPeerChannel", color="orange", status="contract", font_size=15)
    d.rectangle(655, 740, 240, 110, "Semantic Profile\nProvenance contracts", color="orange", status="contract", font_size=16)
    d.rectangle(920, 740, 280, 110, "Audit + idempotency\nTransactional evidence", color="green", font_size=16)

    d.rectangle(390, 880, 240, 100, "Portability\nVersioned export / import", color="green", font_size=16)
    d.rectangle(655, 880, 240, 100, "Observability\nOTel allowlist / suppression", color="green", font_size=16)
    d.rectangle(920, 880, 280, 100, "Channels\nIChannelHost + manifests", color="orange", status="contract", font_size=16)

    # Provider and adapter targets.
    d.rectangle(1305, 310, 250, 130, "Assistant provider\nUser-configured BYOK endpoint\n(or deterministic local fake)", color="purple", font_size=16)
    d.rectangle(1305, 520, 250, 130, "Channel / connector adapter\nNo live connector in Phase 1A", color="purple", status="future", font_size=16)
    d.rectangle(1305, 725, 250, 120, "Identity / OIDC adapter\nBuilt-in passkey now;\noptional OIDC gated", color="purple", status="future", font_size=15)
    d.rectangle(1305, 900, 250, 110, "OTel exporter\nOperational fields only", color="teal", font_size=16)

    # Local stores and custody.
    d.rectangle(
        365,
        1140,
        220,
        115,
        "Tenant-scoped PostgreSQL\nTasks • proposals • identity\nAudit • idempotency • refs",
        color="teal",
        font_size=14,
    )
    d.rectangle(610, 1140, 205, 115, "Attachments\nOutside DB when added", color="purple", status="future", font_size=16)
    d.rectangle(840, 1140, 210, 115, "Data Protection +\nencryption key history", color="teal", font_size=16)
    d.rectangle(1075, 1140, 210, 115, "File-backed operator\nsecrets / configuration", color="teal", font_size=16)
    d.rectangle(1310, 1140, 240, 115, "Local OTel Collector\nOptional local evidence backend", color="teal", font_size=16)
    d.rectangle(365, 1290, 330, 95, "Application export/import archive\nChecksums; no credentials or key material", color="teal", font_size=15)
    d.rectangle(
        730,
        1290,
        330,
        95,
        "UNPROVEN encrypted recovery set\nDB + key/config inventory\nRestored sign-in proof BLOCKED",
        color="purple",
        status="future",
        font_size=14,
    )
    d.rectangle(1095, 1290, 455, 95, "EXTENSION SEAMS\nIAssistantProvider • ISkillHost • IChannelHost • identity/OIDC\nTyped clients • persistence/portability • OTel • IPeerChannel", color="gray", font_size=14)

    # External/gated systems.
    d.rectangle(1625, 310, 245, 135, "Independently hosted\npeer Andreja instance\nPurpose-scoped proposals only", color="red", status="future", font_size=16)
    d.rectangle(1625, 535, 245, 135, "Future external\nchannel / connector systems\nUntrusted inbound content", color="red", status="future", font_size=16)
    d.rectangle(1625, 820, 245, 145, "Future tenant-less\nfeedback / support intake\nSeparate governance", color="red", status="future", font_size=16)
    d.rectangle(
        1625,
        1135,
        245,
        135,
        "GATED operator-controlled\nbackup destination\nNo destination approved",
        color="red",
        status="future",
        font_size=16,
    )

    # Primary data flows.
    d.arrow(290, 320, 365, 320, "F1", color="blue")
    d.arrow(290, 355, 655, 505, "F2", color="blue", label_dx=-10, label_dy=-30)
    d.arrow(895, 525, 1305, 375, "F2", color="purple", label_dx=15, label_dy=-10)
    d.arrow(895, 525, 920, 525, "F3a", color="green", label_dy=-20)
    d.arrow(
        1060,
        575,
        775,
        605,
        "F3b",
        color="green",
        label_dy=-18,
        via=[(1060, 590), (775, 590)],
    )
    d.arrow(
        775,
        710,
        475,
        1140,
        "F3c",
        color="green",
        label_dx=15,
        via=[(640, 720), (640, 1050), (475, 1050)],
    )
    d.arrow(
        290,
        350,
        655,
        657,
        "F4",
        color="green",
        label_dx=-150,
        label_dy=45,
        via=[(340, 350), (340, 657)],
    )
    d.arrow(895, 657, 920, 657, "F5a", color="green", label_dy=-20)
    d.arrow(1060, 710, 1060, 740, "F5b", color="green", label_dx=-35, label_dy=0)
    d.arrow(
        1200,
        795,
        475,
        1140,
        "F5c",
        color="green",
        label_dx=390,
        label_dy=20,
        via=[(1245, 795), (1245, 1030), (475, 1030)],
    )
    d.arrow(
        1625,
        600,
        1200,
        930,
        "F6",
        color="purple",
        status="future",
        label_dx=-150,
        label_dy=140,
        via=[(1270, 600), (1270, 930)],
    )
    d.arrow(
        510,
        980,
        695,
        1338,
        "F7",
        color="teal",
        label_dx=80,
        label_dy=-100,
        via=[(700, 1030), (700, 1338)],
    )
    d.arrow(475, 1255, 780, 1290, "F8a", color="purple", status="future", label_dy=-10)
    d.arrow(945, 1255, 945, 1290, "F8b", color="purple", status="future", label_dx=-35, label_dy=0)
    d.arrow(
        1180,
        1255,
        1030,
        1290,
        "F8c",
        color="purple",
        status="future",
        label_dy=-10,
        via=[(1100, 1275)],
    )
    d.arrow(
        1060,
        1338,
        1625,
        1200,
        "F8d",
        color="purple",
        status="future",
        label_dx=240,
        label_dy=10,
        via=[(1080, 1275), (1585, 1275)],
    )
    d.arrow(
        775,
        980,
        1430,
        1140,
        "F9",
        color="teal",
        label_dx=300,
        label_dy=0,
        via=[(775, 1040), (1430, 1040)],
    )
    d.arrow(1625, 375, 630, 790, "F10", color="red", status="future", label_dx=10, label_dy=-10)

    return d


def excalidraw_bytes(diagram: Diagram) -> bytes:
    document = {
        "type": "excalidraw",
        "version": 2,
        "source": "https://github.com/Jamula/Andreja",
        "elements": diagram.elements,
        "appState": {
            "viewBackgroundColor": "#ffffff",
            "gridSize": 20,
            "gridStep": 5,
            "gridModeEnabled": False,
            "zoom": {"value": 0.55},
        },
        "files": {},
    }
    return (json.dumps(document, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def svg_bytes(diagram: Diagram, source_hash: str) -> bytes:
    markers = []
    for name, (stroke, _) in COLORS.items():
        markers.append(
            f'<marker id="arrow-{name}" viewBox="0 0 10 10" refX="9" refY="5" '
            'markerWidth="8" markerHeight="8" orient="auto-start-reverse">'
            f'<path d="M 0 0 L 10 5 L 0 10 z" fill="{stroke}"/></marker>'
        )
    body = "\n  ".join(diagram.svg)
    output = f"""<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}"
     role="img" aria-labelledby="title desc" data-source-sha256="{source_hash}">
  <title id="title">Andreja high-level architecture and data flows</title>
  <desc id="desc">Current Phase 1A components, current local conformance contracts, future gated components,
  six labelled trust boundaries, ten numbered data flows, local data custody, and extension seams.</desc>
  <defs>
    {''.join(markers)}
    <style>text {{ font-family: Arial, Helvetica, sans-serif; }}</style>
  </defs>
  <rect width="{WIDTH}" height="{HEIGHT}" fill="#ffffff"/>
  {body}
</svg>
"""
    return output.encode("utf-8")


def find_edge() -> Path:
    candidates = [
        Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
        Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError("Microsoft Edge was not found; cannot render PNG.")


def png_chunks(data: bytes) -> list[tuple[bytes, bytes]]:
    if not data.startswith(PNG_SIGNATURE):
        raise ValueError("Invalid PNG signature.")
    chunks = []
    offset = len(PNG_SIGNATURE)
    while offset < len(data):
        if offset + 12 > len(data):
            raise ValueError("Truncated PNG chunk.")
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        chunk_end = offset + 12 + length
        if chunk_end > len(data):
            raise ValueError("Truncated PNG chunk data.")
        chunk_data = data[offset + 8 : offset + 8 + length]
        recorded_crc = struct.unpack(">I", data[offset + 8 + length : chunk_end])[0]
        actual_crc = zlib.crc32(chunk_type + chunk_data) & 0xFFFFFFFF
        if recorded_crc != actual_crc:
            raise ValueError(f"Invalid PNG CRC for {chunk_type.decode('ascii', errors='replace')}.")
        chunks.append((chunk_type, chunk_data))
        offset = chunk_end
        if chunk_type == b"IEND":
            if offset != len(data):
                raise ValueError("Unexpected data after PNG IEND.")
            break
    if not chunks or chunks[-1][0] != b"IEND":
        raise ValueError("PNG is missing IEND.")
    return chunks


def png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", crc)


def png_raster_hash(chunks: list[tuple[bytes, bytes]]) -> str:
    raster_chunks = {b"IHDR", b"PLTE", b"tRNS", b"IDAT"}
    material = b"".join(chunk_type + data for chunk_type, data in chunks if chunk_type in raster_chunks)
    if not material:
        raise ValueError("PNG contains no raster data.")
    return hashlib.sha256(material).hexdigest()


def png_binding_hash(source_hash: str, svg_hash: str, raster_hash: str) -> str:
    material = bytes.fromhex(source_hash) + bytes.fromhex(svg_hash) + bytes.fromhex(raster_hash)
    return hashlib.sha256(material).hexdigest()


def embed_png_provenance(path: Path, source_hash: str, svg_hash: str) -> None:
    chunks = png_chunks(path.read_bytes())
    raster_hash = png_raster_hash(chunks)
    values = {
        PNG_METADATA_KEYS["source"]: source_hash,
        PNG_METADATA_KEYS["svg"]: svg_hash,
        PNG_METADATA_KEYS["raster"]: raster_hash,
        PNG_METADATA_KEYS["binding"]: png_binding_hash(source_hash, svg_hash, raster_hash),
    }
    output = bytearray(PNG_SIGNATURE)
    for chunk_type, data in chunks:
        if chunk_type == b"tEXt":
            keyword = data.split(b"\0", 1)[0].decode("latin-1", errors="replace")
            if keyword in PNG_METADATA_KEYS.values():
                continue
        if chunk_type == b"IEND":
            for keyword, value in values.items():
                output.extend(png_chunk(b"tEXt", keyword.encode("latin-1") + b"\0" + value.encode("ascii")))
        output.extend(png_chunk(chunk_type, data))
    path.write_bytes(output)


def verify_png(path: Path, source_hash: str, svg_hash: str) -> str:
    chunks = png_chunks(path.read_bytes())
    ihdr = next((data for chunk_type, data in chunks if chunk_type == b"IHDR"), None)
    if ihdr is None or len(ihdr) < 8:
        raise ValueError("PNG is missing a valid IHDR.")
    if struct.unpack(">II", ihdr[:8]) != (WIDTH, HEIGHT):
        raise ValueError(f"PNG dimensions are not {WIDTH}x{HEIGHT}.")

    metadata = {}
    for chunk_type, data in chunks:
        if chunk_type != b"tEXt" or b"\0" not in data:
            continue
        keyword, value = data.split(b"\0", 1)
        metadata[keyword.decode("latin-1")] = value.decode("latin-1")

    raster_hash = png_raster_hash(chunks)
    expected = {
        PNG_METADATA_KEYS["source"]: source_hash,
        PNG_METADATA_KEYS["svg"]: svg_hash,
        PNG_METADATA_KEYS["raster"]: raster_hash,
        PNG_METADATA_KEYS["binding"]: png_binding_hash(source_hash, svg_hash, raster_hash),
    }
    mismatches = [keyword for keyword, value in expected.items() if metadata.get(keyword) != value]
    if mismatches:
        raise ValueError("PNG provenance mismatch: " + ", ".join(mismatches))
    return raster_hash


def render_svg_png(output_path: Path) -> None:
    edge = find_edge()
    output_path.unlink(missing_ok=True)
    command = [
        str(edge),
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--force-device-scale-factor=1",
        f"--window-size={WIDTH},{HEIGHT}",
        f"--screenshot={output_path}",
        SVG_PATH.resolve().as_uri(),
    ]
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, timeout=60)
    if result.returncode != 0 or not output_path.exists():
        raise RuntimeError(f"Edge PNG render failed: {result.stderr.strip()}")


def render_png(source_hash: str, svg_hash: str) -> None:
    render_svg_png(PNG_PATH)
    embed_png_provenance(PNG_PATH, source_hash, svg_hash)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail when generated source/render files drift.")
    parser.add_argument("--render-png", action="store_true", help="Render the SVG to PNG with Microsoft Edge.")
    args = parser.parse_args()

    diagram = build_diagram()
    source = excalidraw_bytes(diagram)
    source_hash = hashlib.sha256(source).hexdigest()
    svg = svg_bytes(diagram, source_hash)
    svg_hash = hashlib.sha256(svg).hexdigest()

    if args.check:
        failures = []
        for path, expected in ((EXCALIDRAW_PATH, source), (SVG_PATH, svg)):
            if not path.exists() or path.read_bytes() != expected:
                failures.append(str(path.relative_to(ROOT)))
        if not PNG_PATH.exists():
            failures.append(str(PNG_PATH.relative_to(ROOT)))
        else:
            try:
                committed_raster_hash = verify_png(PNG_PATH, source_hash, svg_hash)
                render_svg_png(CHECK_PNG_PATH)
                fresh_raster_hash = png_raster_hash(png_chunks(CHECK_PNG_PATH.read_bytes()))
                if committed_raster_hash != fresh_raster_hash:
                    raise ValueError("raster does not match a fresh render of the current SVG")
            except (RuntimeError, ValueError) as error:
                failures.append(f"{PNG_PATH.relative_to(ROOT)} ({error})")
            finally:
                CHECK_PNG_PATH.unlink(missing_ok=True)
        if failures:
            print("Architecture artifacts are stale: " + ", ".join(failures), file=sys.stderr)
            return 1
        print(f"Architecture artifacts are consistent (source SHA-256 {source_hash}).")
        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    EXCALIDRAW_PATH.write_bytes(source)
    SVG_PATH.write_bytes(svg)
    if args.render_png:
        render_png(source_hash, svg_hash)
    print(f"Wrote {EXCALIDRAW_PATH.relative_to(ROOT)} and {SVG_PATH.relative_to(ROOT)}.")
    if args.render_png:
        print(f"Wrote {PNG_PATH.relative_to(ROOT)} ({WIDTH}x{HEIGHT}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
