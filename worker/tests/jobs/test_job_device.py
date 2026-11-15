"""Device records of standard and detail jobs, and the idle hook (T027, FR-006, ADR-012 D6/D7)."""

import logging
import threading
from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.detail_runner import DetailRunner
from stemstudio_worker.jobs.detail_service import DetailContext, DetailService
from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.jobs.runtime import DeviceHooks, WorkerRuntime
from stemstudio_worker.jobs.scheduler import PriorityScheduler
from stemstudio_worker.jobs.service import JobService
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    DeviceUse,
    ProgressCallback,
    SeparationResult,
    SourceInfo,
)
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo, JobDevice, SourceType

from .detail_env import STD, completed_standard
from .detail_fakes import FakePipeline
from .fakes import FakeClock, Fakes, FakeSeparator

GPU = DeviceInfo(kind="gpu", name="RTX", total_memory_mib=16_000, free_memory_mib=15_000)
SOURCE = SourceInfo(SourceType.YOUTUBE, "youtube:dQw4w9WgXcQ", "Song", None,
                    "https://www.youtube.com/watch?v=dQw4w9WgXcQ")  # fmt: skip
FALLBACK = JobDevice(kind="cpu", cpu_fallback=True, fallback_reason="The GPU ran out of memory")
GPU_USE = DeviceUse(kind="gpu", torch_device="cuda", name="RTX")
OOM_USE = DeviceUse(kind="cpu", torch_device="cpu", fallback_reason="ran out of memory")


class DeviceSeparator(FakeSeparator):
    """Records the job's device while separating and returns ``device`` in its result."""

    def __init__(self, fakes: Fakes, store: JobStore, device: JobDevice | None) -> None:
        super().__init__(fakes.calls)
        self.store = store
        self.device = device
        self.seen: list[JobDevice | None] = []

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        self.seen.extend(r.job.device for r in self.store.all())
        result = super().separate(wav_path, out_dir, progress)
        return SeparationResult(model=result.model, stems=result.stems, device=self.device)


class StandardEnv:
    def __init__(self, root: Path, device: JobDevice | None, info: DeviceInfo = GPU) -> None:
        paths = DataPaths(root)
        self.store = JobStore(paths.jobs)
        self.fakes = Fakes.create()
        self.separator = DeviceSeparator(self.fakes, self.store, device)
        self.fakes.separator = self.separator
        clock = FakeClock()
        self.runner = JobRunner(JobContext(self.store, paths, clock, info), self.fakes.adapters())
        self.service = JobService(self.store, lambda job_id: None, clock)

    def run(self) -> JobDevice | None:
        job, _ = self.service.create(SOURCE)
        self.runner.process(job.job_id)
        record = self.store.get(job.job_id)
        assert record is not None
        assert record.job.status == "completed"
        return record.job.device


def test_the_planned_device_is_set_when_the_separation_starts(tmp_path: Path) -> None:
    env = StandardEnv(tmp_path, FALLBACK)

    final = env.run()

    assert env.separator.seen == [JobDevice(kind="gpu", name="RTX")]
    assert final == FALLBACK


def test_a_separator_without_a_device_keeps_the_planned_device(tmp_path: Path) -> None:
    assert StandardEnv(tmp_path, None).run() == JobDevice(kind="gpu", name="RTX")


def test_a_cpu_worker_plans_the_cpu(tmp_path: Path) -> None:
    env = StandardEnv(tmp_path, None, CPU_DEVICE)
    assert env.run() == JobDevice(kind="cpu")


def test_the_default_context_is_a_cpu_worker(tmp_path: Path) -> None:
    paths = DataPaths(tmp_path)
    assert JobContext(JobStore(paths.jobs), paths, FakeClock()).device == CPU_DEVICE


def test_results_from_the_cache_have_no_device(tmp_path: Path) -> None:
    env = StandardEnv(tmp_path, FALLBACK)
    env.run()
    assert env.run() is None


# ------------------------------------------------------------------ detail jobs


def _detail_env(
    root: Path, pipeline: FakePipeline
) -> tuple[DetailRunner, DetailService, DataPaths]:
    paths = DataPaths(root)
    jobs = JobStore(paths.jobs)
    completed_standard(paths, jobs)
    context = DetailContext(jobs, DetailJobStore(paths.detail_jobs), paths, FakeClock())
    return DetailRunner(context, pipeline), DetailService(context, lambda d: None), paths


def _drive(runner: DetailRunner, detail_id: str) -> None:
    while runner.step(detail_id):
        pass


