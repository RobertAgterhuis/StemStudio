"""Tests of the render job store: persistence, restart, delete and sweep (007 T023, data-model 2.5)."""

import json
from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.render_store import RenderJobStore, StoredRenderJob
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    RenderInput,
    RenderJob,
    RenderJobRequest,
    RenderJobStatus,
    RenderOutput,
)

from .conftest import T0
from .fakes import FakeClock

S = RenderJobStatus
REQUEST = RenderJobRequest(
    inputs=[RenderInput(path="tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/drums.flac",
                        gain=1, pan=0)],
    master_gain=1, output=RenderOutput(format="flac", bit_depth=16, dither=False),
)  # fmt: skip


def _record(job_id: str, status: RenderJobStatus, age: timedelta = timedelta()) -> StoredRenderJob:
    stamp = T0 - age
    job = RenderJob(render_job_id=job_id, status=status, progress=0.3,
                    created_at=stamp, updated_at=stamp)  # fmt: skip
    return StoredRenderJob(job=job, request=REQUEST)


def _store(tmp_path: Path, *records: StoredRenderJob) -> tuple[RenderJobStore, DataPaths]:
    paths = DataPaths(tmp_path)
    store = RenderJobStore(paths)
    for record in records:
        store.save(record)
        store.output_dir(record.job.render_job_id).mkdir(parents=True, exist_ok=True)
        (store.output_dir(record.job.render_job_id) / "output.flac").write_bytes(b"fLaC")
    reloaded = RenderJobStore(paths)
    reloaded.load_all()
    return reloaded, paths


def test_jobs_are_persisted_as_render_jobs_json(tmp_path: Path) -> None:
    store, paths = _store(tmp_path, _record("1" * 32, S.QUEUED))

    stored = json.loads((paths.render_jobs / f"{'1' * 32}.json").read_text())
    assert stored["job"]["renderJobId"] == "1" * 32
    assert stored["request"]["output"]["format"] == "flac"
    assert store.get("1" * 32) == _record("1" * 32, S.QUEUED)
    assert store.output_dir("1" * 32) == paths.render_jobs / ("1" * 32)


@pytest.mark.parametrize("status", [S.QUEUED, S.RUNNING])
def test_restart_fails_unfinished_jobs_and_removes_their_output(
    tmp_path: Path, status: RenderJobStatus
) -> None:
    store, _ = _store(tmp_path, _record("2" * 32, status))

    assert store.reconcile(FakeClock()) == ["2" * 32]

    record = store.get("2" * 32)
    assert record is not None
    assert (record.job.status, record.job.error) == (S.FAILED, RESTART_INTERRUPTED_TEXT)
    assert record.job.progress == 0.3
    assert record.job.updated_at > T0
    assert not store.output_dir("2" * 32).exists()


@pytest.mark.parametrize("status", [S.COMPLETED, S.FAILED, S.CANCELLED])
def test_restart_leaves_terminal_jobs_alone(tmp_path: Path, status: RenderJobStatus) -> None:
    store, _ = _store(tmp_path, _record("3" * 32, status))

    assert store.reconcile(FakeClock()) == []
    assert store.get("3" * 32) == _record("3" * 32, status)
    assert store.output_dir("3" * 32).is_dir()


def test_remove_deletes_json_and_output_and_is_idempotent(tmp_path: Path) -> None:
    store, paths = _store(tmp_path, _record("4" * 32, S.COMPLETED))

    store.remove("4" * 32)
    store.remove("4" * 32)
    store.remove("not-an-id")

    assert store.get("4" * 32) is None
    assert not (paths.render_jobs / f"{'4' * 32}.json").exists()
    assert not store.output_dir("4" * 32).exists()


def test_sweep_removes_terminal_jobs_older_than_24_hours(tmp_path: Path) -> None:
    store, _ = _store(
        tmp_path,
        _record("5" * 32, S.COMPLETED, age=timedelta(hours=25)),
        _record("6" * 32, S.FAILED, age=timedelta(hours=23)),
        _record("7" * 32, S.RUNNING, age=timedelta(days=3)),
    )

    assert store.sweep(T0) == ["5" * 32]

    assert store.get("5" * 32) is None
    assert not store.output_dir("5" * 32).exists()
    assert store.get("6" * 32) is not None
    assert store.get("7" * 32) is not None
