"""NVML memory reads (``pynvml`` from ``nvidia-ml-py``, imported lazily; ADR-018 D4).

NVML is the free-memory source of the GPU policy: ``torch.cuda.mem_get_info`` ignores other
processes' usage under WSL2 (003 research G-05), and the generator must see the worker, games or
Ollama holding memory.
"""

import threading
from collections.abc import Callable
from dataclasses import dataclass
from types import ModuleType
from typing import Any, Final, Protocol

_BYTES_PER_MIB: Final = 1 << 20


@dataclass(frozen=True, slots=True)
class GpuMemory:
    """Total and free memory of the GPU in MiB (rounded down).

    Attributes:
        total_mib: Total memory.
        free_mib: Free memory now (all processes).
    """

    total_mib: int
    free_mib: int


class NvmlReader(Protocol):
    """Reads the memory of the generator's GPU."""

    def memory(self) -> GpuMemory:
        """Return total and free memory now (raises when NVML is unavailable)."""
        ...


def import_pynvml() -> ModuleType:
    """Import ``pynvml`` (part of the base install; it needs the driver only when called)."""
    import pynvml  # noqa: PLC0415 - imported on first use only

    module: ModuleType = pynvml
    return module


class PynvmlReader:
    """``NvmlReader`` over ``pynvml`` for GPU ``index``; NVML is initialised on the first read."""

    def __init__(self, index: int = 0, loader: Callable[[], Any] = import_pynvml) -> None:
        """Read GPU ``index``; ``loader`` returns the pynvml module (injectable for tests)."""
        self._index = index
        self._loader = loader
        self._nvml: Any = None
        self._init_lock = threading.Lock()

    def memory(self) -> GpuMemory:
        """Return the NVML total and free memory of the GPU."""
        nvml = self._initialised()
        info = nvml.nvmlDeviceGetMemoryInfo(nvml.nvmlDeviceGetHandleByIndex(self._index))
        return GpuMemory(
            total_mib=int(info.total) // _BYTES_PER_MIB, free_mib=int(info.free) // _BYTES_PER_MIB
        )

    def _initialised(self) -> Any:
        with self._init_lock:
            if self._nvml is None:
                module = self._loader()
                module.nvmlInit()
                self._nvml = module
            return self._nvml
