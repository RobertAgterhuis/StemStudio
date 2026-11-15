"""Fake device for the fake stack (``STEMSTUDIO_FAKE_DEVICE``, ADR-012 D9, data-model.md 1).

``cpu`` behaves like a CPU worker; ``gpu`` reports a fake GPU ("Fake GPU") and runs every step
"on it"; ``gpu-oom`` does the same, except that the first step of every job context "runs out of
GPU memory" and is repeated on the CPU. A job context ends when the worker becomes idle
(:meth:`FakeDeviceRunner.release`), so one import gives a fallback in its standard job and in its
detail job. The fake separator and fake detail models ignore the device string.
"""

from collections.abc import Callable
from typing import Final

from stemstudio_worker.config import FakeDeviceKind
from stemstudio_worker.devices.reasons import out_of_memory
from stemstudio_worker.ports import DeviceUse, GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

FAKE_GPU: Final = DeviceInfo(
    kind="gpu", name="Fake GPU", compute_capability="12.0",
    total_memory_mib=16_384, free_memory_mib=15_360,
)  # fmt: skip
"""Device info of the fake GPU."""

_GPU_USE: Final = DeviceUse(kind="gpu", torch_device="cuda", name=FAKE_GPU.name)
_CPU_USE: Final = DeviceUse(kind="cpu", torch_device="cpu")


class FakeDeviceRunner:
    """``DeviceRunner`` for E2E tests of the device display and the fallback notice."""

    def __init__(self, mode: FakeDeviceKind) -> None:
        """Pretend ``mode`` (``cpu``, ``gpu`` or ``gpu-oom``)."""
        self._mode = mode
        self._fresh_context = True

    @property
    def info(self) -> DeviceInfo:
        """:data:`CPU_DEVICE` for ``cpu``, else :data:`FAKE_GPU`."""
        return CPU_DEVICE if self._mode == "cpu" else FAKE_GPU

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        """Run ``work`` and report the fake device (or the simulated OOM fallback)."""
        if self._mode == "cpu":
            return work("cpu"), _CPU_USE
        if self._mode == "gpu-oom" and self._fresh_context:
            self._fresh_context = False
            reason = out_of_memory(need.label)
            return work("cpu"), DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=reason)
        return work("cuda"), _GPU_USE

    def release(self) -> None:
        """The worker became idle: the next step starts a new job context."""
        self._fresh_context = True

    def reclaim_with(self, reclaim: Callable[[GpuNeed], None]) -> None:
        """The fake GPU never runs short of memory; ``reclaim`` is never called."""
