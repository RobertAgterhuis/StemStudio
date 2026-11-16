"""``DeviceRunner`` implementations (ADR-012 D3-D6).

``CpuRunner`` runs every step on the CPU. ``GpuRunner`` holds the GPU lease for each step and, in
the GPU image with the optional generator (feature 009, ADR-018 D3), the shared file lease inside
it; when the file lease is not free within ``gpu_lease_wait_seconds`` the step runs on the CPU with
the reason "GPU busy with another StemStudio job". With both leases held it reads the free GPU
memory with NVML and runs the step on the CPU when it does not fit; otherwise it caps
torch's memory at the free memory for the step (so WSL2 raises out-of-memory instead of spilling
into shared memory) and retries the step once on the CPU when the GPU attempt runs out of memory.
Before a step falls back for lack of free memory, the runner first lets the worker release its own
cached GPU models (the reclaimer, except the step's own model), empties torch's cache and checks
once more: with a continuous queue the worker never becomes idle, and its resident models would
otherwise starve its own later steps (T052 finding). CPU fallbacks run after the file lease is
released, so another service can use the GPU meanwhile.
"""

import gc
import logging
from collections.abc import Callable
from dataclasses import dataclass
from typing import Protocol

from stemstudio_worker.config import GPU_LEASE_BUSY_REASON
from stemstudio_worker.devices.lease import NO_FILE_LEASE, FileLease, GpuLease
from stemstudio_worker.devices.nvml import GpuMemory, NvmlReader
from stemstudio_worker.devices.oom import is_gpu_out_of_memory
from stemstudio_worker.devices.reasons import not_enough_memory, out_of_memory
from stemstudio_worker.ports import DeviceUse, GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

_LOG = logging.getLogger(__name__)

HEADROOM_MIB = 256
"""Free memory left outside the per-step torch budget (CUDA context growth, other processes)."""

CPU_USE = DeviceUse(kind="cpu", torch_device="cpu")
"""A step that ran on the CPU of a CPU worker."""

Reclaimer = Callable[[GpuNeed], None]
"""Releases the worker's cached GPU models that the step with this need does not use."""


class TorchMemory(Protocol):
    """The torch memory controls the GPU runner needs (``TorchCudaMemory`` or a fake)."""

    def set_fraction(self, fraction: float) -> None:
        """Cap torch's allocations on GPU 0 at ``fraction`` of its total memory."""
        ...

    def empty_cache(self) -> None:
        """Return torch's cached, unused GPU blocks to the driver."""
        ...


class TorchCudaMemory:
    """``TorchMemory`` over ``torch.cuda`` (torch imported on first use)."""

    def set_fraction(self, fraction: float) -> None:
        """``torch.cuda.set_per_process_memory_fraction(fraction, 0)``."""
        import torch  # noqa: PLC0415 - GPU workers only

        torch.cuda.set_per_process_memory_fraction(fraction, 0)

    def empty_cache(self) -> None:
        """``torch.cuda.empty_cache()``."""
        import torch  # noqa: PLC0415 - GPU workers only

        torch.cuda.empty_cache()


@dataclass(frozen=True, slots=True)
class GpuTools:
    """What a GPU runner uses: the NVML reader, torch's memory controls and the leases."""

    nvml: NvmlReader
    memory: TorchMemory
    lease: GpuLease
    file_lease: FileLease = NO_FILE_LEASE
    """The cross-container lease (feature 009); :data:`NO_FILE_LEASE` keeps the 003 behaviour."""


@dataclass(frozen=True, slots=True)
class _CpuFallback:
    """The step has to run on the CPU for ``reason`` (decided while the leases were held)."""

    reason: str


