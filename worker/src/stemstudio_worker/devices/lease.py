"""GPU leases: one GPU-heavy model step at a time (FR-005, ADR-012 D3; 009 FR-011, ADR-018 D3).

:class:`GpuLease` is the process-wide thread lease of feature 003. :class:`FileGpuLease` adds the
cross-container lease of feature 009: an exclusive lock on a lock file in the shared ``gpu-lease``
volume, which the optional generator service takes too. The kernel releases the lock when a
holder's process ends. The worker polls the lock without blocking, so it can stop waiting after
``STEMSTUDIO_GPU_LEASE_WAIT_SECONDS`` and run the step on the CPU instead; the order between
waiting services is therefore not strictly first come, first served (documented limitation).
"""

import importlib
import logging
import os
import sys
import threading
import time
from collections.abc import Callable, Iterator
from contextlib import AbstractContextManager, contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Protocol

_LOG = logging.getLogger(__name__)


class GpuLease:
    """A lock held for the duration of one GPU model step."""

    def __init__(self) -> None:
        """Create a free lease."""
        self._lock = threading.Lock()

    @property
    def held(self) -> bool:
        """Whether a step holds the lease right now."""
        return self._lock.locked()

    @contextmanager
    def hold(self) -> Iterator[None]:
        """Block until the lease is free, hold it inside the ``with`` block, then release it."""
        with self._lock:
            yield


GPU_LEASE = GpuLease()
"""The lease shared by every GPU runner of the process."""


class LockApi(Protocol):
    """Non-blocking exclusive locks on an open file descriptor (``flock`` or ``msvcrt``)."""

    def try_lock(self, fd: int) -> bool:
        """Take the exclusive lock; False when another holder has it."""
        ...

    def unlock(self, fd: int) -> None:
        """Release the lock taken by :meth:`try_lock`."""
        ...


class FlockApi:
    """``LockApi`` over ``fcntl.flock`` (Linux; the same lock the generator takes)."""

    def __init__(self, fcntl: Any) -> None:
        """Lock with the ``fcntl`` module (injectable for tests)."""
        self._fcntl = fcntl

    def try_lock(self, fd: int) -> bool:
        """``flock(fd, LOCK_EX | LOCK_NB)``; False when it would block."""
        try:
            self._fcntl.flock(fd, self._fcntl.LOCK_EX | self._fcntl.LOCK_NB)
        except BlockingIOError:
            return False
        return True

    def unlock(self, fd: int) -> None:
        """``flock(fd, LOCK_UN)``."""
        self._fcntl.flock(fd, self._fcntl.LOCK_UN)


class MsvcrtApi:
    """``LockApi`` over ``msvcrt.locking`` on the file's first byte (Windows development)."""

    def __init__(self, msvcrt: Any) -> None:
        """Lock with the ``msvcrt`` module (injectable for tests)."""
        self._msvcrt = msvcrt

    def try_lock(self, fd: int) -> bool:
        """``locking(fd, LK_NBLCK, 1)`` at offset 0; False when another handle holds it."""
        os.lseek(fd, 0, os.SEEK_SET)
        try:
            self._msvcrt.locking(fd, self._msvcrt.LK_NBLCK, 1)
        except OSError:
            return False
        return True

    def unlock(self, fd: int) -> None:
        """``locking(fd, LK_UNLCK, 1)`` at offset 0."""
        os.lseek(fd, 0, os.SEEK_SET)
        self._msvcrt.locking(fd, self._msvcrt.LK_UNLCK, 1)


def platform_lock_api(
    platform: str = sys.platform, importer: Callable[[str], Any] = importlib.import_module
) -> LockApi:
    """The lock API of ``platform``: ``msvcrt`` on Windows, ``fcntl.flock`` elsewhere."""
    if platform == "win32":
        return MsvcrtApi(importer("msvcrt"))
    return FlockApi(importer("fcntl"))


@dataclass(frozen=True, slots=True)
class LeaseWait:
    """How long and how often the worker tries to take the file lease."""

    seconds: float
    """Longest wait before the step runs on the CPU (``gpu_lease_wait_seconds``)."""
    poll_seconds: float = 1.0
    """Pause between two attempts."""
    now: Callable[[], float] = time.monotonic
    """Monotonic clock (injectable for tests)."""
    sleep: Callable[[float], None] = time.sleep
    """Pause function (injectable for tests)."""


class FileLease(Protocol):
    """A cross-container GPU lease (:class:`FileGpuLease` or :data:`NO_FILE_LEASE`)."""

    def hold(self) -> AbstractContextManager[bool]:
        """Try to take the lease; the ``with`` value says whether the GPU may be used."""
        ...


class NoFileLease:
    """No cross-container lease (``STEMSTUDIO_GPU_LEASE_FILE`` unset): the 003 behaviour."""

    @contextmanager
    def hold(self) -> Iterator[bool]:
        """Always grant the GPU; no file is touched."""
        yield True


NO_FILE_LEASE = NoFileLease()
"""The file lease of a worker without the shared lock file."""


class FileGpuLease:
    """The exclusive lock on the shared GPU lock file (ADR-018 D3)."""

    def __init__(self, path: Path, wait: LeaseWait, api: LockApi | None = None) -> None:
        """Lock ``path`` (created when missing), waiting as ``wait`` says, with ``api``."""
        self._path = path
        self._wait = wait
        self._api = api or platform_lock_api()

    @contextmanager
    def hold(self) -> Iterator[bool]:
        """Wait for the lock; yield True while it is held, False when the wait ran out.

        An unopenable lock file is logged and grants the GPU without the lease, like an
        unreadable NVML grants it without the memory check.
        """
        fd = self._open()
        if fd is None:
            yield True
            return
        try:
            granted = self._acquire(fd)
            try:
                yield granted
            finally:
                if granted:
                    self._api.unlock(fd)
        finally:
            os.close(fd)

    def _open(self) -> int | None:
        try:
            return os.open(self._path, os.O_RDONLY | os.O_CREAT, 0o666)
        except OSError:
            _LOG.exception("GPU lease file %s cannot be opened; running without it", self._path)
            return None

    def _acquire(self, fd: int) -> bool:
        wait = self._wait
        deadline = wait.now() + wait.seconds
        while not self._api.try_lock(fd):
            remaining = deadline - wait.now()
            if remaining <= 0:
                _LOG.warning("GPU busy with another StemStudio job after %.0f s", wait.seconds)
                return False
            wait.sleep(min(wait.poll_seconds, remaining))
        return True
