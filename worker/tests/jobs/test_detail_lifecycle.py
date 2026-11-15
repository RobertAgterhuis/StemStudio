"""Priority, cancel and retry of detail jobs end to end in the job subsystem (T065, US3, SC-006).

Uses the real scheduler, runners and the real detail pipeline with fake detail models.
"""

import threading
import time
from collections.abc import Callable, Iterator
from dataclasses import dataclass
from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail.fake import FakeSource
from stemstudio_worker.detail.steps import DetailPipeline
from stemstudio_worker.jobs.detail_store import StoredDetailJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import DetailJobState, JobState

from .detail_env import SHA, STD, completed_standard
from .fakes import Fakes
from .test_cache import OTHER

D = DetailJobState
WAIT = 10.0


class GateSleeper:
    """Fake-model sleeper: records chunks and can block or trigger a callback on a chunk."""

    def __init__(self) -> None:
        self.chunks = 0
        self.entered = threading.Event()
        self.release = threading.Event()
        self.release.set()
        self.on_chunk: dict[int, Callable[[], object]] = {}

    def __call__(self, seconds: float) -> None:
        self.chunks += 1
        self.entered.set()
        assert self.release.wait(WAIT)
        self.on_chunk.get(self.chunks, lambda: None)()


@dataclass
class Env:
    runtime: WorkerRuntime
    sleeper: GateSleeper
    fakes: Fakes

    def detail(self) -> str:
        job, _ = self.runtime.detail_service.create(STD)
        return job.detail_job_id

    def status(self, detail_id: str) -> D:
        return self.runtime.detail_service.get(detail_id).job.status


@pytest.fixture
def env(tmp_path: Path) -> Iterator[Env]:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake")
    sleeper = GateSleeper()
    fakes = Fakes.create()
    paths = DataPaths(tmp_path)
    completed_standard(paths, JobStore(paths.jobs))
    pipeline = DetailPipeline(FakeSource(0.04, sleeper))
    runtime = WorkerRuntime.start(settings, fakes.adapters(), pipeline)
    yield Env(runtime, sleeper, fakes)
    sleeper.release.set()
    runtime.stop()


def _seconds_until_fetch(env: Env) -> float:
    """Release the blocked detail step and measure until the standard job starts downloading."""
    released = time.monotonic()
    env.sleeper.release.set()
    while env.fakes.calls.names[:1] != ["fetch"] and time.monotonic() - released < WAIT:
        time.sleep(0.005)
    assert env.fakes.calls.names[:1] == ["fetch"]
    return time.monotonic() - released


def test_a_standard_job_starts_right_after_the_running_detail_step(env: Env) -> None:
    env.sleeper.release.clear()
    env.detail()
    assert env.sleeper.entered.wait(WAIT)
    env.runtime.service.create(OTHER)
    assert _seconds_until_fetch(env) < 5.0
    assert env.runtime.scheduler.join(WAIT)


def test_the_standard_job_completes_and_the_detail_job_continues(env: Env) -> None:
    env.sleeper.release.clear()
    env.detail()
    assert env.sleeper.entered.wait(WAIT)
    job, _ = env.runtime.service.create(OTHER)
    env.sleeper.release.set()
    assert env.runtime.scheduler.join(WAIT)
    standard = env.runtime.store.get(job.job_id)
    assert standard is not None
    assert standard.job.status is JobState.COMPLETED
    assert env.status(env.detail()) is D.COMPLETED


def test_cancel_during_a_step_stops_within_one_chunk(env: Env) -> None:
    detail_id_box: list[str] = []
    env.sleeper.on_chunk[1] = lambda: env.runtime.detail_service.cancel(detail_id_box[0])
    env.sleeper.release.clear()
    detail_id_box.append(env.detail())
    assert env.sleeper.entered.wait(WAIT)
    env.sleeper.release.set()
    assert env.runtime.scheduler.join(WAIT)
    assert env.status(detail_id_box[0]) is D.CANCELLED
    assert env.sleeper.chunks <= 2


def _start_with_hook(env: Env, chunk: int, hook: Callable[[str], object]) -> str:
    """Create a detail job whose fake model calls ``hook(detail_id)`` after chunk ``chunk``."""
    box: list[str] = []
    env.sleeper.on_chunk[chunk] = lambda: hook(box[0])
    env.sleeper.release.clear()
    box.append(env.detail())
    env.sleeper.release.set()
    return box[0]


def test_retry_after_cancel_resumes_from_the_finished_parents(env: Env) -> None:
    first = _start_with_hook(env, 5, env.runtime.detail_service.cancel)
    assert env.runtime.scheduler.join(WAIT)
    assert env.status(first) is D.CANCELLED
    partial = env.runtime.paths.detail_partial(SHA)
    assert (partial / "drums" / "outcome.json").is_file()
    assert not (partial / "vocals").exists()
    stages: list[str] = []
    second = _start_with_hook(
        env, env.sleeper.chunks + 1,
        lambda d: stages.append(env.runtime.detail_service.get(d).job.stage),
    )  # fmt: skip
    assert env.runtime.scheduler.join(WAIT)
    assert stages[:1] == ["Splitting vocals (1 of 4)"]
    assert env.status(second) is D.COMPLETED
    result = env.runtime.detail_service.result(second)
    assert [p.outcome.value for p in result.parents][:2] == ["split", "split"]


def test_retry_after_a_failure_reuses_the_checkpoints(env: Env) -> None:
    first = env.detail()
    assert env.runtime.scheduler.join(WAIT)
    record = env.runtime.details.get(first)
    assert record is not None
    env.runtime.details.save(
        record.model_copy(update={"job": record.job.model_copy(update={"status": D.FAILED})})
    )
    env.runtime.paths.detail_final(SHA).rename(env.runtime.paths.detail_partial(SHA))
    chunks_before = env.sleeper.chunks
    second = env.detail()
    assert env.runtime.scheduler.join(WAIT)
    assert env.status(second) is D.COMPLETED
    assert env.sleeper.chunks == chunks_before


def test_a_detail_job_reports_waiting_while_a_standard_job_runs(env: Env) -> None:
    stages: list[str] = []
    save = env.runtime.details.save

    def recording_save(record: StoredDetailJob) -> None:
        stages.append(record.job.stage)
        save(record)

    env.runtime.details.save = recording_save  # type: ignore[method-assign]
    _start_with_hook(env, 4, lambda d: env.runtime.service.create(OTHER))
    assert env.runtime.scheduler.join(WAIT)
    drums = stages.index("Splitting drums (1 of 5)")
    vocals = stages.index("Splitting vocals (2 of 5)")
    assert "Waiting for standard separations" in stages[drums:vocals]
    assert env.fakes.calls.names[:1] == ["fetch"]
