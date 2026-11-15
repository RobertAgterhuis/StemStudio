"""Priority scheduler on one worker thread (FR-011, SC-006, research.md D-09).

Standard jobs run whole and always before detail work; detail jobs run in units (plan, one
parent step, finalise), oldest job first. A standard job submitted while a detail unit runs
starts as soon as that unit returns. When a unit leaves both queues empty the ``on_idle`` hook
runs before the scheduler reports idle (feature 003: GPU models are released, ADR-012 D6).
"""

import logging
from collections import deque
from collections.abc import Callable
from dataclasses import dataclass

from stemstudio_worker.jobs.threads import QueueThread

_LOG = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class SchedulerHooks:
    """Optional callbacks of the scheduler (both run on the worker thread).

    Attributes:
        on_yield: Told the active detail job ids right before a standard job runs ahead of
            them (e.g. to report "Waiting for standard separations").
        on_idle: Called after a unit that left nothing queued (releases GPU models).
    """

    on_yield: Callable[[list[str]], None] | None = None
    on_idle: Callable[[], None] | None = None


class PriorityScheduler(QueueThread):
    """Runs ``standard(job_id)`` and ``detail_step(detail_id) -> more`` on a single thread."""

    def __init__(
        self,
        standard: Callable[[str], None],
        detail_step: Callable[[str], bool],
        hooks: SchedulerHooks = SchedulerHooks(),
    ) -> None:
        """Create a stopped scheduler.

        Args:
            standard: Processes one standard job to a terminal state.
            detail_step: Runs the next unit of a detail job; returns True while units remain.
            hooks: Optional yield and idle callbacks.
        """
        super().__init__("stemstudio-jobs")
        self._standard = standard
        self._detail_step = detail_step
        self._hooks = hooks
        self._standard_queue: deque[str] = deque()
        self._detail_queue: deque[str] = deque()

    @property
    def pending(self) -> tuple[int, int]:
        """Numbers of waiting standard jobs and of active detail jobs."""
        with self._changed:
            return len(self._standard_queue), len(self._detail_queue)

    def submit(self, job_id: str) -> None:
        """Queue a standard job (FIFO, ahead of every detail unit)."""
        self._enqueue(self._standard_queue, job_id)

    def submit_detail(self, detail_id: str) -> None:
        """Queue a detail job behind the active ones."""
        self._enqueue(self._detail_queue, detail_id)

    def _enqueue(self, queue: deque[str], item: str) -> None:
        with self._changed:
            queue.append(item)
            self._changed.notify_all()

    def _idle(self) -> bool:
        return not (self._busy or self._standard_queue or self._detail_queue)

    def _loop(self) -> None:
        while (unit := self._next_unit()) is not None:
            self._run(unit)

    def _next_unit(self) -> Callable[[], None] | None:
        with self._changed:
            self._changed.wait_for(lambda: self._stopping or not self._idle())
            if self._stopping:
                return None
            self._busy = True
            if self._standard_queue:
                job_id, waiting = self._standard_queue.popleft(), list(self._detail_queue)
                return lambda: self._standard_unit(job_id, waiting)
            return self._detail_unit(self._detail_queue[0])

    def _standard_unit(self, job_id: str, waiting: list[str]) -> None:
        if waiting and self._hooks.on_yield is not None:
            try:
                self._hooks.on_yield(waiting)
            except Exception:
                _LOG.exception("Reporting the waiting detail jobs failed")
        self._standard(job_id)

    def _detail_unit(self, detail_id: str) -> Callable[[], None]:
        def unit() -> None:
            more = False
            try:
                more = self._detail_step(detail_id)
            finally:
                if not more:
                    self._drop(detail_id)

        return unit

    def _drop(self, detail_id: str) -> None:
        with self._changed:
            if detail_id in self._detail_queue:
                self._detail_queue.remove(detail_id)

    def _run(self, unit: Callable[[], None]) -> None:
        try:
            unit()
        except Exception:
            _LOG.exception("A job unit crashed the scheduler thread")
        finally:
            self._after_unit()

    def _after_unit(self) -> None:
        if self._hooks.on_idle is not None and self._nothing_queued():
            try:
                self._hooks.on_idle()
            except Exception:
                _LOG.exception("Releasing idle GPU memory failed")
        self._finished_unit()

    def _nothing_queued(self) -> bool:
        with self._changed:
            return not (self._standard_queue or self._detail_queue)
