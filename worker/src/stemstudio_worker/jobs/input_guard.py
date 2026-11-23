"""The check of a job's library input paths (400 / 404 at creation, again when the job runs).

A path must pass the job kind's own allow-list first (a regular expression per kind, so ``..``,
absolute paths and backslashes never match), then a path guard that resolves it inside the real
library root: the library reader's guard (render, practice and quality inputs, which are on the
general allow-list) or :func:`resolve_contained` (handout inputs, which are on their own per-job
allow-list only, data-model 2.2). A library that is not readable now is not an error at
creation: the job fails readably when it runs.
"""

from collections.abc import Callable
from pathlib import Path

from stemstudio_worker.ports import InputNotFoundError, InputPathError, RenderError
from stemstudio_worker.schemas import LIBRARY_UNAVAILABLE_TEXT

Allowed = Callable[[str], object]
"""Truthy for a path the job kind accepts (``re.Pattern.fullmatch``, ``is_quality_input_path``)."""

Resolver = Callable[[str], Path]
"""Library path -> real file (``LibraryReader.resolve`` or a :func:`resolve_contained` partial)."""


def _not_allowed(path: str) -> InputPathError:
    return InputPathError(f"Input path is not allowed: {path}")


def check_input(resolve: Resolver, path: str, allowed: Allowed) -> None:
    """Require ``allowed(path)`` and pass the path guard ``resolve``.

    Raises:
        InputPathError: Not allowed for this job kind, not on the allow-list or outside the root.
        InputNotFoundError: The file does not exist.
    """
    if not allowed(path):
        raise _not_allowed(path)
    try:
        resolve(path)
    except (InputPathError, InputNotFoundError):
        raise
    except RenderError:
        return


def resolve_contained(root: Path | None, path: str) -> Path:
    """The real regular file of the (already allowed) ``path`` inside the real ``root``.

    Raises:
        InputPathError: The real path, after following links, leaves the root.
        InputNotFoundError: Nothing exists there, or it is not a regular file.
        RenderError: No library is mounted (``LIBRARY_UNAVAILABLE_TEXT``).
    """
    if root is None or not root.is_dir():
        raise RenderError(LIBRARY_UNAVAILABLE_TEXT)
    base = root.resolve()
    target = base / path
    if not target.exists():
        raise InputNotFoundError(path)
    real = target.resolve()
    if not real.is_relative_to(base):
        raise _not_allowed(path)
    if not real.is_file():
        raise InputNotFoundError(path)
    return real
