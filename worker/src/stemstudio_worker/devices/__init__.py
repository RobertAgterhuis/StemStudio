"""The GPU device policy of the worker (stem-separation agent, feature 003, ADR-012).

Only the composition root (``main.py``) imports this package; ``separation``, ``detail``,
``jobs`` and ``api`` receive the ``ports.DeviceRunner`` it builds. ``create_device_runner`` runs
the start-up probe once (data-model.md 2.1); its result is fixed for the worker's lifetime.
"""

from collections.abc import Callable
from typing import Protocol, runtime_checkable

from stemstudio_worker.config import DeviceKind, WorkerSettings
from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.devices.lease import (
    GPU_LEASE,
    NO_FILE_LEASE,
    FileGpuLease,
    FileLease,
    LeaseWait,
)
from stemstudio_worker.devices.nvml import NvmlReader, PynvmlReader
from stemstudio_worker.devices.probe import load_torch_probe, probe_device
from stemstudio_worker.devices.runner import CpuRunner, GpuRunner, GpuTools, TorchCudaMemory
from stemstudio_worker.ports import DeviceRunner, GpuNeed
from stemstudio_worker.schemas import DeviceInfo

__all__ = ["ManagedRunner", "create_device_runner", "default_probe"]

Probe = Callable[[DeviceKind], DeviceInfo]
"""Decides the worker's device for a ``STEMSTUDIO_DEVICE`` preference."""


@runtime_checkable
class ManagedRunner(DeviceRunner, Protocol):
    """A ``DeviceRunner`` that can give its GPU memory back when the worker is idle."""

    def release(self) -> None:
        """Release cached GPU memory (ADR-012 D6); called when no work is queued."""
        ...

    def reclaim_with(self, reclaim: Callable[[GpuNeed], None]) -> None:
        """Set how the worker's own cached GPU models are released when a step does not fit."""
        ...


def _nvml() -> NvmlReader:
    return PynvmlReader()


def default_probe(preference: DeviceKind) -> DeviceInfo:
    """Probe with the installed torch and pynvml (nothing is loaded for ``cpu``)."""
    return probe_device(preference, load_torch_probe, _nvml)


def create_device_runner(settings: WorkerSettings, probe: Probe = default_probe) -> ManagedRunner:
    """Build the worker's device runner.

    Args:
        settings: ``separator=fake`` gives the fake device (``settings.fake_device``, no probe);
            otherwise ``settings.device`` is probed.
        probe: The start-up probe (injectable for tests).

    Returns:
        A :class:`FakeDeviceRunner`, a :class:`GpuRunner` when the probe found a usable GPU
        (with the shared file lease when ``settings.gpu_lease_file`` is set), or a
        :class:`CpuRunner` (with the probe's reason when a GPU was requested).
    """
    if settings.separator == "fake":
        return FakeDeviceRunner(settings.fake_device)
    info = probe(settings.device)
    if info.kind == "cpu":
        return CpuRunner(info)
    tools = GpuTools(
        nvml=_nvml(), memory=TorchCudaMemory(), lease=GPU_LEASE, file_lease=_file_lease(settings)
    )
    return GpuRunner(info, tools)


def _file_lease(settings: WorkerSettings) -> FileLease:
    """The cross-container lease of a GPU worker (feature 009); none without the lock file."""
    if settings.gpu_lease_file is None:
        return NO_FILE_LEASE
    return FileGpuLease(settings.gpu_lease_file, LeaseWait(settings.gpu_lease_wait_seconds))