class CpuRunner:
    """Runs every step on the CPU; ``info`` carries the probe's reason when a GPU was requested."""

    def __init__(self, info: DeviceInfo = CPU_DEVICE) -> None:
        """Report ``info`` as the worker's device."""
        self._info = info

    @property
    def info(self) -> DeviceInfo:
        """The worker's device (``kind=cpu``)."""
        return self._info

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:  # noqa: ARG002 - port signature
        """Call ``work("cpu")``."""
        return work("cpu"), CPU_USE

    def release(self) -> None:
        """Nothing to release on the CPU."""

    def reclaim_with(self, reclaim: Reclaimer) -> None:
        """A CPU worker holds no GPU models; ``reclaim`` is never called."""


class GpuRunner:
    """Runs steps on the GPU with the ADR-012 policy (lease, memory check, OOM -> CPU retry)."""

    def __init__(self, info: DeviceInfo, tools: GpuTools) -> None:
        """Run on the probed GPU ``info`` with ``tools``."""
        self._info = info
        self._tools = tools
        self._gpu_use = DeviceUse(kind="gpu", torch_device="cuda", name=info.name)
        self._reclaim: Reclaimer | None = None

    @property
    def info(self) -> DeviceInfo:
        """The probed GPU."""
        return self._info

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        """Run ``work`` on the GPU when the leases are free and ``need`` fits, else on the CPU."""
        with self._tools.lease.hold():
            outcome = self._leased(work, need)
            if isinstance(outcome, _CpuFallback):
                return work("cpu"), _fallback(outcome.reason)
            return outcome

    def _leased[T](
        self, work: Callable[[str], T], need: GpuNeed
    ) -> tuple[T, DeviceUse] | _CpuFallback:
        """Take the file lease, then run the GPU attempt (or decide the CPU fallback)."""
        with self._tools.file_lease.hold() as granted:
            if not granted:
                return _CpuFallback(GPU_LEASE_BUSY_REASON)
            return self._on_gpu(work, need)

    def _on_gpu[T](
        self, work: Callable[[str], T], need: GpuNeed
    ) -> tuple[T, DeviceUse] | _CpuFallback:
        memory = self._memory()
        if memory is not None and memory.free_mib < need.memory_mib:
            memory = self._reclaimed(need)
        if memory is None:
            return work("cuda"), self._gpu_use
        if memory.free_mib < need.memory_mib:
            return _CpuFallback(not_enough_memory(need.label, need.memory_mib, memory.free_mib))
        return self._attempt(work, need, memory)

    def reclaim_with(self, reclaim: Reclaimer) -> None:
        """Use ``reclaim`` to release the worker's own GPU models when a step does not fit."""
        self._reclaim = reclaim

    def _reclaimed(self, need: GpuNeed) -> GpuMemory | None:
        """Release the worker's other GPU models and the torch cache, then read NVML again."""
        if self._reclaim is not None:
            try:
                self._reclaim(need)
            except Exception:
                _LOG.exception("Releasing cached GPU models failed")
        self.release()
        return self._memory()

    def release(self) -> None:
        """Drop unreachable GPU tensors and return torch's cached GPU memory (idle worker)."""
        gc.collect()
        self._tools.memory.empty_cache()

    def _memory(self) -> GpuMemory | None:
        try:
            return self._tools.nvml.memory()
        except Exception:
            _LOG.warning("NVML read failed; running the step without a memory check", exc_info=True)
            return None

    def _attempt[T](
        self, work: Callable[[str], T], need: GpuNeed, memory: GpuMemory
    ) -> tuple[T, DeviceUse] | _CpuFallback:
        try:
            return self._capped(work, memory), self._gpu_use
        except Exception as exc:
            if not is_gpu_out_of_memory(exc):
                raise
            _LOG.warning("GPU out of memory during %s; retrying on the CPU", need.label)
        self._tools.memory.empty_cache()
        return _CpuFallback(out_of_memory(need.label))

    def _capped[T](self, work: Callable[[str], T], memory: GpuMemory) -> T:
        total = self._info.total_memory_mib or memory.total_mib
        self._tools.memory.set_fraction(max(memory.free_mib - HEADROOM_MIB, 0) / total)
        try:
            return work("cuda")
        finally:
            self._tools.memory.set_fraction(1.0)


def _fallback(reason: str) -> DeviceUse:
    return DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=reason)
