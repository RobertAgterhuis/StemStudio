"""The shared life cycle of quality and handout jobs on the ``insights`` lane (feature 020).

``queued -> running -> completed | failed | cancelled`` as the render jobs (007): a job is
checked when it is created, runs on the lane under its thread cap, reports progress with the
stage of its kind and stops at its next progress report after ``DELETE``. A finished job has no
stage.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any, ClassVar

from stemstudio_worker.jobs.insights_lane import InsightsLane
from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lane_service import LaneJobService, Outcome
from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import InsightsProgress, LibraryReader
from stemstudio_worker.schemas import RenderJobStatus

Stages = Sequence[tuple[float, str]]
"""``(upper bound, stage)`` in ascending order; the last bound is above 1."""


def stage_in(stages: Stages, fraction: float) -> str:
    """The stage of ``fraction`` (the first whose upper bound is above it)."""
    return next(stage for bound, stage in stages if fraction < bound)


@dataclass(frozen=True, slots=True)
class InsightsContext:
    """What the insights services share: the lane (with the stores), the library, the clock.

    ``library_root`` is the mounted library (``STEMSTUDIO_LIBRARY_DIR``) for the handout inputs,
    which the library reader's general allow-list does not accept (data-model 2.2).
    """

    lane: InsightsLane
    library: LibraryReader
    clock: Clock
    library_root: Path | None = None


class InsightsJobService[R: Any](LaneJobService[R]):
    """Create, run and read jobs of one insights kind.

    Subclasses set ``LABEL``, ``CANCELLED``, ``ERRORS``, ``ID_FIELD`` (the wire id, e.g.
    ``quality_job_id``) and ``STAGES``, and implement :meth:`run`.
    """

    ID_FIELD: ClassVar[str]
    STAGES: ClassVar[Stages]

    def __init__(self, context: InsightsContext, store: LaneJobStore[R]) -> None:
        """Serve ``store`` on the lane of ``context``."""
        super().__init__(context.lane.scheduler, store, context.clock)
        self._insights = context.lane
        self._library = context.library

    def get(self, job_id: str) -> Any:
        """The wire job with ``job_id``, or None."""
        return self.job_of(job_id)

    def _submit(self, job_id: str) -> None:
        """Queue on the insights lane (under its thread cap)."""
        self._insights.submit(self.process, job_id)

    def _stamp(self) -> dict[str, Any]:
        now = self._clock()
        return {self.ID_FIELD: new_id(), "status": RenderJobStatus.QUEUED, "progress": 0,
                "created_at": now, "updated_at": now}  # fmt: skip

    def _queue(self, record: R) -> Any:
        self._store.save(record)
        job = record.job
        self._submit(getattr(job, self.ID_FIELD))
        return job

    def _reporter(self, job_id: str) -> InsightsProgress:
        """Progress of a running job with its stage; raises ``CANCELLED`` once it was deleted."""
        last = [0.0]

        def report(fraction: float, /) -> None:
            last[0] = max(last[0], min(max(fraction, 0.0), 1.0))
            if not self.update(job_id, progress=last[0], stage=stage_in(self.STAGES, last[0])):
                raise self.CANCELLED(f"The {self.LABEL.lower()} job was deleted")

        return report

    def _finish(self, job_id: str, outcome: Outcome) -> None:
        """Store ``outcome`` of a finished job, which has no stage."""
        super()._finish(job_id, {**outcome, "stage": None})
