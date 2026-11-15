"""Restart reconciliation (009 T016; data-model 2.4, SC-006)."""

from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_generator.jobs.reconcile import reconcile
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.schemas import INTERRUPTED_TEXT, GenerationError

from ..fakes import START, FakeClock
from .helpers import TAKE, make_job


@pytest.mark.parametrize("status", ["running", "waitingForGpu", "downloadingModel"])
def test_active_jobs_fail_as_interrupted(tmp_path: Path, status: str) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    store.save(make_job(1).model_copy(update={"status": status, "progress": 40}))
    clock.advance(60)

    assert reconcile(store, clock) == []

    job = store.get(make_job(1).generation_id)
    assert job is not None
    assert job.status == "failed"
    assert job.error == GenerationError(code="interrupted", message=INTERRUPTED_TEXT)
    assert job.updated_at == clock.now()


def test_queued_jobs_stay_queued_and_are_returned_oldest_first(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    store.save(make_job(2, START + timedelta(seconds=5)))
    store.save(make_job(1, START))

    assert reconcile(store, clock) == [make_job(1).generation_id, make_job(2).generation_id]
    job = store.get(make_job(1).generation_id)
    assert job is not None
    assert job.status == "queued"


def test_terminal_jobs_are_left_alone(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    completed = make_job(1).model_copy(
        update={"status": "completed", "progress": 100, "stage": "Completed", "takes": [TAKE]}
    )
    store.save(completed)

    assert reconcile(store, clock) == []
    assert store.get(completed.generation_id) == completed
