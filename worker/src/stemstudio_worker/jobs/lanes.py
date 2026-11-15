"""Light job lanes beside the separation scheduler (ADR-014 D3, ADR-016 D6).

A :class:`LaneScheduler` runs the jobs of one lane (metadata lookups, renders) FIFO on its own
thread, one at a time, so they never wait behind a 30-minute detail separation on the
:class:`~stemstudio_worker.jobs.scheduler.PriorityScheduler` thread. A job that raises is logged
and the lane goes on with the next one.
"""

import logging
from collections import deque
from collections.abc import Callable

from stemstudio_worker.jobs.threads import QueueThread

_LOG = logging.getLogger(__name__)


class LaneScheduler(QueueThread):
    """Runs ``process(job_id)`` for every submitted id, FIFO, on one daemon thread."""

    def __init__(self, name: str, process: Callable[[str], None]) -> None:
        """Create a stopped lane.

        Args:
            name: Lane name, used for the thread name and in log messages (e.g. ``metadata``).
            process: Brings one job to a terminal state; exceptions are logged.
        """
        super().__init__(f"stemstudio-{name}")
        self._name = name
        self._process = process
        self._queue: deque[str] = deque()

    def submit(self, job_id: str) -> None:
        """Queue a job behind the ones already waiting."""
        with self._changed:
            self._queue.append(job_id)
            self._changed.notify_all()

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
            self._finished_unit()
