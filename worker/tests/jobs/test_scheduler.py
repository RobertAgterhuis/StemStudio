"""Tests of the priority scheduler: standard jobs before detail steps (T022, FR-011, SC-006)."""

import logging
import threading
from collections.abc import Callable, Iterator

import pytest

from stemstudio_worker.jobs.scheduler import PriorityScheduler, SchedulerHooks

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
        if unit.split(":", maxsplit=1)[0] in self.crash:
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


def test_a_failing_yield_hook_does_not_stop_the_standard_job(
    caplog: pytest.LogCaptureFixture,
) -> None:
    recorder = Recorder({"a": 1})

    def broken(ids: list[str]) -> None:
        raise RuntimeError("hook")

    scheduler = PriorityScheduler(
        recorder.standard, recorder.detail, SchedulerHooks(on_yield=broken)
    )
    scheduler.submit_detail("a")
    scheduler.submit("s1")
    with caplog.at_level(logging.ERROR):
        _started(scheduler).join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["s1", "a:1"]
    assert "waiting detail jobs failed" in caplog.text


def _started(scheduler: PriorityScheduler) -> PriorityScheduler:
    scheduler.start()
    return scheduler


def test_active_detail_jobs_are_told_before_a_standard_job_runs() -> None:
    recorder = Recorder({"a": 2, "b": 1})
    release = recorder.block("a:1")
    yielded: list[tuple[str, ...]] = []
    scheduler = _started(PriorityScheduler(
        recorder.standard, recorder.detail,
        SchedulerHooks(on_yield=lambda ids: yielded.append(tuple(ids))),
    ))  # fmt: skip
    scheduler.submit_detail("a")
    scheduler.submit_detail("b")
    assert recorder.entered["a:1"].wait(WAIT)
    scheduler.submit("s1")
    release.set()
    scheduler.join()
    scheduler.submit("s2")
    scheduler.join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["a:1", "s1", "a:2", "b:1", "s2"]
    assert yielded == [("a", "b")]


def test_dropping_a_detail_job_that_is_no_longer_queued_is_harmless() -> None:
    scheduler = PriorityScheduler(lambda _job: None, lambda _detail: False)

    scheduler._drop("gone")

    assert scheduler.pending == (0, 0)


# ------------------------------------------------------------------ lyrics alignment (006 T017)


def _aligning(recorder: Recorder, hooks: SchedulerHooks | None = None) -> PriorityScheduler:
    """A scheduler whose align units are recorded as ``align:<id>``."""
    return PriorityScheduler(
        recorder.standard,
        recorder.detail,
        hooks or SchedulerHooks(),
        align=lambda job: recorder.standard(f"align:{job}"),
    )


def test_a_queued_alignment_runs_after_waiting_standard_jobs_and_before_detail_steps() -> None:
    recorder = Recorder({"a": 2})
    scheduler = _aligning(recorder)
    scheduler.submit_detail("a")
    scheduler.submit_align("l1")
    scheduler.submit("s1")
    scheduler.submit_align("l2")
    _started(scheduler).join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["s1", "align:l1", "align:l2", "a:1", "a:2"]


def test_an_alignment_never_preempts_a_running_detail_step() -> None:
    recorder = Recorder({"a": 3})
    release = recorder.block("a:1")
    scheduler = _started(_aligning(recorder))
    scheduler.submit_detail("a")
    assert recorder.entered["a:1"].wait(WAIT)
    scheduler.submit_align("l1")
    scheduler.submit("s1")
    release.set()
    scheduler.join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["a:1", "s1", "align:l1", "a:2", "a:3"]


def test_a_standard_job_submitted_during_an_alignment_runs_next() -> None:
    recorder = Recorder({"a": 1})
    release = recorder.block("align:l1")
    scheduler = _started(_aligning(recorder))
    scheduler.submit_align("l1")
    scheduler.submit_align("l2")
    assert recorder.entered["align:l1"].wait(WAIT)
    scheduler.submit_detail("a")
    scheduler.submit("s1")
    release.set()
    scheduler.join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["align:l1", "s1", "align:l2", "a:1"]


def test_the_idle_hook_waits_for_queued_alignments() -> None:
    recorder = Recorder()
    idle: list[int] = []
    hooks = SchedulerHooks(on_idle=lambda: idle.append(len(recorder.calls)))
    scheduler = _aligning(recorder, hooks)
    scheduler.submit("s1")
    scheduler.submit_align("l1")
    _started(scheduler).join()
    scheduler.stop(WAIT)
    assert idle == [2]
    assert scheduler.pending_alignments == 0


def test_alignments_without_an_align_unit_are_skipped() -> None:
    recorder = Recorder()
    scheduler = PriorityScheduler(recorder.standard, recorder.detail)
    scheduler.submit_align("l1")
    scheduler.submit("s1")
    assert scheduler.pending_alignments == 1
    _started(scheduler).join()
    scheduler.stop(WAIT)
    assert recorder.calls == ["s1"]
