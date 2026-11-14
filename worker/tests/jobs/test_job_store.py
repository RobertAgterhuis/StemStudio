"""Tests of the JSON-file job store (T035)."""

import json
import os
import re
from collections.abc import Callable
from pathlib import Path

import pytest

from stemstudio_worker.jobs.models import StoredJob, metadata_of, source_of
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import SourceInfo
from stemstudio_worker.schemas import JobState, SourceType

RecordFactory = Callable[..., StoredJob]
JOB_A = "a" * 32


def test_new_job_id_is_uuid4_hex() -> None:
    ids = {JobStore.new_job_id() for _ in range(50)}

    assert len(ids) == 50
    assert all(re.fullmatch(r"[0-9a-f]{32}", i) and i[12] == "4" for i in ids)


def test_save_writes_job_file_with_round_trip(tmp_path: Path, make_record: RecordFactory) -> None:
    store = JobStore(tmp_path)
    record = make_record().model_copy(update={"upload_file": "input.mp3", "audio_sha256": "b" * 64})

    store.save(record)

    raw = json.loads((tmp_path / f"{JOB_A}.json").read_text(encoding="utf-8"))
    assert raw["job"]["jobId"] == JOB_A
    assert raw["uploadFile"] == "input.mp3"
    reloaded = JobStore(tmp_path)
    reloaded.load_all()
    assert reloaded.get(JOB_A) == record


def test_save_replaces_atomically(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, make_record: RecordFactory
) -> None:
    calls: list[tuple[str, str]] = []
    real_replace = os.replace

    def spy(src: str | Path, dst: str | Path) -> None:
        calls.append((Path(src).name, Path(dst).name))
        real_replace(src, dst)

    monkeypatch.setattr(os, "replace", spy)
    store = JobStore(tmp_path)

    store.save(make_record())
    store.save(make_record(status=JobState.DOWNLOADING))

    assert calls == [(f"{JOB_A}.json.tmp", f"{JOB_A}.json")] * 2
    assert sorted(p.name for p in tmp_path.iterdir()) == [f"{JOB_A}.json"]
    saved = store.get(JOB_A)
    assert saved is not None
    assert saved.job.status is JobState.DOWNLOADING


def test_get_unknown_or_malformed_id_returns_none(tmp_path: Path) -> None:
    store = JobStore(tmp_path)

    assert store.get("f" * 32) is None
    assert store.get("../../etc/passwd") is None


def test_load_all_skips_corrupt_files(tmp_path: Path, make_record: RecordFactory) -> None:
    JobStore(tmp_path).save(make_record())
    (tmp_path / f"{'c' * 32}.json").write_text("{not json", encoding="utf-8")
    (tmp_path / "notes.txt").write_text("ignored", encoding="utf-8")

    store = JobStore(tmp_path)
    store.load_all()

    assert [r.job.job_id for r in store.all()] == [JOB_A]


def test_load_all_on_missing_directory_creates_it(tmp_path: Path) -> None:
    store = JobStore(tmp_path / "jobs")

    store.load_all()

    assert (tmp_path / "jobs").is_dir()
    assert store.all() == []


@pytest.mark.parametrize("status", [JobState.QUEUED, JobState.SEPARATING, JobState.COMPLETED])
def test_find_active_returns_non_failed_job_for_source_key(
    tmp_path: Path, status: JobState, make_record: RecordFactory
) -> None:
    store = JobStore(tmp_path)
    store.save(make_record(job_id="1" * 32, status=JobState.FAILED))
    store.save(make_record(job_id="2" * 32, status=status))
    store.save(make_record(job_id="3" * 32, source_key="spotify:4cOdK2wGLETKBW3PvgPWqT"))

    found = store.find_active("youtube:dQw4w9WgXcQ")

    assert found is not None
    assert found.job.job_id == "2" * 32


def test_find_active_ignores_failed_jobs(tmp_path: Path, make_record: RecordFactory) -> None:
    store = JobStore(tmp_path)
    store.save(make_record(status=JobState.FAILED))

    assert store.find_active("youtube:dQw4w9WgXcQ") is None


def test_metadata_of_clips_texts_and_round_trips_through_source_of(
    make_record: RecordFactory,
) -> None:
    source = SourceInfo(SourceType.YOUTUBE, "youtube:dQw4w9WgXcQ", "", "x" * 600, "https://y")
    metadata = metadata_of(source)
    job = make_record().job.model_copy(update={"metadata": metadata})

    assert metadata.title == "Untitled"
    assert metadata.artist == "x" * 500
    assert source_of(job) == SourceInfo(
        SourceType.YOUTUBE, "youtube:dQw4w9WgXcQ", "Untitled", "x" * 500, "https://y"
    )


def test_source_of_job_without_metadata(make_record: RecordFactory) -> None:
    job = make_record().job.model_copy(update={"metadata": None})

    assert source_of(job).title == "Untitled"
    assert metadata_of(source_of(job)).artist is None
