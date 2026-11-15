"""Tests of the priority scheduler: standard jobs before detail steps (T022, FR-011, SC-006)."""

import logging
import threading
from collections.abc import Callable, Iterator

import pytest

from stemstudio_worker.jobs.scheduler import PriorityScheduler

WAIT = 5.0


class Recorder:
    """Fake step functions: record calls; detail jobs have a fixed number of steps."""

    def __init__(self, steps: dict[str, int] | None = None) -> None:
        self.calls: list[str] = []
        self.steps = dict(steps or {})
        self.done: dict[str, int] = {}
        self.blocking: dict[str, threading.Event] = {}
        self.entered: dict[str, threading.Event] = {}
        self.crash: set[str] = set()

    def block(self, unit: str) -> threading.Event:
        """Make the call recorded as ``unit`` wait until the returned event is set."""
        self.blocking[unit] = threading.Event()
        self.entered[unit] = threading.Event()
        return self.blocking[unit]

    def _enter(self, unit: str) -> None:
        self.calls.append(unit)
        if unit in self.entered:
            self.entered[unit].set()
            assert self.blocking[unit].wait(WAIT)
        if unit.split(":")[0] in self.crash:
            raise RuntimeError("boom")

    def standard(self, job_id: str) -> None:
        self._enter(job_id)

    def detail(self, detail_id: str) -> bool:
        self.done[detail_id] = self.done.get(detail_id, 0) + 1
        self._enter(f"{detail_id}:{self.done[detail_id]}")
        return self.done[detail_id] < self.steps[detail_id]


Factory = Callable[[Recorder], PriorityScheduler]


@pytest.fixture
def make() -> Iterator[Factory]:
    started: list[PriorityScheduler] = []

    def factory(recorder: Recorder) -> PriorityScheduler:
        scheduler = PriorityScheduler(recorder.standard, recorder.detail)
        started.append(scheduler)
        return scheduler

    yield factory
    for scheduler in started:
        scheduler.stop(WAIT)


def test_standard_jobs_run_in_fifo_order(make: Factory) -> None:
    recorder = Recorder()
    scheduler = make(recorder)
    scheduler.start()
    for job_id in ("s1", "s2", "s3"):
        scheduler.submit(job_id)
    scheduler.join()
    assert recorder.calls == ["s1", "s2", "s3"]


def test_detail_steps_of_the_oldest_job_run_first(make: Factory) -> None:
    recorder = Recorder({"a": 3, "b": 2})
    scheduler = make(recorder)
    scheduler.submit_detail("a")
    scheduler.submit_detail("b")
    scheduler.start()
    scheduler.join()
    assert recorder.calls == ["a:1", "a:2", "a:3", "b:1", "b:2"]


def test_a_submitted_standard_job_runs_before_the_next_detail_step(make: Factory) -> None:
    recorder = Recorder({"a": 3})
    release = recorder.block("a:1")
    scheduler = make(recorder)
    scheduler.start()
    scheduler.submit_detail("a")
    assert recorder.entered["a:1"].wait(WAIT)
    scheduler.submit("s1")
    scheduler.submit("s2")
    release.set()
    scheduler.join()
    assert recorder.calls == ["a:1", "s1", "s2", "a:2", "a:3"]


def test_waiting_standard_jobs_beat_queued_detail_jobs(make: Factory) -> None:
    recorder = Recorder({"a": 1, "b": 1})
    scheduler = make(recorder)
    scheduler.submit_detail("a")
    scheduler.submit_detail("b")
    scheduler.submit("s1")
    scheduler.start()
    scheduler.join()
    assert recorder.calls == ["s1", "a:1", "b:1"]


def test_stop_waits_for_the_current_unit(make: Factory) -> None:
    recorder = Recorder({"a": 5})
    release = recorder.block("a:1")
    scheduler = make(recorder)
    scheduler.start()
    scheduler.submit_detail("a")
    assert recorder.entered["a:1"].wait(WAIT)
    stopper = threading.Thread(target=scheduler.stop, args=(WAIT,))
    stopper.start()
    release.set()
    stopper.join(WAIT)
    assert not scheduler.running
    assert recorder.calls == ["a:1"]


def test_a_crashing_unit_does_not_stop_the_thread(
    make: Factory, caplog: pytest.LogCaptureFixture
) -> None:
    recorder = Recorder({"a": 3, "b": 1})
    recorder.crash = {"s1", "a"}
    scheduler = make(recorder)
    scheduler.submit("s1")
    scheduler.submit_detail("a")
    scheduler.submit_detail("b")
    scheduler.submit("s2")
    with caplog.at_level(logging.ERROR):
        scheduler.start()
        scheduler.join()
    assert scheduler.running
    assert recorder.calls == ["s1", "s2", "a:1", "b:1"]
    assert "crashed" in caplog.text


def test_start_twice_and_stop_without_start_are_harmless(make: Factory) -> None:
    scheduler = make(Recorder())
    scheduler.stop(WAIT)
    scheduler.start()
    scheduler.start()
    assert scheduler.running
    scheduler.stop(WAIT)
    assert not scheduler.running


def test_pending_reports_waiting_work(make: Factory) -> None:
    scheduler = make(Recorder({"a": 1}))
    scheduler.submit("s1")
    scheduler.submit_detail("a")
    assert scheduler.pending == (1, 1)
