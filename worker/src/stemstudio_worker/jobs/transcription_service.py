"""Transcription jobs on their own ``transcribe`` lane (feature 008, ADR-017, data-model 2.9).

``queued -> running -> completed | failed | cancelled`` (statuses of the render jobs). Inputs are
checked when a job is created (library path guard: 400/404); one job runs at a time, never behind
a separation or a render. ``DELETE`` removes a job at once; a running job stops at its next
progress report and leaves nothing behind. Restart and 24 h sweep as the render jobs.
"""

import logging
from pathlib import Path
from typing import Any

from stemstudio_worker.jobs import json_store, lane_service, paths, progress
from stemstudio_worker.jobs.transcription_pipeline import (
    STAGES,
    Pipeline,
    TranscriptionParts,
    resolve_inputs,
)
from stemstudio_worker.jobs.transcription_store import StoredTranscriptionJob, TranscriptionJobStore
from stemstudio_worker.ports import (
    InputNotFoundError,
    InputPathError,
    RenderError,
    TranscriptionCancelledError,
    TranscriptionError,
    TranscriptionProgress,
)
from stemstudio_worker.schemas import (
    FontHealth,
    JobDevice,
    RenderJobStatus,
    ScoreFileRole,
    TranscriptionHealth,
    TranscriptionJob,
    TranscriptionJobRequest,
    TranscriptionResult,
    TranscriptionStage,
)

_LOG = logging.getLogger(__name__)
_S = RenderJobStatus
_PLANNED = JobDevice(kind="cpu")
"""Device shown while a job runs; the piano CRNN's real device replaces it when it ends."""


class TranscriptionService(lane_service.LaneJobService[StoredTranscriptionJob]):
    """Creates, runs, reads and deletes transcription jobs; owns the ``transcribe`` lane."""

    LABEL = "Transcription"
    CANCELLED = TranscriptionCancelledError
    ERRORS = (TranscriptionError, RenderError)

    def __init__(
        self, store: TranscriptionJobStore, parts: TranscriptionParts, clock: progress.Clock
    ) -> None:
        """Serve the loaded ``store`` with ``parts``; use :meth:`start` in production."""
        super().__init__("transcribe", store, clock)
        self._jobs = store
        self._parts = parts

    @classmethod
    def start(
        cls, data: paths.DataPaths, parts: TranscriptionParts, clock: progress.Clock
    ) -> "TranscriptionService":
        """Load the jobs, fail the ones a restart interrupted and start the lane."""
        store = TranscriptionJobStore(data)
        store.load_all()
        store.reconcile(clock)
        service = cls(store, parts, clock)
        service._lane.start()
        return service

    def create(self, request: TranscriptionJobRequest) -> TranscriptionJob:
        """Check the inputs and queue a job.

        Raises:
            InputPathError: An input path is not allowed (HTTP 400).
            InputNotFoundError: An input file does not exist (HTTP 404).

        A library that cannot be read now does not refuse the job: it fails when it runs.
        """
        try:
            resolve_inputs(request, self._parts.library)
        except (InputPathError, InputNotFoundError):
            raise
        except RenderError:
            _LOG.warning("Transcription job queued although its inputs cannot be checked now")
        now = self._clock()
        job = TranscriptionJob(transcription_job_id=json_store.new_id(), status=_S.QUEUED,
                               progress=0, created_at=now, updated_at=now)  # fmt: skip
        self._store.save(StoredTranscriptionJob(job=job, request=request))
        self._lane.submit(job.transcription_job_id)
        return job

    def get(self, job_id: str) -> TranscriptionJob | None:
        """The job with ``job_id``, or None."""
        job: TranscriptionJob | None = self.job_of(job_id)
        return job

    def file(self, job_id: str, role: ScoreFileRole) -> Path | None:
        """The file of ``role`` of job ``job_id``, or None when it is gone."""
        return self._jobs.file(job_id, role)

    def health(self) -> TranscriptionHealth:
        """``Health.transcription``: model readiness and the music font."""
        return TranscriptionHealth(models=self._parts.engines.models_health(),
                                   font=FontHealth(leipzig=self._parts.font()))  # fmt: skip

    def process(self, job_id: str) -> None:
        """Run one job to a terminal state (lane thread); skips unknown and finished jobs."""
        record = self._pending(job_id)
        if record is None:
            return
        self.update(job_id, status=_S.RUNNING, stage="loading-model", device=_PLANNED)
        outcome = self._guarded(job_id, lambda: self._run(record))
        self._finish(job_id, {**outcome, "stage": None})
        self._parts.engines.release()

    def _run(self, record: StoredTranscriptionJob) -> dict[str, Any]:
        job_id = record.job.transcription_job_id
        inputs = resolve_inputs(record.request, self._parts.library)
        done = Pipeline(self._parts, record.request).run(inputs, _Stages(self, job_id))
        files = self._jobs.save_files(job_id, done.files)
        result = TranscriptionResult(files=files, summary=done.summary)
        return {"status": _S.COMPLETED, "progress": 1.0, "result": result, "device": done.device}


class _Stages:
    """Stage reporter of one job: stores stage and progress, stops a deleted job."""

    def __init__(self, service: TranscriptionService, job_id: str) -> None:
        self._service = service
        self._job_id = job_id
        self._last = 0.0

    def __call__(self, stage: TranscriptionStage) -> TranscriptionProgress:
        low, high = STAGES[stage]
        self._store(stage, low)

        def report(fraction: float, /) -> None:
            self._store(stage, low + (high - low) * min(max(fraction, 0.0), 1.0))

        return report

    def _store(self, stage: TranscriptionStage, progress: float) -> None:
        self._last = max(self._last, progress)
        if not self._service.update(self._job_id, stage=stage, progress=round(self._last, 6)):
            raise TranscriptionCancelledError("The transcription job was deleted")
