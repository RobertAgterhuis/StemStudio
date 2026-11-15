"""The thread life cycle shared by the priority scheduler and the light job lanes."""

import threading
from abc import ABC, abstractmethod


class QueueThread(ABC):
    """One daemon thread that works off queued units under a single condition variable.

    Subclasses keep their queues under ``self._changed``, set ``self._busy`` while a unit runs
    and implement :meth:`_idle` and :meth:`_loop`.
    """

    def __init__(self, thread_name: str) -> None:
        """Create a stopped thread named ``thread_name``."""
        self._thread_name = thread_name
        self._busy = False
        self._stopping = False
        self._changed = threading.Condition()
        self._thread: threading.Thread | None = None

    @property
    def running(self) -> bool:
        """Whether the worker thread is alive."""
        return self._thread is not None and self._thread.is_alive()

    def start(self) -> None:
        """Start the worker thread (no-op when already started)."""
        if self._thread is None:
            self._stopping = False
            self._thread = threading.Thread(target=self._loop, name=self._thread_name, daemon=True)
            self._thread.start()

    def join(self, timeout: float | None = None) -> bool:
        """Block until nothing is queued or running; returns False on timeout."""
        with self._changed:
            return self._changed.wait_for(self._idle, timeout)

    def stop(self, timeout: float | None = None) -> None:
        """Stop after the current unit; waits at most ``timeout`` seconds (daemon thread)."""
        thread, self._thread = self._thread, None
        if thread is not None:
            with self._changed:
                self._stopping = True
                self._changed.notify_all()
            thread.join(timeout)

    def _finished_unit(self) -> None:
        """Mark the running unit done and wake waiters (call after every unit)."""
        with self._changed:
            self._busy = False
            self._changed.notify_all()

    @abstractmethod
    def _idle(self) -> bool:
        """True when nothing is queued or running (called with ``_changed`` held)."""

    @abstractmethod
    def _loop(self) -> None:
        """Body of the worker thread; returns once ``_stopping`` is set."""
