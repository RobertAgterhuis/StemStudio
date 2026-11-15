"""Job records with an output folder, as the render (007) and transcription (008) lanes keep them.

``<dir>/<id>.json`` holds the record, ``<dir>/<id>/`` the job's files. After a restart unfinished
jobs are failed ("interrupted by restart", ADR-003) and their files dropped; terminal jobs are
swept after 24 hours; a delete removes both and is idempotent.
"""

import re
import shutil
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, cast

from pydantic import BaseModel

from stemstudio_worker.jobs.job_records import JobRecordStore
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.schemas import RESTART_INTERRUPTED_TEXT, RenderJobStatus

MAX_AGE = timedelta(hours=24)
"""Terminal jobs older than this are swept with their files."""

_ID = re.compile(r"[0-9a-f]{32}")


class LaneJobStore[R: BaseModel](JobRecordStore[R]):
    """Adds the output folder, the restart rule, delete and sweep to a job record store."""

    def output_dir(self, job_id: str) -> Path:
        """The job-private folder of the job's files."""
        return self._dir / job_id

    def reconcile(self, clock: Clock) -> list[str]:
        """Fail every queued or running job ("interrupted by restart") and drop its files."""
        interrupted = self.unfinished()
        for record in interrupted:
            self.save(_interrupted(record, clock))
            shutil.rmtree(self.output_dir(self._key(record)), ignore_errors=True)
        return [self._key(r) for r in interrupted]

    def remove(self, job_id: str) -> None:
        """Delete the record and the files of a job (no-op for unknown or invalid ids)."""
        if _ID.fullmatch(job_id):
            self.delete(job_id)
            shutil.rmtree(self.output_dir(job_id), ignore_errors=True)

    def sweep(self, now: datetime) -> list[str]:
        """Remove terminal jobs last updated more than 24 hours before ``now``."""
        expired = self.expired(now - MAX_AGE)
        for job_id in expired:
            self.remove(job_id)
        return expired


def _interrupted[R: BaseModel](record: R, clock: Clock) -> R:
    """The record with its job failed by the restart (a ``stage`` is cleared when it has one)."""
    job: Any = cast(Any, record).job
    changes: dict[str, Any] = {"status": RenderJobStatus.FAILED, "error": RESTART_INTERRUPTED_TEXT,
                               "updated_at": clock()}  # fmt: skip
    if "stage" in type(job).model_fields:
        changes["stage"] = None
    return record.model_copy(update={"job": job.model_copy(update=changes)})
