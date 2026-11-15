"""Cross-container GPU file lease (009 T022, FR-011, ADR-018 D3).

Real lock files in ``tmp_path`` with the platform's lock API (``flock`` on Linux, ``msvcrt`` on
Windows) and a fake clock; a second ``FileGpuLease`` on the same file plays the generator.
"""

import errno
import os
from contextlib import ExitStack
from pathlib import Path
from types import SimpleNamespace
from typing import Any

import pytest

from stemstudio_worker.config import GPU_LEASE_BUSY_REASON, DeviceKind, WorkerSettings
from stemstudio_worker.devices import create_device_runner
from stemstudio_worker.devices.lease import (
    NO_FILE_LEASE,
    FileGpuLease,
    FlockApi,
    GpuLease,
    LeaseWait,
    MsvcrtApi,
    platform_lock_api,
)
from stemstudio_worker.devices.runner import CpuRunner, GpuRunner, GpuTools
from stemstudio_worker.ports import DeviceUse, GpuNeed
from stemstudio_worker.schemas import DeviceInfo

from .fakes import RTX, FakeClock, FakeMemory, FakeNvml

GPU = DeviceInfo(
    kind="gpu", name=RTX, compute_capability="12.0", total_memory_mib=16_000, free_memory_mib=15_000
)
GPU_USE = DeviceUse(kind="gpu", torch_device="cuda", name=RTX)
SMALL = GpuNeed("Demucs 6-stem", 3_000)
OOM = RuntimeError("CUDA out of memory")


def _lease(path: Path, clock: FakeClock | None = None, seconds: float = 3.0) -> FileGpuLease:
    timer = clock or FakeClock()
    wait = LeaseWait(seconds=seconds, poll_seconds=1.0, now=timer.now, sleep=timer.sleep)
    return FileGpuLease(path, wait)


def _is_locked(path: Path) -> bool:
    """Whether another holder has the lock (an immediate, non-waiting attempt fails)."""
    with _lease(path, seconds=0.0).hold() as granted:
        return not granted


def _runner(lease: FileGpuLease, nvml: FakeNvml | None = None) -> tuple[GpuRunner, GpuLease]:
    thread_lease = GpuLease()
    tools = GpuTools(
        nvml=nvml or FakeNvml(total=16_000, free=[12_000]), memory=FakeMemory(),
        lease=thread_lease, file_lease=lease,
    )  # fmt: skip
    return GpuRunner(GPU, tools), thread_lease


# ------------------------------------------------------------------ GPU runner with the file lease


