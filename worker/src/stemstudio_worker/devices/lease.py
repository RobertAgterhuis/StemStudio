"""The process-wide GPU lease: one GPU-heavy model step at a time (FR-005, ADR-012 D3)."""

import threading
from collections.abc import Iterator
from contextlib import contextmanager


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
