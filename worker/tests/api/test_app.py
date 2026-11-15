"""Tests of the composition root: lifespan, reconciliation and default adapters (T067)."""

from datetime import UTC, datetime
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import BeatAnalyzer, IngestAdapters
from stemstudio_worker.schemas import Job, JobState, SourceType

from .contract import assert_conforms
from .fakes import ApiFakes

JOB_ID = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c"


def _settings(tmp_path: Path) -> WorkerSettings:
    return WorkerSettings(data_dir=tmp_path, separator="fake")


def _interrupted_job(tmp_path: Path) -> None:
    now = datetime(2026, 11, 14, 12, 0, tzinfo=UTC)
    job = Job(
        job_id=JOB_ID, source_key="youtube:dQw4w9WgXcQ", source_type=SourceType.YOUTUBE,
        status=JobState.SEPARATING, stage="Separating stems", progress=47,
        created_at=now, updated_at=now,
    )  # fmt: skip
    JobStore(tmp_path / "jobs").save(StoredJob(job=job))


def test_start_up_fails_jobs_interrupted_by_restart(tmp_path: Path) -> None:
    _interrupted_job(tmp_path)
    app = main.create_app(_settings(tmp_path), adapters=ApiFakes().adapters())

    with TestClient(app) as client:
        response = client.get(f"/v1/jobs/{JOB_ID}")

    assert_conforms(response, "/v1/jobs/{jobId}")
    body = response.json()
    assert (body["status"], body["error"], body["progress"]) == (
        "failed",
        "interrupted by restart",
        47,
    )


def test_default_adapters_come_from_the_factories(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    fakes = ApiFakes()
    seen: list[WorkerSettings] = []

    def ingest(settings: WorkerSettings) -> IngestAdapters:
        seen.append(settings)
        return fakes.adapters().ingest

    def analyzer(settings: WorkerSettings) -> BeatAnalyzer:
        seen.append(settings)
        return fakes

    monkeypatch.setattr(main, "create_ingest_adapters", ingest)
    monkeypatch.setattr(main, "create_beat_analyzer", analyzer)
    settings = _settings(tmp_path)

    with TestClient(main.create_app(settings)) as client:
        job_id = client.post("/v1/jobs", files={"file": ("a.wav", b"RIFF", "audio/wav")}).json()
        client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]
        result = client.get(f"/v1/jobs/{job_id['jobId']}/result").json()

    assert seen == [settings, settings]
    assert result["model"] == "fake"


def test_shutdown_stops_the_worker_thread(tmp_path: Path) -> None:
    app = main.create_app(_settings(tmp_path), adapters=ApiFakes().adapters())

    with TestClient(app):
        runtime = app.state.runtime
        assert runtime.scheduler.running

    assert not runtime.scheduler.running
