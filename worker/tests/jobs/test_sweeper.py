"""Tests of the periodic sweep timer (005 T014: results swept after 24 h, checked hourly)."""

import threading

import pytest

from stemstudio_worker.jobs.sweeper import Sweeper

WAIT = 5.0


def test_runs_every_task_at_start_and_then_periodically() -> None:
    calls: list[str] = []
    twice = threading.Event()

    def first() -> None:
        calls.append("first")
        if calls.count("first") >= 2:
            twice.set()

    sweeper = Sweeper(0.01, [first, lambda: calls.append("second")])
    sweeper.start()
    try:
        assert twice.wait(WAIT)
    finally:
        sweeper.stop(WAIT)

    assert calls[:2] == ["first", "second"]
    assert not sweeper.running


def test_a_failing_task_is_logged_and_the_others_still_run(
    caplog: pytest.LogCaptureFixture,
) -> None:
    ran = threading.Event()

    def broken() -> None:
        raise OSError("disk gone")

    sweeper = Sweeper(3600, [broken, ran.set])
    sweeper.start()
    try:
        assert ran.wait(WAIT)
    finally:
        sweeper.stop(WAIT)

    assert "sweep task failed" in caplog.text


def test_stop_before_start_and_double_start_are_harmless() -> None:
    sweeper = Sweeper(3600, [])
    sweeper.stop()
    sweeper.start()
    sweeper.start()
    sweeper.stop(WAIT)

    assert not sweeper.running
