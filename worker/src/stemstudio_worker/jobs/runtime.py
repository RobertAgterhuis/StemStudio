"""The running job subsystem: stores, reconciliation, scheduler thread and services."""

from dataclasses import dataclass
from datetime import UTC, datetime

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.detail_runner import DetailRunner
from stemstudio_worker.jobs.detail_service import DetailContext, DetailService
from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.reconcile import reconcile, reconcile_detail_jobs
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.jobs.scheduler import PriorityScheduler
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
        details: The detail job store.
        scheduler: The priority scheduler with its worker thread.
        service: Creates (or reuses) jobs.
        detail_service: Creates, reads and cancels detail jobs.
    """

    settings: WorkerSettings
    adapters: PipelineAdapters
    paths: DataPaths
    store: JobStore
    details: DetailJobStore
    scheduler: PriorityScheduler
    service: JobService
    detail_service: DetailService

    @classmethod
    def start(
        cls,
        settings: WorkerSettings,
        adapters: PipelineAdapters,
        pipeline: DetailPipelinePort,
        clock: Clock = utc_now,
    ) -> "WorkerRuntime":
        """Load the jobs, reconcile interrupted ones and start the worker thread."""
        paths = DataPaths(settings.data_dir)
        store, details = JobStore(paths.jobs), DetailJobStore(paths.detail_jobs)
        store.load_all()
        details.load_all()
        reconcile(store, paths, clock)
        reconcile_detail_jobs(details, paths, clock)
        context = DetailContext(store, details, paths, clock)
        runner = JobRunner(JobContext(store, paths, clock), adapters)
        scheduler = PriorityScheduler(runner.process, DetailRunner(context, pipeline).step)
        service = JobService(store, scheduler.submit, clock)
        detail_service = DetailService(context, scheduler.submit_detail)
        scheduler.start()
        return cls(settings, adapters, paths, store, details, scheduler, service, detail_service)

    def stop(self) -> None:
        """Stop the worker thread; a job still running is reconciled at the next start."""
        self.scheduler.stop(_STOP_TIMEOUT_SECONDS)
