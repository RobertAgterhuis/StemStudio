"""Cross-container GPU lease on a shared lock file (009 T014; ADR-018 D3)."""

import subprocess
import sys
import threading
import time
from datetime import UTC, datetime
from pathlib import Path

import pytest

from stemstudio_generator.devices.lease import (
    LEASE_WAIT_STAGE,
    FileLease,
    ThreadLease,
    open_lease,
)
from stemstudio_generator.ports import Cancelled

from ..fakes import FakeClock


def test_a_free_file_lease_is_taken_without_waiting(tmp_path: Path) -> None:
    clock = FakeClock()
    lease = FileLease(tmp_path / "lease" / "gpu.lock", clock)
    waits: list[str] = []

    with lease.hold(waits.append):
        assert (tmp_path / "lease" / "gpu.lock").exists()

    assert waits == []
    assert clock.sleeps == []


def test_a_held_lease_makes_a_second_holder_wait_until_release(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    clock = FakeClock()
    first = FileLease(path, clock)
    second = FileLease(path, clock, poll_seconds=0.5)
    waits: list[str] = []
    holder = first.hold(lambda _: None)
    holder.__enter__()

    def release_after_two_polls() -> None:
        if len(clock.sleeps) == 2:
            holder.__exit__(None, None, None)

    clock.on_sleep = release_after_two_polls
    with second.hold(waits.append):
        pass

    assert waits == [LEASE_WAIT_STAGE, LEASE_WAIT_STAGE]
    assert clock.sleeps == [0.5, 0.5]


def test_the_lease_is_released_when_the_step_raises(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    lease = FileLease(path, FakeClock())

    with pytest.raises(RuntimeError, match="boom"), lease.hold(lambda _: None):
        raise RuntimeError("boom")

    waits: list[str] = []
    with FileLease(path, FakeClock()).hold(waits.append):
        assert waits == []


def test_cancelling_while_waiting_leaves_the_lease_untouched(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    clock = FakeClock()

    def cancel(_: str) -> None:
        raise Cancelled

    with FileLease(path, clock).hold(lambda _: None):
        with pytest.raises(Cancelled), FileLease(path, clock).hold(cancel):
            pass
        assert clock.sleeps == []


class _TwoThreads:
    """First thread holds the lease until released; the second polls for it."""

    def __init__(self, path: Path) -> None:
        self.path = path
        self.order: list[str] = []
        self.entered = threading.Event()
        self.release = threading.Event()

    def first(self) -> None:
        with FileLease(self.path, _RealClock()).hold(lambda _: None):
            self.order.append("first in")
            self.entered.set()
            self.release.wait(5)
            self.order.append("first out")

    def second(self) -> None:
        self.entered.wait(5)
        lease = FileLease(self.path, _RealClock(), poll_seconds=0.01)
        with lease.hold(lambda _: self.order.append("wait")):
            self.order.append("second in")


def test_a_second_thread_blocks_on_the_file_lease(tmp_path: Path) -> None:
    run = _TwoThreads(tmp_path / "gpu.lock")
    threads = [threading.Thread(target=run.first), threading.Thread(target=run.second)]
    for thread in threads:
        thread.start()
    run.entered.wait(5)
    time.sleep(0.1)
    run.release.set()
    for thread in threads:
        thread.join(5)

    assert run.order[0] == "first in"
    assert "wait" in run.order
    assert run.order.index("first out") < run.order.index("second in")


_HOLDER = """
import sys
from pathlib import Path
from stemstudio_generator.devices.lease import FileLease
import time
from datetime import UTC, datetime


class Clock:
    def now(self) -> datetime:
        return datetime.now(UTC)

    def sleep(self, seconds: float) -> None:
        time.sleep(seconds)


with FileLease(Path(sys.argv[1]), Clock()).hold(lambda _: None):
    print("held", flush=True)
    sys.stdin.readline()
"""


def _start_holder(path: Path) -> subprocess.Popen[str]:
    process = subprocess.Popen(  # noqa: S603 - fixed interpreter and script
        [sys.executable, "-c", _HOLDER, str(path)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        text=True,
    )
    assert process.stdout is not None
    assert process.stdout.readline().strip() == "held"
    return process


def _finish(process: subprocess.Popen[str]) -> None:
    assert process.stdin is not None
    process.stdin.write("\n")
    process.stdin.flush()
    process.wait(10)


def test_another_process_holding_the_lease_blocks_this_one(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    process = _start_holder(path)
    try:
        clock = FakeClock()
        waits: list[str] = []
        clock.on_sleep = lambda: _finish(process)
        with FileLease(path, clock).hold(waits.append):
            assert waits == [LEASE_WAIT_STAGE]
    finally:
        process.kill()
        process.wait(10)


def test_thread_lease_serialises_steps_in_one_process() -> None:
    lease = ThreadLease()

    with lease.hold(lambda _: None):
        assert lease.held
    assert not lease.held


def test_open_lease_picks_the_file_lease_only_when_a_path_is_configured(tmp_path: Path) -> None:
    clock = FakeClock()

    assert isinstance(open_lease(tmp_path / "gpu.lock", clock), FileLease)
    assert isinstance(open_lease(None, clock), ThreadLease)


class _RealClock:
    def now(self) -> datetime:
        return datetime.now(UTC)

    def sleep(self, seconds: float) -> None:
        time.sleep(seconds)