def test_every_gpu_step_holds_the_file_lease_inside_the_thread_lease(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner, thread_lease = _runner(_lease(lock))
    seen: list[tuple[bool, bool, str]] = []

    def step(device: str) -> str:
        seen.append((thread_lease.held, _is_locked(lock), device))
        return "stems"

    assert runner.run(step, SMALL) == ("stems", GPU_USE)
    assert runner.run(step, SMALL) == ("stems", GPU_USE)
    assert seen == [(True, True, "cuda"), (True, True, "cuda")]
    assert not _is_locked(lock)


def test_a_held_lock_makes_the_step_wait_until_it_is_released(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    generator = ExitStack()
    assert generator.enter_context(_lease(lock).hold())
    clock = FakeClock(on_sleep=lambda count: generator.close() if count == 2 else None)
    runner, _ = _runner(_lease(lock, clock, seconds=900.0))

    assert runner.run(lambda device: device, SMALL) == ("cuda", GPU_USE)
    assert clock.sleeps == [1.0, 1.0]


def test_after_the_wait_limit_the_step_runs_on_the_cpu_with_the_reason(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    lock = tmp_path / "gpu.lock"
    clock = FakeClock()
    nvml = FakeNvml()
    runner, _ = _runner(_lease(lock, clock, seconds=2.5), nvml)

    with _lease(lock).hold():
        result, use = runner.run(lambda device: device, SMALL)

    assert result == "cpu"
    assert use == DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=GPU_LEASE_BUSY_REASON)
    assert clock.sleeps == [1.0, 1.0, 0.5]
    assert nvml.reads == 0
    assert "GPU busy with another StemStudio job" in caplog.text


def test_the_file_lease_is_released_when_the_step_fails(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner, thread_lease = _runner(_lease(lock))

    def step(device: str) -> str:
        raise KeyError(device)

    with pytest.raises(KeyError):
        runner.run(step, SMALL)
    assert not _is_locked(lock)
    assert not thread_lease.held


def test_a_step_that_does_not_fit_runs_on_the_cpu_outside_the_file_lease(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner, thread_lease = _runner(_lease(lock), FakeNvml(total=16_000, free=[1_000]))
    seen: list[tuple[str, bool, bool]] = []

    def step(device: str) -> str:
        seen.append((device, thread_lease.held, _is_locked(lock)))
        return device

    result, use = runner.run(step, SMALL)

    assert (result, use.kind) == ("cpu", "cpu")
    assert use.fallback_reason is not None
    assert use.fallback_reason.startswith("Not enough free GPU memory for Demucs 6-stem")
    assert seen == [("cpu", True, False)]


def test_the_cpu_retry_after_gpu_oom_runs_outside_the_file_lease(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner, _ = _runner(_lease(lock))
    seen: list[tuple[str, bool]] = []

    def step(device: str) -> str:
        seen.append((device, _is_locked(lock)))
        if device == "cuda":
            raise OOM
        return device

    result, use = runner.run(step, SMALL)

    assert result == "cpu"
    assert use.fallback_reason == (
        "The GPU ran out of memory during Demucs 6-stem; the step was repeated on the CPU."
    )
    assert seen == [("cuda", True), ("cpu", False)]


def test_an_unopenable_lock_file_is_logged_and_the_step_runs_without_it(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    runner, _ = _runner(_lease(tmp_path / "missing" / "gpu.lock"))

    assert runner.run(lambda device: device, SMALL) == ("cuda", GPU_USE)
    assert "GPU lease file" in caplog.text
    assert not (tmp_path / "missing").exists()


def test_without_the_setting_the_runner_uses_no_file(tmp_path: Path) -> None:
    tools = GpuTools(nvml=FakeNvml(), memory=FakeMemory(), lease=GpuLease())
    assert tools.file_lease is NO_FILE_LEASE
    runner = GpuRunner(GPU, tools)

    assert runner.run(lambda device: device, SMALL) == ("cuda", GPU_USE)
    with NO_FILE_LEASE.hold() as granted:
        assert granted


# ------------------------------------------------------------------ factory wiring


def _settings(tmp_path: Path, device: DeviceKind, lease_file: Path | None) -> WorkerSettings:
    return WorkerSettings(
        data_dir=tmp_path / "data", device=device,
        gpu_lease_file=lease_file, gpu_lease_wait_seconds=5.0,
    )  # fmt: skip


def test_the_factory_gives_the_gpu_runner_the_configured_lock_file(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner = create_device_runner(_settings(tmp_path, "cuda", lock), lambda preference: GPU)

    assert isinstance(runner, GpuRunner)
    # A need no GPU can meet keeps the real NVML/torch adapters away from CUDA.
    runner.run(lambda device: device, GpuNeed("huge", 10**9))
    assert lock.exists()
    assert not _is_locked(lock)


def test_the_factory_without_the_setting_keeps_the_003_runner(tmp_path: Path) -> None:
    runner = create_device_runner(_settings(tmp_path, "cuda", None), lambda preference: GPU)

    assert isinstance(runner, GpuRunner)
    runner.run(lambda device: device, GpuNeed("huge", 10**9))
    assert list(tmp_path.iterdir()) == []


def test_the_cpu_runner_never_touches_the_lock_file(tmp_path: Path) -> None:
    lock = tmp_path / "gpu.lock"
    runner = create_device_runner(_settings(tmp_path, "cpu", lock))

    assert isinstance(runner, CpuRunner)
    assert runner.run(lambda device: device, SMALL)[0] == "cpu"
    assert not lock.exists()


# ------------------------------------------------------------------ lock APIs


class FakeFcntl:
    """The ``fcntl`` names ``FlockApi`` uses; ``busy`` makes non-blocking locks fail."""

    LOCK_EX, LOCK_NB, LOCK_UN = 2, 4, 8

    def __init__(self, busy: bool = False) -> None:
        self.busy = busy
        self.calls: list[tuple[int, int]] = []

    def flock(self, fd: int, operation: int) -> None:
        self.calls.append((fd, operation))
        if self.busy and operation & self.LOCK_NB:
            raise BlockingIOError(errno.EAGAIN, "busy")


class FakeMsvcrt:
    """The ``msvcrt`` names ``MsvcrtApi`` uses; ``busy`` makes ``LK_NBLCK`` fail."""

    LK_NBLCK, LK_UNLCK = 2, 0

    def __init__(self, busy: bool = False) -> None:
        self.busy = busy
        self.calls: list[tuple[int, int, int]] = []

    def locking(self, fd: int, mode: int, nbytes: int) -> None:
        self.calls.append((fd, mode, nbytes))
        if self.busy and mode == self.LK_NBLCK:
            raise PermissionError(errno.EACCES, "locked")


def test_flock_api_locks_exclusively_without_blocking_and_unlocks() -> None:
    fcntl = FakeFcntl()
    api = FlockApi(fcntl)

    assert api.try_lock(7)
    api.unlock(7)
    assert fcntl.calls == [(7, FakeFcntl.LOCK_EX | FakeFcntl.LOCK_NB), (7, FakeFcntl.LOCK_UN)]
    assert not FlockApi(FakeFcntl(busy=True)).try_lock(7)


def test_msvcrt_api_locks_the_first_byte_and_unlocks(tmp_path: Path) -> None:
    msvcrt = FakeMsvcrt()
    fd = os.open(tmp_path / "gpu.lock", os.O_RDONLY | os.O_CREAT)
    try:
        api = MsvcrtApi(msvcrt)
        assert api.try_lock(fd)
        api.unlock(fd)
        assert not MsvcrtApi(FakeMsvcrt(busy=True)).try_lock(fd)
    finally:
        os.close(fd)
    assert msvcrt.calls == [(fd, FakeMsvcrt.LK_NBLCK, 1), (fd, FakeMsvcrt.LK_UNLCK, 1)]


@pytest.mark.parametrize(
    ("platform", "module", "api"),
    [("win32", "msvcrt", MsvcrtApi), ("linux", "fcntl", FlockApi)],
)
def test_the_platform_picks_its_lock_api(platform: str, module: str, api: type) -> None:
    imported: list[str] = []

    def importer(name: str) -> Any:
        imported.append(name)
        return SimpleNamespace()

    assert isinstance(platform_lock_api(platform, importer), api)
    assert imported == [module]
