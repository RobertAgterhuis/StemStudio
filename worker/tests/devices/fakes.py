"""Fake torch, NVML and torch-memory adapters for the device tests (no GPU needed)."""

from collections.abc import Callable
from dataclasses import dataclass, field

from stemstudio_worker.devices.nvml import GpuMemory

RTX = "NVIDIA GeForce RTX 5070 Ti"


@dataclass
class FakeTorch:
    """A ``CudaProbe`` whose every answer is configurable; ``kernel`` may be an exception."""

    cuda: str | None = "13.2"
    available: bool = True
    name: str = RTX
    capability: tuple[int, int] = (12, 0)
    arches: list[str] = field(default_factory=lambda: ["sm_75", "sm_86", "sm_120"])
    kernel: bool | Exception = True
    calls: list[str] = field(default_factory=list)

    def cuda_build(self) -> str | None:
        self.calls.append("cuda_build")
        return self.cuda

    def is_available(self) -> bool:
        self.calls.append("is_available")
        return self.available

    def device_name(self) -> str:
        return self.name

    def device_capability(self) -> tuple[int, int]:
        return self.capability

    def arch_list(self) -> list[str]:
        return self.arches

    def test_kernel(self) -> bool:
        self.calls.append("test_kernel")
        if isinstance(self.kernel, Exception):
            raise self.kernel
        return self.kernel


@dataclass
class FakeNvml:
    """An ``NvmlReader`` returning ``free`` (a list is consumed one value per read) or raising."""

    total: int = 16_303
    free: list[int] = field(default_factory=lambda: [15_469])
    error: Exception | None = None
    reads: int = 0

    def memory(self) -> GpuMemory:
        self.reads += 1
        if self.error is not None:
            raise self.error
        value = self.free.pop(0) if len(self.free) > 1 else self.free[0]
        return GpuMemory(total_mib=self.total, free_mib=value)


@dataclass
class FakeMemory:
    """A ``TorchMemory`` recording fraction changes and cache releases."""

    events: list[str] = field(default_factory=list)

    def set_fraction(self, fraction: float) -> None:
        self.events.append(f"fraction {fraction:.6f}")

    def empty_cache(self) -> None:
        self.events.append("empty_cache")


@dataclass
class FakeClock:
    """A monotonic clock whose ``sleep`` advances time and then calls ``on_sleep(count)``."""

    time: float = 0.0
    sleeps: list[float] = field(default_factory=list)
    on_sleep: Callable[[int], None] | None = None

    def now(self) -> float:
        return self.time

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.time += seconds
        if self.on_sleep is not None:
            self.on_sleep(len(self.sleeps))
