"""Detail job creation (idempotent per audio), lookup and cancellation (FR-010, FR-016)."""

import threading
from collections.abc import Callable
from dataclasses import dataclass

from stemstudio_worker.jobs import detail_texts as texts
from stemstudio_worker.jobs.cache import ResultCache
from stemstudio_worker.jobs.detail_progress import DetailTracker
from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import DetailJob, DetailJobState, DetailResult, JobResult, JobState

_NOT_CANCELLABLE = frozenset({DetailJobState.COMPLETED, DetailJobState.FAILED})


class UnknownJobError(LookupError):
    """The standard or detail job does not exist (HTTP 404)."""


class DetailConflictError(Exception):
    """The request conflicts with the current job state (HTTP 409); the message is readable."""


@dataclass(frozen=True, slots=True)
class DetailContext:
    """Where standard and detail jobs live and how time is read.

    Attributes:
        jobs: The standard job store.
        details: The detail job store.
        paths: The data directory layout.
        clock: Current UTC time.
    """

    jobs: JobStore
    details: DetailJobStore
    paths: DataPaths
    clock: Clock


def _request_cancel(record: StoredDetailJob) -> StoredDetailJob:
    return record.model_copy(update={"cancel_requested": True})


def _state_conflict(record: StoredDetailJob) -> DetailConflictError:
    return DetailConflictError(f"Detail job is in state {record.job.status.value}.")


class DetailService:
    """Creates, reads and cancels detail jobs."""

    def __init__(self, context: DetailContext, submit: Callable[[str], None]) -> None:
        """Create the service; ``submit`` enqueues a new detail job id for processing."""
        self._context = context
        self._submit = submit
        self._lock = threading.Lock()

    def create(self, standard_job_id: str) -> tuple[DetailJob, bool]:
        """Return the reusable detail job for the audio of ``standard_job_id`` or queue a new one.

        Raises:
            UnknownJobError: The standard job does not exist.
            DetailConflictError: It is not completed or its stems left the cache.
        """
        result = self._standard_result(standard_job_id)
        with self._lock:
            existing = self._context.details.find_reusable(result.audio_sha256)
            if existing is not None:
                return existing.job, False
            record = self._new_record(standard_job_id, result.audio_sha256)
            self._context.details.save(record)
        self._submit(record.job.detail_job_id)
        return record.job, True

    def get(self, detail_id: str) -> StoredDetailJob:
        """Return the detail job record.

        Raises:
            UnknownJobError: ``detail_id`` is unknown.
        """
        record = self._context.details.get(detail_id)
        if record is None:
            msg = f"Detail job {detail_id[:64]} does not exist."
            raise UnknownJobError(msg)
        return record

    def result(self, detail_id: str) -> DetailResult:
        """Return the result of a completed detail job.

        Raises:
            UnknownJobError: ``detail_id`` is unknown.
            DetailConflictError: The job is not completed.
        """
        record = self.get(detail_id)
        if record.job.status is not DetailJobState.COMPLETED or record.result is None:
            raise _state_conflict(record)
        return record.result

    def cancel(self, detail_id: str) -> DetailJob:
        """Cancel a queued or running job (no-op when already cancelled).

        Raises:
            UnknownJobError: ``detail_id`` is unknown.
            DetailConflictError: The job is completed or failed.
        """
        record = self.get(detail_id)
        if record.job.status in _NOT_CANCELLABLE:
            raise _state_conflict(record)
        if record.job.status is DetailJobState.CANCELLED:
            return record.job
        self._context.details.update(detail_id, _request_cancel)
        DetailTracker(self._context.details, detail_id, self._context.clock).cancel()
        return self.get(detail_id).job

    def _standard_result(self, standard_job_id: str) -> JobResult:
        record = self._context.jobs.get(standard_job_id)
        if record is None:
            msg = f"Job {standard_job_id[:64]} does not exist."
            raise UnknownJobError(msg)
        if record.job.status is not JobState.COMPLETED or record.result is None:
            msg = f"Job is in state {record.job.status.value}."
            raise DetailConflictError(msg)
        if ResultCache(self._context.paths).lookup_audio(record.result.audio_sha256) is None:
            raise DetailConflictError(texts.STEMS_MISSING)
        return record.result

    def _new_record(self, standard_job_id: str, audio_sha256: str) -> StoredDetailJob:
        now = self._context.clock()
        job = DetailJob(
            detail_job_id=DetailJobStore.new_detail_id(), standard_job_id=standard_job_id,
            audio_sha256=audio_sha256, status=DetailJobState.QUEUED, stage=texts.QUEUED,
            progress=0, created_at=now, updated_at=now,
        )  # fmt: skip
        return StoredDetailJob(job=job)
