"""The shared life cycle of jobs on a light lane (render, 007; transcription, 008).

A :class:`LaneJobService` owns one :class:`~stemstudio_worker.jobs.lanes.LaneScheduler` and a
:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`: ``queued -> running -> completed |
failed | cancelled``; ``DELETE`` removes a job at once (a running one stops at its next progress
report, whose ``update`` then fails); a job's files are removed unless it completed.
"""

import logging
import shutil
import threading
from collections.abc import Callable
from typing import Any, ClassVar

from pydantic import BaseModel

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.progress import UNEXPECTED_ERROR, Clock
from stemstudio_worker.schemas import LaneHealth, RenderJobStatus

_LOG = logging.getLogger(__name__)
_S = RenderJobStatus
_MAX_ERROR = 500

Outcome = dict[str, Any]
"""Job changes of a finished run (``status`` plus ``error``/``result``/... )."""


class LaneJobService[R: BaseModel]:
    """Base of the render and transcription services (one lane, one store, one lock).

    Subclasses set ``LABEL`` (log name), ``CANCELLED`` (raised by a progress report of a deleted
    job) and ``ERRORS`` (failures whose message is shown) and implement :meth:`process`.
    """

    LABEL: ClassVar[str]
    CANCELLED: ClassVar[type[Exception]]
    ERRORS: ClassVar[tuple[type[Exception], ...]]

    def __init__(self, lane: str | LaneScheduler, store: LaneJobStore[R], clock: Clock) -> None:
        """Serve ``store`` on a new lane named ``lane`` (or a shared lane), times from ``clock``.

        A shared lane (feature 014's ``practice`` lane) is started and stopped by its owner; jobs
        reach it through :meth:`_submit` with this service's :meth:`process`.
        """
        self._store = store
        self._clock = clock
        self._lock = threading.RLock()
        self._lane = LaneScheduler(lane, self.process) if isinstance(lane, str) else lane

    def process(self, job_id: str) -> None:
        """Run one job to a terminal state (lane thread)."""
        raise NotImplementedError

    def job_of(self, job_id: str) -> Any:
        """The wire job with ``job_id``, or None."""
        record = self._store.get(job_id)
        return None if record is None else _job(record)

    def delete(self, job_id: str) -> None:
        """Remove a job and its files (any state, idempotent); a running job stops."""
        with self._lock:
            self._store.remove(job_id)

    def sweep(self) -> list[str]:
        """Remove terminal jobs older than 24 hours."""
        with self._lock:
            return self._store.sweep(self._clock())

    def lane_health(self) -> LaneHealth:
        """State of the lane (``Health.lanes``)."""
        return self._lane.health()

    def join(self, timeout: float | None = None) -> bool:
        """Block until the lane is idle; False on timeout."""
        return self._lane.join(timeout)

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane; a running job is failed at the next start."""
        self._lane.stop(timeout)

    def update(self, job_id: str, **changes: Any) -> bool:
        """Apply ``changes`` to a job that still exists; False when it was deleted."""
        with self._lock:
            record = self._store.get(job_id)
            if record is None:
                return False
            job = _job(record).model_copy(update={**changes, "updated_at": self._clock()})
            self._store.save(record.model_copy(update={"job": job}))
            return True

    def _submit(self, job_id: str) -> None:
        """Queue ``process(job_id)`` on this service's lane (also when the lane is shared)."""
        self._lane.submit_call(self.process, job_id)

    def _pending(self, job_id: str) -> R | None:
        """The record of a job that still has to run (unknown and finished jobs: None)."""
        record = self._store.get(job_id)
        return None if record is None or _job(record).status.is_terminal else record

    def _guarded(self, job_id: str, run: Callable[[], Outcome]) -> Outcome:
        """``run()``, with cancels, shown errors and unexpected errors as outcomes."""
        try:
            return run()
        except self.CANCELLED:
            return {"status": _S.CANCELLED}
        except self.ERRORS as exc:
            return {"status": _S.FAILED, "error": (str(exc) or UNEXPECTED_ERROR)[:_MAX_ERROR]}
        except Exception:
            _LOG.exception("%s job %s failed unexpectedly", self.LABEL, job_id)
            return {"status": _S.FAILED, "error": UNEXPECTED_ERROR}

    def _finish(self, job_id: str, outcome: Outcome) -> None:
        """Store ``outcome``; drop the job's files unless it completed (or when it is gone)."""
        if not self.update(job_id, **outcome) or outcome["status"] is not _S.COMPLETED:
            shutil.rmtree(self._store.output_dir(job_id), ignore_errors=True)


def _job(record: BaseModel) -> Any:
    return getattr(record, "job")  # noqa: B009 - every lane record has a wire job
