"""The GPU gate: lease, free-memory policy and memory cap per generation step (ADR-018 D3/D4).

``hold(needs, on_wait)`` takes the lease, reads NVML free memory and admits the first
``GpuNeed`` that fits (standard before low-memory). When none fits it releases the lease again
(other StemStudio steps may run meanwhile), reports the wait through ``on_wait`` and re-checks
every 10 s; after the wait limit it fails with ``gpuBusy``. While a step is admitted the
per-process memory fraction is limited to the free memory minus a head-room; leaving the block
(also by an exception) resets the cap and releases the lease.
"""

from collections.abc import Iterator, Sequence
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime
from typing import Final

from stemstudio_generator.config import MEMORY_RECHECK_SECONDS
from stemstudio_generator.devices.lease import LEASE_POLL_SECONDS, Lease
from stemstudio_generator.devices.memory import MemoryCap, cap_fraction
from stemstudio_generator.devices.nvml import GpuMemory, NvmlReader
from stemstudio_generator.devices.oom import is_gpu_oom
from stemstudio_generator.devices.probe import NO_NVML
from stemstudio_generator.ports import (
    Clock,
    DeviceUse,
    GpuBusyError,
    GpuNeed,
    GpuRequiredError,
    WaitCallback,
)
from stemstudio_generator.schemas import GPU_BUSY_TEMPLATE, JobDevice

MEMORY_WAIT_STAGE: Final = "Waiting for free GPU memory: needs {need:.1f} GB, {free:.1f} GB free"
"""Stage text while too little memory is free (``str.format(need=, free=)`` in GB)."""

_MIB_PER_GB: Final = 1024


@dataclass(frozen=True, slots=True)
class GatePolicy:
    """Timing of the gate.

    Attributes:
        wait_seconds: Longest wait for enough free memory (``STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS``).
        recheck_seconds: Interval of the free-memory re-check.
        poll_seconds: Interval of the wait callback (cancel latency) while waiting.
    """

    wait_seconds: float = 600.0
    recheck_seconds: float = MEMORY_RECHECK_SECONDS
    poll_seconds: float = LEASE_POLL_SECONDS


@dataclass(frozen=True, slots=True)
class GpuAccess:
    """The GPU behind the gate.

    Attributes:
        lease: The GPU lease (file lease or in-process).
        nvml: Free-memory reader.
        cap: Per-process memory cap.
        name: GPU name for ``Generation.device`` (from the probe).
    """

    lease: Lease
    nvml: NvmlReader
    cap: MemoryCap
    name: str | None


@dataclass(frozen=True, slots=True)
class _Request:
    """One ``hold`` call: what it needs, whom to tell about waits, when it started."""

    needs: Sequence[GpuNeed]
    on_wait: WaitCallback
    started: datetime


class LeaseGpuGate:
    """``GpuGate`` over a lease, NVML and a memory cap."""

    def __init__(self, gpu: GpuAccess, clock: Clock, policy: GatePolicy) -> None:
        """Create the gate."""
        self._gpu = gpu
        self._clock = clock
        self._policy = policy
        self._device = JobDevice(kind="gpu", name=gpu.name)

    @property
    def lease(self) -> Lease:
        """The lease the gate takes."""
        return self._gpu.lease

    @contextmanager
    def hold(self, needs: Sequence[GpuNeed], on_wait: WaitCallback) -> Iterator[DeviceUse]:
        """Take the lease and admit the first need that fits; release on exit (also on errors).

        Raises:
            GpuBusyError: No need fitted within the wait limit.
            GpuRequiredError: NVML cannot be read.
        """
        request = _Request(needs, on_wait, self._clock.now())
        while True:
            with self._gpu.lease.hold(on_wait):
                memory = self._read()
                need = _first_fit(needs, memory.free_mib)
                if need is not None:
                    with self._capped(need, memory) as use:
                        yield use
                    return
            self._wait(request, memory)

    def is_out_of_memory(self, error: BaseException) -> bool:
        """True for a CUDA OOM or ACE-Step's "Insufficient free VRAM" guard."""
        return is_gpu_oom(error)

    @contextmanager
    def _capped(self, need: GpuNeed, memory: GpuMemory) -> Iterator[DeviceUse]:
        self._gpu.cap.limit(cap_fraction(memory))
        try:
            yield DeviceUse(need=need, device=self._device, free_memory_mib=memory.free_mib)
        finally:
            self._gpu.cap.reset()

    def _read(self) -> GpuMemory:
        try:
            return self._gpu.nvml.memory()
        except Exception as exc:
            raise GpuRequiredError(NO_NVML) from exc

    def _wait(self, request: _Request, memory: GpuMemory) -> None:
        smallest = min(request.needs, key=lambda need: need.memory_mib)
        amounts = {"need": smallest.memory_mib / _MIB_PER_GB, "free": memory.free_mib / _MIB_PER_GB}
        elapsed = (self._clock.now() - request.started).total_seconds()
        remaining = self._policy.wait_seconds - elapsed
        if remaining <= 0:
            raise GpuBusyError(GPU_BUSY_TEMPLATE.format(**amounts))
        self._pause(
            min(self._policy.recheck_seconds, remaining),
            MEMORY_WAIT_STAGE.format(**amounts),
            request.on_wait,
        )

    def _pause(self, seconds: float, stage: str, on_wait: WaitCallback) -> None:
        while seconds > 0:
            on_wait(stage)
            step = min(self._policy.poll_seconds, seconds)
            self._clock.sleep(step)
            seconds -= step


def _first_fit(needs: Sequence[GpuNeed], free_mib: int) -> GpuNeed | None:
    return next((need for need in needs if need.memory_mib <= free_mib), None)
