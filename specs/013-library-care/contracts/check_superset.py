"""Checks that the 013 backend contract contains the older one ("newer contains older").

Usage (repo root):
``python -I specs/013-library-care/contracts/check_superset.py [older] [newer]``
Defaults: older = specs/012-practice-tempo/contracts/backend-api.openapi.yaml (1.11.0, the full
document on ``main`` before 013), newer = specs/013-library-care/contracts/backend-api.openapi.yaml
(1.12.0 = 1.11.0 + the 013 delta). Phase 0 also runs it with the delta as ``older`` (every delta
entry is in 1.12.0 unchanged, except the shared components listed in ``SHARED``).

Rules: every path, operation, parameter, header, response, example and schema of the older
document exists unchanged in the newer one, except the entries in ``ALLOWED``, which may only
gain properties (never lose or change one). Requires PyYAML. Exit code 0 = superset.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any

import yaml

OLDER = Path("specs/012-practice-tempo/contracts/backend-api.openapi.yaml")
NEWER = Path("specs/013-library-care/contracts/backend-api.openapi.yaml")
# 1.12.0 may only add to these: `DetailPhase.filesRemovedAt` and the write-gate responses
# (`x-stemstudio-write-gate`, `409 BackupRunning`, `503 Maintenance`) of the gated operations.
ALLOWED = {
    ("schemas", "DetailPhase"),
    ("paths", "/api/tracks/{trackId}"),
    ("paths", "/api/tracks/{trackId}/retry"),
    ("paths", "/api/tracks/{trackId}/artwork"),
    ("paths", "/api/assets/{assetId}"),
    ("paths", "/api/generations/{generationId}"),
    ("paths", "/api/voice-profiles/{profileId}"),
    ("paths", "/api/styles/{styleId}"),
    ("paths", "/api/lyrics/{lyricsId}"),
    ("paths", "/api/agent/conversations/{conversationId}"),
}
# Delta-only definitions of components that already exist in the full document (DELTA.md 1) or
# that only carry a merge note; they are skipped when the delta is the ``older`` document.
SHARED = {
    ("responses", "NotFound"),
    ("responses", "ValidationProblem"),
    ("schemas", "ProblemDetails"),
    ("schemas", "HttpValidationProblemDetails"),
    ("schemas", "DetailPhaseAddition"),
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
