"""Lyrics jobs: creation, reads, cancel, delete, restart and sweep (feature 006, ADR-015 D2/D3).

``mode: lookup`` jobs run on the 005 network lane (beside the metadata jobs, never behind a
separation); ``mode: align`` jobs are units of the priority scheduler (standard > align > detail
parent step, never pre-empting). A worker restart runs queued and running jobs again from the
start; ``DELETE`` removes a job and its result at once; terminal jobs are swept after 24 hours.
"""

from collections.abc import Callable
from dataclasses import dataclass

from stemstudio_worker.jobs.detail_service import UnknownJobError
from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lyrics_runner import (
    LyricsContext,
    LyricsParts,
    LyricsRunner,
    Outcome,
    VocalLoader,
    vocal_stem,
)
from stemstudio_worker.jobs.lyrics_store import MAX_AGE, StoredLyricsJob
from stemstudio_worker.ports import AlignmentError, estimate_alignment_seconds
from stemstudio_worker.schemas import (
    AlignLyricsRequest,
    JobState,
    LookupLyricsRequest,
    LyricsJob,
    LyricsJobState,
    LyricsResult,
)

__all__ = [
    "LyricsConflictError",
    "LyricsContext",
    "LyricsParts",
    "LyricsQueues",
    "LyricsRunner",
    "LyricsService",
    "VocalLoader",
]

_S = LyricsJobState
QUEUED = "Queued"

STANDARD_NOT_COMPLETED = "standard-job-not-completed"
"""Problem type: the standard job of an alignment is not completed."""

VOCAL_STEM_MISSING = "vocal-stem-missing"
"""Problem type: the vocal stem of an alignment left the worker cache."""

NOT_CANCELLABLE = "lyrics-job-not-cancellable"
"""Problem type: the job is a lookup or already terminal."""

NOT_COMPLETED = "lyrics-not-completed"
"""Problem type: the job has no result (yet)."""


class LyricsConflictError(Exception):
    """The request conflicts with a job's state (HTTP 409); ``kind`` is the problem type."""

    def __init__(self, kind: str, message: str) -> None:
        """Create a conflict of ``kind`` with a readable ``message``."""
        super().__init__(message)
        self.kind = kind


@dataclass(frozen=True, slots=True)
class LyricsQueues:
    """Where new jobs are queued.

    Attributes:
        lookup: Queues a lookup on the network lane.
        align: Queues an alignment on the priority scheduler.
    """

    lookup: Callable[[str], None]
    align: Callable[[str], None]

    def submit(self, record: StoredLyricsJob) -> None:
        """Queue ``record`` by its mode."""
        queue = self.lookup if isinstance(record.request, LookupLyricsRequest) else self.align
        queue(record.job.lyrics_job_id)


def _unknown(job_id: str) -> UnknownJobError:
    return UnknownJobError(f"Lyrics job {job_id[:64]} does not exist.")


def _state(record: StoredLyricsJob) -> str:
    return f"Lyrics job is in state {record.job.status.value}."


class LyricsService:
    """Creates, reads, cancels, deletes and sweeps lyrics jobs."""

    def __init__(self, runner: LyricsRunner, queues: LyricsQueues) -> None:
        """Serve the jobs of ``runner``; new jobs go to ``queues``. Use :meth:`start`."""
        self._runner = runner
        self._jobs = runner.jobs
        self._context = runner.jobs.context
        self._queues = queues

    @classmethod
    def start(cls, runner: LyricsRunner, queues: LyricsQueues) -> "LyricsService":
        """Queue every unfinished job again from the start (oldest first)."""
        service = cls(runner, queues)
        for record in service._context.store.unfinished():
            service._jobs.update(record.job.lyrics_job_id, status=_S.QUEUED, stage=QUEUED,
                                 progress=0.0, device=None, error=None)  # fmt: skip
            queues.submit(record)
        return service

    def create(self, request: LookupLyricsRequest | AlignLyricsRequest) -> LyricsJob:
        """Persist a queued job for ``request`` and queue it.

        Raises:
            UnknownJobError: The standard job of an alignment does not exist.
            LyricsConflictError: It is not completed or its vocal stem is gone.
        """
        estimate = None
        if isinstance(request, AlignLyricsRequest):
            estimate = self._estimate(request.standard_job_id)
        now = self._context.clock()
        job = LyricsJob(lyrics_job_id=new_id(), mode=request.mode, status=_S.QUEUED, stage=QUEUED,
                        progress=0, estimated_seconds=estimate, created_at=now,
                        updated_at=now)  # fmt: skip
        record = StoredLyricsJob(job=job, request=request)
        self._context.store.save(record)
        self._queues.submit(record)
        return job

    def get(self, job_id: str) -> LyricsJob:
        """The job with ``job_id``.

        Raises:
            UnknownJobError: No such job.
        """
        return self._record(job_id).job

    def result(self, job_id: str) -> LyricsResult:
        """The result of a completed job.

        Raises:
            UnknownJobError: No such job, or its result file is gone.
            LyricsConflictError: The job is not completed (``lyrics-not-completed``).
        """
        record = self._record(job_id)
        if record.job.status is not _S.COMPLETED:
            raise LyricsConflictError(NOT_COMPLETED, _state(record))
        result = self._context.store.result(job_id)
        if result is None:
            raise UnknownJobError("The lyrics result no longer exists.")
        return result

    def cancel(self, job_id: str) -> LyricsJob:
        """Cancel a queued or running alignment (a running one stops at its next report).

        Raises:
            UnknownJobError: No such job.
            LyricsConflictError: The job is a lookup or already terminal.
        """
        record = self._record(job_id)
        if record.job.mode == "lookup" or not self._jobs.finish(job_id, Outcome(_S.CANCELLED)):
            raise LyricsConflictError(NOT_CANCELLABLE, _state(record))
        return self.get(job_id)

    def delete(self, job_id: str) -> None:
        """Remove a job and its result in any state (a running alignment stops).

        Raises:
            UnknownJobError: No such job.
        """
        if not self._jobs.remove(job_id):
            raise _unknown(job_id)

    def sweep(self) -> list[str]:
        """Remove terminal jobs (and results) last updated more than 24 hours ago."""
        expired = self._context.store.expired(self._context.clock() - MAX_AGE)
        for job_id in expired:
            self._jobs.remove(job_id)
        return expired

    def _record(self, job_id: str) -> StoredLyricsJob:
        record = self._context.store.get(job_id)
        if record is None:
            raise _unknown(job_id)
        return record

    def _estimate(self, standard_job_id: str) -> float:
        """Check the standard job of an alignment and estimate the alignment time."""
        record = self._context.jobs.get(standard_job_id)
        if record is None:
            raise UnknownJobError(f"Job {standard_job_id[:64]} does not exist.")
        if record.job.status is not JobState.COMPLETED or record.result is None:
            detail = f"Job {standard_job_id} is in state {record.job.status.value}."
            raise LyricsConflictError(STANDARD_NOT_COMPLETED, detail)
        try:
            vocal_stem(self._context, standard_job_id)
        except AlignmentError as exc:
            raise LyricsConflictError(VOCAL_STEM_MISSING, str(exc)) from exc
        parts = self._runner.parts
        return estimate_alignment_seconds(record.result.duration_seconds, parts.devices.info.kind,
                                          model_cached=parts.model_cached())  # fmt: skip
