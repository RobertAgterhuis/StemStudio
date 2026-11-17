"""Checks that the 014 backend contract contains the older one ("newer contains older").

Usage (repo root):
``python -I specs/014-practice-flow/contracts/check_superset.py [older] [newer]``
Defaults: older = specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml (1.23.0, the
full document 014 was re-based onto at the 014+015 round start, DELTA.md 8), newer =
specs/014-practice-flow/contracts/backend-api.openapi.yaml (1.13.0 = 1.23.0 + the 014 delta). Phase 0 also runs it with the delta as ``older``
(``practice-api.openapi.yaml``: every delta entry is in 1.13.0 unchanged, except the shared
components listed in ``SHARED``). (1.12.0 as ``older`` no longer passes: 022/023 extend
``Asset`` and the lyrics schemas, which ``merge_full.py --check`` allows.)

Rules: every path, operation, parameter, header, response, example and schema of the older
document exists unchanged in the newer one, except the entries in ``ALLOWED``, which may only
gain properties (never lose or change one). Requires PyYAML. Exit code 0 = superset.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any

import yaml

OLDER = Path("specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml")
NEWER = Path("specs/014-practice-flow/contracts/backend-api.openapi.yaml")
# 1.13.0 may only add to these: `LoopRegion.sectionId`.
ALLOWED = {
    ("schemas", "LoopRegion"),
}
# Delta-only copies of components that already exist in the full document (DELTA.md 1); they are
# skipped when the delta is the ``older`` document.
SHARED = {
    ("schemas", "ChannelId"),
    ("schemas", "ScoreProfile"),
    ("responses", "NotFound"),
    ("responses", "ValidationProblem"),
    ("responses", "WorkerUnavailable"),
}
OLDER_ARG = 1
NEWER_ARG = 2
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")


def load(path: Path) -> dict[str, Any]:
    """Parses one OpenAPI document."""
    with path.open(encoding="utf-8") as handle:
        document: dict[str, Any] = yaml.safe_load(handle)
        return document


def grows(old: Any, new: Any) -> bool:
    """True when ``new`` equals ``old`` or only adds mapping keys (recursively)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key in new and grows(value, new[key]) for key, value in old.items())
    return bool(old == new)


def problems(older: dict[str, Any], newer: dict[str, Any]) -> list[str]:
    """Lists every violation of the superset rule."""
    found: list[str] = []
    skip = SHARED if "-delta" in str(older["info"]["version"]) else set()
    pairs = [("paths", older["paths"], newer["paths"])]
    pairs += [(s, older["components"].get(s, {}), newer["components"].get(s, {})) for s in SECTIONS]
    for section, old_items, new_items in pairs:
        for name, value in old_items.items():
            if (section, name) in skip:
                continue
            check = grows if (section, name) in ALLOWED else (lambda a, b: bool(a == b))
            if name not in new_items or not check(value, new_items[name]):
                found.append(f"{section}/{name}")
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
