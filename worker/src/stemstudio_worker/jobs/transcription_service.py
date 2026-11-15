"""Transcription jobs on their own ``transcribe`` lane (feature 008, ADR-017, data-model 2.9).

``queued -> running -> completed | failed | cancelled`` (statuses of the render jobs). Inputs are
checked when a job is created (library path guard: 400/404); one job runs at a time, never behind
a separation or a render. ``DELETE`` removes a job at once; a running job stops at its next
progress report and leaves nothing behind. Restart and 24 h sweep as the render jobs.
"""

import logging
import shutil
import threading
from typing import Any

from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import UNEXPECTED_ERROR, Clock
from stemstudio_worker.jobs.transcription_pipeline import (
    STAGES,
    Outcome,
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
    LaneHealth,
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
_MAX_ERROR = 500
_PLANNED = JobDevice(kind="cpu")
"""Device shown while a job runs; the piano CRNN's real device replaces it when it ends."""


class TranscriptionService:
    """Creates, runs, reads and deletes transcription jobs; owns the ``transcribe`` lane."""

    def __init__(
        self, store: TranscriptionJobStore, parts: TranscriptionParts, clock: Clock
    ) -> None:
        """Serve the loaded ``store`` with ``parts``; use :meth:`start` in production."""
        self._store = store
        self._parts = parts
        self._clock = clock
        self._lock = threading.RLock()
        self._lane = LaneScheduler("transcribe", self.process)

    @classmethod
    def start(
        cls, paths: DataPaths, parts: TranscriptionParts, clock: Clock
    ) -> "TranscriptionService":
        """Load the jobs, fail the ones a restart interrupted and start the lane."""
        store = TranscriptionJobStore(paths)
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
        job = TranscriptionJob(transcription_job_id=new_id(), status=_S.QUEUED, progress=0,
                               created_at=now, updated_at=now)  # fmt: skip
        self._store.save(StoredTranscriptionJob(job=job, request=request))
        self._lane.submit(job.transcription_job_id)
        return job

    def get(self, job_id: str) -> TranscriptionJob | None:
        """The job with ``job_id``, or None."""
        record = self._store.get(job_id)
        return None if record is None else record.job

    def file(self, job_id: str, role: ScoreFileRole) -> Any:
        """The file of ``role`` of job ``job_id`` (a ``Path``), or None when it is gone."""
        return self._store.file(job_id, role)

    def delete(self, job_id: str) -> None:
        """Remove a job and its files (any state, idempotent); a running job stops."""
        with self._lock:
            self._store.remove(job_id)

    def sweep(self) -> list[str]:
        """Remove terminal jobs older than 24 hours."""
        with self._lock:
            return self._store.sweep(self._clock())

    def lane_health(self) -> LaneHealth:
        """State of the lane (``Health.lanes.transcribe``)."""
        return self._lane.health()

    def health(self) -> TranscriptionHealth:
        """``Health.transcription``: model readiness and the music font."""
        return TranscriptionHealth(models=self._parts.engines.models_health(),
                                   font=FontHealth(leipzig=self._parts.font()))  # fmt: skip

    def join(self, timeout: float | None = None) -> bool:
        """Block until the lane is idle; False on timeout."""
        return self._lane.join(timeout)

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane; a running job is failed at the next start."""
        self._lane.stop(timeout)

    def process(self, job_id: str) -> None:
        """Run one job to a terminal state (lane thread); skips unknown and finished jobs."""
        record = self._store.get(job_id)
        if record is None or record.job.status.is_terminal:
            return
        self.update(job_id, status=_S.RUNNING, stage="loading-model", device=_PLANNED)
        outcome = self._run(record)
        if not self.update(job_id, stage=None, **outcome) or outcome["status"] is not _S.COMPLETED:
            shutil.rmtree(self._store.output_dir(job_id), ignore_errors=True)
        self._parts.engines.release()

    def _run(self, record: StoredTranscriptionJob) -> dict[str, Any]:
        job_id = record.job.transcription_job_id
        try:
            inputs = resolve_inputs(record.request, self._parts.library)
            done = Pipeline(self._parts, record.request).run(inputs, _Stages(self, job_id))
        except TranscriptionCancelledError:
            return {"status": _S.CANCELLED}
        except (TranscriptionError, RenderError) as exc:
            return {"status": _S.FAILED, "error": (str(exc) or UNEXPECTED_ERROR)[:_MAX_ERROR]}
        except Exception:
            _LOG.exception("Transcription job %s failed unexpectedly", job_id)
            return {"status": _S.FAILED, "error": UNEXPECTED_ERROR}
        return self._completed(job_id, done)

    def _completed(self, job_id: str, done: Outcome) -> dict[str, Any]:
        files = self._store.save_files(job_id, done.files)
        result = TranscriptionResult(files=files, summary=done.summary)
        return {"status": _S.COMPLETED, "progress": 1.0, "result": result, "device": done.device}

    def update(self, job_id: str, **changes: Any) -> bool:
        """Apply ``changes`` to a job that still exists; False when it was deleted."""
        with self._lock:
            record = self._store.get(job_id)
            if record is None:
                return False
            job = record.job.model_copy(update={**changes, "updated_at": self._clock()})
            self._store.save(record.model_copy(update={"job": job}))
            return True


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
