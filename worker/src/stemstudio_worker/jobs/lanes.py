"""Light job lanes beside the separation scheduler (ADR-014 D3, ADR-016 D6).

A :class:`LaneScheduler` runs the jobs of one lane (metadata lookups, renders) FIFO on its own
thread, one at a time, so they never wait behind a 30-minute detail separation on the
:class:`~stemstudio_worker.jobs.scheduler.PriorityScheduler` thread. A job that raises is logged
and the lane goes on with the next one. Other job kinds can share a lane with their own process
function (:meth:`LaneScheduler.submit_call`; feature 006 lyrics lookups on the network lane).
"""

import logging
from collections import deque
from collections.abc import Callable

from stemstudio_worker.jobs.threads import QueueThread
from stemstudio_worker.schemas import LaneHealth

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
        self._queue: deque[tuple[Callable[[str], None], str]] = deque()

    def submit(self, job_id: str) -> None:
        """Queue a job behind the ones already waiting."""
        self.submit_call(self._process, job_id)

    def submit_call(self, process: Callable[[str], None], job_id: str) -> None:
        """Queue ``process(job_id)`` (a job of another kind) behind the ones already waiting."""
        with self._changed:
            self._queue.append((process, job_id))
            self._changed.notify_all()

    def health(self) -> LaneHealth:
        """Waiting jobs and whether one runs now (``Health.lanes``)."""
        with self._changed:
            return LaneHealth(queued=len(self._queue), running=self._busy)

    def _idle(self) -> bool:
        return not (self._busy or self._queue)

    def _loop(self) -> None:
        while (item := self._next()) is not None:
            self._run(*item)

    def _next(self) -> tuple[Callable[[str], None], str] | None:
        with self._changed:
            self._changed.wait_for(lambda: self._stopping or bool(self._queue))
            if self._stopping:
                return None
            self._busy = True
            return self._queue.popleft()

    def _run(self, process: Callable[[str], None], job_id: str) -> None:
        try:
            process(job_id)
        except Exception:
            _LOG.exception("A job in the %s lane crashed", self._name)
        finally:
            self._finished_unit()


def _unknown_kind(job_id: str) -> None:  # pragma: no cover - every job names its process
    """Default process of a shared lane: jobs are always submitted with their kind's function."""
    raise LookupError(job_id)


class SharedLane:
    """A lane several job kinds share (014 ``practice``, 020 ``insights``); jobs name a process."""

    def __init__(self, name: str) -> None:
        """Create a stopped lane named ``name``."""
        self.scheduler = LaneScheduler(name, _unknown_kind)

    def start(self) -> None:
        """Start the lane thread."""
        self.scheduler.start()

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane thread; a running job fails at the next start."""
        self.scheduler.stop(timeout)

    def join(self, timeout: float | None = None) -> bool:
        """Block until no job waits or runs; False on timeout."""
        return self.scheduler.join(timeout)

    def health(self) -> LaneHealth:
        """Waiting jobs and whether one runs (``Health.lanes``)."""
        return self.scheduler.health()
