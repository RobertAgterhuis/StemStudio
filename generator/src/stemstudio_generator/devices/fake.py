"""Fake device adapters for the fake stack (``STEMSTUDIO_GENERATOR=fake``; CI and E2E).

The fake stack runs the real gate (lease, policy, wait texts) over a fake NVML whose free memory
``main.py`` takes from the fake engine's mode (``engines.fake.fake_free_memory_mib``: mode
``busy`` never has enough). Mode ``no-gpu`` makes the probe report the real "CUDA not
available" text (003 data-model 2.1); every other mode looks like the reference GPU.
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

FAKE_CAPABILITY: Final = "12.0"
"""Compute capability the fake probe reports."""


class FakeNvml:
    """``NvmlReader`` with fixed memory."""

    def __init__(self, memory: GpuMemory) -> None:
        """Always report ``memory``."""
        self._memory = memory

    def memory(self) -> GpuMemory:
        """The fixed memory."""
        return self._memory


class FakeDeviceProbe:
    """``DeviceProbe`` of the fake stack."""

    def __init__(self, mode: FakeGeneratorMode, memory: GpuMemory) -> None:
        """Probe result for ``mode`` with ``memory`` (CPU with the 003 reason for ``no-gpu``)."""
        self._info = (
            DeviceInfo(kind="cpu", reason=NOT_AVAILABLE)
            if mode == "no-gpu"
            else DeviceInfo(
                kind="gpu",
                name=FAKE_GPU_NAME,
                compute_capability=FAKE_CAPABILITY,
                total_memory_mib=memory.total_mib,
                free_memory_mib=memory.free_mib,
            )
        )

    def probe(self) -> DeviceInfo:
        """The fixed result."""
        return self._info


def fake_gate(
    memory: GpuMemory, clock: Clock, wait_seconds: float, lease_file: Path | None
) -> LeaseGpuGate:
    """The real gate over a fake NVML reporting ``memory``, without a memory cap."""
    gpu = GpuAccess(
        lease=open_lease(lease_file, clock), nvml=FakeNvml(memory), cap=NoMemoryCap(),
        name=FAKE_GPU_NAME,
    )  # fmt: skip
    return LeaseGpuGate(gpu, clock, GatePolicy(wait_seconds=wait_seconds))
