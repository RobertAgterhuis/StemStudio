"""Tests of the ``insights`` lane (020 T016, data-model 2.1): FIFO, one job, thread cap, health."""

import threading
from collections.abc import Callable, Iterator
from contextlib import contextmanager
from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs import insights_lane
from stemstudio_worker.jobs.handout_store import StoredHandoutJob
from stemstudio_worker.jobs.insights_lane import InsightsLane, InsightsStores, blas_threads
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.quality_store import StoredQualityJob
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    HandoutJob,
    LaneHealth,
    QualityJob,
    RenderJobStatus,
)

from .conftest import T0
from .fakes import FakeClock
from .insights_requests import handout_request, quality_request

S = RenderJobStatus
WAIT = 5.0


class CapSpy:
    """A thread cap that records its limit and whether a job ran inside it."""

    def __init__(self) -> None:
        self.limits: list[int] = []
        self.inside = False

    @contextmanager
    def __call__(self, limit: int) -> Iterator[None]:
        self.limits.append(limit)
        self.inside = True
        try:
            yield
        finally:
            self.inside = False


class Kinds:
    """Process functions of two job kinds that record their order and any overlap."""

    def __init__(self, cap: CapSpy) -> None:
        self.gate, self.started = threading.Event(), threading.Event()
        self.order: list[str] = []
        self.overlap: list[str] = []
        self.capped: list[bool] = []
        self._cap = cap
        self._running = threading.Lock()

    def of(self, name: str) -> Callable[[str], None]:
        def process(job_id: str) -> None:
            if not self._running.acquire(blocking=False):
                self.overlap.append(job_id)
                return
            try:
                self.capped.append(self._cap.inside)
                self.started.set()
                self.gate.wait(WAIT)
                self.order.append(f"{name}:{job_id}")
            finally:
                self._running.release()

        return process


@pytest.fixture
def cap() -> CapSpy:
    return CapSpy()


@pytest.fixture
def lane(tmp_path: Path, cap: CapSpy) -> Iterator[InsightsLane]:
    started = InsightsLane.loaded(DataPaths(tmp_path), FakeClock(), threads=3, cap=cap)
    started.start()
    yield started
    started.stop(WAIT)


def test_jobs_of_both_kinds_run_fifo_one_at_a_time_under_the_thread_cap(
    lane: InsightsLane, cap: CapSpy
) -> None:
    kinds = Kinds(cap)
    for name, job_id in (("quality", "a"), ("handout", "b"), ("quality", "c"), ("handout", "d")):
        lane.submit(kinds.of(name), job_id)
    assert kinds.started.wait(WAIT)

    busy = lane.health()
    kinds.gate.set()

    assert lane.join(WAIT)
    assert busy == LaneHealth(queued=3, running=True)
    assert kinds.order == ["quality:a", "handout:b", "quality:c", "handout:d"]
    assert kinds.overlap == []
    assert kinds.capped == [True] * 4
    assert cap.limits == [3] * 4
    assert lane.health() == LaneHealth(queued=0, running=False)


def test_a_crashing_job_does_not_stop_the_lane(lane: InsightsLane, cap: CapSpy) -> None:
    done: list[str] = []

    def crash(job_id: str) -> None:
        raise RuntimeError(job_id)

    lane.submit(crash, "a")
    lane.submit(done.append, "b")

    assert lane.join(WAIT)
    assert done == ["b"]
    assert not cap.inside


def _quality(job_id: str, status: S, age: timedelta = timedelta()) -> StoredQualityJob:
    job = QualityJob(quality_job_id=job_id, status=status, progress=0, created_at=T0 - age,
                     updated_at=T0 - age)  # fmt: skip
    return StoredQualityJob(job=job, request=quality_request())


def _handout(job_id: str, status: S, age: timedelta = timedelta()) -> StoredHandoutJob:
    job = HandoutJob(handout_job_id=job_id, status=status, progress=0, created_at=T0 - age,
                     updated_at=T0 - age)  # fmt: skip
    return StoredHandoutJob(job=job, request=handout_request())


def test_loading_fails_the_jobs_a_restart_interrupted(tmp_path: Path, cap: CapSpy) -> None:
    stores = InsightsStores.of(DataPaths(tmp_path))
    stores.quality.save(_quality("1" * 32, S.RUNNING))
    stores.handouts.save(_handout("2" * 32, S.QUEUED))
    stores.handouts.save(_handout("3" * 32, S.COMPLETED))

    lane = InsightsLane.loaded(DataPaths(tmp_path), FakeClock(), threads=4, cap=cap)

    quality = lane.stores.quality.get("1" * 32)
    handout = lane.stores.handouts.get("2" * 32)
    assert quality is not None
    assert handout is not None
    assert (quality.job.status, quality.job.error) == (S.FAILED, RESTART_INTERRUPTED_TEXT)
    assert (handout.job.status, handout.job.error) == (S.FAILED, RESTART_INTERRUPTED_TEXT)
    completed = lane.stores.handouts.get("3" * 32)
    assert completed is not None
    assert completed.job.status is S.COMPLETED


def test_sweep_covers_both_stores(tmp_path: Path, cap: CapSpy) -> None:
    stores = InsightsStores.of(DataPaths(tmp_path))
    stores.quality.save(_quality("1" * 32, S.COMPLETED, timedelta(hours=25)))
    stores.handouts.save(_handout("2" * 32, S.FAILED, timedelta(hours=25)))
    stores.handouts.save(_handout("3" * 32, S.FAILED, timedelta(hours=1)))
    lane = InsightsLane(stores, lambda: T0, 4, cap)

    assert sorted(lane.sweep()) == ["1" * 32, "2" * 32]
    assert stores.handouts.get("3" * 32) is not None


class FakeThreadpoolctl:
    """Stands in for ``threadpoolctl``: records the limits it was asked for."""

    def __init__(self) -> None:
        self.calls: list[dict[str, object]] = []

    @contextmanager
    def threadpool_limits(self, **kwargs: object) -> Iterator[None]:
        self.calls.append(kwargs)
        yield


def test_blas_threads_limits_the_blas_pools(monkeypatch: pytest.MonkeyPatch) -> None:
    fake = FakeThreadpoolctl()
    monkeypatch.setattr(insights_lane, "_import", lambda name: fake)

    with blas_threads(2):
        pass

    assert fake.calls == [{"limits": 2, "user_api": "blas"}]


def test_blas_threads_without_threadpoolctl_changes_nothing(monkeypatch: pytest.MonkeyPatch) -> None:
    def missing(name: str) -> object:
        raise ImportError(name)

    monkeypatch.setattr(insights_lane, "_import", missing)

    with blas_threads(2):
        ran = True

    assert ran
