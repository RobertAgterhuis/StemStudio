"""JSON job store (009 T016; data-model 2.2)."""

import json
from datetime import timedelta
from pathlib import Path

from stemstudio_generator.jobs.clock import SystemClock
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import Clock, JobStore

from ..fakes import START
from .helpers import make_job


def test_saved_jobs_round_trip_as_camel_case_json(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    job = make_job()

    store.save(job)

    raw = json.loads((tmp_path / "jobs" / f"{job.generation_id}.json").read_text("utf-8"))
    assert raw["generationId"] == job.generation_id
    assert "song" not in raw
    assert store.get(job.generation_id) == job
    assert isinstance(store, JobStore)


def test_unknown_jobs_are_none(tmp_path: Path) -> None:
    assert JsonJobStore(tmp_path).get("f" * 32) is None


def test_saving_replaces_the_file_atomically(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    job = make_job()
    store.save(job)

    store.save(job.model_copy(update={"stage": "Loading model"}))

    stored = store.get(job.generation_id)
    assert stored is not None
    assert stored.stage == "Loading model"
    assert sorted(path.name for path in (tmp_path / "jobs").iterdir()) == [
        f"{job.generation_id}.json"
    ]


def test_list_all_is_oldest_first_and_survives_a_restart(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    newer = make_job(2, START + timedelta(minutes=5))
    older = make_job(1, START)
    store.save(newer)
    store.save(older)

    assert [job.generation_id for job in JsonJobStore(tmp_path).list_all()] == [
        older.generation_id,
        newer.generation_id,
    ]


def test_list_all_skips_unreadable_files(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    store.save(make_job())
    (tmp_path / "jobs" / "broken.json").write_text("{not json", "utf-8")

    assert len(store.list_all()) == 1


def test_an_empty_store_lists_nothing(tmp_path: Path) -> None:
    assert JsonJobStore(tmp_path / "missing").list_all() == []


def test_delete_removes_the_file_and_the_job_directory(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    job = make_job()
    store.save(job)
    work = store.job_dir(job.generation_id)
    (work / "take-1.flac").write_bytes(b"flac")

    store.delete(job.generation_id)
    store.delete(job.generation_id)

    assert store.get(job.generation_id) is None
    assert not work.exists()


def test_job_dir_is_created_under_the_jobs_directory(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)

    work = store.job_dir("0" * 32)

    assert work == tmp_path / "jobs" / ("0" * 32)
    assert work.is_dir()


def test_system_clock_is_utc_and_sleeps() -> None:
    clock = SystemClock()
    before = clock.now()
    clock.sleep(0.0)

    assert isinstance(clock, Clock)
    assert before.utcoffset() == timedelta(0)
    assert clock.now() >= before
