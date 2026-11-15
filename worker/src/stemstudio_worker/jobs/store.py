"""Thread-safe job store: in-memory index backed by one JSON file per job (atomic replace)."""

from pathlib import Path

from stemstudio_worker.jobs.json_store import JsonStore, new_id
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.schemas import JobState


def _job_id(record: StoredJob) -> str:
    return record.job.job_id


class JobStore(JsonStore[StoredJob]):
    """Persists :class:`StoredJob` records as ``<jobs_dir>/<jobId>.json``."""

    def __init__(self, jobs_dir: Path) -> None:
        """Create a store over ``jobs_dir`` (created on first write or load)."""
        super().__init__(jobs_dir, StoredJob, _job_id)

    @staticmethod
    def new_job_id() -> str:
        """Return a fresh job id: ``uuid4().hex`` (32 lower-case hex characters)."""
        return new_id()

    def find_active(self, source_key: str) -> StoredJob | None:
        """Return the newest non-failed job for ``source_key``, if any."""
        matches = [
            r
            for r in self.all()
            if r.job.source_key == source_key and r.job.status is not JobState.FAILED
        ]
        return max(matches, key=lambda r: r.job.created_at, default=None)
