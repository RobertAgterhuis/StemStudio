"""Job state updates of one running job: status, stage, progress and the cancel flag.

``JobReporter`` is the only writer of a job while it is processed: every change is validated
(``Generation`` cross-field rules), stamped with the clock and saved. ``progress`` is the
engine's ``ProgressCallback`` and ``wait`` the gate's ``WaitCallback``; both raise
``Cancelled`` once a cancel was requested, so engines stop between diffusion steps and waits
end. Jobs that have not reached the GPU yet are cancelled immediately (data-model 2.4).
"""

import threading
from collections.abc import Mapping
from dataclasses import dataclass, field
from datetime import datetime
from typing import Final

from stemstudio_generator.ports import Cancelled, Clock, JobStore
from stemstudio_generator.schemas import (
    TERMINAL_STATUSES,
    GeneratedTake,
    Generation,
    GenerationError,
    GenerationErrorCode,
    GenerationStatus,
    JobDevice,
)

QUEUED_STAGE: Final = "Queued"
"""Stage text of a new job."""

COMPLETED_STAGE: Final = "Completed"
"""Stage text of a completed job."""

FAILED_STAGE: Final = "Failed"
"""Stage text of a failed job (the reason is in ``error``)."""

CANCELLED_STAGE: Final = "Cancelled"
"""Stage text of a cancelled job."""

_BEFORE_GPU: Final = frozenset({"queued", "waitingForGpu"})
_PERCENT: Final = 100


@dataclass(frozen=True, slots=True)
class JobOutcome:
    """What a completed job step produced.

    Attributes:
        takes: The takes (FLAC files already written, hashed and tagged).
        warnings: Readable warnings (for example clipped source stems).
    """

    takes: list[GeneratedTake]
    warnings: list[str] = field(default_factory=list)


def updated(job: Generation, changes: Mapping[str, object], now: datetime) -> Generation:
    """``job`` with ``changes`` (field names) and ``updated_at = now``, validated."""
    return Generation.model_validate({**job.model_dump(), **changes, "updated_at": now})


class JobReporter:
    """Writes the state of one job while it is queued or processed."""

    def __init__(self, job: Generation, store: JobStore, clock: Clock) -> None:
        """Report for ``job`` (already saved) into ``store``."""
        self._job = job
        self._store = store
        self._clock = clock
        self._lock = threading.Lock()
        self._cancel = False

    @property
    def job(self) -> Generation:
        """The current state."""
        return self._job

    @property
    def cancel_requested(self) -> bool:
        """Whether a cancel was requested."""
        return self._cancel

    def request_cancel(self) -> Generation:
        """Cancel: immediately before the GPU step, else at the next callback; terminal: no-op."""
        with self._lock:
            if self._job.status not in TERMINAL_STATUSES:
                self._cancel = True
                if self._job.status in _BEFORE_GPU:
                    self._write({"status": "cancelled", "stage": CANCELLED_STAGE})
            return self._job

    def wait(self, stage: str) -> None:
        """``WaitCallback``: ``waitingForGpu`` (a running retry stays ``running``)."""
        status = "running" if self._job.status == "running" else "waitingForGpu"
        self._update({"status": status, "stage": stage})

    def status(self, status: GenerationStatus, stage: str) -> None:
        """Enter ``status`` (a new phase: progress starts at 0)."""
        self._update({"status": status, "stage": stage, "progress": 0})

    def progress(self, fraction: float, stage: str) -> None:
        """``ProgressCallback``: integer percentage, never lower than before."""
        percent = round(min(max(fraction, 0.0), 1.0) * _PERCENT)
        self._update({"progress": max(percent, self._job.progress), "stage": stage})

    def admitted(self, device: JobDevice) -> None:
        """Record the device the gate granted."""
        self._update({"device": device})

    def complete(self, outcome: JobOutcome) -> None:
        """Finish as ``completed`` with the takes."""
        self._update({
            "status": "completed", "progress": _PERCENT, "stage": COMPLETED_STAGE,
            "takes": outcome.takes, "warnings": outcome.warnings,
        })  # fmt: skip

    def fail(self, code: GenerationErrorCode, message: str) -> None:
        """Finish as ``failed``."""
        error = GenerationError(code=code, message=message)
        self._update({"status": "failed", "stage": FAILED_STAGE, "error": error})

    def cancelled(self) -> None:
        """Finish as ``cancelled`` (idempotent)."""
        with self._lock:
            if self._job.status != "cancelled":
                self._write({"status": "cancelled", "stage": CANCELLED_STAGE})

    def _update(self, changes: Mapping[str, object]) -> None:
        with self._lock:
            if self._cancel:
                raise Cancelled
            self._write(changes)

    def _write(self, changes: Mapping[str, object]) -> None:
        self._job = updated(self._job, changes, self._clock.now())
        self._store.save(self._job)
