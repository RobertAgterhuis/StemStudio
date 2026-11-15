"""Fake device adapters of the fake stack (CI/E2E, 009 data-model 1)."""

from pathlib import Path

import pytest

from stemstudio_generator.devices.fake import (
    FAKE_GPU_NAME,
    FakeDeviceProbe,
    FakeNvml,
    fake_gate,
)
from stemstudio_generator.devices.lease import FileLease, ThreadLease
from stemstudio_generator.devices.probe import NOT_AVAILABLE, is_ready
from stemstudio_generator.ports import (
    COVER_TURBO,
    RESTYLE_NEEDS,
    DeviceProbe,
    GpuBusyError,
    GpuGate,
)

from ..fakes import FakeClock


@pytest.mark.parametrize("mode", ["ready", "oom", "oom-twice", "busy", "slow", "unvoiced-sample"])
def test_fake_probe_reports_a_gpu_in_every_mode_but_no_gpu(mode: str) -> None:
    probe = FakeDeviceProbe(mode)  # type: ignore[arg-type]

    info = probe.probe()

    assert isinstance(probe, DeviceProbe)
    assert info.kind == "gpu"
    assert info.name == FAKE_GPU_NAME
    assert info.total_memory_mib == 16_303
    assert is_ready(info)


def test_fake_probe_in_no_gpu_mode_gives_the_real_reason_text() -> None:
    info = FakeDeviceProbe("no-gpu").probe()

    assert info.kind == "cpu"
    assert info.reason == NOT_AVAILABLE
    assert not is_ready(info)


def test_fake_nvml_reports_too_little_memory_only_when_busy() -> None:
    assert FakeNvml("ready").memory().free_mib == 14_800
    assert FakeNvml("busy").memory().free_mib == 2_048


def test_fake_gate_admits_the_standard_profile_when_ready(tmp_path: Path) -> None:
    gate = fake_gate("ready", FakeClock(), wait_seconds=600.0, lease_file=None)

    assert isinstance(gate, GpuGate)
    with gate.hold(RESTYLE_NEEDS, lambda _: None) as use:
        assert use.need is COVER_TURBO
        assert use.device.name == FAKE_GPU_NAME


def test_fake_gate_in_busy_mode_fails_after_the_wait_limit() -> None:
    clock = FakeClock()
    gate = fake_gate("busy", clock, wait_seconds=20.0, lease_file=None)

    with pytest.raises(GpuBusyError, match="needs 8.4 GB, 2.0 GB free"):
        with gate.hold(RESTYLE_NEEDS, lambda _: None):
            pass
    assert sum(clock.sleeps) == pytest.approx(20.0)


def test_fake_gate_takes_the_file_lease_when_configured(tmp_path: Path) -> None:
    clock = FakeClock()
    lease_file = tmp_path / "gpu.lock"
    gate = fake_gate("ready", clock, wait_seconds=0.0, lease_file=lease_file)

    with gate.hold(RESTYLE_NEEDS, lambda _: None):
        assert lease_file.exists()
    assert isinstance(gate.lease, FileLease)
    assert isinstance(fake_gate("ready", clock, 0.0, None).lease, ThreadLease)
