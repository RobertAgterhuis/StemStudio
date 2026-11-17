"""Tests of the storage routes of worker API 1.7.0 (T029): ``/v1/storage``, clean-up, rehydrate.

The app runs with fake adapters; the meter, janitor and rehydrator are the real ones over the
temporary data and library directories.
"""

import shutil
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from .conftest import Api, ApiFactory
from .contract import assert_conforms, spec

STORAGE, CLEANUP, REHYDRATE = "/v1/storage", "/v1/storage/cleanup", "/v1/jobs/rehydrate"
_SIX = ["vocals", "drums", "bass", "guitar", "piano", "other"]


@pytest.fixture
def api(make_api: ApiFactory) -> Api:
    return make_api()


def _example() -> dict[str, Any]:
    body = spec()["paths"][REHYDRATE]["post"]["requestBody"]["content"]["application/json"]
    return dict(body["example"])


def _library(api: Api) -> Path:
    library = api.settings.render.library_dir
    assert library is not None
    return library


def _stems(api: Api, track_id: str, stems: list[str]) -> None:
    """Short FLAC files for ``stems`` in the library of ``api``."""
    folder = _library(api) / "tracks" / track_id / "stems"
    folder.mkdir(parents=True, exist_ok=True)
    for index, stem in enumerate(stems):
        frames = np.full((2205, 2), index / 10, dtype=np.float32)
        sf.write(folder / f"{stem}.flac", frames, 44100, format="FLAC")


def _rehydrated(api: Api) -> str:
    body = _example()
    _stems(api, body["trackId"], body["stems"])
    job_id: str = api.client.post(REHYDRATE, json=body).json()["jobId"]
    return job_id


def _problem(response: Any, status: int) -> dict[str, Any]:
    assert response.status_code == status
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    return body


def test_storage_report_conforms_and_attributes_the_job(api: Api) -> None:
    job_id = api.completed_job()

    response = api.client.get(STORAGE)

    assert response.status_code == 200
    assert_conforms(response, STORAGE)
    (entry,) = response.json()["cacheEntries"]
    assert entry["jobIds"] == [job_id]
    assert [v["id"] for v in response.json()["volumes"]] == ["data", "models"]


def test_cleanup_of_every_entry_evicts_the_job(api: Api) -> None:
    job_id = api.completed_job()

    response = api.client.post(CLEANUP, json={"cacheEntries": ["*"], "scratch": True})

    assert response.status_code == 200
    assert_conforms(response, CLEANUP, "post")
    assert len(response.json()["removedEntries"]) == 1
    assert response.json()["freedBytes"] > 0
    assert api.client.get(f"/v1/jobs/{job_id}").status_code == 200
    assert api.client.get(f"/v1/jobs/{job_id}/stems/vocals").status_code == 404
    assert api.post_detail(job_id).status_code == 409
    assert api.client.get(STORAGE).json()["cacheEntries"] == []


def test_a_new_job_for_an_evicted_source_is_separated_again(api: Api) -> None:
    first = api.completed_job()
    api.client.post(CLEANUP, json={"cacheEntries": ["*"]})

    second = api.post_url()

    assert second.status_code == 202
    assert second.json()["jobId"] != first


@pytest.mark.parametrize("body", [{"cacheEntries": ["*", "a" * 64]}, {"scratch": "maybe"}])
def test_cleanup_rejects_bad_requests(api: Api, body: dict[str, Any]) -> None:
    response = api.client.post(CLEANUP, json=body)

    _problem(response, 400)
    assert_conforms(response, CLEANUP, "post")


def test_rehydrate_creates_then_returns_the_job(api: Api) -> None:
    body = _example()
    _stems(api, body["trackId"], body["stems"])

    created = api.client.post(REHYDRATE, json=body)
    again = api.client.post(REHYDRATE, json=body)

    assert created.status_code == 201
    assert_conforms(created, REHYDRATE, "post")
    assert created.headers["location"] == f"/v1/jobs/{created.json()['jobId']}"
    assert again.status_code == 200
    assert_conforms(again, REHYDRATE, "post")
    assert again.json() == created.json()


def test_rehydrated_jobs_hide_result_stems_and_artwork(api: Api) -> None:
    job_id = _rehydrated(api)

    status = api.client.get(f"/v1/jobs/{job_id}")

    assert status.status_code == 200
    assert status.json()["status"] == "completed"
    for tail in ("result", "stems/vocals", "artwork/cover"):
        assert _problem(api.client.get(f"/v1/jobs/{job_id}/{tail}"), 404)["title"] == "Not found"


def test_a_rehydrated_job_starts_a_detail_job(api: Api) -> None:
    response = api.post_detail(_rehydrated(api))

    assert response.status_code == 202


def test_a_rehydrated_source_is_not_reused_by_a_new_job(api: Api) -> None:
    rehydrated = _rehydrated(api)

    response = api.post_url()

    assert response.status_code == 202
    assert response.json()["jobId"] != rehydrated


def test_rehydrate_needs_the_models_stem_set(api: Api) -> None:
    response = api.client.post(REHYDRATE, json={**_example(), "model": "htdemucs"})

    problem = _problem(response, 422)
    assert_conforms(response, REHYDRATE, "post")
    assert "stems" in problem["errors"]


def test_rehydrate_validates_the_body(api: Api) -> None:
    response = api.client.post(REHYDRATE, json={**_example(), "trackId": "../../etc"})

    problem = _problem(response, 422)
    assert_conforms(response, REHYDRATE, "post")
    assert "trackId" in problem["errors"]


def test_rehydrate_rejects_malformed_json(api: Api) -> None:
    response = api.client.post(
        REHYDRATE, content=b"{not json", headers={"content-type": "application/json"}
    )

    _problem(response, 400)
    assert_conforms(response, REHYDRATE, "post")


def test_rehydrate_reports_the_missing_stem(api: Api) -> None:
    body = _example()
    _stems(api, body["trackId"], [s for s in body["stems"] if s != "piano"])

    response = api.client.post(REHYDRATE, json=body)

    problem = _problem(response, 404)
    assert_conforms(response, REHYDRATE, "post")
    assert problem["type"] == "https://stemstudio.local/problems/stem-missing"
    assert problem["stem"] == "piano"
    assert problem["detail"] == f"tracks/{body['trackId']}/stems/piano.flac does not exist."


def test_rehydrate_without_the_library_mount(api: Api) -> None:
    shutil.rmtree(_library(api))

    response = api.client.post(REHYDRATE, json=_example())

    problem = _problem(response, 409)
    assert_conforms(response, REHYDRATE, "post")
    assert problem["type"] == "https://stemstudio.local/problems/library-unavailable"


def test_rehydrate_rejects_an_unreadable_stem(api: Api) -> None:
    body = _example()
    _stems(api, body["trackId"], body["stems"])
    (_library(api) / "tracks" / body["trackId"] / "stems" / "bass.flac").write_bytes(b"x")

    response = api.client.post(REHYDRATE, json=body)

    problem = _problem(response, 422)
    assert "bass" in problem["detail"]
