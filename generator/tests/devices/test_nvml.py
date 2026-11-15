"""NVML memory reads over a fake ``pynvml`` module (009 T012)."""

import threading
from types import SimpleNamespace
from typing import Any

import pytest

from stemstudio_generator.devices.nvml import GpuMemory, PynvmlReader, import_pynvml

_MIB = 1024 * 1024


class FakePynvml:
    """The pynvml calls the reader makes."""

    def __init__(self, total: int, free: int) -> None:
        self.total = total
        self.free = free
        self.init_calls = 0
        self.handles: list[int] = []

    def nvmlInit(self) -> None:  # noqa: N802 - pynvml name
        self.init_calls += 1

    def nvmlDeviceGetHandleByIndex(self, index: int) -> str:  # noqa: N802 - pynvml name
        self.handles.append(index)
        return f"handle-{index}"

    def nvmlDeviceGetMemoryInfo(self, handle: str) -> Any:  # noqa: N802 - pynvml name
        assert handle.startswith("handle-")
        return SimpleNamespace(total=self.total, free=self.free, used=self.total - self.free)


def test_memory_is_reported_in_whole_mib_rounded_down() -> None:
    fake = FakePynvml(total=16_303 * _MIB + 512, free=14_800 * _MIB + 1)
    reader = PynvmlReader(loader=lambda: fake)

    assert reader.memory() == GpuMemory(total_mib=16_303, free_mib=14_800)


def test_nvml_is_initialised_once_and_reads_the_configured_gpu() -> None:
    fake = FakePynvml(total=8 * 1024 * _MIB, free=4 * 1024 * _MIB)
    reader = PynvmlReader(index=1, loader=lambda: fake)

    reader.memory()
    fake.free = 1024 * _MIB
    second = reader.memory()

    assert fake.init_calls == 1
    assert fake.handles == [1, 1]
    assert second.free_mib == 1024


def test_concurrent_first_reads_initialise_once() -> None:
    fake = FakePynvml(total=_MIB, free=_MIB)
    reader = PynvmlReader(loader=lambda: fake)
    threads = [threading.Thread(target=reader.memory) for _ in range(8)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert fake.init_calls == 1


def test_loader_errors_propagate_so_the_probe_can_report_them() -> None:
    def broken() -> Any:
        raise ImportError("pynvml")

    with pytest.raises(ImportError):
        PynvmlReader(loader=broken).memory()


def test_the_default_loader_imports_pynvml() -> None:
    module = import_pynvml()

    assert hasattr(module, "nvmlInit")
