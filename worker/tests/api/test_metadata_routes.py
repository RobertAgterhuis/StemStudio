"""Tests of the metadata job routes, 422 problem details and ``Health.onlineLookups`` (005 T035)."""

import hashlib
import sys
import threading
import types
from collections.abc import Iterator
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import OnlineSettings, WorkerSettings
from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import (
    ArtworkImages,
    MetadataFindings,
    MetadataLookup,
    MetadataLookupError,
    MetadataQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import METADATA_FAILED_TEXT, MetadataResult

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .fakes import ApiFakes

WAIT = 5.0
COVER, THUMB = b"CAA cover jpeg", b"CAA thumb jpeg"
IMAGES = ArtworkImages(COVER, THUMB, hashlib.sha256(COVER).hexdigest())
FOUND = MetadataFindings(
    MetadataResult(outcome="found", artwork=IMAGES.info("coverartarchive")), IMAGES
)
BODY = {"title": "Hero of the Day", "artist": "Metallica", "durationSeconds": 262.3}
JOBS = "/v1/metadata-jobs"


class ScriptedLookup:
    """Returns ``findings`` or raises ``error``; ``gate`` holds lookups until it is set."""

    def __init__(self) -> None:
        self.findings = FOUND
        self.error: Exception | None = None
        self.gate = threading.Event()
        self.gate.set()
        self.queries: list[MetadataQuery] = []

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        self.queries.append(query)
        assert self.gate.wait(WAIT)
        progress(0.5, "Reading Wikipedia")
        if self.error is not None:
            raise self.error
        return self.findings


@pytest.fixture
def lookup() -> ScriptedLookup:
    return ScriptedLookup()


@pytest.fixture
def meta_api(make_api: ApiFactory, lookup: ScriptedLookup) -> Api:
    return make_api(overrides=AppOverrides(metadata_lookup=lookup))


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.metadata.join(WAIT)  # type: ignore[attr-defined]


def _created(api: Api, body: dict[str, object] | None = None) -> str:
    response = api.client.post(JOBS, json=body or BODY)
    assert response.status_code == 202, response.text
    job_id: str = response.json()["metadataJobId"]
    _wait(api)
    return job_id


# ------------------------------------------------------------------ create and read


def test_create_answers_202_with_location_and_a_queued_job(
    meta_api: Api, lookup: ScriptedLookup
) -> None:
    lookup.gate.clear()

    response = meta_api.client.post(JOBS, json=BODY)

    assert response.status_code == 202
    assert_conforms(response, JOBS, "post")
    job = response.json()
    assert response.headers["location"] == f"{JOBS}/{job['metadataJobId']}"
    assert (job["status"], job["stage"]) == ("queued", "Queued")
    lookup.gate.set()
    _wait(meta_api)


def test_the_job_completes_and_its_result_and_cover_are_served(
    meta_api: Api, lookup: ScriptedLookup
) -> None:
    job_id = _created(meta_api)

    job = meta_api.client.get(f"{JOBS}/{job_id}")
    result = meta_api.client.get(f"{JOBS}/{job_id}/result")
    cover = meta_api.client.get(f"{JOBS}/{job_id}/artwork/cover")

    assert_conforms(job, f"{JOBS}/{{metadataJobId}}")
    assert_conforms(result, f"{JOBS}/{{metadataJobId}}/result")
    assert_conforms(cover, f"{JOBS}/{{metadataJobId}}/artwork/{{variant}}")
    assert (job.json()["status"], result.json()["outcome"]) == ("completed", "found")
    assert (cover.content, cover.headers["etag"]) == (COVER, f'"{IMAGES.sha256}"')
    assert lookup.queries == [MetadataQuery("Hero of the Day", "Metallica", None, 262.3, None)]


def test_the_thumbnail_variant_is_served(meta_api: Api) -> None:
    job_id = _created(meta_api)

    response = meta_api.client.get(f"{JOBS}/{job_id}/artwork/thumb")

    assert (response.status_code, response.content) == (200, THUMB)


def test_a_failed_lookup_is_reported_with_the_fixed_text(
    meta_api: Api, lookup: ScriptedLookup
) -> None:
    lookup.error = MetadataLookupError("503")
    job_id = _created(meta_api)

    job = meta_api.client.get(f"{JOBS}/{job_id}")
    result = meta_api.client.get(f"{JOBS}/{job_id}/result")

    assert_conforms(job, f"{JOBS}/{{metadataJobId}}")
    assert (job.json()["status"], job.json()["error"]) == ("failed", METADATA_FAILED_TEXT)
    assert result.status_code == 409
    assert_conforms(result, f"{JOBS}/{{metadataJobId}}/result")


def test_the_result_of_an_unfinished_job_is_a_409(meta_api: Api, lookup: ScriptedLookup) -> None:
    lookup.gate.clear()
    job_id = meta_api.client.post(JOBS, json=BODY).json()["metadataJobId"]

    response = meta_api.client.get(f"{JOBS}/{job_id}/result")

    assert response.status_code == 409
    assert "running" in response.json()["detail"] or "queued" in response.json()["detail"]
    lookup.gate.set()
    _wait(meta_api)


@pytest.mark.parametrize("suffix", ["", "/result", "/artwork/cover"])
def test_unknown_metadata_jobs_answer_404(meta_api: Api, suffix: str) -> None:
    response = meta_api.client.get(f"{JOBS}/{'0' * 32}{suffix}")

    assert response.status_code == 404
    assert response.headers["content-type"] == "application/problem+json"


def test_artwork_is_404_for_a_result_without_cover_or_an_unknown_variant(
    meta_api: Api, lookup: ScriptedLookup
) -> None:
    job_id = _created(meta_api)
    lookup.findings = MetadataFindings(MetadataResult(outcome="not-found"))
    plain = _created(meta_api)

    assert meta_api.client.get(f"{JOBS}/{plain}/artwork/cover").status_code == 404
    assert meta_api.client.get(f"{JOBS}/{job_id}/artwork/poster").status_code == 404


# ------------------------------------------------------------------ 422 problem details


@pytest.mark.parametrize(
    ("body", "field"),
    [
        ({"title": ""}, "title"),
        ({"artist": "Only artist"}, "title"),
        ({"title": "T", "durationSeconds": 0}, "durationSeconds"),
        ({"title": "T", "isrc": "nope"}, "isrc"),
        ({"title": "T", "unknown": 1}, "unknown"),
    ],
)
def test_invalid_bodies_answer_422_problem_details_with_errors(
    meta_api: Api, body: dict[str, object], field: str
) -> None:
    response = meta_api.client.post(JOBS, json=body)

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")
    problem = response.json()
    assert (problem["title"], problem["status"]) == ("Validation failed", 422)
    assert problem["detail"] == "The request body is not valid."
    assert field in problem["errors"]
    assert all(isinstance(m, str) for m in problem["errors"][field])


def test_malformed_json_answers_422(meta_api: Api) -> None:
    response = meta_api.client.post(
        JOBS, content=b"{", headers={"content-type": "application/json"}
    )

    assert response.status_code == 422
    assert response.headers["content-type"] == "application/problem+json"
    assert response.json()["errors"]


def test_standard_job_routes_keep_their_400_for_bad_json(meta_api: Api) -> None:
    response = meta_api.client.post("/v1/jobs", json={"sourceType": "youtube"})

    assert response.status_code == 400


# ------------------------------------------------------------------ health and composition


def test_health_reports_online_lookups_on_by_default(meta_api: Api) -> None:
    assert meta_api.client.get("/health").json()["onlineLookups"] is True


def test_health_reports_online_lookups_off(tmp_path: Path) -> None:
    settings = WorkerSettings(
        data_dir=tmp_path, separator="fake", online=OnlineSettings(lookups_enabled=False)
    )
    app = main.create_app(settings, ApiFakes().adapters())

    with TestClient(app) as client:
        response = client.get("/health")

    assert_conforms(response, "/health")
    assert response.json()["onlineLookups"] is False


@pytest.fixture
def no_metadata_package(monkeypatch: pytest.MonkeyPatch) -> Iterator[None]:
    monkeypatch.setitem(sys.modules, main.METADATA_PACKAGE, None)
    yield


def _run_one(tmp_path: Path) -> dict[str, object]:
    app = main.create_app(
        WorkerSettings(data_dir=tmp_path, separator="fake"), ApiFakes().adapters()
    )
    with TestClient(app) as client:
        job_id = client.post(JOBS, json=BODY).json()["metadataJobId"]
        assert app.state.runtime.lanes.metadata.join(WAIT)
        result: dict[str, object] = client.get(f"{JOBS}/{job_id}/result").json()
    return result


@pytest.mark.usefixtures("no_metadata_package")
def test_without_the_metadata_package_lookups_complete_as_disabled(tmp_path: Path) -> None:
    assert _run_one(tmp_path)["outcome"] == "disabled"


def test_the_default_lookup_comes_from_the_metadata_factory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, lookup: ScriptedLookup
) -> None:
    seen: list[WorkerSettings] = []

    def factory(settings: WorkerSettings) -> MetadataLookup:
        seen.append(settings)
        return lookup

    module = types.ModuleType(main.METADATA_PACKAGE)
    module.create_metadata_lookup = factory  # type: ignore[attr-defined]
    monkeypatch.setitem(sys.modules, main.METADATA_PACKAGE, module)

    assert _run_one(tmp_path)["outcome"] == "found"
    assert len(seen) == 1


def test_shutdown_stops_the_metadata_lane_and_the_sweeper(
    tmp_path: Path, lookup: ScriptedLookup
) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake")
    app = main.create_app(
        settings, ApiFakes().adapters(), overrides=AppOverrides(metadata_lookup=lookup)
    )

    with TestClient(app):
        lanes = app.state.runtime.lanes
        assert lanes.sweeper.running

    assert not lanes.sweeper.running
