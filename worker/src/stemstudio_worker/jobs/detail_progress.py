"""State changes of one detail job (data-model.md 4.2); terminal jobs are never changed again."""

from typing import Any

from stemstudio_worker.jobs import detail_texts as texts
from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import DeviceUse, merge_device
from stemstudio_worker.schemas import DetailJobState, DetailResult, ParentStemName

_MAX_ERROR = 1000


class DetailTracker:
    """Applies updates to one detail job; progress never decreases."""

    def __init__(self, store: DetailJobStore, detail_id: str, clock: Clock) -> None:
        """Track ``detail_id`` in ``store``, stamping ``updatedAt`` with ``clock``."""
        self._store = store
        self._detail_id = detail_id
        self._clock = clock

    @property
    def cancel_requested(self) -> bool:
        """Whether a cancel was requested (or the job already ended)."""
        record = self._store.get(self._detail_id)
        return record is None or record.cancel_requested or record.job.status.is_terminal

    def running(self, stage: str, progress: int, current_parent: ParentStemName | None) -> None:
        """Report ``running`` with ``stage``, ``progress`` (kept monotonic) and the parent."""
        self._change(status=DetailJobState.RUNNING, stage=stage, progress=progress,
                     current_parent=current_parent)  # fmt: skip

    def complete(self, result: DetailResult) -> None:
        """Mark the job completed with ``result`` (``updatedAt`` = ``result.completedAt``)."""
        changes = {"status": DetailJobState.COMPLETED, "stage": texts.COMPLETED,
                   "progress": 100, "current_parent": None}  # fmt: skip
        self._store.update(self._detail_id, lambda r: _apply(r, changes, result))

    def fail(self, error: str) -> None:
        """Mark the job failed with a readable ``error`` (clipped to 1000 characters)."""
        self._change(status=DetailJobState.FAILED, stage=texts.FAILED, current_parent=None,
                     error=error[:_MAX_ERROR])  # fmt: skip

    def waiting(self) -> None:
        """Report that the job waits for standard separations (status and progress kept)."""
        self._change(stage=texts.WAITING, current_parent=None)

    def cancel(self) -> None:
        """Mark the job cancelled (no-op when it already ended)."""
        self._change(status=DetailJobState.CANCELLED, stage=texts.CANCELLED, current_parent=None)

    def fold_device(self, use: DeviceUse) -> None:
        """Fold where a parent step ran into the job's device record (data-model.md 2.2)."""
        now = self._clock()
        self._store.update(self._detail_id, lambda r: _fold(r, use, now))

    def _change(self, **changes: Any) -> None:
        stamped = {**changes, "updated_at": self._clock()}
        self._store.update(self._detail_id, lambda r: _apply(r, stamped, None))


def _fold(record: StoredDetailJob, use: DeviceUse, now: Any) -> StoredDetailJob:
    """Return ``record`` with ``use`` folded into its device unless the job already ended."""
    if record.job.status.is_terminal:
        return record
    device = merge_device(record.job.device, use)
    return record.model_copy(
        update={"job": record.job.model_copy(update={"device": device, "updated_at": now})}
    )


def _apply(
    record: StoredDetailJob, changes: dict[str, Any], result: DetailResult | None
) -> StoredDetailJob:
    """Return ``record`` with ``changes`` (and ``result``) unless the job already ended."""
    if record.job.status.is_terminal:
        return record
    progress = max(record.job.progress, changes.get("progress", record.job.progress))
    if result is not None:
        changes = {**changes, "updated_at": result.completed_at}
    job = record.job.model_copy(update={**changes, "progress": progress})
    return record.model_copy(update={"job": job, "result": result or record.result})
