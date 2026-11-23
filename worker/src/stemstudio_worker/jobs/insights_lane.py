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
from stemstudio_worker.jobs.lanes import SharedLane
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.quality_store import QualityJobStore

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


@dataclass(frozen=True, slots=True)
class ThreadLimit:
    """The intra-op thread limit of every insights job.

    Attributes:
        threads: Thread count (``STEMSTUDIO_INSIGHTS_THREADS``, 1..16).
        cap: Enters the limit around one job.
    """

    threads: int
    cap: ThreadCap = blas_threads


class InsightsLane(SharedLane):
    """The shared insights lane over :class:`InsightsStores`."""

    def __init__(self, stores: InsightsStores, clock: Clock, limit: ThreadLimit) -> None:
        """Create a stopped lane over loaded ``stores``; ``clock`` stamps the sweep."""
        super().__init__("insights")
        self.stores = stores
        self._clock = clock
        self._limit = limit

    @classmethod
    def loaded(cls, paths: DataPaths, clock: Clock, limit: ThreadLimit) -> "InsightsLane":
        """Load both stores and fail the jobs a restart interrupted (the lane is stopped)."""
        stores = InsightsStores.of(paths)
        stores.load_all()
        stores.reconcile(clock)
        return cls(stores, clock, limit)

    def submit(self, process: Callable[[str], None], job_id: str) -> None:
        """Queue ``process(job_id)`` behind the jobs of both kinds already waiting."""
        self.scheduler.submit_call(self._capped(process), job_id)

    def _capped(self, process: Callable[[str], None]) -> Callable[[str], None]:
        def run(job_id: str) -> None:
            with self._limit.cap(self._limit.threads):
                process(job_id)

        return run

    def sweep(self) -> list[str]:
        """Remove terminal jobs of both kinds older than 24 hours."""
        return self.stores.sweep(self._clock())
