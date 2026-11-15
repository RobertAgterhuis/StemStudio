"""GPU gate: lease + NVML free-memory policy + memory cap (009 T014; ADR-018 D3/D4)."""

from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

import pytest

from stemstudio_generator.devices.gate import (
    MEMORY_WAIT_STAGE,
    GatePolicy,
    GpuAccess,
    LeaseGpuGate,
)
from stemstudio_generator.devices.lease import FileLease, ThreadLease
from stemstudio_generator.devices.memory import cap_fraction
from stemstudio_generator.devices.nvml import GpuMemory
from stemstudio_generator.ports import (
    COVER_TURBO,
    COVER_TURBO_LOWMEM,
    RESTYLE_NEEDS,
    SVC_FP16,
    Cancelled,
    GpuBusyError,
    GpuOutOfMemoryError,
    GpuRequiredError,
    WaitCallback,
)
from stemstudio_generator.schemas import JobDevice

from ..fakes import FakeClock

_TOTAL = 16_303


class ScriptedNvml:
    """Free memory per read; the last value repeats."""

    def __init__(self, *free: int) -> None:
        self.free = list(free)
        self.reads = 0

    def memory(self) -> GpuMemory:
        self.reads += 1
        value = self.free[0] if len(self.free) == 1 else self.free.pop(0)
        return GpuMemory(total_mib=_TOTAL, free_mib=value)


class BrokenNvml:
    def memory(self) -> GpuMemory:
        raise RuntimeError("NVML: driver not loaded")


class RecordingCap:
    def __init__(self) -> None:
        self.calls: list[float | str] = []

    def limit(self, fraction: float) -> None:
        self.calls.append(fraction)

    def reset(self) -> None:
        self.calls.append("reset")


class RecordingLease:
    """ThreadLease that records holds and releases."""

    def __init__(self) -> None:
        self.events: list[str] = []
        self._inner = ThreadLease()

    @contextmanager
    def hold(self, on_wait: WaitCallback) -> Iterator[None]:
        with self._inner.hold(on_wait):
            self.events.append("acquire")
            try:
                yield
            finally:
                self.events.append("release")


def _gate(
    nvml: object, lease: object | None = None, wait: float = 600.0
) -> tuple[LeaseGpuGate, RecordingCap, FakeClock]:
    cap, clock = RecordingCap(), FakeClock()
    access = GpuAccess(nvml=nvml, cap=cap, name="NVIDIA GeForce RTX 5070 Ti")  # type: ignore[arg-type]
    gate = LeaseGpuGate(
        lease or ThreadLease(),  # type: ignore[arg-type]
        access,
        clock,
        GatePolicy(wait_seconds=wait),
    )
    return gate, cap, clock


def test_enough_free_memory_admits_the_standard_profile() -> None:
    gate, cap, _ = _gate(ScriptedNvml(14_800))

    with gate.hold(RESTYLE_NEEDS, lambda _: None) as use:
        assert use.need is COVER_TURBO
        assert use.need.profile == "standard"
        assert use.free_memory_mib == 14_800
        assert use.device == JobDevice(kind="gpu", name="NVIDIA GeForce RTX 5070 Ti")


def test_less_memory_admits_the_low_memory_profile() -> None:
    gate, _, _ = _gate(ScriptedNvml(10_000))

    with gate.hold(RESTYLE_NEEDS, lambda _: None) as use:
        assert use.need is COVER_TURBO_LOWMEM
        assert use.need.profile == "lowMemory"


def test_memory_fraction_is_set_while_held_and_reset_after() -> None:
    gate, cap, _ = _gate(ScriptedNvml(14_800))

    with gate.hold(RESTYLE_NEEDS, lambda _: None):
        assert cap.calls == [pytest.approx((14_800 - 256) / _TOTAL)]

    assert cap.calls[-1] == "reset"


def test_lease_and_cap_are_released_when_the_step_raises() -> None:
    lease = RecordingLease()
    gate, cap, _ = _gate(ScriptedNvml(14_800), lease)

    with pytest.raises(GpuOutOfMemoryError), gate.hold(RESTYLE_NEEDS, lambda _: None):
        raise GpuOutOfMemoryError

    assert lease.events == ["acquire", "release"]
    assert cap.calls[-1] == "reset"


