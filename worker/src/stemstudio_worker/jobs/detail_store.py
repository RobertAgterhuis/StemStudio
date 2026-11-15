"""Detail job records ``detail-jobs/<detailJobId>.json`` (data-model.md 2.1)."""

from collections.abc import Callable
from pathlib import Path

from stemstudio_worker.jobs.json_store import JsonStore, new_id
from stemstudio_worker.schemas import ContractModel, DetailJob, DetailJobState, DetailResult

_REUSABLE = frozenset({DetailJobState.QUEUED, DetailJobState.RUNNING, DetailJobState.COMPLETED})


class StoredDetailJob(ContractModel):
    """Content of ``detail-jobs/<detailJobId>.json``.

    Attributes:
        job: The detail job as reported on the wire.
        cancel_requested: Set by the cancel endpoint; the running step stops before its next
            inference chunk.
        result: The result once completed.
    """

    job: DetailJob
    cancel_requested: bool = False
    result: DetailResult | None = None


def _detail_id(record: StoredDetailJob) -> str:
    return record.job.detail_job_id


class DetailJobStore(JsonStore[StoredDetailJob]):
    """Persists :class:`StoredDetailJob` records; changes go through :meth:`update`."""

    def __init__(self, directory: Path) -> None:
        """Create a store over ``directory`` (created on first write or load)."""
        super().__init__(directory, StoredDetailJob, _detail_id)

    @staticmethod
    def new_detail_id() -> str:
        """Return a fresh detail job id (32 lower-case hex characters)."""
        return new_id()

    def update(
        self, detail_id: str, change: Callable[[StoredDetailJob], StoredDetailJob]
    ) -> StoredDetailJob:
        """Apply ``change`` to the stored record atomically (no concurrent update interleaves).

        Raises:
            LookupError: ``detail_id`` is unknown.
        """
        with self._lock:
            record = self.get(detail_id)
            if record is None:
                msg = f"Detail job {detail_id[:64]} does not exist."
                raise LookupError(msg)
            changed = change(record)
            self.save(changed)
            return changed

    def find_reusable(self, audio_sha256: str) -> StoredDetailJob | None:
        """Return the newest queued, running or completed detail job for the audio, if any."""
        matches = [
            r
            for r in self.all()
            if r.job.audio_sha256 == audio_sha256 and r.job.status in _REUSABLE
        ]
        return max(matches, key=lambda r: r.job.created_at, default=None)
