"""The check of a job's library input paths when the job is created (400 / 404).

A path must pass the job kind's own allow-list first (a regular expression per kind, so ``..``,
absolute paths and backslashes never match), then the library's path guard. A library that is not
readable now is not an error at creation: the job fails readably when it runs.
"""

from collections.abc import Callable

from stemstudio_worker.ports import InputNotFoundError, InputPathError, LibraryReader, RenderError

Allowed = Callable[[str], object]
"""Truthy for a path the job kind accepts (``re.Pattern.fullmatch``, ``is_quality_input_path``)."""


def check_input(library: LibraryReader, path: str, allowed: Allowed) -> None:
    """Require ``allowed(path)`` and pass the library's path guard.

    Raises:
        InputPathError: Not allowed for this job kind, not on the allow-list or outside the root.
        InputNotFoundError: The file does not exist.
    """
    if not allowed(path):
        raise InputPathError(f"Input path is not allowed: {path}")
    try:
        library.resolve(path)
    except (InputPathError, InputNotFoundError):
        raise
    except RenderError:
        return
