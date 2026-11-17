"""Helpers that write small fake repository trees below ``tmp_path`` for the check tests."""

from collections.abc import Mapping
from pathlib import Path

import dependency_check
from common import Severity

WORKER_PACKAGE = "worker/src/stemstudio_worker"
"""Repository-relative path of the worker package."""


def write_file(root: Path, rel: str, text: str) -> Path:
    """Write ``text`` to ``root / rel`` (creating parent folders) and return the path."""
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def write_module(root: Path, rel: str, text: str) -> Path:
    """Write a worker module ``rel`` (relative to the worker package) and return the path."""
    return write_file(root, f"{WORKER_PACKAGE}/{rel}", text)


GENERATOR_PACKAGE = "generator/src/stemstudio_generator"
"""Repository-relative path of the generator package (features 009/010)."""


def write_generator_module(root: Path, rel: str, text: str) -> Path:
    """Write a generator module ``rel`` (relative to the generator package); return the path."""
    return write_file(root, f"{GENERATOR_PACKAGE}/{rel}", text)


def write_generator_tree(root: Path, tree: Mapping[str, str]) -> None:
    """Write every generator module of ``tree`` (package-relative path -> source)."""
    for rel, text in tree.items():
        write_generator_module(root, rel, text)


API_PROJECT = "src/StemStudio.Api"
"""Repository-relative path of the API project."""


def api_findings(root: Path, rel: str, text: str) -> list[tuple[Severity, str, int, str]]:
    """Write ``src/StemStudio.Api/<rel>``, run the dependency check below ``root``.

    Returns (severity, message, line, owner) per finding.
    """
    write_file(root, f"{API_PROJECT}/{rel}", text)
    return [(f.severity, f.message, f.line, f.owner) for f in dependency_check.run(root)]


def src_findings(root: Path, rel: str, text: str) -> list[tuple[Severity, str, int, str]]:
    """Write ``src/<rel>``, run the dependency check below ``root``.

    Like ``api_findings`` for any project folder (``StemStudio.Domain/Library/X.cs``); returns
    (severity, message, line, owner) per finding.
    """
    write_file(root, f"src/{rel}", text)
    return [(f.severity, f.message, f.line, f.owner) for f in dependency_check.run(root)]
