"""Tests of the section job routes (014 T052, worker-practice delta, FR-001)."""

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import StructureError

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .practice_fakes import (
    TRACK,
    WAIT,
    FakeAnalyzer,
    section_body,
    stem_path,
    write_library,
)

JOBS = "/v1/section-jobs"
ONE = f"{JOBS}/{{jobId}}"


@pytest.fixture
def analyzer() -> FakeAnalyzer:
    return FakeAnalyzer()


@pytest.fixture
def api(make_api: ApiFactory, analyzer: FakeAnalyzer) -> Api:
    started = make_api(overrides=AppOverrides(structure_analyzer=analyzer))
    write_library(started.settings.render.library_dir)  # type: ignore[arg-type]
    return started


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.practice.lane.join(WAIT)  # type: ignore[attr-defined]


def _create(api: Api, **changes: object) -> str:
    response = api.client.post(JOBS, json=section_body(**changes))
    assert response.status_code == 202, response.text
    job_id: str = response.json()["jobId"]
    return job_id


def test_create_answers_202_with_location(api: Api, analyzer: FakeAnalyzer) -> None:
    analyzer.gate.clear()

    response = api.client.post(JOBS, json=section_body())

    assert response.status_code == 202
    assert_conforms(response, JOBS, "post")
    job = response.json()
    assert response.headers["location"] == f"{JOBS}/{job['jobId']}"
    assert job["status"] in ("queued", "running")
    assert "updatedAt" not in job
    analyzer.gate.set()
    _wait(api)


def test_a_completed_job_has_the_result_and_the_resolved_files(
    api: Api, analyzer: FakeAnalyzer
) -> None:
    job_id = _create(api)
    _wait(api)

    response = api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    job = response.json()
    assert (job["status"], job["progress"], job["error"]) == ("completed", 1, None)
    assert job["result"]["sections"][0]["endSeconds"] == 60
    files = analyzer.files[0]
    assert set(files) == {"vocals", "drums", "bass", "guitar", "piano", "other", "other.woodwinds"}
    assert all(path.is_file() for path in files.values())


def test_progress_is_reported_while_running(api: Api, analyzer: FakeAnalyzer) -> None:
    analyzer.gate.clear()
    job_id = _create(api)
    assert analyzer.entered.wait(WAIT)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["progress"]) == ("running", 0.5)
    analyzer.gate.set()
    _wait(api)


def test_an_analyser_error_fails_the_job_with_its_text(api: Api, analyzer: FakeAnalyzer) -> None:
    analyzer.error = StructureError("The stems could not be read.")
    job_id = _create(api)
    _wait(api)

    response = api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    assert (response.json()["status"], response.json()["error"]) == (
        "failed", "The stems could not be read."
    )  # fmt: skip


def test_an_unexpected_error_fails_with_the_generic_text(api: Api, analyzer: FakeAnalyzer) -> None:
    analyzer.error = ZeroDivisionError("boom")
    job_id = _create(api)
    _wait(api)

    assert api.client.get(f"{JOBS}/{job_id}").json()["error"] == "Unexpected error"


@pytest.mark.parametrize(
    "stems",
    [
        {"vocals": "assets/9a8b7c6d-1111-4222-8333-444455556666/score.musicxml"},
        {"vocals": stem_path("bass")},
        {"vocals": f"tracks/{TRACK}/stems/../stems/vocals.flac"},
        {"vocals": f"tracks/{TRACK}/detail/vocals/lead.flac"},
    ],
)
def test_a_path_that_is_not_the_stem_is_400(api: Api, stems: dict[str, str]) -> None:
    body = section_body()
    body["stems"] = {**body["stems"], **stems}

    response = api.client.post(JOBS, json=body)

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")
    assert response.json()["type"] == "invalid-input-path"


def test_a_detail_path_of_another_channel_is_400(api: Api) -> None:
    details = [{"channel": "guitar.electric-guitar",
                "path": f"tracks/{TRACK}/detail/other/woodwinds.flac"}]  # fmt: skip

    response = api.client.post(JOBS, json=section_body(details=details))

    assert response.status_code == 400


def test_a_missing_stem_is_404(api: Api) -> None:
    root = api.settings.render.library_dir
    assert root is not None
    (root / stem_path("piano")).unlink()

    response = api.client.post(JOBS, json=section_body())

    assert response.status_code == 404
    assert_conforms(response, JOBS, "post")
    assert response.json()["type"] == "input-not-found"


@pytest.mark.parametrize(
    "changes",
    [{"durationSeconds": 0}, {"maxSections": 65}, {"stems": {"vocals": stem_path("vocals")}},
     {"grid": {"bpm": 120}}, {"unknown": 1}],
)  # fmt: skip
def test_schema_errors_are_422(api: Api, changes: dict[str, object]) -> None:
    response = api.client.post(JOBS, json=section_body(**changes))

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


def test_unknown_jobs_are_404_and_delete_is_idempotent(api: Api) -> None:
    job_id = _create(api)
    _wait(api)

    assert api.client.delete(f"{JOBS}/{job_id}").status_code == 204
    assert api.client.delete(f"{JOBS}/{job_id}").status_code == 204
    response = api.client.get(f"{JOBS}/{job_id}")
    assert response.status_code == 404
    assert_conforms(response, ONE)
    assert api.client.delete(f"{JOBS}/not-a-job").status_code == 400


def test_deleting_a_running_job_cancels_it(api: Api, analyzer: FakeAnalyzer) -> None:
    analyzer.gate.clear()
    job_id = _create(api)
    assert analyzer.entered.wait(WAIT)

    assert api.client.delete(f"{JOBS}/{job_id}").status_code == 204
    analyzer.gate.set()
    _wait(api)

    assert api.client.get(f"{JOBS}/{job_id}").status_code == 404


def test_without_a_library_the_job_fails_when_it_runs(make_api: ApiFactory) -> None:
    api = make_api(overrides=AppOverrides(structure_analyzer=FakeAnalyzer()))
    root = api.settings.render.library_dir
    assert root is not None
    root.rmdir()

    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()
    assert job["status"] == "failed"
    assert "library" in job["error"].lower()
