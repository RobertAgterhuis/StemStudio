"""Render jobs on their own lane (feature 007, ADR-016 D6, data-model.md 2.4-2.5).

``queued -> running -> completed | failed | cancelled``. A job is checked when it is created
(input path guard), rendered by the injected :class:`~stemstudio_worker.ports.RenderExecutor`
into ``render-jobs/<id>/`` and reports pass 1 as 0..0.5 and pass 2 as 0.5..1. ``DELETE`` removes
a job at once; a running render stops at its next block and leaves nothing behind.
"""

import logging
from pathlib import Path
from typing import Any

from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lane_service import LaneJobService
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.render_store import RenderJobStore, StoredRenderJob
from stemstudio_worker.ports import (
    InputNotFoundError,
    InputPathError,
    RenderCancelledError,
    RenderError,
    RenderExecutor,
)
from stemstudio_worker.schemas import (
    LIBRARY_NOT_CHECKED,
    LIBRARY_UNAVAILABLE_TEXT,
    LaneHealth,
    LibraryHealth,
    RenderJob,
    RenderJobRequest,
    RenderJobStatus,
    RenderResult,
)

_LOG = logging.getLogger(__name__)
_S = RenderJobStatus


class NoLibrary:
    """Library reader of a worker built without the ``render`` package: never readable."""

    def status(self) -> LibraryHealth:
        """Always ``LIBRARY_NOT_CHECKED`` (readable false, fixed reason)."""
        return LIBRARY_NOT_CHECKED

    def resolve(self, path: str) -> Path:  # noqa: ARG002 - port signature
        """Refuse every path: there is no library."""
        raise RenderError(LIBRARY_UNAVAILABLE_TEXT)


class NoRenderer:
    """Render executor of a worker built without the ``render`` package: every job fails."""

    def validate(self, request: RenderJobRequest) -> None:
        """Accept the request; the job fails when it runs."""

    def render(
        self,
        request: RenderJobRequest,  # noqa: ARG002
        out_dir: Path,  # noqa: ARG002
        progress: object,  # noqa: ARG002
    ) -> RenderResult:
        """Fail with the "library is not available" text."""
        raise RenderError(LIBRARY_UNAVAILABLE_TEXT)


class RenderService(LaneJobService[StoredRenderJob]):
    """Creates, runs, reads and deletes render jobs; owns the ``render`` lane."""

    LABEL = "Render"
    CANCELLED = RenderCancelledError
    ERRORS = (RenderError,)

    def __init__(self, store: RenderJobStore, executor: RenderExecutor, clock: Clock) -> None:
        """Serve the loaded ``store`` with ``executor``; use :meth:`start` in production."""
        super().__init__("render", store, clock)
        self._executor = executor

    @classmethod
    def start(cls, paths: DataPaths, executor: RenderExecutor, clock: Clock) -> "RenderService":
        """Load the jobs, fail the ones a restart interrupted and start the lane."""
        store = RenderJobStore(paths)
        store.load_all()
        store.reconcile(clock)
        service = cls(store, executor, clock)
        service._lane.start()
        return service

    def create(self, request: RenderJobRequest) -> RenderJob:
        """Check the inputs and queue a job.

        Raises:
            InputPathError: An input path is not allowed (HTTP 400).
            InputNotFoundError: An input file does not exist (HTTP 404).

        Any other ``RenderError`` (e.g. the library is not mounted) is not raised: the job is
        queued and fails with that text when it runs.
        """
        try:
            self._executor.validate(request)
        except (InputPathError, InputNotFoundError):
            raise
        except RenderError:
            _LOG.warning("Render job queued although its inputs cannot be checked now")
        now = self._clock()
        job = RenderJob(render_job_id=new_id(), status=_S.QUEUED, progress=0,
                        created_at=now, updated_at=now)  # fmt: skip
        self._store.save(StoredRenderJob(job=job, request=request))
        self._lane.submit(job.render_job_id)
        return job

    def get(self, job_id: str) -> RenderJob | None:
        """The job with ``job_id``, or None."""
        job: RenderJob | None = self.job_of(job_id)
        return job

    def output(self, job_id: str, result: RenderResult) -> Path | None:
        """The file ``result`` describes for job ``job_id``, or None when it is gone."""
        path = self._store.output_dir(job_id) / result.file_name
        return path if path.is_file() else None

    def health(self) -> LaneHealth:
        """State of the render lane (``Health.lanes.render``)."""
        return self.lane_health()

    def process(self, job_id: str) -> None:
        """Render one job to a terminal state (lane thread); skips unknown and finished jobs."""
        self._run_job(job_id, self._render)

    def _render(self, record: StoredRenderJob, out_dir: Path) -> dict[str, Any]:
        reporter = _Reporter(self, record.job.render_job_id)
        result = self._executor.render(record.request, out_dir, reporter)
        return {"status": _S.COMPLETED, "progress": 1.0, "result": result}


class _Reporter:
    """``RenderProgress`` of one job: stores the progress, stops a deleted job."""

    def __init__(self, service: RenderService, job_id: str) -> None:
        self._service = service
        self._job_id = job_id
        self._last = 0.0

    def __call__(self, fraction: float, /) -> None:
        self._last = max(self._last, min(max(fraction, 0.0), 1.0))
        if not self._service.update(self._job_id, progress=self._last):
            raise RenderCancelledError("The render job was deleted")
