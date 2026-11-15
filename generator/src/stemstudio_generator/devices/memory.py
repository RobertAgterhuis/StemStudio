"""Per-process GPU memory cap (ADR-012 D4, ADR-018 D4).

While a step holds the GPU the generator limits itself to the memory that was free when it was
admitted, minus a head-room: on WSL2 an allocation beyond the dedicated memory otherwise spills
into shared system memory and becomes very slow instead of failing with an OOM that the
low-memory retry can handle.
"""

from typing import Any, Final, Protocol

from stemstudio_generator.config import MEMORY_CAP_MARGIN_MIB
from stemstudio_generator.devices.nvml import GpuMemory

_DEVICE: Final = 0


class MemoryCap(Protocol):
    """Sets and clears the per-process memory fraction of the GPU."""

    def limit(self, fraction: float) -> None:
        """Allow at most ``fraction`` (0..1) of the total GPU memory."""
        ...

    def reset(self) -> None:
        """Remove the limit."""
        ...


class TorchMemoryCap:
    """``MemoryCap`` over ``torch.cuda.set_per_process_memory_fraction`` for GPU 0."""

    def __init__(self, torch: Any) -> None:
        """Use the given ``torch`` module."""
        self._torch = torch

    def limit(self, fraction: float) -> None:
        """Set the fraction."""
        self._torch.cuda.set_per_process_memory_fraction(fraction, _DEVICE)

    def reset(self) -> None:
        """Set the fraction back to 1.0."""
        self._torch.cuda.set_per_process_memory_fraction(1.0, _DEVICE)


class NoMemoryCap:
    """``MemoryCap`` that does nothing (fake engine; no torch in the process)."""

    def limit(self, fraction: float) -> None:
        """Ignore the fraction."""

    def reset(self) -> None:
        """Nothing to reset."""


def load_torch_cap() -> TorchMemoryCap:
    """Import torch and wrap it (GPU image variants only)."""
    import torch  # noqa: PLC0415 - only the GPU image variants have torch

    return TorchMemoryCap(torch)


def cap_fraction(memory: GpuMemory) -> float:
    """``(free - 256 MiB) / total``, clamped to 0..1."""
    usable = (memory.free_mib - MEMORY_CAP_MARGIN_MIB) / memory.total_mib
    return min(max(usable, 0.0), 1.0)
