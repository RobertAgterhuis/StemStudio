"""Helpers that write small fake repository trees below ``tmp_path`` for the check tests."""

from pathlib import Path

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
