"""Feature 013: rehydrated and evicted jobs are never reused for a new job of the same source."""

from collections.abc import Callable
from pathlib import Path

import pytest

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import JobState

RecordFactory = Callable[..., StoredJob]


@pytest.mark.parametrize("flag", ["from_library", "cache_evicted"])
def test_flagged_completed_jobs_are_not_reused(
    tmp_path: Path, make_record: RecordFactory, flag: str
) -> None:
    store = JobStore(tmp_path)
    record = make_record(status=JobState.COMPLETED)
    store.save(record.model_copy(update={flag: True}))

    assert store.find_active(record.job.source_key) is None


def test_plain_completed_jobs_are_reused(tmp_path: Path, make_record: RecordFactory) -> None:
    store = JobStore(tmp_path)
    record = make_record(status=JobState.COMPLETED)
    store.save(record)

    assert store.find_active(record.job.source_key) == record


def test_flags_survive_a_reload(tmp_path: Path, make_record: RecordFactory) -> None:
    store = JobStore(tmp_path)
    record = make_record(status=JobState.COMPLETED).model_copy(
        update={"from_library": True, "cache_evicted": True}
    )
    store.save(record)

    reloaded = JobStore(tmp_path)
    reloaded.load_all()

    assert reloaded.get(record.job.job_id) == record
