"""Restart reconciliation (data-model 2.4, SC-006).

At start-up no job can still be on the GPU: jobs found ``running``, ``waitingForGpu`` or
``downloadingModel`` fail with ``interrupted``; ``queued`` jobs stay queued and are returned in
submission order for the scheduler to resume.
"""

from typing import Final

from stemstudio_generator.jobs.progress import FAILED_STAGE, updated
from stemstudio_generator.ports import Clock, JobStore
from stemstudio_generator.schemas import INTERRUPTED_TEXT, GenerationError

_ACTIVE: Final = frozenset({"running", "waitingForGpu", "downloadingModel"})
_INTERRUPTED: Final = GenerationError(code="interrupted", message=INTERRUPTED_TEXT)


def reconcile(store: JobStore, clock: Clock) -> list[str]:
    """Fail interrupted jobs; return the ids of queued jobs, oldest first."""
    queued: list[str] = []
    for job in store.list_all():
        if job.status in _ACTIVE:
            changes = {"status": "failed", "stage": FAILED_STAGE, "error": _INTERRUPTED}
            store.save(updated(job, changes, clock.now()))
        elif job.status == "queued":
            queued.append(job.generation_id)
    return queued
