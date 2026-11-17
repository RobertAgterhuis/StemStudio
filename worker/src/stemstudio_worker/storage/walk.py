"""Size of a file tree without following links and without reading file contents (feature 013)."""

import os
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True, slots=True)
class Usage:
    """Bytes and number of regular files.

    Attributes:
        bytes: Sum of the file sizes.
        files: Number of regular files.
    """

    bytes: int = 0
    files: int = 0

    def __add__(self, other: "Usage") -> "Usage":
        """The usage of both."""
        return Usage(self.bytes + other.bytes, self.files + other.files)


def tree_usage(root: Path) -> Usage:
    """Usage of ``root``: a regular file, a directory tree, or nothing (missing).

    Symbolic links (and junctions) are neither followed nor counted.
    """
    if root.is_symlink() or root.is_junction() or not root.exists():
        return Usage()
    if root.is_file():
        return Usage(root.stat().st_size, 1)
    return _directory_usage(root)


def trees_usage(roots: list[Path]) -> Usage:
    """Sum of :func:`tree_usage` over ``roots``."""
    total = Usage()
    for root in roots:
        total += tree_usage(root)
    return total


def _directory_usage(root: Path) -> Usage:
    total, pending = Usage(), [root]
    while pending:
        total += _scan(pending.pop(), pending)
    return total


def _scan(directory: Path, pending: list[Path]) -> Usage:
    """Usage of the regular files directly in ``directory``; sub-directories go to ``pending``."""
    total = Usage()
    try:
        entries = list(os.scandir(directory))
    except OSError:
        return total
    for entry in entries:
        total += _entry(entry, pending)
    return total


def _entry(entry: os.DirEntry[str], pending: list[Path]) -> Usage:
    """A regular file's usage; a real sub-directory goes to ``pending``; links count nothing."""
    if entry.is_dir(follow_symlinks=False) and not entry.is_junction():
        pending.append(Path(entry.path))
    elif entry.is_file(follow_symlinks=False):
        return Usage(entry.stat(follow_symlinks=False).st_size, 1)
    return Usage()
