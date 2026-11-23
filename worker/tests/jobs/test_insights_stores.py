"""Tests of the quality and handout job stores of the ``insights`` lane (020 T016, data-model 2.4/2.5)."""

import json
from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs.handout_store import HandoutJobStore, StoredHandoutJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.quality_store import QualityJobStore, StoredQualityJob
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    HandoutJob,
    QualityJob,
    RenderJobStatus,
)

from .conftest import T0
from .fakes import FakeClock
from .insights_requests import handout_request, quality_request

S = RenderJobStatus
Store = QualityJobStore | HandoutJobStore


def _quality(job_id: str, status: S, age: timedelta = timedelta()) -> StoredQualityJob:
    stamp = T0 - age
    stage = "analysing" if status is S.RUNNING else None
    job = QualityJob(quality_job_id=job_id, status=status, stage=stage, progress=0.4,
                     created_at=stamp, updated_at=stamp)  # fmt: skip
    return StoredQualityJob(job=job, request=quality_request())


def _handout(job_id: str, status: S, age: timedelta = timedelta()) -> StoredHandoutJob:
    stamp = T0 - age
    stage = "layout" if status is S.RUNNING else None
    job = HandoutJob(handout_job_id=job_id, status=status, stage=stage, progress=0.4,
                     created_at=stamp, updated_at=stamp)  # fmt: skip
    return StoredHandoutJob(job=job, request=handout_request())


def _stores(tmp_path: Path) -> tuple[QualityJobStore, HandoutJobStore]:
    paths = DataPaths(tmp_path)
    return QualityJobStore(paths.quality_jobs), HandoutJobStore(paths.handout_jobs)


def _reloaded(tmp_path: Path, *records: StoredQualityJob | StoredHandoutJob
              ) -> tuple[QualityJobStore, HandoutJobStore]:  # fmt: skip
    """Save ``records`` with one output file each and return freshly loaded stores."""
    quality, handouts = _stores(tmp_path)
    for record in records:
        store: Store = quality if isinstance(record, StoredQualityJob) else handouts
        store.save(record)  # type: ignore[arg-type]
        folder = store.output_dir(_id(record))
        folder.mkdir(parents=True, exist_ok=True)
        (folder / "handout.pdf").write_bytes(b"%PDF")
    fresh = _stores(tmp_path)
    for store in fresh:
        store.load_all()
    return fresh


def _id(record: StoredQualityJob | StoredHandoutJob) -> str:
    if isinstance(record, StoredQualityJob):
        return record.job.quality_job_id
    return record.job.handout_job_id


def test_records_persist_in_their_own_folders_with_wire_names(tmp_path: Path) -> None:
    quality, handouts = _reloaded(tmp_path, _quality("1" * 32, S.QUEUED),
                                  _handout("2" * 32, S.COMPLETED))  # fmt: skip

    stored = json.loads((tmp_path / "quality-jobs" / f"{'1' * 32}.json").read_text())
    assert stored["job"]["qualityJobId"] == "1" * 32
    assert stored["request"]["titles"]["title"] == "Example Song"
    assert (tmp_path / "handout-jobs" / f"{'2' * 32}.json").is_file()
    assert quality.get("1" * 32) == _quality("1" * 32, S.QUEUED)
    assert handouts.get("2" * 32) == _handout("2" * 32, S.COMPLETED)


def test_the_handout_pdf_lives_in_the_job_folder(tmp_path: Path) -> None:
    _, handouts = _stores(tmp_path)

    assert handouts.pdf_path("2" * 32) == tmp_path / "handout-jobs" / ("2" * 32) / "handout.pdf"


@pytest.mark.parametrize("status", [S.QUEUED, S.RUNNING])
def test_restart_fails_unfinished_jobs_and_clears_the_stage(tmp_path: Path, status: S) -> None:
    stores = _reloaded(tmp_path, _quality("1" * 32, status), _handout("2" * 32, status))

    for store, job_id in zip(stores, ("1" * 32, "2" * 32), strict=True):
        assert store.reconcile(FakeClock()) == [job_id]
        record = store.get(job_id)
        assert record is not None
        job = record.job
        assert (job.status, job.error, job.stage) == (S.FAILED, RESTART_INTERRUPTED_TEXT, None)
        assert job.updated_at > T0
        assert not store.output_dir(job_id).exists()


def test_restart_leaves_finished_jobs_alone(tmp_path: Path) -> None:
    quality, handouts = _reloaded(tmp_path, _quality("1" * 32, S.COMPLETED),
                                  _handout("2" * 32, S.FAILED))  # fmt: skip

    assert quality.reconcile(FakeClock()) == []
    assert handouts.reconcile(FakeClock()) == []
    assert quality.output_dir("1" * 32).is_dir()


def test_delete_is_idempotent_and_removes_the_files(tmp_path: Path) -> None:
    _, handouts = _reloaded(tmp_path, _handout("2" * 32, S.COMPLETED))

    handouts.remove("2" * 32)
    handouts.remove("2" * 32)
    handouts.remove("../escape")

    assert handouts.get("2" * 32) is None
    assert not (tmp_path / "handout-jobs" / f"{'2' * 32}.json").exists()
    assert not handouts.output_dir("2" * 32).exists()


def test_sweep_removes_only_terminal_jobs_older_than_24_hours(tmp_path: Path) -> None:
    old, fresh = timedelta(hours=25), timedelta(hours=23)
    quality, handouts = _reloaded(
        tmp_path, _quality("1" * 32, S.COMPLETED, old), _quality("2" * 32, S.RUNNING, old),
        _handout("3" * 32, S.CANCELLED, old), _handout("4" * 32, S.COMPLETED, fresh),
    )  # fmt: skip

    assert quality.sweep(T0) == ["1" * 32]
    assert handouts.sweep(T0) == ["3" * 32]
    assert quality.get("2" * 32) is not None
    assert handouts.get("4" * 32) is not None
