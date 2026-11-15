"""Stage and error texts of detail jobs (data-model.md 4.2); errors are shown to users verbatim."""

from typing import Final

QUEUED: Final = "Queued"
STARTING: Final = "Preparing detail separation"
FINALISING: Final = "Finishing detail stems"
COMPLETED: Final = "Completed"
FAILED: Final = "Failed"
CANCELLED: Final = "Cancelled"

NO_MEMORY: Final = "Not enough memory for detail separation."
NO_DISK: Final = "Not enough disk space for detail separation."
STEMS_MISSING: Final = (
    "The standard stems of this track are no longer available in the worker; re-import the track."
)
INTERRUPTED: Final = "interrupted by restart"
UNEXPECTED: Final = "Unexpected error"


def splitting(parents: tuple[str, ...], index: int, total: int) -> str:
    """Stage text of a running parent step, e.g. "Splitting drums (1 of 3)"."""
    return f"Splitting {' and '.join(parents)} ({index} of {total})"
