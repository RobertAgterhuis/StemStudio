"""Start-up GPU probe (data-model.md 2.1, ADR-012 D2): decides the worker's device once.

Checks in order, the first failure wins and its text becomes ``DeviceInfo.reason``: torch is a
CUDA build, CUDA is available, the GPU's architecture is in the build, a test kernel runs and
NVML reads the memory. The probe never raises: the worker always starts (on the CPU when the
GPU is not usable). torch and pynvml are reached only through lazily created adapters, so the
``cpu`` preference imports neither.
"""

from collections.abc import Callable
from typing import Protocol

from stemstudio_worker.config import DeviceKind
from stemstudio_worker.devices.nvml import GpuMemory, NvmlReader
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

NO_CUDA_BUILD = "This worker image has no GPU support (CPU build); use the GPU start option."
NOT_AVAILABLE = (
    "No usable NVIDIA GPU: CUDA is not available in this container "
    "(is the GPU option enabled and the driver current?)."
)
UNSUPPORTED = "The GPU {name} (compute capability {capability}) is not supported by this build."
KERNEL_FAILED = "The GPU test kernel failed: {error}."
NO_NVML = "GPU memory cannot be read (NVML unavailable)."

_MAX_ERROR = 200
_MAX_NAME = 200
_KERNEL_SIZE = 256


class CudaProbe(Protocol):
    """The torch facts the probe needs (``TorchProbe`` or a fake)."""

    def cuda_build(self) -> str | None:
        """CUDA version torch was built with; None for a CPU build."""
        ...

    def is_available(self) -> bool:
        """Whether CUDA can be used now."""
        ...

    def device_name(self) -> str:
        """Name of GPU 0."""
        ...

    def device_capability(self) -> tuple[int, int]:
        """Compute capability ``(major, minor)`` of GPU 0."""
        ...

    def arch_list(self) -> list[str]:
        """Architectures in the build, e.g. ``["sm_75", "sm_120"]``."""
        ...

    def test_kernel(self) -> bool:
        """Run a small matmul on the GPU; True when its result is finite (raises on errors)."""
        ...


class TorchProbe:
    """``CudaProbe`` over the installed torch (imported when the adapter is created)."""

    def __init__(self, device: str = "cuda") -> None:
        """Probe CUDA; ``device`` is where the test kernel runs (``cpu`` only in tests)."""
        import torch  # noqa: PLC0415 - loaded only when a GPU was requested

        self._torch = torch
        self._device = device

    def cuda_build(self) -> str | None:
        """``torch.version.cuda``."""
        version: str | None = self._torch.version.cuda
        return version

    def is_available(self) -> bool:
        """``torch.cuda.is_available()``."""
        return bool(self._torch.cuda.is_available())

    def device_name(self) -> str:
        """``torch.cuda.get_device_name(0)``."""
        return str(self._torch.cuda.get_device_name(0))

    def device_capability(self) -> tuple[int, int]:
        """``torch.cuda.get_device_capability(0)``."""
        major, minor = self._torch.cuda.get_device_capability(0)
        return int(major), int(minor)

    def arch_list(self) -> list[str]:
        """``torch.cuda.get_arch_list()``."""
        return list(self._torch.cuda.get_arch_list())

    def test_kernel(self) -> bool:
        """A 256 x 256 matmul on the probe device; True when every value is finite."""
        torch = self._torch
        matrix = torch.full((_KERNEL_SIZE, _KERNEL_SIZE), 0.5, device=self._device)
        return bool(torch.isfinite(matrix @ matrix).all().item())


def load_torch_probe() -> CudaProbe:
    """Create the real :class:`TorchProbe` (imports torch)."""
    return TorchProbe()


class _ProbeError(Exception):
    """A probe step failed; the message is the user-facing reason."""


def probe_device(
    preference: DeviceKind,
    torch: Callable[[], CudaProbe],
    nvml: Callable[[], NvmlReader],
) -> DeviceInfo:
    """Return the worker's device for ``preference`` (never raises).

    Args:
        preference: ``STEMSTUDIO_DEVICE``; ``cpu`` returns :data:`CPU_DEVICE` without loading
            anything, ``cuda`` and ``auto`` probe the GPU.
        torch: Creates the torch adapter (called only for ``cuda``/``auto``).
        nvml: Creates the NVML reader (called only when every torch check passed).

    Returns:
        ``kind=gpu`` with name, compute capability and memory, or ``kind=cpu`` with the reason.
    """
    if preference == "cpu":
        return CPU_DEVICE
    try:
        return _gpu_info(_load(torch), nvml)
    except _ProbeError as failure:
        return DeviceInfo(kind="cpu", reason=str(failure))


def _load(torch: Callable[[], CudaProbe]) -> CudaProbe:
    try:
        return torch()
    except ImportError as exc:
        raise _ProbeError(NO_CUDA_BUILD) from exc


def _gpu_info(torch: CudaProbe, nvml: Callable[[], NvmlReader]) -> DeviceInfo:
    _require(torch.cuda_build() is not None, NO_CUDA_BUILD)
    _require(torch.is_available(), NOT_AVAILABLE)
    name, capability = _architecture(torch)
    _kernel(torch)
    memory = _memory(nvml)
    return DeviceInfo(
        kind="gpu", name=name[:_MAX_NAME], compute_capability=capability,
        total_memory_mib=memory.total_mib, free_memory_mib=memory.free_mib,
    )  # fmt: skip


def _architecture(torch: CudaProbe) -> tuple[str, str]:
    """Return name and ``"major.minor"``; fail when ``sm_<major><minor>`` is not in the build."""
    try:
        name, (major, minor), arches = (
            torch.device_name(), torch.device_capability(), torch.arch_list()
        )  # fmt: skip
    except Exception as exc:
        raise _kernel_failure(exc) from exc
    capability = f"{major}.{minor}"
    _require(f"sm_{major}{minor}" in arches, UNSUPPORTED.format(name=name, capability=capability))
    return name, capability


def _kernel(torch: CudaProbe) -> None:
    try:
        finite = torch.test_kernel()
    except Exception as exc:
        raise _kernel_failure(exc) from exc
    _require(finite, KERNEL_FAILED.format(error="the result was not finite"))


def _memory(nvml: Callable[[], NvmlReader]) -> GpuMemory:
    try:
        return nvml().memory()
    except Exception as exc:
        raise _ProbeError(NO_NVML) from exc


def _kernel_failure(exc: Exception) -> _ProbeError:
    lines = str(exc).strip().splitlines()
    first = lines[0][:_MAX_ERROR] if lines else type(exc).__name__
    return _ProbeError(KERNEL_FAILED.format(error=first))


def _require(condition: bool, reason: str) -> None:
    if not condition:
        raise _ProbeError(reason)
