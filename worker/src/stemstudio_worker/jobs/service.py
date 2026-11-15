"""Job creation with idempotency per source key (one job per non-failed source)."""

import threading
from collections.abc import Callable

from stemstudio_worker.jobs.models import StoredJob, metadata_of, pending_artwork
from stemstudio_worker.jobs.progress import QUEUED, Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import SourceInfo
from stemstudio_worker.schemas import Job, JobState


class JobService:
    """Creates jobs; a queued, running or completed job for the same source key is reused."""

    def __init__(self, store: JobStore, submit: Callable[[str], None], clock: Clock) -> None:
        """Create the service.

        Args:
            store: Where jobs are persisted.
            submit: Enqueues a new job id for processing.
            clock: Current UTC time.
        """
        self._store = store
        self._submit = submit
        self._clock = clock
        self._lock = threading.Lock()

    def create(
        self, source: SourceInfo, job_id: str | None = None, upload_file: str | None = None
    ) -> tuple[Job, bool]:
        """Return the existing non-failed job for ``source.source_key`` or queue a new one.

        Args:
            source: The resolved source.
            job_id: Id to use for a new job (uploads are stored under it first); generated
                when omitted.
            upload_file: Generated upload file name below ``uploads/<jobId>/``.

        Returns:
            The job and whether it was newly created.
        """
        with self._lock:
            existing = self._store.find_active(source.source_key)
            if existing is not None:
                return existing.job, False
            record = self._new_record(source, job_id or JobStore.new_job_id(), upload_file)
            self._store.save(record)
        self._submit(record.job.job_id)
        return record.job, True

    def _new_record(self, source: SourceInfo, job_id: str, upload_file: str | None) -> StoredJob:
        now = self._clock()
        job = Job(
            job_id=job_id, source_key=source.source_key, source_type=source.source_type,
            status=JobState.QUEUED, stage=QUEUED, progress=0, metadata=metadata_of(source),
            created_at=now, updated_at=now,
        )  # fmt: skip
        return StoredJob(
            job=job, upload_file=upload_file, artwork_candidate=pending_artwork(source)
        )
