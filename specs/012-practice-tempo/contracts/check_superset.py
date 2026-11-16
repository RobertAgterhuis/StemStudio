"""Checks that the 012 backend contract contains the older one ("newer contains older").

Usage (repo root):
``python -I specs/012-practice-tempo/contracts/check_superset.py [older] [newer]``
Defaults: older = specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml (1.9.0),
newer = specs/012-practice-tempo/contracts/backend-api.openapi.yaml.

Rules: every path, operation, parameter, header, response, example and schema of the older
document exists unchanged in the newer one, except the entries in ``ALLOWED``, which may only
gain properties (never lose or change one). Requires PyYAML. Exit code 0 = superset.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any

import yaml

OLDER = Path("specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml")
NEWER = Path("specs/012-practice-tempo/contracts/backend-api.openapi.yaml")
ALLOWED = {("schemas", "MixSettings"), ("paths", "/api/mixes/{mixId}")}
OLDER_ARG = 1
NEWER_ARG = 2
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")


def load(path: Path) -> dict[str, Any]:
    """Parses one OpenAPI document."""
    with path.open(encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def grows(old: Any, new: Any) -> bool:
    """True when ``new`` equals ``old`` or only adds mapping keys (recursively)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key in new and grows(value, new[key]) for key, value in old.items())
    return old == new


def problems(older: dict[str, Any], newer: dict[str, Any]) -> list[str]:
    """Lists every violation of the superset rule."""
    found: list[str] = []
    pairs = [("paths", older["paths"], newer["paths"])]
    pairs += [(s, older["components"].get(s, {}), newer["components"].get(s, {})) for s in SECTIONS]
    for section, old_items, new_items in pairs:
        for name, value in old_items.items():
            check = grows if (section, name) in ALLOWED else (lambda a, b: a == b)
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
