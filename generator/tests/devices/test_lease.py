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


def test_a_second_thread_blocks_on_the_file_lease(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    order: list[str] = []
    entered = threading.Event()
    release = threading.Event()

    def first() -> None:
        with FileLease(path, _RealClock()).hold(lambda _: None):
            order.append("first in")
            entered.set()
            release.wait(5)
            order.append("first out")

    def second() -> None:
        entered.wait(5)
        with FileLease(path, _RealClock(), poll_seconds=0.01).hold(lambda _: order.append("wait")):
            order.append("second in")

    threads = [threading.Thread(target=first), threading.Thread(target=second)]
    for thread in threads:
        thread.start()
    entered.wait(5)
    time.sleep(0.1)
    release.set()
    for thread in threads:
        thread.join(5)

    assert order[0] == "first in"
    assert "wait" in order
    assert order.index("first out") < order.index("second in")


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


def test_another_process_holding_the_lease_blocks_this_one(tmp_path: Path) -> None:
    path = tmp_path / "gpu.lock"
    process = subprocess.Popen(  # noqa: S603 - fixed interpreter and script
        [sys.executable, "-c", _HOLDER, str(path)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        text=True,
    )
    try:
        assert process.stdout is not None
        assert process.stdout.readline().strip() == "held"
        clock = FakeClock()
        waits: list[str] = []

        def finish_holder_after_first_poll() -> None:
            if len(clock.sleeps) == 1:
                assert process.stdin is not None
                process.stdin.write("\n")
                process.stdin.flush()
                process.wait(10)

        clock.on_sleep = finish_holder_after_first_poll
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
