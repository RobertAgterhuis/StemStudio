"""Checks that the 015 backend contract contains the older one ("newer contains older").

Usage (repo root):
``python -I specs/015-play-along-recording/contracts/check_superset.py [older] [newer]``
Defaults: older = specs/014-practice-flow/contracts/backend-api.openapi.yaml (014's 1.13.0 = 023's 1.23.0 +
014, the base of the round-start re-merge; 013's 1.12.0 in 015 Phase 0), newer =
specs/015-play-along-recording/contracts/backend-api.openapi.yaml (1.14.0 = 1.13.0 + the 015 delta). Phase 0 also runs it with the delta as ``older``: every delta path and new
component is in 1.14.0 unchanged, except the renamed responses (``RENAMED`` in merge_recording_delta.py),
the write gate on three take operations and the shared components / merge fragments in ``SHARED``.

Rules: every path, operation, parameter, header, response, example and schema of the older document exists
in the newer one unchanged, except the entries in ``ALLOWED``, which may only grow: mappings gain keys,
lists gain items (the old items stay, in order), descriptions gain text at the end or a new description is
added. The export channel lists may reference ``ExportChannelId`` instead of ``ChannelId`` (checked by sample values). Requires PyYAML.
Exit code 0 = superset.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path
from typing import Any

import yaml

OLDER = Path("specs/014-practice-flow/contracts/backend-api.openapi.yaml")
NEWER = Path("specs/015-play-along-recording/contracts/backend-api.openapi.yaml")
# 1.14.0 may only add to these (DELTA.md 2).
ALLOWED = {
    ("schemas", "AssetOrigin"),
    ("schemas", "Provenance"),
    ("schemas", "CreateAudioExportRequest"),
    ("schemas", "MixSettings"),
    ("schemas", "Track"),
    ("schemas", "ExportRecipe"),
    ("paths", "/api/assets"),
    ("paths", "/api/assets/{assetId}"),
    ("paths", "/api/assets/{assetId}/retry"),
}
# The export channel lists of 1.14.0 reference ``ExportChannelId`` (= ``ChannelId`` or ``take``) instead of ``ChannelId``;
# the reference is compared as the old one, and ``ExportChannelId`` must accept every old sample, ``take``, and refuse the bad ones.
WIDER_REF = ("#/components/schemas/ExportChannelId", "#/components/schemas/ChannelId")
SAMPLES = (["vocals", "drums.kick", "other.acoustic-guitar", "bass"], ["take"], ["takes", "take.x", "Take", "kick"])
# Delta definitions that the merged document keeps from the base, renames or extends (delta as ``older``).
SHARED = {
    ("responses", "NotFound"),
    ("schemas", "ProblemDetails"),
    ("schemas", "ValidationProblemDetails"),
}
# Delta paths that gain the 1.12.0 write gate in the merged document (they may only grow).
GATED_PATHS = {("paths", "/api/tracks/{trackId}/takes/delete"), ("paths", "/api/takes/{takeId}"), ("paths", "/api/takes/{takeId}/audio")}
# Response names of the delta that the merged document calls ``Take*`` (merge_recording_delta.py RENAMED).
RENAMED = {
    "BadRequest": "TakeBadRequest",
    "Conflict": "TakeConflict",
    "PayloadTooLarge": "TakePayloadTooLarge",
    "UnsupportedMediaType": "TakeUnsupportedMediaType",
    "Unprocessable": "TakeUnprocessable",
    "WorkerUnavailable": "TakeWorkerUnavailable",
}
OLDER_ARG = 1
NEWER_ARG = 2
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")


def load(path: Path) -> dict[str, Any]:
    """Parses one OpenAPI document; a delta is read with its responses renamed as in the merged document."""
    text = path.read_text(encoding="utf-8")
    document: dict[str, Any] = yaml.safe_load(text)
    if "-delta" not in str(document["info"]["version"]):
        return document
    for old, new in RENAMED.items():
        text = text.replace(f"'#/components/responses/{old}'", f"'#/components/responses/{new}'")
    renamed: dict[str, Any] = yaml.safe_load(text)
    responses = renamed["components"].get("responses", {})
    renamed["components"]["responses"] = {RENAMED.get(name, name): value for name, value in responses.items()}
    return renamed


def grows(old: Any, new: Any, key: str = "") -> bool:
    """True when ``new`` equals ``old`` or only grows it (see the module docstring)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(name in new and grows(value, new[name], name) for name, value in old.items())
    if isinstance(old, list) and isinstance(new, list):
        rest = iter(new)
        return all(any(grows(item, candidate) for candidate in rest) for item in old)
    if key == "description" and isinstance(old, str) and isinstance(new, str):
        return new.startswith(old.rstrip())
    return bool(old == new)


def narrowed_refs(node: Any) -> Any:
    """``node`` with the wider export channel reference read as the old one."""
    if isinstance(node, dict):
        return {key: narrowed_refs(value) for key, value in node.items()}
    if isinstance(node, list):
        return [narrowed_refs(value) for value in node]
    return WIDER_REF[1] if node == WIDER_REF[0] else node


def export_channel_widens(older: dict[str, Any], newer: dict[str, Any]) -> bool:
    """True when ``ExportChannelId`` (if present) accepts every ``ChannelId`` sample plus ``take`` and refuses the bad ones."""
    wider = newer["components"]["schemas"].get("ExportChannelId")
    base = older["components"]["schemas"].get("ChannelId")
    if wider is None or base is None:
        return True
    olds, news, bads = SAMPLES
    old_rx, new_rx = re.compile(base["pattern"]), re.compile(wider["pattern"])
    keeps = all(old_rx.match(s) and new_rx.match(s) for s in olds)
    adds = all(new_rx.match(s) and not old_rx.match(s) for s in news)
    return keeps and adds and not any(new_rx.match(s) for s in bads)


def compatible(entry: tuple[str, str], old: Any, new: Any) -> bool:
    """Applies the rule for one entry."""
    return grows(old, narrowed_refs(new)) if entry in ALLOWED or entry in GATED_PATHS else bool(old == new)


def problems(older: dict[str, Any], newer: dict[str, Any]) -> list[str]:
    """Lists every violation of the superset rule."""
    found: list[str] = []
    skip = SHARED if "-delta" in str(older["info"]["version"]) else set()
    pairs = [("paths", older["paths"], newer["paths"])]
    pairs += [(s, older["components"].get(s, {}), newer["components"].get(s, {})) for s in SECTIONS]
    for section, old_items, new_items in pairs:
        for name, value in old_items.items():
            entry = (section, name)
            if entry in skip or name.endswith("Additions"):
                continue
            if name not in new_items or not compatible(entry, value, new_items[name]):
                found.append(f"{section}/{name}")
    if not export_channel_widens(older, newer):
        found.append("schemas/ExportChannelId (must widen ChannelId by take only)")
    return found


def main(argv: list[str]) -> int:
    """Entry point; prints the result."""
    older = load(Path(argv[OLDER_ARG]) if len(argv) > OLDER_ARG else OLDER)
    newer = load(Path(argv[NEWER_ARG]) if len(argv) > NEWER_ARG else NEWER)
    found = problems(older, newer)
    for item in found:
        sys.stdout.write(f"changed or missing: {item}\n")
    sys.stdout.write("superset: yes\n" if not found else f"superset: no ({len(found)} problems)\n")
    return 0 if not found else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
