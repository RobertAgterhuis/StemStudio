"""Fake device adapters for the fake stack (``STEMSTUDIO_GENERATOR=fake``; CI and E2E).

The fake stack runs the real gate (lease, policy, wait texts) over a fake NVML: mode ``busy``
never has enough free memory, ``no-gpu`` makes the probe report the real "CUDA not available"
text, every other mode looks like the reference GPU (RTX 5070 Ti, 16 GB).
"""

from pathlib import Path
from typing import Final

from stemstudio_generator.config import FakeGeneratorMode
from stemstudio_generator.devices.gate import GatePolicy, GpuAccess, LeaseGpuGate
from stemstudio_generator.devices.lease import open_lease
from stemstudio_generator.devices.memory import NoMemoryCap
from stemstudio_generator.devices.nvml import GpuMemory
from stemstudio_generator.devices.probe import NOT_AVAILABLE
from stemstudio_generator.ports import Clock
from stemstudio_generator.schemas import DeviceInfo

FAKE_GPU_NAME: Final = "Fake GPU (StemStudio test stack)"
"""Device name of the fake stack."""

_TOTAL_MIB: Final = 16_303
_FREE_MIB: Final = 14_800
_BUSY_FREE_MIB: Final = 2_048


class FakeNvml:
    """``NvmlReader`` with fixed memory; too little free memory in mode ``busy``."""

    def __init__(self, mode: FakeGeneratorMode) -> None:
        """Report the memory of ``mode``."""
        free = _BUSY_FREE_MIB if mode == "busy" else _FREE_MIB
        self._memory = GpuMemory(total_mib=_TOTAL_MIB, free_mib=free)

    def memory(self) -> GpuMemory:
        """The fixed memory."""
        return self._memory


class FakeDeviceProbe:
    """``DeviceProbe`` of the fake stack."""

    def __init__(self, mode: FakeGeneratorMode) -> None:
        """Probe result for ``mode``."""
        self._info = (
            DeviceInfo(kind="cpu", reason=NOT_AVAILABLE)
            if mode == "no-gpu"
            else DeviceInfo(
                kind="gpu",
                name=FAKE_GPU_NAME,
                compute_capability="12.0",
                total_memory_mib=_TOTAL_MIB,
                free_memory_mib=_FREE_MIB,
            )
        )

    def probe(self) -> DeviceInfo:
        """The fixed result."""
        return self._info


def fake_gate(
    mode: FakeGeneratorMode, clock: Clock, wait_seconds: float, lease_file: Path | None
) -> LeaseGpuGate:
    """The real gate over the fake NVML, without a memory cap."""
    gpu = GpuAccess(
        lease=open_lease(lease_file, clock), nvml=FakeNvml(mode), cap=NoMemoryCap(),
        name=FAKE_GPU_NAME,
    )  # fmt: skip
    return LeaseGpuGate(gpu, clock, GatePolicy(wait_seconds=wait_seconds))
