"""Start-up GPU probe of the generator (009 T012; 003 data-model 2.1, ADR-012 D2)."""

import sys
from collections.abc import Callable
from dataclasses import dataclass, field
from types import SimpleNamespace
from typing import Any

import pytest

from stemstudio_generator.devices.nvml import GpuMemory, NvmlReader
from stemstudio_generator.devices.probe import (
    KERNEL_FAILED,
    NO_CUDA_BUILD,
    NO_NVML,
    NOT_AVAILABLE,
    UNSUPPORTED,
    CudaProbe,
    GpuProbe,
    TorchProbe,
    is_ready,
    load_torch_probe,
    probe_device,
)
from stemstudio_generator.ports import DeviceProbe
from stemstudio_generator.schemas import DeviceInfo


@dataclass
class FakeCuda:
    """Configurable torch facts."""

    build: str | None = "12.8"
    available: bool = True
    name: str = "NVIDIA GeForce RTX 5070 Ti"
    capability: tuple[int, int] = (12, 0)
    arches: list[str] = field(default_factory=lambda: ["sm_90", "sm_120"])
    kernel: bool = True
    kernel_error: Exception | None = None
    name_error: Exception | None = None

    def cuda_build(self) -> str | None:
        return self.build

    def is_available(self) -> bool:
        return self.available

    def device_name(self) -> str:
        if self.name_error is not None:
            raise self.name_error
        return self.name

    def device_capability(self) -> tuple[int, int]:
        return self.capability

    def arch_list(self) -> list[str]:
        return self.arches

    def test_kernel(self) -> bool:
        if self.kernel_error is not None:
            raise self.kernel_error
        return self.kernel


class FakeNvml:
    def __init__(self, memory: GpuMemory | None = None) -> None:
        self._memory = memory

    def memory(self) -> GpuMemory:
        if self._memory is None:
            raise RuntimeError("NVML Shared Library Not Found")
        return self._memory


def _torch(cuda: CudaProbe) -> Callable[[], CudaProbe]:
    return lambda: cuda


def _nvml(memory: GpuMemory | None = GpuMemory(16_303, 14_800)) -> Callable[[], NvmlReader]:
    return lambda: FakeNvml(memory)


def _probe(cuda: FakeCuda, memory: GpuMemory | None = GpuMemory(16_303, 14_800)) -> DeviceInfo:
    return probe_device(_torch(cuda), _nvml(memory))


def test_reason_texts_are_identical_to_the_worker_probe() -> None:
    assert NO_CUDA_BUILD == (
        "This worker image has no GPU support (CPU build); use the GPU start option."
    )
    assert NOT_AVAILABLE == (
        "No usable NVIDIA GPU: CUDA is not available in this container "
        "(is the GPU option enabled and the driver current?)."
    )
    assert UNSUPPORTED == (
        "The GPU {name} (compute capability {capability}) is not supported by this build."
    )
    assert KERNEL_FAILED == "The GPU test kernel failed: {error}."
    assert NO_NVML == "GPU memory cannot be read (NVML unavailable)."


def test_every_check_passing_gives_a_gpu_with_memory() -> None:
    info = _probe(FakeCuda())

    assert info == DeviceInfo(
        kind="gpu",
        name="NVIDIA GeForce RTX 5070 Ti",
        compute_capability="12.0",
        total_memory_mib=16_303,
        free_memory_mib=14_800,
    )
    assert is_ready(info)


def test_torch_missing_is_a_cpu_build() -> None:
    def no_torch() -> CudaProbe:
        raise ImportError("No module named 'torch'")

    info = probe_device(no_torch, _nvml())

    assert info == DeviceInfo(kind="cpu", reason=NO_CUDA_BUILD)
    assert not is_ready(info)


