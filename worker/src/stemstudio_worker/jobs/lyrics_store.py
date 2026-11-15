"""Lyrics job records and results (feature 006, data-model.md 2.2).

``lyrics-jobs/<id>.json`` holds the job and its request, ``lyrics-jobs/<id>/result.json`` the
result of a completed job. Lyrics are copyrighted: the backend deletes a job once it copied the
result, and terminal jobs are swept after 24 hours (ADR-015 D6). Neither the request text nor
the result is ever logged.
"""

import shutil
from datetime import datetime, timedelta

from pydantic import ValidationError

from stemstudio_worker.jobs.json_store import JsonStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import (
    ContractModel,
    CreateLyricsJobRequest,
    LyricsJob,
    LyricsResult,
)

MAX_AGE = timedelta(hours=24)
"""Terminal lyrics jobs older than this are swept with their result."""


class StoredLyricsJob(ContractModel):
    """Content of ``lyrics-jobs/<id>.json``: the wire job and its validated request."""

    job: LyricsJob
    request: CreateLyricsJobRequest


def _job_id(record: StoredLyricsJob) -> str:
    return record.job.lyrics_job_id


class LyricsJobStore(JsonStore[StoredLyricsJob]):
    """Persists lyrics jobs below ``paths.lyrics_jobs``."""

    def __init__(self, paths: DataPaths) -> None:
        """Create a store over the lyrics job directory of ``paths``."""
        super().__init__(paths.lyrics_jobs, StoredLyricsJob, _job_id)
        self._paths = paths

    def save_result(self, job_id: str, result: LyricsResult) -> None:
        """Write ``result.json`` of a job (atomic replace)."""
        target = self._paths.lyrics_result(job_id)
        target.parent.mkdir(parents=True, exist_ok=True)
        temp = target.with_name(target.name + ".tmp")
        temp.write_text(result.model_dump_json(by_alias=True), encoding="utf-8")
        temp.replace(target)

    def result(self, job_id: str) -> LyricsResult | None:
        """The stored result of a job; None when it is missing or unreadable."""
        try:
            raw = self._paths.lyrics_result(job_id).read_text(encoding="utf-8")
            return LyricsResult.model_validate_json(raw)
        except (OSError, ValidationError):
            return None

    def unfinished(self) -> list[StoredLyricsJob]:
        """Every queued or running job, oldest first (run again after a restart)."""
        pending = [r for r in self.all() if not r.job.status.is_terminal]
        return sorted(pending, key=lambda r: r.job.created_at)

    def expired(self, cutoff: datetime) -> list[str]:
        """Ids of terminal jobs last updated before ``cutoff``."""
        return [
            r.job.lyrics_job_id
            for r in self.all()
            if r.job.status.is_terminal and r.job.updated_at < cutoff
        ]

    def remove(self, job_id: str) -> bool:
        """Delete the record and the job's files; False when the job was not known."""
        known = self.delete(job_id)
        if known:
            shutil.rmtree(self._paths.lyrics_job_dir(job_id), ignore_errors=True)
        return known
