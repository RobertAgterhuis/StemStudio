"""Start-up GPU probe (T011, data-model.md 2.1): fake torch and NVML adapters, no GPU."""

import subprocess
import sys
from types import SimpleNamespace

import pytest
import torch

from stemstudio_worker.devices import nvml as nvml_module
from stemstudio_worker.devices import probe as probe_module
from stemstudio_worker.devices.nvml import GpuMemory, PynvmlReader
from stemstudio_worker.devices.probe import TorchProbe, load_torch_probe, probe_device
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

from .fakes import RTX, FakeNvml, FakeTorch

NO_CUDA_BUILD = "This worker image has no GPU support (CPU build); use the GPU start option."
NOT_AVAILABLE = (
    "No usable NVIDIA GPU: CUDA is not available in this container "
    "(is the GPU option enabled and the driver current?)."
)
NO_NVML = "GPU memory cannot be read (NVML unavailable)."


def _probe(fake: FakeTorch, nvml: FakeNvml | None = None, preference: str = "auto") -> DeviceInfo:
    reader = nvml or FakeNvml()
    return probe_device(preference, lambda: fake, lambda: reader)  # type: ignore[arg-type]


def _never() -> FakeTorch:
    raise AssertionError("the cpu preference must not load torch or NVML")


def test_cpu_preference_never_loads_torch_or_nvml() -> None:
    assert probe_device("cpu", _never, _never) == CPU_DEVICE  # type: ignore[arg-type]


@pytest.mark.parametrize("preference", ["cuda", "auto"])
def test_a_usable_gpu_yields_its_device_info(preference: str) -> None:
    info = _probe(FakeTorch(), FakeNvml(total=16_303, free=[15_469]), preference)

    assert info == DeviceInfo(
        kind="gpu", name=RTX, compute_capability="12.0",
        total_memory_mib=16_303, free_memory_mib=15_469, reason=None,
    )  # fmt: skip


def test_a_cpu_build_of_torch_is_reported() -> None:
    fake = FakeTorch(cuda=None)
    info = _probe(fake)
    assert info == DeviceInfo(kind="cpu", reason=NO_CUDA_BUILD)
    assert fake.calls == ["cuda_build"]


def test_torch_that_cannot_be_imported_counts_as_a_cpu_build() -> None:
    def missing() -> FakeTorch:
        raise ImportError("No module named 'torch'")

    info = probe_device("auto", missing, FakeNvml)  # type: ignore[arg-type]
    assert info == DeviceInfo(kind="cpu", reason=NO_CUDA_BUILD)


def test_cuda_unavailable_is_reported() -> None:
    assert _probe(FakeTorch(available=False)).reason == NOT_AVAILABLE


def test_an_architecture_missing_from_the_build_is_reported() -> None:
    info = _probe(FakeTorch(name="Old GPU", capability=(6, 1)))
    assert info.kind == "cpu"
    assert info.reason == (
        "The GPU Old GPU (compute capability 6.1) is not supported by this build."
    )


def test_a_failing_test_kernel_reports_the_first_line_truncated_to_200_characters() -> None:
    error = RuntimeError("CUDA error: " + "x" * 300 + "\nsecond line")
    info = _probe(FakeTorch(kernel=error))
    assert info.kind == "cpu"
    assert info.reason == f"The GPU test kernel failed: CUDA error: {'x' * 188}."


def test_a_test_kernel_with_a_non_finite_result_is_reported() -> None:
    info = _probe(FakeTorch(kernel=False))
    assert info.reason == "The GPU test kernel failed: the result was not finite."


def test_an_error_without_text_names_its_type() -> None:
    info = _probe(FakeTorch(kernel=RuntimeError()))
    assert info.reason == "The GPU test kernel failed: RuntimeError."


def test_nvml_that_cannot_read_memory_is_reported() -> None:
    info = _probe(FakeTorch(), FakeNvml(error=RuntimeError("NVML Shared Library Not Found")))
    assert info == DeviceInfo(kind="cpu", reason=NO_NVML)


def test_the_first_failing_step_wins() -> None:
    fake = FakeTorch(available=False, kernel=RuntimeError("never reached"))
    nvml = FakeNvml(error=RuntimeError("never reached"))
    assert _probe(fake, nvml).reason == NOT_AVAILABLE
    assert "test_kernel" not in fake.calls
    assert nvml.reads == 0


def test_a_long_gpu_name_is_clipped_to_the_contract_limit() -> None:
    info = _probe(FakeTorch(name="G" * 250))
    assert info.name == "G" * 200


# ------------------------------------------------------------------ real adapters (CPU torch)


def test_torch_probe_reports_the_installed_cpu_build() -> None:
    adapter = load_torch_probe()
    assert adapter.cuda_build() is torch.version.cuda
    assert adapter.is_available() is torch.cuda.is_available()


def test_torch_probe_runs_the_test_kernel_on_its_device() -> None:
    assert TorchProbe(device="cpu").test_kernel() is True


def test_torch_probe_reads_device_facts_from_torch_cuda(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(torch.cuda, "get_device_name", lambda index: f"GPU {index}")
    monkeypatch.setattr(torch.cuda, "get_device_capability", lambda index: (12, 0))
    monkeypatch.setattr(torch.cuda, "get_arch_list", lambda: ["sm_120"])
    adapter = TorchProbe()

    assert (adapter.device_name(), adapter.device_capability(), adapter.arch_list()) == (
        "GPU 0",
        (12, 0),
        ["sm_120"],
    )


def test_pynvml_reader_initialises_once_and_reads_device_zero_in_mib() -> None:
    calls: list[str] = []
    mib = 1024 * 1024
    module = SimpleNamespace(
        nvmlInit=lambda: calls.append("init"),
        nvmlDeviceGetHandleByIndex=lambda index: f"handle {index}",
        nvmlDeviceGetMemoryInfo=lambda handle: SimpleNamespace(
            total=16_303 * mib + 5, free=15_469 * mib + 7, used=0
        ),
    )
    reader = PynvmlReader(loader=lambda: module)

    assert reader.memory() == GpuMemory(total_mib=16_303, free_mib=15_469)
    assert reader.memory() == GpuMemory(total_mib=16_303, free_mib=15_469)
    assert calls == ["init"]


def test_pynvml_is_the_default_nvml_module() -> None:
    module = nvml_module.import_pynvml()
    assert callable(module.nvmlDeviceGetMemoryInfo)


def test_importing_the_probe_imports_neither_torch_nor_pynvml() -> None:
    code = (
        "import sys; import stemstudio_worker.devices.probe, stemstudio_worker.devices.nvml; "
        "print('torch' in sys.modules, 'pynvml' in sys.modules)"
    )
    out = subprocess.run(  # noqa: S603 - fixed interpreter and code
        [sys.executable, "-c", code], capture_output=True, text=True, check=True
    )
    assert out.stdout.split() == ["False", "False"]
    assert probe_module.__name__.endswith("probe")
