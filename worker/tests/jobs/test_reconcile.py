"""Tests of start-up reconciliation (FR-009a, T065)."""

from pathlib import Path

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.reconcile import reconcile
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import JobState

from .conftest import RecordFactory
from .fakes import FakeClock

S = JobState
NON_TERMINAL = [S.QUEUED, S.DOWNLOADING, S.SEPARATING, S.ANALYSING]


def _saved(paths: DataPaths, make_record: RecordFactory, status: JobState, job_id: str) -> None:
    record = make_record(job_id=job_id, status=status)
    job = record.job.model_copy(update={"progress": 42 if not status.is_terminal else 0})
    JobStore(paths.jobs).save(record.model_copy(update={"job": job}))


@pytest.mark.parametrize("status", NON_TERMINAL)
def test_non_terminal_jobs_become_failed_interrupted_by_restart(
    tmp_path: Path, make_record: RecordFactory, status: JobState
) -> None:
    paths = DataPaths(tmp_path)
    _saved(paths, make_record, status, "1" * 32)
    store = JobStore(paths.jobs)
    store.load_all()

    failed = reconcile(store, paths, FakeClock())

    assert failed == ["1" * 32]
    job = store.get("1" * 32)
    assert job is not None
    assert (job.job.status, job.job.stage, job.job.error, job.job.progress) == (
        S.FAILED,
        "Failed",
        "interrupted by restart",
        42,
    )
    reloaded = JobStore(paths.jobs)
    reloaded.load_all()
    assert reloaded.get("1" * 32) == job


@pytest.mark.parametrize("status", [S.COMPLETED, S.FAILED])
def test_terminal_jobs_are_left_alone(
    tmp_path: Path, make_record: RecordFactory, status: JobState
) -> None:
    paths = DataPaths(tmp_path)
    _saved(paths, make_record, status, "2" * 32)
    store = JobStore(paths.jobs)
    store.load_all()
    before = store.get("2" * 32)

    assert reconcile(store, paths, FakeClock()) == []
    assert store.get("2" * 32) == before


def test_leftover_work_upload_and_partial_cache_files_are_removed(
    tmp_path: Path, make_record: RecordFactory
) -> None:
    paths = DataPaths(tmp_path)
    _saved(paths, make_record, S.SEPARATING, "3" * 32)
    for directory in (paths.work("3" * 32), paths.uploads("3" * 32)):
        directory.mkdir(parents=True)
        (directory / "file").write_bytes(b"x")
    partial = paths.by_audio("e" * 64).with_name("e" * 64 + ".partial")
    partial.mkdir(parents=True)
    store = JobStore(paths.jobs)
    store.load_all()

    reconcile(store, paths, FakeClock())

    assert not paths.work("3" * 32).exists()
    assert not paths.uploads("3" * 32).exists()
    assert not partial.exists()


def test_reconcile_on_empty_data_dir(tmp_path: Path) -> None:
    paths = DataPaths(tmp_path / "fresh")
    store = JobStore(paths.jobs)
    store.load_all()

    assert reconcile(store, paths, FakeClock()) == []
