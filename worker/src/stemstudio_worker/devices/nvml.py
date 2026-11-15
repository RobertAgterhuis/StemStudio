"""NVML memory reads (``pynvml`` from ``nvidia-ml-py``, imported lazily; ADR-012 D4).

NVML is the free-memory source of the GPU policy: ``torch.cuda.mem_get_info`` was measured to
ignore other processes' usage under WSL2 (research.md G-05).
"""

import threading
from collections.abc import Callable
from dataclasses import dataclass
from types import ModuleType
from typing import Any, Protocol

_MIB = 1024 * 1024


@dataclass(frozen=True, slots=True)
class GpuMemory:
    """Total and free memory of the GPU in MiB (rounded down)."""

    total_mib: int
    free_mib: int


class NvmlReader(Protocol):
    """Reads the memory of the worker's GPU."""

    def memory(self) -> GpuMemory:
        """Return total and free memory now.

        Raises:
            Exception: NVML is unavailable (no driver, no library) or the read failed.
        """
        ...


def import_pynvml() -> ModuleType:
    """Import ``pynvml`` (only when a GPU was requested)."""
    import pynvml  # noqa: PLC0415 - loaded only on a GPU worker

    module: ModuleType = pynvml
    return module


class PynvmlReader:
    """``NvmlReader`` over ``pynvml`` for GPU ``index`` (initialises NVML on the first read)."""

    def __init__(self, index: int = 0, loader: Callable[[], Any] = import_pynvml) -> None:
        """Read GPU ``index``; ``loader`` returns the pynvml module (injectable for tests)."""
        self._index = index
        self._loader = loader
        self._module: Any = None
        self._lock = threading.Lock()

    def memory(self) -> GpuMemory:
        """Return the NVML total and free memory of the GPU."""
        nvml = self._nvml()
        info = nvml.nvmlDeviceGetMemoryInfo(nvml.nvmlDeviceGetHandleByIndex(self._index))
        return GpuMemory(total_mib=int(info.total) // _MIB, free_mib=int(info.free) // _MIB)

    def _nvml(self) -> Any:
        with self._lock:
            if self._module is None:
                module = self._loader()
                module.nvmlInit()
                self._module = module
            return self._module