@pytest.mark.parametrize(
    ("cuda", "reason"),
    [
        (FakeCuda(build=None), NO_CUDA_BUILD),
        (FakeCuda(available=False), NOT_AVAILABLE),
        (
            FakeCuda(arches=["sm_80", "sm_90"]),
            "The GPU NVIDIA GeForce RTX 5070 Ti (compute capability 12.0) is not supported "
            "by this build.",
        ),
        (FakeCuda(kernel=False), "The GPU test kernel failed: the result was not finite."),
        (
            FakeCuda(kernel_error=RuntimeError("CUDA error: no kernel image\nmore details")),
            "The GPU test kernel failed: CUDA error: no kernel image.",
        ),
        (FakeCuda(kernel_error=RuntimeError("")), "The GPU test kernel failed: RuntimeError."),
        (
            FakeCuda(name_error=RuntimeError("driver too old")),
            "The GPU test kernel failed: driver too old.",
        ),
    ],
)
def test_first_failing_check_wins(cuda: FakeCuda, reason: str) -> None:
    info = _probe(cuda)

    assert info.kind == "cpu"
    assert info.reason == reason
    assert info.name is None
    assert not is_ready(info)


def test_long_kernel_errors_are_cut_to_200_characters() -> None:
    info = _probe(FakeCuda(kernel_error=RuntimeError("x" * 300)))

    assert info.reason == KERNEL_FAILED.format(error="x" * 200)


def test_nvml_failure_is_reported_after_the_torch_checks() -> None:
    assert _probe(FakeCuda(), memory=None) == DeviceInfo(kind="cpu", reason=NO_NVML)


def test_long_device_names_are_cut_to_200_characters() -> None:
    info = _probe(FakeCuda(name="G" * 250, arches=["sm_120"]))

    assert info.name == "G" * 200


def test_gpu_probe_runs_once_and_satisfies_the_port() -> None:
    calls: list[str] = []

    def torch() -> CudaProbe:
        calls.append("torch")
        return FakeCuda()

    probe = GpuProbe(torch, _nvml())

    assert isinstance(probe, DeviceProbe)
    assert probe.probe() is probe.probe()
    assert calls == ["torch"]


class _FakeMatrix:
    def __init__(self, finite: bool) -> None:
        self._finite = finite

    def __matmul__(self, other: object) -> "_FakeMatrix":
        return self

    def all(self) -> "_FakeMatrix":
        return self

    def item(self) -> bool:
        return self._finite


def _fake_torch_module(finite: bool = True) -> Any:
    created: list[tuple[object, ...]] = []
    cuda = SimpleNamespace(
        is_available=lambda: True,
        get_device_name=lambda index: f"GPU {index}",
        get_device_capability=lambda index: (12, 0),
        get_arch_list=lambda: ["sm_120"],
    )

    def full(shape: tuple[int, int], value: float, device: str) -> _FakeMatrix:
        created.append((shape, value, device))
        return _FakeMatrix(finite)

    return SimpleNamespace(
        version=SimpleNamespace(cuda="12.8"),
        cuda=cuda,
        full=full,
        isfinite=lambda matrix: matrix,
        created=created,
    )


def test_torch_probe_reads_the_cuda_facts_from_torch() -> None:
    module = _fake_torch_module()
    probe = TorchProbe(module)

    assert probe.cuda_build() == "12.8"
    assert probe.is_available()
    assert probe.device_name() == "GPU 0"
    assert probe.device_capability() == (12, 0)
    assert probe.arch_list() == ["sm_120"]
    assert probe.test_kernel()
    assert module.created == [((256, 256), 0.5, "cuda")]


def test_torch_probe_reports_a_non_finite_kernel() -> None:
    assert not TorchProbe(_fake_torch_module(finite=False)).test_kernel()


def test_load_torch_probe_imports_torch(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setitem(sys.modules, "torch", _fake_torch_module())

    assert load_torch_probe().cuda_build() == "12.8"


def test_load_torch_probe_without_torch_is_an_import_error(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setitem(sys.modules, "torch", None)

    with pytest.raises(ImportError):
        load_torch_probe()
