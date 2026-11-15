"""``create_device_runner`` (T015): CPU, probed GPU, probe failure and the fake device."""

import subprocess
import sys
from pathlib import Path

import pytest

from stemstudio_worker import devices
from stemstudio_worker.config import DeviceKind, WorkerSettings
from stemstudio_worker.devices import ManagedRunner, create_device_runner, default_probe
from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.devices.lease import GPU_LEASE
from stemstudio_worker.devices.probe import NO_CUDA_BUILD
from stemstudio_worker.devices.runner import CpuRunner, GpuRunner
from stemstudio_worker.ports import GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

from .fakes import RTX, FakeNvml

GPU = DeviceInfo(kind="gpu", name=RTX, compute_capability="12.0", total_memory_mib=16_303)


def _settings(device: DeviceKind = "cpu", separator: str = "demucs") -> WorkerSettings:
    return WorkerSettings(data_dir=Path("data"), device=device, separator=separator)  # type: ignore[arg-type]


def test_the_cpu_preference_gives_the_cpu_runner() -> None:
    runner = create_device_runner(_settings("cpu"))
    assert isinstance(runner, CpuRunner)
    assert isinstance(runner, ManagedRunner)
    assert runner.info == CPU_DEVICE


@pytest.mark.parametrize("device", ["cuda", "auto"])
def test_a_passing_probe_gives_the_gpu_runner(device: DeviceKind) -> None:
    seen: list[str] = []

    def probe(preference: DeviceKind) -> DeviceInfo:
        seen.append(preference)
        return GPU

    runner = create_device_runner(_settings(device), probe)

    assert isinstance(runner, GpuRunner)
    assert runner.info is GPU
    assert seen == [device]


def test_a_failing_probe_gives_the_cpu_runner_with_the_reason() -> None:
    info = DeviceInfo(kind="cpu", reason="No usable NVIDIA GPU")
    runner = create_device_runner(_settings("auto"), lambda preference: info)
    assert isinstance(runner, CpuRunner)
    assert runner.info is info


def test_the_fake_separator_gets_the_fake_runner_without_a_probe() -> None:
    def probe(preference: DeviceKind) -> DeviceInfo:
        raise AssertionError("no probe in fake mode")

    settings = WorkerSettings(data_dir=Path("data"), separator="fake", device="cuda",
                              fake_device="gpu")  # fmt: skip
    runner = create_device_runner(settings, probe)

    assert isinstance(runner, FakeDeviceRunner)
    assert runner.info.name == "Fake GPU"


def test_the_default_probe_on_this_cpu_build_reports_it() -> None:
    assert default_probe("cpu") == CPU_DEVICE
    assert default_probe("auto") == DeviceInfo(kind="cpu", reason=NO_CUDA_BUILD)


def test_the_gpu_runner_of_the_factory_uses_the_shared_lease(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # A fake NVML: without it the result depends on the host (no libnvidia-ml on CI means "no
    # memory check", so the step would run on the GPU path).
    monkeypatch.setattr(devices, "_nvml", FakeNvml)
    runner = create_device_runner(_settings("cuda"), lambda preference: GPU)
    # A need no GPU can meet keeps the torch adapter away from CUDA.
    held, use = runner.run(lambda device: GPU_LEASE.held, GpuNeed("huge", 10**9))
    assert held
    assert use.kind == "cpu"


def test_the_cpu_worker_start_up_imports_neither_torch_nor_pynvml() -> None:
    code = (
        "import sys; from pathlib import Path; "
        "from stemstudio_worker.config import WorkerSettings; "
        "from stemstudio_worker.devices import create_device_runner; "
        "runner = create_device_runner(WorkerSettings(data_dir=Path('d'))); "
        "print(runner.info.kind, 'torch' in sys.modules, 'pynvml' in sys.modules)"
    )
    out = subprocess.run(  # noqa: S603 - fixed interpreter and code
        [sys.executable, "-c", code], capture_output=True, text=True, check=True
    )
    assert out.stdout.split() == ["cpu", "False", "False"]