def test_too_little_memory_releases_the_lease_and_rechecks_every_ten_seconds() -> None:
    lease = RecordingLease()
    gate, cap, clock = _gate(ScriptedNvml(2_000, 2_000, 14_800), lease)
    waits: list[str] = []
    lease_held_while_sleeping: list[bool] = []
    clock.on_sleep = lambda: lease_held_while_sleeping.append(lease.events[-1] == "acquire")

    with gate.hold(RESTYLE_NEEDS, waits.append) as use:
        assert use.need is COVER_TURBO

    assert lease.events == ["acquire", "release"] * 3
    assert sum(clock.sleeps) == pytest.approx(20.0)
    assert not any(lease_held_while_sleeping)
    expected = MEMORY_WAIT_STAGE.format(need=8_602 / 1024, free=2_000 / 1024)
    assert waits[0] == expected
    assert set(waits) == {expected}
    assert cap.calls == [pytest.approx((14_800 - 256) / _TOTAL), "reset"]


def test_waiting_ends_with_gpu_busy_after_the_wait_limit() -> None:
    gate, cap, clock = _gate(ScriptedNvml(2_048), wait=60.0)

    with pytest.raises(GpuBusyError) as caught, gate.hold(RESTYLE_NEEDS, lambda _: None):
        pytest.fail("must not be admitted")

    assert str(caught.value) == "GPU busy: needs 8.4 GB, 2.0 GB free."
    assert caught.value.code == "gpuBusy"
    assert sum(clock.sleeps) == pytest.approx(60.0)
    assert cap.calls == []


def test_a_zero_wait_limit_fails_on_the_first_check() -> None:
    gate, _, clock = _gate(ScriptedNvml(1_000), wait=0.0)

    with pytest.raises(GpuBusyError, match="needs 6.0 GB, 1.0 GB free"):
        with gate.hold((SVC_FP16,), lambda _: None):
            pass
    assert clock.sleeps == []


def test_cancel_while_waiting_for_memory_stops_the_wait() -> None:
    gate, _, clock = _gate(ScriptedNvml(2_000))
    calls: list[str] = []

    def cancel_on_third(stage: str) -> None:
        calls.append(stage)
        if len(calls) == 3:
            raise Cancelled

    with pytest.raises(Cancelled), gate.hold(RESTYLE_NEEDS, cancel_on_third):
        pass
    assert sum(clock.sleeps) < 10.0


def test_unreadable_nvml_means_the_gpu_is_not_usable() -> None:
    lease = RecordingLease()
    gate, _, _ = _gate(BrokenNvml(), lease)

    with pytest.raises(GpuRequiredError, match="NVML unavailable"):
        with gate.hold(RESTYLE_NEEDS, lambda _: None):
            pass
    assert lease.events == ["acquire", "release"]


def test_the_gate_uses_the_file_lease(tmp_path: Path) -> None:
    clock = FakeClock()
    path = tmp_path / "gpu.lock"
    gate = LeaseGpuGate(
        FileLease(path, clock),
        GpuAccess(nvml=ScriptedNvml(14_800), cap=RecordingCap(), name=None),
        clock,
        GatePolicy(),
    )
    waits: list[str] = []

    with gate.hold(RESTYLE_NEEDS, lambda _: None) as use:
        assert use.device == JobDevice(kind="gpu")
        other = FileLease(path, clock)
        clock.on_sleep = lambda: (_ for _ in ()).throw(Cancelled())
        with pytest.raises(Cancelled), other.hold(waits.append):
            pass

    assert len(waits) == 1


def test_gate_recognises_out_of_memory_errors() -> None:
    gate, _, _ = _gate(ScriptedNvml(14_800))

    assert gate.is_out_of_memory(GpuOutOfMemoryError())
    assert not gate.is_out_of_memory(ValueError("bad input"))


@pytest.mark.parametrize(
    ("free", "expected"),
    [(14_800, (14_800 - 256) / _TOTAL), (_TOTAL + 1_000, 1.0), (100, 0.0)],
)
def test_cap_fraction_keeps_256_mib_head_room_within_zero_and_one(
    free: int, expected: float
) -> None:
    assert cap_fraction(GpuMemory(total_mib=_TOTAL, free_mib=free)) == pytest.approx(expected)
