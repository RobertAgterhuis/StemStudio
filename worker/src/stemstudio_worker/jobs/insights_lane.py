"""The ``insights`` lane: one thread, FIFO across quality and handout jobs (feature 020, 2.1).

Quality jobs (stem quality reports) and handout jobs (lesson PDFs) share this lane, so they never
wait behind a separation, a render or a transcription and exports keep their 15-second target.
Every job runs under a cap of the numpy/scipy BLAS thread pools (``STEMSTUDIO_INSIGHTS_THREADS``).
The lane owns the sweep of both stores (``Health.lanes.insights``).
"""

import importlib
from collections.abc import Callable, Iterator
from contextlib import AbstractContextManager, contextmanager
from dataclasses import dataclass
from datetime import datetime
from typing import Any

from stemstudio_worker.jobs.handout_store import HandoutJobStore
from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.quality_store import QualityJobStore
from stemstudio_worker.schemas import LaneHealth

ThreadCap = Callable[[int], AbstractContextManager[object]]
"""Enters a limit of ``n`` intra-op threads for the duration of one job."""


def _import(name: str) -> Any:
    """Import ``name`` (replaced in tests)."""
    return importlib.import_module(name)


@contextmanager
def blas_threads(limit: int) -> Iterator[None]:
    """Limit the BLAS pools (``threadpoolctl``) to ``limit`` threads; a no-op without it."""
    try:
        threadpoolctl = _import("threadpoolctl")
    except ImportError:
        yield
        return
    with threadpoolctl.threadpool_limits(limits=limit, user_api="blas"):
        yield


@dataclass(frozen=True, slots=True)
class InsightsStores:
    """The two stores of the ``insights`` lane.

    Attributes:
        quality: Quality jobs.
        handouts: Handout jobs and their PDFs.
    """

    quality: QualityJobStore
    handouts: HandoutJobStore

    @classmethod
    def of(cls, paths: DataPaths) -> "InsightsStores":
        """Stores over the insights directories of ``paths`` (not loaded yet)."""
        return cls(QualityJobStore(paths.quality_jobs), HandoutJobStore(paths.handout_jobs))

    def load_all(self) -> None:
        """Read every record file of both stores."""
        self.quality.load_all()
        self.handouts.load_all()

    def reconcile(self, clock: Clock) -> list[str]:
        """Fail the unfinished jobs of both stores ("interrupted by restart")."""
        return [*self.quality.reconcile(clock), *self.handouts.reconcile(clock)]

    def sweep(self, now: datetime) -> list[str]:
        """Remove the terminal jobs older than 24 hours of both stores."""
        return [*self.quality.sweep(now), *self.handouts.sweep(now)]


def _unknown_kind(job_id: str) -> None:  # pragma: no cover - every job names its process
    """Default process of the lane: jobs are always submitted with their kind's function."""
    raise LookupError(job_id)


class InsightsLane:
    """The shared insights lane over :class:`InsightsStores`."""

    def __init__(self, stores: InsightsStores, clock: Clock, threads: int, cap: ThreadCap) -> None:
        """Create a stopped lane over loaded ``stores``.

        Args:
            stores: The two job stores.
            clock: Stamps the sweep.
            threads: Intra-op thread limit of every job (1..16).
            cap: Enters the thread limit around one job.
        """
        self.stores = stores
        self.scheduler = LaneScheduler("insights", _unknown_kind)
        self._clock = clock
        self._threads = threads
        self._cap = cap

    @classmethod
    def loaded(
        cls, paths: DataPaths, clock: Clock, threads: int, cap: ThreadCap = blas_threads
    ) -> "InsightsLane":
        """Load both stores and fail the jobs a restart interrupted (the lane is stopped)."""
        stores = InsightsStores.of(paths)
        stores.load_all()
        stores.reconcile(clock)
        return cls(stores, clock, threads, cap)

    def submit(self, process: Callable[[str], None], job_id: str) -> None:
        """Queue ``process(job_id)`` behind the jobs of both kinds already waiting."""
        self.scheduler.submit_call(self._capped(process), job_id)

    def _capped(self, process: Callable[[str], None]) -> Callable[[str], None]:
        def run(job_id: str) -> None:
            with self._cap(self._threads):
                process(job_id)

        return run

    def start(self) -> None:
        """Start the lane thread."""
        self.scheduler.start()

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane thread; a running job fails at the next start."""
        self.scheduler.stop(timeout)

    def join(self, timeout: float | None = None) -> bool:
        """Block until no job waits or runs; False on timeout."""
        return self.scheduler.join(timeout)

    def health(self) -> LaneHealth:
        """Waiting jobs and whether one runs (``Health.lanes.insights``)."""
        return self.scheduler.health()

    def sweep(self) -> list[str]:
        """Remove terminal jobs of both kinds older than 24 hours."""
        return self.stores.sweep(self._clock())
