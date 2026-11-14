"""The running job subsystem: store, reconciliation, runner thread and job service."""

from dataclasses import dataclass
from datetime import UTC, datetime

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.queue import JobQueue
from stemstudio_worker.jobs.reconcile import reconcile
from stemstudio_worker.jobs.runner import JobRunner
from stemstudio_worker.jobs.service import JobService
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import PipelineAdapters

_STOP_TIMEOUT_SECONDS = 5.0


def utc_now() -> datetime:
    """Return the current time in UTC."""
    return datetime.now(UTC)


@dataclass(frozen=True, slots=True)
class WorkerRuntime:
    """Everything the HTTP layer needs while the worker runs.

    Attributes:
        settings: Worker settings.
        adapters: The pipeline adapters (used directly for resolving URLs and accepting uploads).
        paths: Data directory layout.
        store: The job store.
        queue: The FIFO queue with its worker thread.
        service: Creates (or reuses) jobs.
    """

    settings: WorkerSettings
    adapters: PipelineAdapters
    paths: DataPaths
    store: JobStore
    queue: JobQueue
    service: JobService

    @classmethod
    def start(
        cls, settings: WorkerSettings, adapters: PipelineAdapters, clock: Clock = utc_now
    ) -> "WorkerRuntime":
        """Load the jobs, reconcile interrupted ones and start the worker thread."""
        paths = DataPaths(settings.data_dir)
        store = JobStore(paths.jobs)
        store.load_all()
        reconcile(store, paths, clock)
        queue = JobQueue(JobRunner(store, adapters, paths, clock).process)
        service = JobService(store, queue.submit, clock)
        queue.start()
        return cls(settings, adapters, paths, store, queue, service)

    def stop(self) -> None:
        """Stop the worker thread; a job still running is reconciled at the next start."""
        self.queue.stop(_STOP_TIMEOUT_SECONDS)