def test_the_detail_device_is_folded_after_every_step_and_persisted(tmp_path: Path) -> None:
    pipeline = FakePipeline(uses={"drums": OOM_USE, "vocals": GPU_USE})
    runner, service, paths = _detail_env(tmp_path, pipeline)
    detail_id = service.create(STD)[0].detail_job_id
    seen: list[JobDevice | None] = []
    pipeline.during["vocals"] = lambda: seen.append(service.get(detail_id).job.device)

    _drive(runner, detail_id)

    assert seen == [JobDevice(kind="cpu", cpu_fallback=True, fallback_reason="ran out of memory")]
    reloaded = DetailJobStore(paths.detail_jobs)
    reloaded.load_all()
    record = reloaded.get(detail_id)
    assert record is not None
    assert record.job.status == "completed"
    assert record.job.device == JobDevice(
        kind="gpu", name="RTX", cpu_fallback=True, fallback_reason="ran out of memory"
    )


def test_steps_that_report_no_device_leave_the_record_empty(tmp_path: Path) -> None:
    runner, service, _ = _detail_env(tmp_path, FakePipeline())
    detail_id = service.create(STD)[0].detail_job_id
    _drive(runner, detail_id)
    assert service.get(detail_id).job.device is None


def test_a_cached_detail_result_has_no_device(tmp_path: Path) -> None:
    runner, service, paths = _detail_env(tmp_path, FakePipeline(uses={"drums": GPU_USE}))
    first = service.create(STD)[0].detail_job_id
    _drive(runner, first)
    store = DetailJobStore(paths.detail_jobs)
    store.load_all()
    record = store.get(first)
    assert record is not None
    assert record.job.device == JobDevice(kind="gpu", name="RTX")
    failed = record.job.model_copy(update={"status": "failed"})
    runner, service, _ = _reuse(paths, record.model_copy(update={"job": failed}))

    second = service.create(STD)[0].detail_job_id
    _drive(runner, second)

    assert service.get(second).job.status == "completed"
    assert service.get(second).job.device is None


def _reuse(paths: DataPaths, record: StoredDetailJob) -> tuple[DetailRunner, DetailService, None]:
    """A fresh runner over ``paths`` where ``record`` replaced the stored detail job."""
    jobs, details = JobStore(paths.jobs), DetailJobStore(paths.detail_jobs)
    jobs.load_all()
    details.load_all()
    details.save(record)
    context = DetailContext(jobs, details, paths, FakeClock())
    return DetailRunner(context, FakePipeline()), DetailService(context, lambda d: None), None


# ------------------------------------------------------------------ idle hook


def test_the_idle_hook_runs_once_both_queues_are_empty() -> None:
    idle: list[int] = []
    gate = threading.Event()
    units: list[str] = []

    def standard(job_id: str) -> None:
        gate.wait(5)
        units.append(job_id)

    scheduler = PriorityScheduler(
        standard, lambda d: False, on_idle=lambda: idle.append(len(units))
    )
    scheduler.submit("a")
    scheduler.submit("b")
    scheduler.start()
    gate.set()
    assert scheduler.join(5)
    scheduler.stop(5)

    assert units == ["a", "b"]
    assert idle == [2]


def test_the_idle_hook_waits_for_the_last_detail_unit() -> None:
    idle: list[int] = []
    remaining = [2]

    def detail(detail_id: str) -> bool:
        remaining[0] -= 1
        return remaining[0] > 0

    scheduler = PriorityScheduler(lambda j: None, detail, on_idle=lambda: idle.append(remaining[0]))
    scheduler.submit_detail("d")
    scheduler.start()
    assert scheduler.join(5)
    scheduler.stop(5)
    assert idle == [0]


def test_a_failing_idle_hook_is_logged_and_the_scheduler_keeps_running(
    caplog: pytest.LogCaptureFixture,
) -> None:
    done: list[str] = []

    def broken() -> None:
        raise RuntimeError("release failed")

    scheduler = PriorityScheduler(done.append, lambda d: False, on_idle=broken)
    scheduler.start()
    with caplog.at_level(logging.ERROR):
        scheduler.submit("a")
        assert scheduler.join(5)
        scheduler.submit("b")
        assert scheduler.join(5)
    scheduler.stop(5)
    assert done == ["a", "b"]
    assert "Releasing idle GPU memory failed" in caplog.text


def test_the_runtime_reports_the_device_and_releases_when_idle(tmp_path: Path) -> None:
    released: list[int] = []
    hooks = DeviceHooks(info=GPU, on_idle=lambda: released.append(1))
    settings = WorkerSettings(data_dir=tmp_path, separator="fake")
    runtime = WorkerRuntime.start(settings, Fakes.create().adapters(), FakePipeline(), hooks)
    try:
        job, _ = runtime.service.create(SOURCE)
        assert runtime.scheduler.join(5)
        record = runtime.store.get(job.job_id)
        assert record is not None
        assert record.job.device == JobDevice(kind="gpu", name="RTX")
        assert runtime.device is GPU
        assert released == [1]
    finally:
        runtime.stop()


def test_the_default_hooks_describe_a_cpu_worker() -> None:
    hooks = DeviceHooks()
    hooks.on_idle()
    assert hooks.info == CPU_DEVICE
