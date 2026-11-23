"""Per-family handout fixtures (qa, feature 020, T028/T029, SC-006): stub, implemented in T029."""

from __future__ import annotations

from collections.abc import Sequence
from pathlib import Path
from typing import Any


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write the setlist songs below ``out_dir`` and the manifest; return the manifest (stub)."""
    del out_dir, manifest_path
    return {}


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point (stub)."""
    del argv
    return 1
