"""Tests of the lyrics job routes (006 T029, FR-001, FR-010; worker contract 1.5.0).

The lookup, aligner and vocal loader are fakes; the store, the network lane, the scheduler and the
device runner (fake stack: CPU) are real. Every response is validated against the contract.
"""

from typing import Any

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import estimate_alignment_seconds

from .conftest import Api, ApiFactory
from .contract import Response, assert_conforms
from .lyrics_fakes import WAIT, GatedAligner, QuietVocals, ScriptedLookup, aligned, example

JOBS = "/v1/lyrics-jobs"
ONE = f"{JOBS}/{{lyricsJobId}}"
RESULT = f"{ONE}/result"
CANCEL = f"{ONE}/cancel"
LOOKUP = {"mode": "lookup", "title": "Fake Song", "artist": "Fake Band", "durationSeconds": 180}
TEXT = "Fake line one\nLa la la"
UNKNOWN = "e" * 32


@pytest.fixture
def aligner() -> GatedAligner:
    return GatedAligner()


@pytest.fixture
def lookup() -> ScriptedLookup:
    return ScriptedLookup()


@pytest.fixture
def lyrics_api(make_api: ApiFactory, lookup: ScriptedLookup, aligner: GatedAligner) -> Api:
    overrides = AppOverrides(lyrics_lookup=lookup, aligner=aligner, vocal_loader=QuietVocals())
    return make_api(overrides=overrides)


def _wait(api: Api) -> None:
    runtime: Any = api.client.app.state.runtime  # type: ignore[attr-defined]
    assert runtime.lanes.metadata.join(WAIT)
    assert runtime.scheduler.join(WAIT)


def _post(api: Api, body: dict[str, object]) -> Response:
    response = api.client.post(JOBS, json=body)
    assert_conforms(response, JOBS, "post")
    return response


def _created(api: Api, body: dict[str, object]) -> str:
    response = _post(api, body)
    assert response.status_code == 202, response.json()
    job_id: str = response.json()["lyricsJobId"]
    assert response.headers["location"] == f"{JOBS}/{job_id}"
    return job_id


def _align(standard_job_id: str, **changes: object) -> dict[str, object]:
    return {"mode": "align", "standardJobId": standard_job_id, "text": TEXT, **changes}


def _problem(response: Response, status: int, problem_type: str) -> None:
    assert response.status_code == status, response.json()
    assert response.json()["type"] == problem_type


# ------------------------------------------------------------------ lookup


def test_a_lookup_is_accepted_runs_and_serves_its_result_without_caching(
    lyrics_api: Api, lookup: ScriptedLookup
) -> None:
    job_id = _created(lyrics_api, LOOKUP)
    _wait(lyrics_api)

    job = lyrics_api.client.get(f"{JOBS}/{job_id}")
    result = lyrics_api.client.get(f"{JOBS}/{job_id}/result")

    assert_conforms(job, ONE)
    assert (job.json()["status"], job.json()["progress"]) == ("completed", 1)
    assert_conforms(result, RESULT)
    assert result.headers["cache-control"] == "no-store"
    assert result.json() == example("WordSyncedLyricsResult").model_dump(mode="json", by_alias=True)
    assert [q.title for q in lookup.queries] == ["Fake Song"]


@pytest.mark.parametrize(
    "body",
    [
        {"mode": "lookup", "durationSeconds": 180},
        {"mode": "lookup", "title": "Fake Song", "durationSeconds": 0},
        {"mode": "sing", "title": "Fake Song"},
        {"mode": "align", "standardJobId": "b" * 32, "text": "bad\x07text"},
        {"mode": "align", "standardJobId": "b" * 32, "text": TEXT, "language": "English"},
    ],
)
def test_invalid_bodies_answer_422(lyrics_api: Api, body: dict[str, object]) -> None:
    response = _post(lyrics_api, body)

    assert response.status_code == 422
    assert response.json()["title"] == "Validation failed"


def test_a_lookup_cannot_be_cancelled(lyrics_api: Api) -> None:
    job_id = _created(lyrics_api, LOOKUP)
    _wait(lyrics_api)

    response = lyrics_api.client.post(f"{JOBS}/{job_id}/cancel")

    assert_conforms(response, CANCEL, "post")
    _problem(response, 409, "lyrics-job-not-cancellable")


def test_delete_removes_a_job_and_its_result(lyrics_api: Api) -> None:
    job_id = _created(lyrics_api, LOOKUP)
    _wait(lyrics_api)

    deleted = lyrics_api.client.delete(f"{JOBS}/{job_id}")
    again = lyrics_api.client.delete(f"{JOBS}/{job_id}")

    assert deleted.status_code == 204
    assert_conforms(again, ONE, "delete")
    assert again.status_code == 404
    assert lyrics_api.client.get(f"{JOBS}/{job_id}/result").status_code == 404


