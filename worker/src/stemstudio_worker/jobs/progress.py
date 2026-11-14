"""Stage texts, progress ranges and the per-job state tracker (data-model.md section 3.2)."""

from collections.abc import Callable
from datetime import datetime
from typing import Any

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import ProgressCallback
from stemstudio_worker.schemas import JobResult, JobState

Clock = Callable[[], datetime]
"""Returns the current UTC time."""

QUEUED = "Queued"
DOWNLOADING = "Downloading audio"
PREPARING = "Preparing audio"
SEPARATING = "Separating stems"
ANALYSING = "Analysing beats"
COMPLETED = "Completed"
FAILED = "Failed"
UNEXPECTED_ERROR = "Unexpected error"
INTERRUPTED = "interrupted by restart"

SEPARATION_START = 10
ANALYSIS_START = 90
_DOWNLOAD_SPAN = 9
_SEPARATION_SPAN = 80
_MAX_ERROR = 1000


def download_percent(fraction: float) -> int:
    """Map download progress 0..1 to job progress 0..9."""
    return int(_DOWNLOAD_SPAN * _clamp(fraction))


def separation_percent(fraction: float) -> int:
    """Map separation progress 0..1 to job progress ``10 + 80 x fraction``."""
    return SEPARATION_START + round(_SEPARATION_SPAN * _clamp(fraction))


def _clamp(fraction: float) -> float:
    return min(max(fraction, 0.0), 1.0)


class JobTracker:
    """Applies state changes of one job to the store; progress never decreases."""

    def __init__(self, store: JobStore, job_id: str, clock: Clock) -> None:
        """Track ``job_id`` in ``store``, stamping ``updatedAt`` with ``clock``."""
        self._store = store
        self._job_id = job_id
        self._clock = clock

    def update(self, status: JobState, stage: str, progress: int) -> None:
        """Move to ``status``/``stage``; saves only when something changed."""
        job = self._record().job
        progress = max(job.progress, progress)
        if (job.status, job.stage, job.progress) != (status, stage, progress):
            self._save_job(status=status, stage=stage, progress=progress)

    def callback(
        self, status: JobState, stage: str, percent: Callable[[float], int]
    ) -> ProgressCallback:
        """Return a port progress callback reporting ``percent(fraction)`` in ``status``."""
        return _Reporter(self, status, stage, percent)

    def complete(self, result: JobResult) -> None:
        """Mark the job completed with ``result`` (``updatedAt`` = ``result.completedAt``)."""
        job = self._record().job.model_copy(
            update={"status": JobState.COMPLETED, "stage": COMPLETED, "progress": 100,
                    "updated_at": result.completed_at},
        )  # fmt: skip
        record = self._record().model_copy(
            update={"job": job, "result": result, "audio_sha256": result.audio_sha256}
        )
        self._store.save(record)

    def fail(self, error: str) -> None:
        """Mark the job failed with a readable ``error`` (clipped to 1000 characters)."""
        self._save_job(status=JobState.FAILED, stage=FAILED, error=error[:_MAX_ERROR])

    def _record(self) -> StoredJob:
        record = self._store.get(self._job_id)
        if record is None:  # pragma: no cover - jobs are never deleted
            msg = f"Job {self._job_id} disappeared"
            raise LookupError(msg)
        return record

    def _save_job(self, **changes: Any) -> None:
        record = self._record()
        job = record.job.model_copy(update={**changes, "updated_at": self._clock()})
        self._store.save(record.model_copy(update={"job": job}))


class _Reporter:
    """``ProgressCallback`` adapter; the adapter's own stage text is replaced by a fixed one."""

    def __init__(
        self, tracker: JobTracker, status: JobState, stage: str, percent: Callable[[float], int]
    ) -> None:
        self._tracker = tracker
        self._status = status
        self._stage = stage
        self._percent = percent

    def __call__(self, fraction: float, stage: str) -> None:  # noqa: ARG002 - port signature
        self._tracker.update(self._status, self._stage, self._percent(fraction))
