"""The cross-container GPU lease: an exclusive lock on a shared file (ADR-018 D3).

``FileLease`` takes ``fcntl.flock(LOCK_EX | LOCK_NB)`` on Linux (the containers; the kernel
releases it when a holder crashes) and ``msvcrt.locking`` on Windows (local development only).
Every attempt opens its own descriptor, so threads of one process exclude each other as well.
The lease is polled, not blocked on, so the waiting job can report ``waitingForGpu`` and be
cancelled: the wait callback raises ``Cancelled``. Without a configured file the generator only
serialises its own steps (``ThreadLease``; fake stack and tests).
"""

import os
import sys
import threading
from collections.abc import Iterator
from contextlib import AbstractContextManager, contextmanager
from pathlib import Path
from typing import Final, Protocol

from stemstudio_generator.ports import Clock, WaitCallback

LEASE_WAIT_STAGE: Final = "Waiting for the GPU (another StemStudio job is using it)"
"""Stage text while another holder has the lease."""

LEASE_POLL_SECONDS: Final = 1.0
"""Interval between two lock attempts."""


class Lease(Protocol):
    """One GPU step at a time."""

    def hold(self, on_wait: WaitCallback) -> AbstractContextManager[None]:
        """Context manager: wait (reporting through ``on_wait``), hold, release on exit."""
        ...


class ThreadLease:
    """In-process lease (no lock file configured)."""

    def __init__(self) -> None:
        """Create a free lease."""
        self._lock = threading.Lock()

    @property
    def held(self) -> bool:
        """Whether a step holds the lease right now."""
        return self._lock.locked()

    @contextmanager
    def hold(self, on_wait: WaitCallback) -> Iterator[None]:
        """Hold the lease for the ``with`` block (never waits long: one job runs at a time)."""
        del on_wait
        with self._lock:
            yield


class FileLease:
    """Exclusive lock on ``path``, shared by the worker (GPU image), generator and singer."""

    def __init__(self, path: Path, clock: Clock, poll_seconds: float = LEASE_POLL_SECONDS) -> None:
        """Lock ``path`` (created with its directory on first use); poll every ``poll_seconds``."""
        self._path = path
        self._clock = clock
        self._poll = poll_seconds

    @contextmanager
    def hold(self, on_wait: WaitCallback) -> Iterator[None]:
        """Wait until the lock is free, hold it inside the ``with`` block, then release it."""
        self._path.parent.mkdir(parents=True, exist_ok=True)
        fd = os.open(self._path, os.O_RDWR | os.O_CREAT, 0o666)
        try:
            self._acquire(fd, on_wait)
            try:
                yield
            finally:
                _unlock(fd)
        finally:
            os.close(fd)

    def _acquire(self, fd: int, on_wait: WaitCallback) -> None:
        while not _try_lock(fd):
            on_wait(LEASE_WAIT_STAGE)
            self._clock.sleep(self._poll)


def open_lease(path: Path | None, clock: Clock) -> FileLease | ThreadLease:
    """``FileLease`` on ``path`` (``STEMSTUDIO_GPU_LEASE_FILE``), or a ``ThreadLease`` if None."""
    return ThreadLease() if path is None else FileLease(path, clock)


if sys.platform == "win32":  # local development on Windows
    import msvcrt

    def _try_lock(fd: int) -> bool:
        os.lseek(fd, 0, os.SEEK_SET)
        try:
            msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
        except OSError:
            return False
        return True

    def _unlock(fd: int) -> None:
        os.lseek(fd, 0, os.SEEK_SET)
        msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)

else:  # Linux containers and CI
    import fcntl

    def _try_lock(fd: int) -> bool:
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return False
        return True

    def _unlock(fd: int) -> None:
        fcntl.flock(fd, fcntl.LOCK_UN)
