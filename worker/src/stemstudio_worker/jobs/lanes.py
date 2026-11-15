"""Light job lanes beside the separation scheduler (ADR-014 D3, ADR-016 D6).

A :class:`LaneScheduler` runs the jobs of one lane (metadata lookups, renders) FIFO on its own
thread, one at a time, so they never wait behind a 30-minute detail separation on the
:class:`~stemstudio_worker.jobs.scheduler.PriorityScheduler` thread. A job that raises is logged
and the lane goes on with the next one.
"""

import logging
import threading
from collections import deque
from collections.abc import Callable

_LOG = logging.getLogger(__name__)


class LaneScheduler:
    """Runs ``process(job_id)`` for every submitted id, FIFO, on one daemon thread."""

    def __init__(self, name: str, process: Callable[[str], None]) -> None:
        """Create a stopped lane.

        Args:
            name: Lane name, used for the thread name and in log messages (e.g. ``metadata``).
            process: Brings one job to a terminal state; exceptions are logged.
        """
        self._name = name
        self._process = process
        self._queue: deque[str] = deque()
        self._busy = False
        self._stopping = False
        self._changed = threading.Condition()
        self._thread: threading.Thread | None = None

    @property
    def running(self) -> bool:
        """Whether the lane thread is alive."""
        return self._thread is not None and self._thread.is_alive()

    def start(self) -> None:
        """Start the lane thread (no-op when already started)."""
        if self._thread is None:
            self._stopping = False
            name = f"stemstudio-{self._name}"
            self._thread = threading.Thread(target=self._loop, name=name, daemon=True)
            self._thread.start()

    def submit(self, job_id: str) -> None:
        """Queue a job behind the ones already waiting."""
        with self._changed:
            self._queue.append(job_id)
            self._changed.notify_all()

    def join(self, timeout: float | None = None) -> bool:
        """Block until nothing is queued or running; returns False on timeout."""
        with self._changed:
            return self._changed.wait_for(self._idle, timeout)

    def stop(self, timeout: float | None = None) -> None:
        """Stop after the current job; waits at most ``timeout`` seconds (daemon thread)."""
        thread, self._thread = self._thread, None
        if thread is not None:
            with self._changed:
                self._stopping = True
                self._changed.notify_all()
            thread.join(timeout)

    def _idle(self) -> bool:
        return not (self._busy or self._queue)

    def _loop(self) -> None:
        while (job_id := self._next()) is not None:
            self._run(job_id)

    def _next(self) -> str | None:
        with self._changed:
            self._changed.wait_for(lambda: self._stopping or bool(self._queue))
            if self._stopping:
                return None
            self._busy = True
            return self._queue.popleft()

    def _run(self, job_id: str) -> None:
        try:
            self._process(job_id)
        except Exception:
            _LOG.exception("A job in the %s lane crashed", self._name)
        finally:
            with self._changed:
                self._busy = False
                self._changed.notify_all()