@pytest.mark.parametrize(
    ("method", "path", "template"),
    [("get", "", ONE), ("get", "/result", RESULT), ("post", "/cancel", CANCEL)],
)
def test_unknown_jobs_answer_404(lyrics_api: Api, method: str, path: str, template: str) -> None:
    response = lyrics_api.client.request(method, f"{JOBS}/{UNKNOWN}{path}")

    assert_conforms(response, template, method)
    assert response.status_code == 404


# ------------------------------------------------------------------ align


def test_an_alignment_of_a_completed_job_returns_the_aligned_document(
    lyrics_api: Api, aligner: GatedAligner
) -> None:
    standard = lyrics_api.completed_job()

    job_id = _created(lyrics_api, _align(standard, language="en"))
    _wait(lyrics_api)

    job = lyrics_api.client.get(f"{JOBS}/{job_id}").json()
    result = lyrics_api.client.get(f"{JOBS}/{job_id}/result")
    assert (job["mode"], job["status"], job["device"]["kind"]) == ("align", "completed", "cpu")
    assert job["estimatedSeconds"] > 0
    assert_conforms(result, RESULT)
    assert result.json()["lyrics"] == aligned().model_dump(mode="json", by_alias=True)
    assert aligner.devices == ["cpu"]


def test_the_result_of_a_running_alignment_answers_409(
    lyrics_api: Api, aligner: GatedAligner
) -> None:
    aligner.gate.clear()
    job_id = _created(lyrics_api, _align(lyrics_api.completed_job()))
    assert aligner.entered.wait(WAIT)

    response = lyrics_api.client.get(f"{JOBS}/{job_id}/result")
    running = lyrics_api.client.get(f"{JOBS}/{job_id}")
    aligner.gate.set()
    _wait(lyrics_api)

    assert_conforms(response, RESULT)
    _problem(response, 409, "lyrics-not-completed")
    assert_conforms(running, ONE)
    assert running.json()["status"] == "running"


def test_a_queued_alignment_is_cancelled(lyrics_api: Api, aligner: GatedAligner) -> None:
    standard = lyrics_api.completed_job()
    aligner.gate.clear()
    _created(lyrics_api, _align(standard))
    assert aligner.entered.wait(WAIT)
    queued = _created(lyrics_api, _align(standard))

    response = lyrics_api.client.post(f"{JOBS}/{queued}/cancel")
    aligner.gate.set()
    _wait(lyrics_api)

    assert_conforms(response, CANCEL, "post")
    assert (response.status_code, response.json()["status"]) == (200, "cancelled")
    assert len(aligner.devices) == 1


def test_an_alignment_needs_a_known_standard_job(lyrics_api: Api) -> None:
    response = _post(lyrics_api, _align(UNKNOWN))

    assert response.status_code == 404


def test_an_alignment_needs_a_completed_standard_job(lyrics_api: Api) -> None:
    lyrics_api.fakes.gate.clear()
    standard = lyrics_api.post_url().json()["jobId"]

    response = _post(lyrics_api, _align(standard))
    lyrics_api.fakes.gate.set()
    lyrics_api.wait()

    _problem(response, 409, "standard-job-not-completed")


def test_an_alignment_needs_the_vocal_stem_in_the_cache(lyrics_api: Api) -> None:
    standard = lyrics_api.completed_job()
    record = lyrics_api.client.app.state.runtime.store.get(standard)  # type: ignore[attr-defined]
    lyrics_api.client.app.state.runtime.paths.stem_file(record.audio_sha256, "vocals").unlink()  # type: ignore[attr-defined]

    response = _post(lyrics_api, _align(standard))

    _problem(response, 409, "vocal-stem-missing")


# ------------------------------------------------------------------ composition


def test_the_default_app_runs_lookups_with_its_own_provider(api: Api) -> None:
    job_id = _created(api, LOOKUP)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()
    result = api.client.get(f"{JOBS}/{job_id}/result")

    assert job["status"] == "completed"
    assert_conforms(result, RESULT)


# ------------------------------------------------------------------ estimate and health


def test_an_alignment_estimate_follows_the_device_formula(lyrics_api: Api) -> None:
    standard = lyrics_api.completed_job()
    duration = lyrics_api.client.get(f"/v1/jobs/{standard}/result").json()["durationSeconds"]

    job = _post(lyrics_api, _align(standard)).json()
    _wait(lyrics_api)

    assert job["estimatedSeconds"] == estimate_alignment_seconds(duration, "cpu", model_cached=True)


def test_health_reports_the_lyrics_alignment(lyrics_api: Api) -> None:
    response = lyrics_api.client.get("/health")

    assert_conforms(response, "/health")
    assert response.json()["lyricsAlignment"] == {
        "available": True, "model": "large-v3-turbo", "modelCached": True, "auto": "gpu",
        "reason": None,
    }  # fmt: skip
