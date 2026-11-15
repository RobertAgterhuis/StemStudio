"""Start-up GPU probe (003 data-model 2.1, ADR-012 D2, ADR-018 D1): decides readiness once.

The generator has no CPU path, so it always probes: torch is a CUDA build, CUDA is available,
the GPU's architecture is in the build, a test kernel runs and NVML reads the memory. The first
failure wins and its text (identical to the worker's) becomes ``DeviceInfo.reason``; the service
then starts but is not ready. The probe never raises. torch is reached only through a lazily
created adapter, so the base install (no torch) reports a CPU build.
"""

from collections.abc import Callable
from typing import Any, Final, Protocol

from stemstudio_generator.devices.nvml import GpuMemory, NvmlReader
from stemstudio_generator.schemas import DeviceInfo

NO_CUDA_BUILD: Final = "This worker image has no GPU support (CPU build); use the GPU start option."
NOT_AVAILABLE: Final = (
    "No usable NVIDIA GPU: CUDA is not available in this container "
    "(is the GPU option enabled and the driver current?)."
)
UNSUPPORTED: Final = (
    "The GPU {name} (compute capability {capability}) is not supported by this build."
)
KERNEL_FAILED: Final = "The GPU test kernel failed: {error}."
NO_NVML: Final = "GPU memory cannot be read (NVML unavailable)."

_MAX_TEXT: Final = 200
_KERNEL_SIZE: Final = 256


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
        """Architectures in the build, e.g. ``["sm_90", "sm_120"]``."""
        ...

    def test_kernel(self) -> bool:
        """Run a small matmul on the GPU; True when the result is finite (raises on errors)."""
        ...


class TorchProbe:
    """``CudaProbe`` over a torch module."""

    def __init__(self, torch: Any) -> None:
        """Probe with the given ``torch`` module."""
        self._torch = torch

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
        """A 256 x 256 matmul on ``cuda``; True when every value is finite."""
        torch = self._torch
        matrix = torch.full((_KERNEL_SIZE, _KERNEL_SIZE), 0.5, device="cuda")
        return bool(torch.isfinite(matrix @ matrix).all().item())


def load_torch_probe() -> CudaProbe:
    """Import torch and wrap it (raises ``ImportError`` in the base install)."""
    import torch  # noqa: PLC0415 - only the GPU image variants have torch

    return TorchProbe(torch)


class _ProbeError(Exception):
    """A probe step failed; the message is the user-facing reason."""


def probe_device(torch: Callable[[], CudaProbe], nvml: Callable[[], NvmlReader]) -> DeviceInfo:
    """Probe the GPU (never raises).

    Args:
        torch: Creates the torch adapter.
        nvml: Creates the NVML reader (called only when every torch check passed).

    Returns:
        ``kind=gpu`` with name, compute capability and memory, or ``kind=cpu`` with the reason.
    """
    try:
        return _gpu_info(_load(torch), nvml)
    except _ProbeError as failure:
        return DeviceInfo(kind="cpu", reason=str(failure))


def is_ready(device: DeviceInfo) -> bool:
    """The service accepts generations only on a usable GPU (FR-010)."""
    return device.kind == "gpu"


class GpuProbe:
    """``DeviceProbe`` that probes once and keeps the result for the process lifetime."""

    def __init__(self, torch: Callable[[], CudaProbe], nvml: Callable[[], NvmlReader]) -> None:
        """Probe with the given adapter factories (first call of :meth:`probe`)."""
        self._torch = torch
        self._nvml = nvml
        self._info: DeviceInfo | None = None

    def probe(self) -> DeviceInfo:
        """The probe result (computed on the first call)."""
        if self._info is None:
            self._info = probe_device(self._torch, self._nvml)
        return self._info


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
        kind="gpu", name=name[:_MAX_TEXT], compute_capability=capability,
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
    first = lines[0][:_MAX_TEXT] if lines else type(exc).__name__
    return _ProbeError(KERNEL_FAILED.format(error=first))


def _require(condition: bool, reason: str) -> None:
    if not condition:
        raise _ProbeError(reason)
