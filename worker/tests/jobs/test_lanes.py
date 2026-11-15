"""Tests of the render lane on the generic ``LaneScheduler`` (007 T021, FR-011, ADR-016 D6).

The lane itself was built generic in 005 (``test_metadata_lane.py``); these tests add the render
scenario and the queue state ``/health`` reports.
"""

import logging
import threading
from collections.abc import Iterator

import pytest

from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.scheduler import PriorityScheduler
from stemstudio_worker.schemas import LaneHealth

WAIT = 5.0


class Renders:
    """A fake render step: records ids, blocks on request, crashes on request."""

    def __init__(self) -> None:
        self.done: list[str] = []
        self.entered: dict[str, threading.Event] = {}
        self.release: dict[str, threading.Event] = {}
        self.crash: set[str] = set()

    def hold(self, job_id: str) -> threading.Event:
        self.entered[job_id], self.release[job_id] = threading.Event(), threading.Event()
        return self.release[job_id]

    def __call__(self, job_id: str) -> None:
        if job_id in self.release:
            self.entered[job_id].set()
            assert self.release[job_id].wait(WAIT)
        if job_id in self.crash:
            raise RuntimeError("encoder exploded")
        self.done.append(job_id)


@pytest.fixture
def renders() -> Iterator[tuple[Renders, LaneScheduler]]:
    fake = Renders()
    lane = LaneScheduler("render", fake)
    lane.start()
    yield fake, lane
    for event in fake.release.values():
        event.set()
    lane.stop(WAIT)


@pytest.fixture
def blocked_separation() -> Iterator[PriorityScheduler]:
    started, release = threading.Event(), threading.Event()

    def separation(_job_id: str) -> None:
        started.set()
        assert release.wait(WAIT)

    scheduler = PriorityScheduler(separation, lambda _detail: False)
    scheduler.start()
    scheduler.submit("s" * 32)
    assert started.wait(WAIT)
    yield scheduler
    release.set()
    scheduler.stop(WAIT)


def test_a_render_starts_while_a_separation_blocks_the_priority_scheduler(
    renders: tuple[Renders, LaneScheduler], blocked_separation: PriorityScheduler
) -> None:
    fake, lane = renders
    lane.submit("r1")

    assert lane.join(WAIT)
    assert fake.done == ["r1"]
    assert not blocked_separation.join(0)


def test_renders_run_fifo_one_at_a_time(renders: tuple[Renders, LaneScheduler]) -> None:
    fake, lane = renders
    release = fake.hold("r1")
    for job_id in ("r1", "r2", "r3"):
        lane.submit(job_id)
    assert fake.entered["r1"].wait(WAIT)

    assert fake.done == []
    release.set()
    assert lane.join(WAIT)
    assert fake.done == ["r1", "r2", "r3"]


def test_a_crashing_render_does_not_stop_the_lane(
    renders: tuple[Renders, LaneScheduler], caplog: pytest.LogCaptureFixture
) -> None:
    fake, lane = renders
    fake.crash.add("r1")
    with caplog.at_level(logging.ERROR):
        lane.submit("r1")
        lane.submit("r2")
        assert lane.join(WAIT)

    assert fake.done == ["r2"]
    assert "render lane crashed" in caplog.text


def test_health_reports_an_idle_lane(renders: tuple[Renders, LaneScheduler]) -> None:
    _, lane = renders

    assert lane.health() == LaneHealth(queued=0, running=False)


def test_health_reports_the_running_job_and_the_waiting_ones(
    renders: tuple[Renders, LaneScheduler],
) -> None:
    fake, lane = renders
    release = fake.hold("r1")
    for job_id in ("r1", "r2", "r3"):
        lane.submit(job_id)
    assert fake.entered["r1"].wait(WAIT)

    assert lane.health() == LaneHealth(queued=2, running=True)
    release.set()
    assert lane.join(WAIT)
    assert lane.health() == LaneHealth(queued=0, running=False)


def test_health_of_a_stopped_lane_counts_what_waits() -> None:
    lane = LaneScheduler("render", lambda _job: None)
    lane.submit("r1")

    assert lane.health() == LaneHealth(queued=1, running=False)
