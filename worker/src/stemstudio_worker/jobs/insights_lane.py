"""The ``insights`` lane (feature 020). Red stub."""

from collections.abc import Callable
from contextlib import AbstractContextManager

ThreadCap = Callable[[int], AbstractContextManager[object]]


class InsightsLane:
    """Stub."""

    def __init__(self, *args: object) -> None:
        """Stub."""
        raise NotImplementedError


class InsightsStores:
    """Stub."""


def blas_threads(limit: int) -> AbstractContextManager[object]:
    """Stub."""
    raise NotImplementedError


def _import(name: str) -> object:
    raise NotImplementedError
