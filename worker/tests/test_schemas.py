"""Contract tests for the worker pydantic models (camelCase JSON, bounds)."""

from datetime import UTC, datetime

import pytest
from pydantic import ValidationError

from stemstudio_worker.schemas import (
    BeatGrid,
    CreateJobRequest,
    Job,
    JobResult,
    JobState,
    ProblemDetails,
    SourceMetadata,
    SourceType,
    StemInfo,
)

NOW = datetime(2026, 11, 14, 12, 0, tzinfo=UTC)
JOB_ID = "0" * 32


def _grid() -> BeatGrid:
    return BeatGrid(
        bpm=120.0, beats=[0.5, 1.0, 1.5], downbeats=[0.5], confidence=0.8, reliable=True
    )


def test_job_serialises_camel_case() -> None:
    job = Job(
        job_id=JOB_ID,
        source_key="youtube:dQw4w9WgXcQ",
        source_type=SourceType.YOUTUBE,
        status=JobState.SEPARATING,
        stage="Separating stems",
        progress=42,
        created_at=NOW,
        updated_at=NOW,
    )

    data = job.model_dump(mode="json", by_alias=True)

    assert data["jobId"] == JOB_ID
    assert data["sourceType"] == "youtube"
    assert data["status"] == "separating"
    assert data["createdAt"] == "2026-11-14T12:00:00Z"


def test_create_job_request_accepts_camel_case_and_rejects_upload() -> None:
    request = CreateJobRequest.model_validate(
        {"sourceType": "spotify", "url": "https://open.spotify.com/track/x"}
    )
    assert request.source_type is SourceType.SPOTIFY

    with pytest.raises(ValidationError):
        CreateJobRequest.model_validate({"sourceType": "upload", "url": "x"})


def test_result_round_trips_through_json() -> None:
    result = JobResult(
        job_id=JOB_ID,
        source_key="upload:" + "a" * 64,
        metadata=SourceMetadata(title="Song"),
        duration_seconds=180.5,
        audio_sha256="b" * 64,
        model="htdemucs_6s",
        stems=[StemInfo(name="vocals", path=f"/v1/jobs/{JOB_ID}/stems/vocals", size_bytes=10)],
        beat_grid=_grid(),
        completed_at=NOW,
    )

    copy = JobResult.model_validate_json(result.model_dump_json(by_alias=True))

    assert copy == result
    assert copy.sample_rate == 44100
    assert copy.beat_grid.beats_per_bar == 4


@pytest.mark.parametrize(
    ("field", "value"),
    [("progress", 101), ("job_id", "not-hex"), ("source_key", "vimeo:123")],
)
def test_job_rejects_out_of_contract_values(field: str, value: object) -> None:
    base: dict[str, object] = {
        "job_id": JOB_ID,
        "source_key": "upload:abc",
        "source_type": "upload",
        "status": "queued",
        "stage": "Queued",
        "progress": 0,
        "created_at": NOW,
        "updated_at": NOW,
    }
    with pytest.raises(ValidationError):
        Job.model_validate({**base, field: value})


def test_terminal_states() -> None:
    assert {s for s in JobState if s.is_terminal} == {JobState.COMPLETED, JobState.FAILED}


def test_problem_details_defaults_and_extensions() -> None:
    problem = ProblemDetails.model_validate({"title": "Not found", "status": 404, "jobId": JOB_ID})

    assert problem.type == "about:blank"
    assert problem.model_dump()["jobId"] == JOB_ID


@pytest.mark.parametrize(("field", "limit"), [("stage", 200), ("error", 1000)])
def test_job_text_limits(field: str, limit: int) -> None:
    base = {
        "jobId": JOB_ID,
        "sourceKey": "youtube:dQw4w9WgXcQ",
        "sourceType": "youtube",
        "status": "failed",
        "stage": "Failed",
        "progress": 5,
        "createdAt": NOW,
        "updatedAt": NOW,
    }
    Job.model_validate({**base, field: "x" * limit})
    with pytest.raises(ValidationError):
        Job.model_validate({**base, field: "x" * (limit + 1)})


def test_metadata_artist_limit() -> None:
    SourceMetadata(title="t", artist="a" * 500)
    with pytest.raises(ValidationError):
        SourceMetadata(title="t", artist="a" * 501)
