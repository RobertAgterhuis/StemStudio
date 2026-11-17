"""Checks lyric-assist-output 1.0.0 and the scripted outputs that use it (023 Phase 0, T002).

Usage (repo root): ``python -I specs/023-lyric-writing-assistant/contracts/check_output_schema.py``

1. The schema is valid JSON Schema 2020-12 (meta-schema).
2. Every block of every ``e2e/lyric-scripts/*.json`` script (format DELTA.md section 4) validates.
3. Built-in samples: the valid ones validate, the invalid ones are rejected.

Requires ``jsonschema`` (4.x). Exit code 0 = all checks passed.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator

SCHEMA = Path("specs/023-lyric-writing-assistant/contracts/lyric-assist-output.schema.json")
SCRIPTS = Path("e2e/lyric-scripts")
ITEM = {
    "type": "spelling.typo",
    "severity": "error",
    "line": 3,
    "original": "wiet",
    "proposal": "weet",
    "explanation": "Spelfout.",
}
VALID: list[dict[str, Any]] = [
    {"items": []},
    {"items": [ITEM], "misses": [{"line": 2, "word": "Zelmara", "verdict": "keep"}]},
    {
        "items": [],
        "misses": [
            {
                "line": 1,
                "word": "effe",
                "verdict": "licence",
                "licenceType": "slang",
                "correction": None,
            }
        ],
    },
]
INVALID: list[dict[str, Any]] = [
    {},
    {"items": [], "extra": 1},
    {"items": [{**ITEM, "type": "spelling.unknown-word"}]},
    {"items": [{**ITEM, "explanation": ""}]},
    {"items": [{**ITEM, "original": "x" * 401}]},
    {"items": [{**ITEM, "tool": "delete_track"}]},
    {"items": [ITEM] * 41},
    {"items": [], "misses": [{"line": 1, "word": "x", "verdict": "maybe"}]},
]


def load(path: Path) -> Any:
    """Parses one JSON file."""
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def script_errors(validator: Draft202012Validator) -> list[str]:
    """Errors of the scripted outputs (empty when every block validates)."""
    errors: list[str] = []
    for path in sorted(SCRIPTS.glob("*.json")):
        script = load(path)
        if script.get("version") != 1 or not isinstance(script.get("blocks"), list):
            errors.append(f"{path}: not a version-1 script")
            continue
        for index, block in enumerate(script["blocks"]):
            errors += [
                f"{path} block {index}: {error.message}" for error in validator.iter_errors(block)
            ]
    return errors


def sample_errors(validator: Draft202012Validator) -> list[str]:
    """Errors of the built-in samples."""
    errors = [
        f"valid sample {i} rejected"
        for i, sample in enumerate(VALID)
        if not validator.is_valid(sample)
    ]
    return errors + [
        f"invalid sample {i} accepted"
        for i, sample in enumerate(INVALID)
        if validator.is_valid(sample)
    ]


def main() -> int:
    """Runs the checks and prints the result."""
    schema = load(SCHEMA)
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)
    errors = script_errors(validator) + sample_errors(validator)
    scripts = len(list(SCRIPTS.glob("*.json")))
    summary = f"samples: {len(VALID)} valid, {len(INVALID)} invalid; errors: {len(errors)}"
    lines = [*errors, f"meta-schema: valid; scripts: {scripts}; {summary}"]
    sys.stdout.write("\n".join(lines) + "\n")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
