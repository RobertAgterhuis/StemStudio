"""Thread-safe job store: in-memory index backed by one JSON file per job (atomic replace)."""

import logging
import re
import threading
import uuid
from pathlib import Path

from pydantic import ValidationError

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.schemas import JobState

_LOG = logging.getLogger(__name__)
_JOB_ID = re.compile(r"[0-9a-f]{32}")


class JobStore:
    """Persists :class:`StoredJob` records as ``<jobs_dir>/<jobId>.json``."""

    def __init__(self, jobs_dir: Path) -> None:
        """Create a store over ``jobs_dir`` (created on first write or load)."""
        self._dir = jobs_dir
        self._lock = threading.Lock()
        self._jobs: dict[str, StoredJob] = {}

    @staticmethod
    def new_job_id() -> str:
        """Return a fresh job id: ``uuid4().hex`` (32 lower-case hex characters)."""
        return uuid.uuid4().hex

    def load_all(self) -> None:
        """Read every job file into memory; unreadable files are logged and skipped."""
        self._dir.mkdir(parents=True, exist_ok=True)
        for path in sorted(self._dir.glob("*.json")):
            record = _read(path)
            if record is not None:
                with self._lock:
                    self._jobs[record.job.job_id] = record

    def save(self, record: StoredJob) -> None:
        """Write ``record`` to a temporary file, then atomically replace the job file."""
        self._dir.mkdir(parents=True, exist_ok=True)
        target = self._dir / f"{record.job.job_id}.json"
        temp = target.with_name(target.name + ".tmp")
        with self._lock:
            temp.write_text(record.model_dump_json(by_alias=True), encoding="utf-8")
            temp.replace(target)
            self._jobs[record.job.job_id] = record

    def get(self, job_id: str) -> StoredJob | None:
        """Return the job with ``job_id``, or None when unknown or not a valid id."""
        if not _JOB_ID.fullmatch(job_id):
            return None
        with self._lock:
            return self._jobs.get(job_id)

    def all(self) -> list[StoredJob]:
        """Return a snapshot of every known job."""
        with self._lock:
            return list(self._jobs.values())

    def find_active(self, source_key: str) -> StoredJob | None:
        """Return the newest non-failed job for ``source_key``, if any."""
        matches = [
            r
            for r in self.all()
            if r.job.source_key == source_key and r.job.status is not JobState.FAILED
        ]
        return max(matches, key=lambda r: r.job.created_at, default=None)


def _read(path: Path) -> StoredJob | None:
    """Parse one job file; return None (and log) when it is not a valid record."""
    try:
        return StoredJob.model_validate_json(path.read_text(encoding="utf-8"))
    except (OSError, ValidationError) as exc:
        _LOG.warning("Skipping unreadable job file %s: %s", path.name, exc)
        return None
