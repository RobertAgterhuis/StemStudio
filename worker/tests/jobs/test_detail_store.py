"""Tests of the detail job store ``detail-jobs/*.json`` (T020, FR-010)."""

import re
from pathlib import Path

import pytest

from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.schemas import DetailJob, DetailJobState

from .conftest import T0

SHA = "9" * 64
D = DetailJobState


def detail_record(
    detail_id: str = "d" * 32, status: D = D.QUEUED, sha: str = SHA, minute: int = 0
) -> StoredDetailJob:
    created = T0.replace(minute=minute)
    job = DetailJob(
        detail_job_id=detail_id, standard_job_id="b" * 32, audio_sha256=sha, status=status,
        stage="Queued", progress=0, created_at=created, updated_at=created,
    )  # fmt: skip
    return StoredDetailJob(job=job)


def test_new_ids_are_32_lower_hex() -> None:
    assert re.fullmatch(r"[0-9a-f]{32}", DetailJobStore.new_detail_id())


def test_saved_jobs_are_persisted_and_loaded(tmp_path: Path) -> None:
    record = detail_record().model_copy(update={"cancel_requested": True})
    DetailJobStore(tmp_path).save(record)
    assert (tmp_path / f"{'d' * 32}.json").is_file()

    reloaded = DetailJobStore(tmp_path)
    reloaded.load_all()
    assert reloaded.get("d" * 32) == record
    assert reloaded.all() == [record]


def test_unreadable_files_and_bad_ids_are_ignored(tmp_path: Path) -> None:
    tmp_path.mkdir(exist_ok=True)
    (tmp_path / "broken.json").write_text("{", encoding="utf-8")
    store = DetailJobStore(tmp_path)
    store.load_all()
    assert store.all() == []
    assert store.get("../../etc") is None
    assert store.get("e" * 32) is None


def test_update_applies_a_change_and_persists_it(tmp_path: Path) -> None:
    store = DetailJobStore(tmp_path)
    store.save(detail_record())

    def to_running(record: StoredDetailJob) -> StoredDetailJob:
        return record.model_copy(
            update={"job": record.job.model_copy(update={"status": D.RUNNING})}
        )

    updated = store.update("d" * 32, to_running)
    assert updated.job.status is D.RUNNING
    reloaded = DetailJobStore(tmp_path)
    reloaded.load_all()
    stored = reloaded.get("d" * 32)
    assert stored is not None
    assert stored.job.status is D.RUNNING


def test_find_reusable_returns_the_newest_queued_running_or_completed(tmp_path: Path) -> None:
    store = DetailJobStore(tmp_path)
    store.save(detail_record("1" * 32, D.COMPLETED, minute=1))
    store.save(detail_record("2" * 32, D.RUNNING, minute=2))
    store.save(detail_record("3" * 32, D.FAILED, minute=3))
    store.save(detail_record("4" * 32, D.CANCELLED, minute=4))
    store.save(detail_record("5" * 32, D.QUEUED, sha="8" * 64, minute=5))
    found = store.find_reusable(SHA)
    assert found is not None
    assert found.job.detail_job_id == "2" * 32
    assert store.find_reusable("7" * 64) is None


def test_update_of_an_unknown_job_raises(tmp_path: Path) -> None:
    with pytest.raises(LookupError, match="does not exist"):
        DetailJobStore(tmp_path).update("e" * 32, lambda record: record)
