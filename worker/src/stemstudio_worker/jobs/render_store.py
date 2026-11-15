"""Render job records (feature 007, data-model.md 2.5).

``render-jobs/<id>.json`` holds the job and its request; ``render-jobs/<id>/output.<ext>`` the
rendered file. After a restart unfinished jobs are failed ("interrupted by restart", ADR-003);
terminal jobs are swept after 24 hours; a delete removes both (idempotent).
"""

import re
import shutil
from datetime import datetime, timedelta
from pathlib import Path

from stemstudio_worker.jobs.json_store import JsonStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    ContractModel,
    RenderJob,
    RenderJobRequest,
    RenderJobStatus,
)

MAX_AGE = timedelta(hours=24)
"""Terminal render jobs older than this are swept with their output."""

_ID = re.compile(r"[0-9a-f]{32}")


class StoredRenderJob(ContractModel):
    """Content of ``render-jobs/<id>.json``: the wire job and its validated request."""

    job: RenderJob
    request: RenderJobRequest


def _job_id(record: StoredRenderJob) -> str:
    return record.job.render_job_id


class RenderJobStore(JsonStore[StoredRenderJob]):
    """Persists render jobs below ``paths.render_jobs``."""

    def __init__(self, paths: DataPaths) -> None:
        """Create a store over the render job directory of ``paths``."""
        super().__init__(paths.render_jobs, StoredRenderJob, _job_id)
        self._paths = paths

    def output_dir(self, job_id: str) -> Path:
        """Job-private output directory, named after the job (the renderer's dither seed)."""
        return self._paths.render_jobs / job_id

    def reconcile(self, clock: Clock) -> list[str]:
        """Fail every queued or running job ("interrupted by restart") and drop its output."""
        interrupted = [r for r in self.all() if not r.job.status.is_terminal]
        for record in interrupted:
            job = record.job.model_copy(update={"status": RenderJobStatus.FAILED,
                                                "error": RESTART_INTERRUPTED_TEXT,
                                                "updated_at": clock()})  # fmt: skip
            self.save(record.model_copy(update={"job": job}))
            shutil.rmtree(self.output_dir(job.render_job_id), ignore_errors=True)
        return [r.job.render_job_id for r in interrupted]

    def remove(self, job_id: str) -> None:
        """Delete the record and the output of a job (no-op for unknown or invalid ids)."""
        if _ID.fullmatch(job_id):
            self.delete(job_id)
            shutil.rmtree(self.output_dir(job_id), ignore_errors=True)

    def sweep(self, now: datetime) -> list[str]:
        """Remove terminal jobs last updated more than 24 hours before ``now``."""
        cutoff = now - MAX_AGE
        expired = [
            r.job.render_job_id
            for r in self.all()
            if r.job.status.is_terminal and r.job.updated_at < cutoff
        ]
        for job_id in expired:
            self.remove(job_id)
        return expired
