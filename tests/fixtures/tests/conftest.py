"""Shared fixtures of the generator tests: each test module names its ``GENERATOR`` module."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest


@pytest.fixture(scope="module")
def generated(
    request: pytest.FixtureRequest, tmp_path_factory: pytest.TempPathFactory
) -> tuple[Path, dict[str, Any]]:
    """Runs the module's ``GENERATOR`` once into a temp folder; returns (folder, manifest)."""
    generator = request.module.GENERATOR
    out_dir = tmp_path_factory.mktemp("audio")
    manifest_path = out_dir / "manifest.json"
    generator.generate(out_dir, manifest_path)
    manifest: dict[str, Any] = json.loads(manifest_path.read_text(encoding="utf-8"))
    return out_dir, manifest
