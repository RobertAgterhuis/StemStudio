"""The storage services of the worker app and their wiring (feature 013).

One storage lock is shared by the janitor and the rehydrator (a clean-up never removes an entry
a rehydration is placing); the janitor also holds the scheduler's queue lock. Both drop the
meter's cached report when they are done.
"""

import threading
from collections.abc import Callable, Iterator
from contextlib import AbstractContextManager, contextmanager
from dataclasses import dataclass
from pathlib import Path

from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import CacheJanitor, StorageMeter
from stemstudio_worker.storage.janitor import DiskCacheJanitor, JanitorContext
from stemstudio_worker.storage.library import FileLibraryStemReader
from stemstudio_worker.storage.meter import DiskStorageMeter, MeterContext
from stemstudio_worker.storage.rehydrate import RehydrateContext, Rehydrator

Hold = Callable[[], AbstractContextManager[object]]
"""A context manager factory (a lock held for the duration of a ``with`` block)."""


@dataclass(frozen=True, slots=True)
class StorageServices:
    """What the storage routes call.

    Attributes:
        meter: ``GET /v1/storage``.
        janitor: ``POST /v1/storage/cleanup``.
        rehydrator: ``POST /v1/jobs/rehydrate``.
    """

    meter: StorageMeter
    janitor: CacheJanitor
    rehydrator: Rehydrator


@dataclass(frozen=True, slots=True)
class StorageWiring:
    """The parts of the running job subsystem the storage services use.

    Attributes:
        paths: Worker data directory layout (with the library root).
        jobs: The live standard job store.
        details: The live detail job store.
        model_dir: ``STEMSTUDIO_MODEL_DIR``.
        queue_hold: The scheduler's queue lock (``PriorityScheduler.hold``).
        sweep_artwork: Removes expired user pictures (``UserArtworkStore.sweep``).
        clock: Current UTC time.
    """

    paths: DataPaths
    jobs: JobStore
    details: DetailJobStore
    model_dir: Path
    queue_hold: Hold
    sweep_artwork: Callable[[], object]
    clock: Clock


def create_storage_services(wiring: StorageWiring) -> StorageServices:
    """Build the meter, the janitor and the rehydrator over ``wiring``."""
    meter = DiskStorageMeter(MeterContext(wiring.paths, wiring.model_dir, wiring.clock))
    lock = threading.Lock()
    rehydrator = Rehydrator(
        RehydrateContext(
            paths=wiring.paths, jobs=wiring.jobs, reader=FileLibraryStemReader(wiring.paths),
            clock=wiring.clock, lock=lambda: lock, on_change=meter.invalidate,
        )
    )  # fmt: skip
    return StorageServices(meter, _janitor(wiring, lock, meter.invalidate), rehydrator)


def _janitor(
    wiring: StorageWiring, lock: threading.Lock, on_change: Callable[[], None]
) -> DiskCacheJanitor:
    return DiskCacheJanitor(
        JanitorContext(
            paths=wiring.paths, jobs=wiring.jobs, details=wiring.details, clock=wiring.clock,
            lock=_both(wiring.queue_hold, lock), sweep_artwork=wiring.sweep_artwork,
            on_change=on_change,
        )
    )  # fmt: skip


def _both(queue_hold: Hold, lock: threading.Lock) -> Hold:
    """The queue lock, then the storage lock."""

    @contextmanager
    def held() -> Iterator[None]:
        with queue_hold(), lock:
            yield

    return held
