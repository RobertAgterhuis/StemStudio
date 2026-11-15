"""Path guard over the read-only library mount (data-model.md 2.1, ADR-016 D5)."""

import os
from pathlib import Path

from stemstudio_worker.ports import (
    InputNotFoundError,
    InputPathError,
    RenderError,
    is_allowed_library_path,
)
from stemstudio_worker.schemas import LIBRARY_UNAVAILABLE_TEXT, LibraryHealth


class MountedLibrary:
    """``LibraryReader`` over the API data directory mounted read-only into the worker.

    A path is accepted only when it fully matches the allow-list (``ports.LIBRARY_PATH_PATTERNS``,
    which already excludes ``..``, absolute paths and backslashes) and its real path, after
    following links, stays inside the real library root. Nothing is ever written.
    """

    def __init__(self, root: Path | None) -> None:
        """Create the reader.

        Args:
            root: ``WorkerSettings.render.library_dir``; None when ``STEMSTUDIO_LIBRARY_DIR`` is
                unset.
        """
        self._root = root

    def status(self) -> LibraryHealth:
        """Report whether the library root exists and is listable (checked on every call)."""
        if self._listable_root() is None:
            return LibraryHealth(readable=False, reason=LIBRARY_UNAVAILABLE_TEXT)
        return LibraryHealth(readable=True, reason=None)

    def resolve(self, path: str) -> Path:
        """Check a library-relative input path and return the real file path.

        Args:
            path: Path from ``RenderInput.path``.

        Returns:
            The resolved file inside the library root.

        Raises:
            InputPathError: Not on the allow-list, or the real path escapes the root.
            InputNotFoundError: The file does not exist (or is not a regular file).
            RenderError: The library root is unset or unreadable (``LIBRARY_UNAVAILABLE_TEXT``).
        """
        if not is_allowed_library_path(path):
            raise InputPathError(f"Input path is not allowed: {path}")
        root = self._listable_root()
        if root is None:
            raise RenderError(LIBRARY_UNAVAILABLE_TEXT)
        return _contained_file(root, path)

    def _listable_root(self) -> Path | None:
        """Return the real root when it is set and can be listed, else None."""
        if self._root is None:
            return None
        try:
            with os.scandir(self._root):
                pass
        except OSError:
            return None
        return self._root.resolve()


def _contained_file(root: Path, path: str) -> Path:
    """Resolve ``root/path`` strictly and require a regular file inside ``root``."""
    try:
        real = (root / path).resolve(strict=True)
    except FileNotFoundError:
        raise InputNotFoundError(path) from None
    if not real.is_relative_to(root):
        raise InputPathError(f"Input path is not allowed: {path}")
    if not real.is_file():
        raise InputNotFoundError(path)
    return real
