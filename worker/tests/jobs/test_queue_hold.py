"""``QueueThread.hold`` (feature 013): no unit starts while the queue lock is held."""

import threading
from collections.abc import Iterator

import pytest

from stemstudio_worker.jobs.scheduler import PriorityScheduler


class _Standard:
    def __init__(self) -> None:
        self.ran: list[str] = []
        self.started = threading.Event()

    def __call__(self, job_id: str) -> None:
        self.ran.append(job_id)
        self.started.set()


@pytest.fixture
def standard() -> _Standard:
    return _Standard()


@pytest.fixture
def scheduler(standard: _Standard) -> Iterator[PriorityScheduler]:
    running = PriorityScheduler(standard, lambda _d: False)
    running.start()
    yield running
    running.stop(5)


def test_no_unit_starts_while_held(scheduler: PriorityScheduler, standard: _Standard) -> None:
    with scheduler.hold():
        scheduler.submit("a" * 32)
        assert not standard.started.wait(0.2)

    assert scheduler.join(5)
    assert standard.ran == ["a" * 32]
