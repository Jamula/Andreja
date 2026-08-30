#!/usr/bin/env python3
"""Generate Andreja's editable architecture diagram and GitHub renderings."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
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
PNG_HASH_PATH = OUT_DIR / "andreja-high-level.png.sha256"
CHECK_PNG_PATH = OUT_DIR / ".andreja-high-level.check.png"
STAGING_PNG_PATH = OUT_DIR / ".andreja-high-level.rendering.png"
WIDTH = 1920
HEIGHT = 2710
GITHUB_PRESENTATION_WIDTH = 960
MIN_EFFECTIVE_FONT_SIZE = 11
MIN_SOURCE_FONT_SIZE = math.ceil(
    MIN_EFFECTIVE_FONT_SIZE * WIDTH / GITHUB_PRESENTATION_WIDTH
)
TEXT_LINE_HEIGHT = 1.25
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_METADATA_KEYS = {
    "source": "Andreja-Source-SHA256",
    "svg": "Andreja-SVG-SHA256",
    "raster": "Andreja-Raster-SHA256",
    "binding": "Andreja-Binding-SHA256",
}
PNG_ALLOWED_CHUNK_TYPES = {b"IHDR", b"IDAT", b"tEXt", b"IEND"}

COLORS = {
    "blue": ("#1864ab", "#d0ebff"),
    "green": ("#2f9e44", "#d3f9d8"),
    "orange": ("#e67700", "#fff3bf"),
    "purple": ("#862e9c", "#f3d9fa"),
    "teal": ("#0c8599", "#c5f6fa"),
    "gray": ("#495057", "#f1f3f5"),
    "red": ("#c92a2a", "#ffe3e3"),
}


def estimated_text_width(text: str, size: int, weight: str = "normal") -> float:
    """Return a conservative Arial/Helvetica width estimate in source pixels."""
    width_units = 0.0
    for character in text:
        if character.isspace():
            width_units += 0.32
        elif character in "ilI.,:;|'`![]()":
            width_units += 0.34
        elif character in "mwMW@#%&":
            width_units += 0.9
        elif character.isupper():
            width_units += 0.68
        elif character.isdigit():
            width_units += 0.58
        else:
            width_units += 0.55
    weight_factor = 1.06 if weight == "bold" else 1.0
    return width_units * size * weight_factor


def wrap_text(text: str, width: int, size: int, weight: str = "normal") -> str:
    """Wrap explicit label paragraphs without splitting identifiers."""
    output: list[str] = []
    for paragraph in text.splitlines() or [""]:
        words = paragraph.split()
        if not words:
            output.append("")
            continue
        line = words[0]
        if estimated_text_width(line, size, weight) > width:
            raise ValueError(f"Label token does not fit in {width}px: {line!r}")
        for word in words[1:]:
            candidate = f"{line} {word}"
            if estimated_text_width(candidate, size, weight) <= width:
                line = candidate
                continue
            output.append(line)
            line = word
            if estimated_text_width(line, size, weight) > width:
                raise ValueError(f"Label token does not fit in {width}px: {line!r}")
        output.append(line)
    return "\n".join(output)


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
        container_id: str | None = None,
        role: str = "annotation",
    ) -> dict[str, object]:
        lines = text.splitlines() or [""]
        height = math.ceil(size * TEXT_LINE_HEIGHT * len(lines))
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
                "containerId": container_id,
                "originalText": text,
                "autoResize": False,
                "lineHeight": TEXT_LINE_HEIGHT,
                "customData": {
                    "diagramRole": role,
                    "fontWeight": weight,
                },
            }
        )
        self.elements.append(item)

        anchor = {"left": "start", "center": "middle", "right": "end"}[align]
        svg_x = x if align == "left" else x + width / 2 if align == "center" else x + width
        tspans = []
        for index, line in enumerate(lines):
            dy = 0 if index == 0 else size * TEXT_LINE_HEIGHT
            tspans.append(
                f'<tspan x="{svg_x:g}" dy="{dy}">{html.escape(line)}</tspan>'
            )
        container_attribute = (
            f' data-container-id="{html.escape(container_id)}"'
            if container_id
            else ""
        )
        self.svg.append(
            f'<text id="{element_id}" data-role="{role}"{container_attribute} '
            f'x="{svg_x:g}" y="{y + size:g}" text-anchor="{anchor}" '
            f'font-size="{size}" font-weight="{weight}" fill="#000000">'
            + "".join(tspans)
            + "</text>"
        )
        return item

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
        role: str = "node",
    ) -> dict[str, object]:
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
                "customData": {
                    "diagramRole": "boundary" if container else role,
                },
            }
        )
        self.elements.append(item)

        dash = {"solid": "", "dashed": ' stroke-dasharray="12 8"', "dotted": ' stroke-dasharray="3 8"'}[style]
        svg_fill = "none" if container else fill
        self.svg.append(
            f'<rect id="{element_id}" data-role="{"boundary" if container else role}" '
            f'x="{x}" y="{y}" width="{width}" height="{height}" rx="12" '
            f'fill="{svg_fill}" stroke="{stroke}" stroke-width="2"{dash}/>'
        )
        if label:
            if font_size < MIN_SOURCE_FONT_SIZE:
                raise ValueError(
                    f"{role} label font {font_size}px is below the "
                    f"{MIN_SOURCE_FONT_SIZE}px source minimum."
                )
            horizontal_padding = 18
            vertical_padding = 12
            label_width = width - horizontal_padding * 2
            weight = "bold" if container else "normal"
            wrapped_label = wrap_text(label, label_width, font_size, weight)
            line_count = len(wrapped_label.splitlines())
            text_height = math.ceil(font_size * TEXT_LINE_HEIGHT * line_count)
            text_y = (
                y + vertical_padding
                if container
                else y + (height - text_height) // 2
            )
            if (
                label_width <= 0
                or text_y < y + vertical_padding
                or text_y + text_height > y + height - vertical_padding
            ):
                raise ValueError(
                    f"Label does not fit inside {role} rectangle at "
                    f"({x}, {y}, {width}, {height}): {wrapped_label!r}"
                )
            text_item = self.text(
                x + horizontal_padding,
                text_y,
                wrapped_label,
                size=font_size,
                width=label_width,
                align=label_align,
                weight=weight,
                container_id=element_id,
                role=f"{role}-label",
            )
            item["boundElements"] = [{"id": text_item["id"], "type": "text"}]
        return item

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
        label_at: tuple[int, int] | None = None,
        bidirectional: bool = False,
    ) -> dict[str, object]:
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
                "startArrowhead": "arrow" if bidirectional else None,
                "endArrowhead": "arrow",
                "elbowed": False,
                "customData": {"diagramRole": "flow", "flowId": label},
            }
        )
        self.elements.append(item)

        dash = {"solid": "", "dashed": ' stroke-dasharray="12 8"', "dotted": ' stroke-dasharray="3 8"'}[style]
        points = " ".join(f"{x},{y}" for x, y in absolute_points)
        marker_start = f' marker-start="url(#arrow-{color})"' if bidirectional else ""
        self.svg.append(
            f'<polyline id="{element_id}" data-role="flow" points="{points}" '
            f'fill="none" stroke="{stroke}" stroke-width="3"{dash}{marker_start} '
            f'marker-end="url(#arrow-{color})"/>'
        )
        label_x = label_at[0] if label_at else int((x1 + x2) / 2) + label_dx
        label_y = label_at[1] if label_at else int((y1 + y2) / 2) + label_dy
        label_width = 86 if len(label) > 4 else 46
        label_height = 36
        label_padding = 4 if len(label) > 4 else 1
        label_box = self._id("flow-label")
        label_item = self._base(
            "rectangle",
            label_box,
            label_x - label_width // 2,
            label_y - label_height // 2,
            label_width,
            label_height,
        )
        label_item.update(
            {
                "strokeColor": COLORS["gray"][0],
                "backgroundColor": "#ffffff",
                "fillStyle": "solid",
                "strokeStyle": "solid",
                "customData": {"diagramRole": "flow-label"},
            }
        )
        self.elements.append(label_item)
        self.svg.append(
            f'<rect id="{label_box}" data-role="flow-label" '
            f'x="{label_x - label_width // 2}" y="{label_y - label_height // 2}" '
            f'width="{label_width}" height="{label_height}" '
            'rx="8" fill="#ffffff" stroke="#495057" stroke-width="1"/>'
        )
        text_item = self.text(
            label_x - label_width // 2 + label_padding,
            label_y - math.ceil(MIN_SOURCE_FONT_SIZE * TEXT_LINE_HEIGHT / 2),
            label,
            size=MIN_SOURCE_FONT_SIZE,
            width=label_width - label_padding * 2,
            align="center",
            weight="bold",
            container_id=label_box,
            role="flow-label-text",
        )
        label_item["boundElements"] = [{"id": text_item["id"], "type": "text"}]
        return item


def build_diagram() -> Diagram:
    d = Diagram()

    d.text(
        35,
        20,
        "Andreja high-level architecture and data flows",
        size=40,
        width=1200,
        weight="bold",
        role="title",
    )
    d.text(
        35,
        82,
        "Ratified-plan evidence view • ADRs 0001–0005 + 0008 are Proposed • one modular-monolith deployable",
        size=22,
        width=1260,
        role="subtitle",
    )
    d.rectangle(
        1320,
        15,
        565,
        145,
        "CONTENT RULE\nUser content follows policy-labelled flows only.\nNever telemetry, public/help, or secret files.",
        color="red",
        font_size=22,
        role="callout",
    )

    d.rectangle(
        35,
        175,
        300,
        130,
        "CURRENT PHASE 1A\nsolid border / flow",
        color="gray",
        status="current",
        font_size=22,
        role="legend",
    )
    d.rectangle(
        355,
        175,
        340,
        130,
        "CURRENT CONTRACT-ONLY\nlocal conformance; dashed",
        color="gray",
        status="contract",
        font_size=22,
        role="legend",
    )
    d.rectangle(
        715,
        175,
        300,
        130,
        "FUTURE / GATED\nnot deployed; dotted",
        color="gray",
        status="future",
        font_size=22,
        role="legend",
    )
    d.rectangle(
        1035,
        175,
        850,
        130,
        "Trust boundaries are labelled TB1–TB6.\nFlows F1–F10 and semantic flow S1 map to the companion.\nStatus is always written; color only groups areas.",
        color="gray",
        font_size=22,
        role="legend",
    )

    # Trust boundary containers. They are intentionally transparent.
    d.rectangle(
        25,
        340,
        290,
        1420,
        "TB1 • CLIENT / BROWSER\nTRUST BOUNDARY",
        color="blue",
        container=True,
        font_size=22,
    )
    d.rectangle(
        335,
        340,
        925,
        1420,
        "TB2 • AUTHENTICATED APP TRUST BOUNDARY",
        color="green",
        container=True,
        font_size=22,
    )
    d.rectangle(
        365,
        640,
        865,
        1090,
        "TB3 • TENANT + PRINCIPAL + PURPOSE ISOLATION",
        color="orange",
        container=True,
        font_size=22,
    )
    d.rectangle(
        1280,
        340,
        275,
        1420,
        "TB4 • LOCAL ADAPTER + CREDENTIAL HANDLE\nTRUST BOUNDARY",
        color="purple",
        container=True,
        font_size=22,
    )
    d.rectangle(
        335,
        1790,
        1245,
        880,
        "TB5 • LOCAL OPERATOR CUSTODY BOUNDARY",
        color="teal",
        container=True,
        font_size=22,
    )
    d.rectangle(
        1605,
        340,
        290,
        2330,
        "TB6 • EXTERNAL / PEER\nTRUST BOUNDARY",
        color="red",
        container=True,
        font_size=22,
    )

    # Product clients. Public/help hosting is a separate external TB6 surface.
    d.rectangle(
        50,
        470,
        240,
        150,
        "Responsive Blazor web\nTyped API client",
        color="blue",
        font_size=22,
    )
    d.rectangle(
        50,
        740,
        240,
        170,
        "Future native + third-party clients\nFUTURE / GATED",
        color="purple",
        status="future",
        font_size=22,
    )

    # Composition and API boundary.
    d.rectangle(
        365,
        440,
        245,
        160,
        "AppHost\nComposition + Blazor host",
        color="green",
        font_size=22,
    )
    d.rectangle(
        645,
        440,
        255,
        160,
        "Versioned HTTP API\nTyped contracts / clients",
        color="green",
        font_size=22,
    )
    d.rectangle(
        935,
        440,
        285,
        160,
        "Adapters\nIdentity • PostgreSQL • OTel\nAssistant adapter",
        color="green",
        font_size=22,
    )

    # Capability modules: current implementation or explicit local contracts.
    d.rectangle(
        390,
        730,
        240,
        180,
        "Identity + Tenancy\nPasskeys / scoped context",
        color="green",
        font_size=22,
    )
    d.rectangle(
        655,
        700,
        260,
        210,
        "Assistant Runtime\nProvider-neutral session\nCompleteAsync → one\nAssistantResponse",
        color="green",
        font_size=22,
    )
    d.rectangle(
        920,
        730,
        280,
        180,
        "Skills\nISkillHost + manifests",
        color="green",
        font_size=22,
    )

    d.rectangle(
        390,
        950,
        240,
        180,
        "Policy evaluator\nTenant • principal • purpose",
        color="green",
        font_size=22,
    )
    d.rectangle(
        655,
        950,
        240,
        210,
        "Proposal / Control Plane\nDurable proposal state\nConfirm before task write",
        color="green",
        font_size=22,
    )
    d.rectangle(
        920,
        950,
        280,
        180,
        "Open Loops / Tasks\nAccess-scoped domain write",
        color="green",
        font_size=22,
    )

    d.rectangle(
        390,
        1170,
        240,
        270,
        "Sharing / Federation\nCURRENT CONTRACT-ONLY\nGrant • consent • disclosure\nLocal fixture: no content sent",
        color="orange",
        status="contract",
        font_size=22,
    )
    d.rectangle(
        655,
        1170,
        240,
        300,
        "Semantic / Provenance\nCURRENT CONTRACT-ONLY\nUser-approved assertions +\nminimized source refs/digests\nNo durable tables",
        color="orange",
        status="contract",
        font_size=22,
    )
    d.rectangle(
        920,
        1170,
        280,
        220,
        "Audit + idempotency\nTransactional evidence",
        color="green",
        font_size=22,
    )

    d.rectangle(
        390,
        1510,
        240,
        180,
        "Portability\nCURRENT PHASE 1A\nVersioned export / import",
        color="green",
        font_size=22,
    )
    d.rectangle(
        655,
        1510,
        240,
        180,
        "Observability\nOTel allowlist / suppression",
        color="green",
        font_size=22,
    )
    d.rectangle(
        920,
        1510,
        280,
        180,
        "Channels\nCURRENT CONTRACT-ONLY\nIChannelHost + manifests",
        color="orange",
        status="contract",
        font_size=22,
    )

    # Local adapter implementation and fake remain TB4. External provider egress is TB6.
    d.rectangle(
        1292,
        500,
        250,
        520,
        "Assistant adapter + local fake\nCURRENT PHASE 1A\nNon-secret credential handle stays here\nSecret value: TB5 custody\nTransport discloses provider credential only to operator configured allowlisted provider endpoint for authentication\nFake: no egress",
        color="green",
        font_size=22,
    )
    d.rectangle(
        1292,
        1100,
        250,
        190,
        "Optional OIDC adapter\nFUTURE / GATED\nNo provider selected",
        color="purple",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1292,
        1450,
        250,
        170,
        "OTel exporter\nOperational fields only",
        color="teal",
        font_size=22,
    )

    # Local stores and custody.
    d.rectangle(
        365,
        1880,
        280,
        320,
        "Tenant-scoped PostgreSQL\nTasks • proposals • identity\nPasskey public credential data\nHashed recovery-code state\nAudit • idempotency • refs",
        color="teal",
        font_size=22,
    )
    d.rectangle(
        660,
        1880,
        180,
        200,
        "Attachments\nOutside DB when added\nFUTURE / GATED",
        color="purple",
        status="future",
        font_size=22,
    )
    d.rectangle(
        855,
        1880,
        200,
        220,
        "Data Protection + encryption key history",
        color="teal",
        font_size=22,
    )
    d.rectangle(
        1070,
        1880,
        220,
        260,
        "File-backed operator secrets / configuration\nProvider credential value",
        color="teal",
        font_size=22,
    )
    d.rectangle(
        1305,
        1880,
        245,
        200,
        "Local OTel Collector\nOptional local evidence backend",
        color="teal",
        font_size=22,
    )
    d.rectangle(
        365,
        2240,
        330,
        390,
        "F7 • APPLICATION ARCHIVE\nUser-selected supported data +\nminimized provenance may flow\nOwning tenant controls export\nNO reusable authority / credentials",
        color="teal",
        font_size=22,
    )
    d.rectangle(
        730,
        2240,
        330,
        420,
        "F8 • UNPROVEN RECOVERY SET\nLOGICAL DB includes all durable rows (passkey credential data, hashed recovery-code state, token rows per ADR 0003). Raw recovery codes not stored in DB.\nFile-backed secrets + private keys:\noutside DB and dump.\nSeparate protected key/config inventory.",
        color="purple",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1095,
        2240,
        455,
        220,
        "EXTENSION SEAMS\nIAssistantProvider • ISkillHost • IChannelHost • identity/OIDC\nTyped clients • persistence/portability • OTel • IPeerChannel",
        color="gray",
        font_size=22,
    )

    # External/gated systems.
    d.rectangle(
        1615,
        480,
        270,
        570,
        "EXTERNAL BYOK MODEL PROVIDER\nFUTURE / GATED ACTIVATION\nMay receive current request +\nallowlisted tool schema only\nPurpose: task proposal\nDisclosed content ceiling applies\nProvider credential: authentication only\nOwning-tenant policy authoritative\nNO passkeys, recovery, or other tenant secrets",
        color="red",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1615,
        1080,
        270,
        440,
        "INDEPENDENT PEER INSTANCE\nFUTURE / GATED\nMay receive user-approved purpose-scoped minimum disclosure\nMay submit a proposal; never an authoritative write\nOwning tenant stays authoritative\nCURRENT: no content / traffic",
        color="red",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1615,
        1550,
        270,
        260,
        "Future external channel / connector systems\nUntrusted inbound content\nFUTURE / GATED",
        color="red",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1615,
        1840,
        270,
        340,
        "PUBLIC / HELP HOSTING\nFUTURE / GATED external surface\nSeparate origin + deployment\nReceives NO app cookie, token,\nor product / user data",
        color="red",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1615,
        2210,
        270,
        240,
        "Future tenant-less feedback / support intake\nSeparate governance\nFUTURE / GATED",
        color="red",
        status="future",
        font_size=22,
    )
    d.rectangle(
        1615,
        2480,
        270,
        210,
        "GATED operator-controlled\nbackup destination\nNo destination approved",
        color="red",
        status="future",
        font_size=22,
    )

    # Primary data flows.
    d.arrow(
        290,
        545,
        390,
        820,
        "F1",
        color="blue",
        label_at=(275, 680),
    )
    d.arrow(
        290,
        520,
        645,
        520,
        "F2a",
        color="blue",
        via=[(320, 400), (630, 400), (630, 520)],
        label_at=(480, 390),
    )
    d.arrow(
        775,
        600,
        775,
        700,
        "F2b",
        color="blue",
        label_at=(825, 620),
    )
    d.arrow(
        915,
        800,
        1292,
        620,
        "F2c",
        color="green",
        via=[(910, 680), (1260, 680), (1260, 620)],
        label_at=(1080, 705),
    )
    d.arrow(
        1542,
        620,
        1615,
        620,
        "F2d",
        color="red",
        status="future",
        label_at=(1580, 620),
    )
    d.arrow(
        915,
        820,
        920,
        820,
        "F3a",
        color="green",
        label_at=(1200, 700),
    )
    d.arrow(
        1200,
        820,
        775,
        950,
        "F3b",
        color="green",
        via=[(1245, 820), (1245, 930), (775, 930)],
        label_at=(1180, 930),
    )
    d.arrow(
        775,
        1130,
        475,
        1880,
        "F3c",
        color="green",
        via=[(640, 1150), (640, 1760), (475, 1760)],
        label_at=(665, 1710),
    )
    d.arrow(
        700,
        600,
        655,
        1040,
        "F4",
        color="green",
        via=[(640, 630), (640, 1040)],
        label_at=(675, 620),
    )
    d.arrow(
        895,
        1040,
        920,
        1040,
        "F5a",
        color="green",
        label_at=(908, 930),
    )
    d.arrow(
        1060,
        1130,
        1060,
        1170,
        "F5b",
        color="green",
        label_at=(1110, 1150),
    )
    d.arrow(
        1200,
        1280,
        475,
        1880,
        "F5c",
        color="green",
        via=[(1245, 1280), (1245, 1760), (475, 1760)],
        label_at=(1200, 1710),
    )
    d.arrow(
        1615,
        1680,
        390,
        1040,
        "F6",
        color="purple",
        status="future",
        via=[(1590, 1680), (1590, 1740), (275, 1740), (275, 1040)],
        label_at=(275, 1650),
    )
    d.arrow(
        510,
        1690,
        530,
        2240,
        "F7",
        color="teal",
        via=[(530, 1760)],
        label_at=(575, 1815),
    )
    d.arrow(
        775,
        1470,
        630,
        1600,
        "S1",
        color="orange",
        status="contract",
        label_at=(690, 1490),
    )
    d.arrow(
        475,
        2200,
        780,
        2240,
        "F8a",
        color="purple",
        status="future",
        label_at=(625, 2220),
    )
    d.arrow(
        945,
        2100,
        945,
        2240,
        "F8b",
        color="purple",
        status="future",
        label_at=(995, 2140),
    )
    d.arrow(
        1180,
        2140,
        1030,
        2240,
        "F8c",
        color="purple",
        status="future",
        via=[(1100, 2150)],
        label_at=(1095, 2190),
    )
    d.arrow(
        1060,
        2440,
        1615,
        2585,
        "F8d",
        color="purple",
        status="future",
        via=[(1080, 2145), (1590, 2145), (1590, 2585)],
        label_at=(1450, 2125),
    )
    d.arrow(
        775,
        1690,
        1430,
        1880,
        "F9",
        color="teal",
        via=[(775, 1760), (1430, 1760)],
        label_at=(1350, 1710),
    )
    d.arrow(
        510,
        1440,
        1615,
        1300,
        "F10 ↔",
        color="red",
        status="future",
        via=[
            (510, 1490),
            (1270, 1490),
            (1270, 1050),
            (1590, 1050),
            (1590, 1300),
        ],
        label_at=(1450, 1050),
        bidirectional=True,
    )

    return d


def rectangles_overlap(
    first: dict[str, object],
    second: dict[str, object],
    *,
    margin: int = 0,
) -> bool:
    return (
        int(first["x"]) < int(second["x"]) + int(second["width"]) + margin
        and int(first["x"]) + int(first["width"]) + margin > int(second["x"])
        and int(first["y"]) < int(second["y"]) + int(second["height"]) + margin
        and int(first["y"]) + int(first["height"]) + margin > int(second["y"])
    )


def validate_diagram_readability(diagram: Diagram) -> dict[str, float | int]:
    scale = GITHUB_PRESENTATION_WIDTH / WIDTH
    rectangles = {
        str(element["id"]): element
        for element in diagram.elements
        if element["type"] == "rectangle"
    }
    text_elements = [
        element for element in diagram.elements if element["type"] == "text"
    ]
    errors: list[str] = []

    for element in text_elements:
        element_id = str(element["id"])
        font_size = int(element["fontSize"])
        if font_size * scale < MIN_EFFECTIVE_FONT_SIZE:
            errors.append(
                f"{element_id} renders at {font_size * scale:.1f}px, below "
                f"{MIN_EFFECTIVE_FONT_SIZE}px"
            )
        for line in str(element["text"]).splitlines():
            width = estimated_text_width(
                line,
                font_size,
                str(element.get("customData", {}).get("fontWeight", "normal")),
            )
            if width > int(element["width"]):
                errors.append(
                    f"{element_id} line is {width:.1f}px wide in "
                    f"{element['width']}px: {line!r}"
                )

        x = int(element["x"])
        y = int(element["y"])
        right = x + int(element["width"])
        bottom = y + int(element["height"])
        if x < 0 or y < 0 or right > WIDTH or bottom > HEIGHT:
            errors.append(f"{element_id} leaves the {WIDTH}x{HEIGHT} canvas")

        container_id = element.get("containerId")
        if container_id:
            container = rectangles.get(str(container_id))
            if container is None:
                errors.append(f"{element_id} refers to missing container {container_id}")
            elif (
                x < int(container["x"])
                or y < int(container["y"])
                or right > int(container["x"]) + int(container["width"])
                or bottom > int(container["y"]) + int(container["height"])
            ):
                errors.append(f"{element_id} crosses container {container_id}")

    for element in diagram.elements:
        if element["type"] != "arrow":
            continue
        element_x = int(element["x"])
        element_y = int(element["y"])
        for point in list(element["points"]):
            abs_x = element_x + point[0]
            abs_y = element_y + point[1]
            if abs_x < 0 or abs_x > WIDTH or abs_y < 0 or abs_y > HEIGHT:
                errors.append(
                    f"Arrow {element['id']} point ({abs_x:.0f}, {abs_y:.0f}) "
                    f"is outside the {WIDTH}x{HEIGHT} canvas"
                )

        flow_labels = [
        element
        for element in rectangles.values()
        if element.get("customData", {}).get("diagramRole") == "flow-label"
    ]
    nodes = [
        element
        for element in rectangles.values()
        if element.get("customData", {}).get("diagramRole")
        not in {"boundary", "flow-label"}
    ]
    boundaries = [
        element
        for element in rectangles.values()
        if element.get("customData", {}).get("diagramRole") == "boundary"
    ]
    actual_text_rectangles: list[tuple[dict[str, object], dict[str, object]]] = []
    for element in text_elements:
        actual_width = math.ceil(
            max(
                (
                    estimated_text_width(
                        line,
                        int(element["fontSize"]),
                        str(
                            element.get("customData", {}).get(
                                "fontWeight",
                                "normal",
                            )
                        ),
                    )
                    for line in str(element["text"]).splitlines()
                ),
                default=0,
            )
        )
        align = str(element["textAlign"])
        x = int(element["x"])
        if align == "center":
            x += (int(element["width"]) - actual_width) // 2
        elif align == "right":
            x += int(element["width"]) - actual_width
        actual_text_rectangles.append(
            (
                element,
                {
                    "x": x,
                    "y": int(element["y"]),
                    "width": actual_width,
                    "height": int(element["height"]),
                },
            )
        )

    boundary_labels = [
        (element, bounds)
        for element, bounds in actual_text_rectangles
        if element.get("customData", {}).get("diagramRole") == "boundary-label"
    ]
    for boundary_label, bounds in boundary_labels:
        for node in nodes:
            if rectangles_overlap(bounds, node, margin=4):
                errors.append(
                    f"{boundary_label['id']} overlaps node/callout {node['id']}"
                )

    for flow_label in flow_labels:
        for node in nodes:
            if rectangles_overlap(flow_label, node, margin=2):
                errors.append(
                    f"{flow_label['id']} overlaps node/callout {node['id']}"
                )
        for boundary_label, bounds in boundary_labels:
            if rectangles_overlap(flow_label, bounds, margin=4):
                errors.append(
                    f"{flow_label['id']} overlaps boundary label "
                    f"{boundary_label['id']}"
                )
        for boundary in boundaries:
            if not rectangles_overlap(flow_label, boundary):
                continue
            fully_inside = (
                int(flow_label["x"]) >= int(boundary["x"]) + 2
                and int(flow_label["y"]) >= int(boundary["y"]) + 2
                and int(flow_label["x"]) + int(flow_label["width"])
                <= int(boundary["x"]) + int(boundary["width"]) - 2
                and int(flow_label["y"]) + int(flow_label["height"])
                <= int(boundary["y"]) + int(boundary["height"]) - 2
            )
            if not fully_inside:
                errors.append(
                    f"{flow_label['id']} crosses boundary {boundary['id']}"
                )

    for index, first in enumerate(flow_labels):
        for second in flow_labels[index + 1 :]:
            if rectangles_overlap(first, second, margin=2):
                errors.append(
                    f"{first['id']} overlaps flow label {second['id']}"
                )

    for index, (first, first_bounds) in enumerate(actual_text_rectangles):
        for second, second_bounds in actual_text_rectangles[index + 1 :]:
            if rectangles_overlap(first_bounds, second_bounds, margin=2):
                errors.append(
                    f"{first['id']} text overlaps {second['id']} text"
                )

    if errors:
        raise ValueError("Diagram readability geometry failed:\n- " + "\n- ".join(errors))
    return {
        "presentation_width": GITHUB_PRESENTATION_WIDTH,
        "scale": scale,
        "minimum_source_font": min(
            int(element["fontSize"]) for element in text_elements
        ),
        "minimum_effective_font": min(
            int(element["fontSize"]) * scale for element in text_elements
        ),
        "contained_labels": sum(
            1 for element in text_elements if element.get("containerId")
        ),
        "overflow_count": 0,
    }


def readability_summary(report: dict[str, float | int]) -> str:
    return (
        f"Readability at {report['presentation_width']}px: "
        f"{report['scale']:.3f} scale, "
        f"{report['minimum_effective_font']:.1f}px minimum text, "
        f"{report['contained_labels']} contained labels, "
        f"{report['overflow_count']} geometry overflows."
    )


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
  six labelled trust boundaries, ten numbered data flows, a semantic provenance flow, local data custody,
  and extension seams.</desc>
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


def validate_png_policy(chunks: list[tuple[bytes, bytes]], *, require_provenance: bool) -> None:
    chunk_types = [chunk_type for chunk_type, _ in chunks]
    disallowed = sorted(set(chunk_types) - PNG_ALLOWED_CHUNK_TYPES)
    if disallowed:
        names = ", ".join(chunk.decode("ascii", errors="replace") for chunk in disallowed)
        raise ValueError(f"Non-allowlisted PNG chunk: {names}")
    if chunk_types[0] != b"IHDR" or chunk_types.count(b"IHDR") != 1:
        raise ValueError("PNG policy requires exactly one leading IHDR.")
    if len(chunks[0][1]) != 13:
        raise ValueError("PNG policy requires the IHDR payload to be exactly 13 bytes.")
    if chunk_types[-1] != b"IEND" or chunk_types.count(b"IEND") != 1:
        raise ValueError("PNG policy requires exactly one trailing IEND.")
    if chunks[-1][1]:
        raise ValueError("PNG policy requires the IEND payload to be empty.")
    idat_indices = [index for index, chunk_type in enumerate(chunk_types) if chunk_type == b"IDAT"]
    if not idat_indices:
        raise ValueError("PNG policy requires IDAT data.")
    if idat_indices != list(range(idat_indices[0], idat_indices[-1] + 1)):
        raise ValueError("PNG policy requires contiguous IDAT chunks.")

    metadata: dict[str, str] = {}
    for chunk_type, data in chunks:
        if chunk_type != b"tEXt":
            continue
        if b"\0" not in data:
            raise ValueError("PNG policy requires keyword/value tEXt chunks.")
        keyword_bytes, value_bytes = data.split(b"\0", 1)
        try:
            keyword = keyword_bytes.decode("ascii")
            value = value_bytes.decode("ascii")
        except UnicodeDecodeError:
            raise ValueError("PNG provenance tEXt must be ASCII.") from None
        if keyword not in PNG_METADATA_KEYS.values():
            raise ValueError(f"Non-allowlisted PNG tEXt keyword: {keyword}")
        if keyword in metadata:
            raise ValueError(f"Duplicate PNG provenance tEXt keyword: {keyword}")
        if len(value) != 64 or any(character not in "0123456789abcdef" for character in value):
            raise ValueError(f"Invalid PNG provenance SHA-256 for {keyword}")
        metadata[keyword] = value

    expected_keywords = set(PNG_METADATA_KEYS.values())
    if require_provenance and set(metadata) != expected_keywords:
        missing = ", ".join(sorted(expected_keywords - set(metadata)))
        raise ValueError(f"PNG policy is missing provenance tEXt: {missing}")
    if not require_provenance and metadata and set(metadata) != expected_keywords:
        raise ValueError("PNG policy permits either no provenance or the exact provenance set.")


def png_raster_hash(chunks: list[tuple[bytes, bytes]]) -> str:
    raster_chunks = {b"IHDR", b"IDAT"}
    material = b"".join(chunk_type + data for chunk_type, data in chunks if chunk_type in raster_chunks)
    if not material:
        raise ValueError("PNG contains no raster data.")
    return hashlib.sha256(material).hexdigest()


def png_binding_hash(source_hash: str, svg_hash: str, raster_hash: str) -> str:
    material = bytes.fromhex(source_hash) + bytes.fromhex(svg_hash) + bytes.fromhex(raster_hash)
    return hashlib.sha256(material).hexdigest()


def embed_png_provenance(path: Path, source_hash: str, svg_hash: str) -> None:
    chunks = png_chunks(path.read_bytes())
    validate_png_policy(chunks, require_provenance=False)
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


def verify_png_bytes(data: bytes, source_hash: str, svg_hash: str) -> str:
    chunks = png_chunks(data)
    validate_png_policy(chunks, require_provenance=True)
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


def verify_png_artifact(data: bytes, manifest: str, source_hash: str, svg_hash: str) -> None:
    verify_png_bytes(data, source_hash, svg_hash)
    actual_png_hash = hashlib.sha256(data).hexdigest()
    expected_hash_line = f"{actual_png_hash}  {PNG_PATH.name}\n"
    if manifest != expected_hash_line:
        raise ValueError(f"full PNG SHA-256 does not match {PNG_HASH_PATH.name}")


def render_svg_png(output_path: Path) -> None:
    STAGING_PNG_PATH.unlink(missing_ok=True)
    edge = find_edge()
    command = [
        str(edge),
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--force-device-scale-factor=1",
        f"--window-size={WIDTH},{HEIGHT}",
        f"--screenshot={STAGING_PNG_PATH}",
        SVG_PATH.resolve().as_uri(),
    ]
    try:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, timeout=60)
    except subprocess.TimeoutExpired:
        STAGING_PNG_PATH.unlink(missing_ok=True)
        raise RuntimeError("Edge PNG render timed out after 60 seconds.") from None
    if result.returncode != 0 or not STAGING_PNG_PATH.exists():
        STAGING_PNG_PATH.unlink(missing_ok=True)
        raise RuntimeError(f"Edge PNG render failed: {result.stderr.strip()}")
    STAGING_PNG_PATH.replace(output_path)


def render_png(source_hash: str, svg_hash: str) -> None:
    original_png = PNG_PATH.read_bytes() if PNG_PATH.exists() else None
    original_hash = PNG_HASH_PATH.read_bytes() if PNG_HASH_PATH.exists() else None
    try:
        render_svg_png(CHECK_PNG_PATH)
        embed_png_provenance(CHECK_PNG_PATH, source_hash, svg_hash)
        png_hash = hashlib.sha256(CHECK_PNG_PATH.read_bytes()).hexdigest()
        CHECK_PNG_PATH.replace(PNG_PATH)
        PNG_HASH_PATH.write_text(f"{png_hash}  {PNG_PATH.name}\n", encoding="ascii", newline="\n")
    except OSError as error:
        if original_png is None:
            PNG_PATH.unlink(missing_ok=True)
        else:
            PNG_PATH.write_bytes(original_png)
        if original_hash is None:
            PNG_HASH_PATH.unlink(missing_ok=True)
        else:
            PNG_HASH_PATH.write_bytes(original_hash)
        raise RuntimeError(f"Unable to replace PNG artifacts atomically: {error}") from None
    finally:
        CHECK_PNG_PATH.unlink(missing_ok=True)
        STAGING_PNG_PATH.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail when generated source/render files drift.")
    parser.add_argument("--render-png", action="store_true", help="Render the SVG to PNG with Microsoft Edge.")
    args = parser.parse_args()

    diagram = build_diagram()
    readability = validate_diagram_readability(diagram)
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
                committed_png = PNG_PATH.read_bytes()
                if not PNG_HASH_PATH.exists():
                    raise ValueError(f"missing {PNG_HASH_PATH.relative_to(ROOT)}")
                verify_png_artifact(
                    committed_png,
                    PNG_HASH_PATH.read_text(encoding="ascii"),
                    source_hash,
                    svg_hash,
                )
            except (FileNotFoundError, RuntimeError, ValueError) as error:
                failures.append(f"{PNG_PATH.relative_to(ROOT)} ({error})")
            finally:
                CHECK_PNG_PATH.unlink(missing_ok=True)
                STAGING_PNG_PATH.unlink(missing_ok=True)
        if failures:
            print("Architecture artifacts are stale: " + ", ".join(failures), file=sys.stderr)
            return 1
        print(f"Architecture artifacts are consistent (source SHA-256 {source_hash}).")
        print(readability_summary(readability))
        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    EXCALIDRAW_PATH.write_bytes(source)
    SVG_PATH.write_bytes(svg)
    if args.render_png:
        try:
            render_png(source_hash, svg_hash)
        except (FileNotFoundError, RuntimeError, ValueError) as error:
            print(f"Unable to render {PNG_PATH.relative_to(ROOT)}: {error}", file=sys.stderr)
            return 1
    print(f"Wrote {EXCALIDRAW_PATH.relative_to(ROOT)} and {SVG_PATH.relative_to(ROOT)}.")
    if args.render_png:
        print(f"Wrote {PNG_PATH.relative_to(ROOT)} ({WIDTH}x{HEIGHT}).")
    print(readability_summary(readability))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
