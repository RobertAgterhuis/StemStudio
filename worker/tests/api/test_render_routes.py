"""Tests of the render job routes and the 007 ``/health`` fields (007 T033, FR-001, FR-011).

The renderer is a fake ``RenderExecutor``; the store and the lane are real.
"""

from datetime import UTC, datetime
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import RenderSettings, WorkerSettings
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.render_store import RenderJobStore, StoredRenderJob
from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import InputNotFoundError, InputPathError, RenderError
from stemstudio_worker.schemas import (
    LIBRARY_UNAVAILABLE_TEXT,
    RenderJob,
    RenderJobRequest,
    RenderJobStatus,
)

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .fakes import ApiFakes
from .render_fakes import OUTPUT, STEM, WAIT, FakeExecutor, body

JOBS = "/v1/render-jobs"
ONE = f"{JOBS}/{{renderJobId}}"
FILE = f"{ONE}/file"


@pytest.fixture
def executor() -> FakeExecutor:
    return FakeExecutor()


@pytest.fixture
def render_api(make_api: ApiFactory, executor: FakeExecutor) -> Api:
    return make_api(overrides=AppOverrides(render_executor=executor))


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.render.join(WAIT)  # type: ignore[attr-defined]


def _create(api: Api, **changes: object) -> str:
    response = api.client.post(JOBS, json=body(**changes))
    assert response.status_code == 202, response.text
    job_id: str = response.json()["renderJobId"]
    return job_id


def _completed(api: Api) -> str:
    job_id = _create(api)
    _wait(api)
    return job_id


# ------------------------------------------------------------------ create, run, read


def test_create_answers_202_with_location_and_a_queued_job(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.gate.clear()

    response = render_api.client.post(JOBS, json=body())

    assert response.status_code == 202
    assert_conforms(response, JOBS, "post")
    job = response.json()
    assert response.headers["location"] == f"{JOBS}/{job['renderJobId']}"
    assert job["status"] in ("queued", "running")
    assert [r.inputs[0].path for r in executor.validated] == [STEM]
    executor.gate.set()
    _wait(render_api)


def test_a_completed_job_reports_its_result(render_api: Api, executor: FakeExecutor) -> None:
    job_id = _completed(render_api)

    response = render_api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    job = response.json()
    assert (job["status"], job["progress"], job["error"]) == ("completed", 1, None)
    assert job["result"]["sizeBytes"] == len(OUTPUT)
    assert executor.out_dirs[0].name == job_id


def test_progress_is_reported_while_running(render_api: Api, executor: FakeExecutor) -> None:
    executor.gate.clear()
    job_id = _create(render_api)
    assert executor.entered.wait(WAIT)

    response = render_api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    assert (response.json()["status"], response.json()["progress"]) == ("running", 0.25)
    executor.gate.set()
    _wait(render_api)


def test_a_render_runs_while_a_separation_is_busy(render_api: Api) -> None:
    render_api.fakes.gate.clear()
    render_api.post_url()
    try:
        job_id = _completed(render_api)
        status = render_api.client.get(f"{JOBS}/{job_id}").json()["status"]
    finally:
        render_api.fakes.gate.set()
        render_api.wait()

    assert status == "completed"


# ------------------------------------------------------------------ errors at create


def test_a_path_outside_the_allow_list_is_400_and_nothing_is_queued(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.validate_error = InputPathError("Input path is not allowed: tracks/../stemstudio.db")

    response = render_api.client.post(JOBS, json=body())

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")
    problem = response.json()
    assert (problem["type"], problem["title"]) == ("invalid-input-path", "Invalid input path")
    assert problem["detail"] == "Input path is not allowed: tracks/../stemstudio.db"
    assert executor.out_dirs == []


def test_a_missing_input_is_404_naming_the_path(render_api: Api, executor: FakeExecutor) -> None:
    executor.validate_error = InputNotFoundError(STEM)

    response = render_api.client.post(JOBS, json=body())

    assert response.status_code == 404
    assert_conforms(response, JOBS, "post")
    assert (response.json()["type"], response.json()["detail"]) == (
        "input-not-found",
        f"Input not found: {STEM}",
    )


@pytest.mark.parametrize(
    "changes",
    [
        {"inputs": []},
        {"masterGain": 2},
        {"range": {"startSeconds": 10, "endSeconds": 10.2}},
        {"output": {"format": "ogg"}},
        {"inputs": [{"path": STEM, "gain": 1.5, "pan": 0}]},
    ],
)
def test_schema_violations_are_422_problems(render_api: Api, changes: dict[str, object]) -> None:
    response = render_api.client.post(JOBS, json=body(**changes))

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")
    assert response.json()["errors"]


def test_an_unavailable_library_fails_the_job_with_its_reason(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.validate_error = RenderError(LIBRARY_UNAVAILABLE_TEXT)
    executor.render_error = RenderError(LIBRARY_UNAVAILABLE_TEXT)
    job_id = _completed(render_api)

    job = render_api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"]) == ("failed", LIBRARY_UNAVAILABLE_TEXT)


# ------------------------------------------------------------------ errors while rendering


def test_a_render_error_fails_the_job_and_removes_partial_output(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.render_error = RenderError("Nothing to export: the selected channels are silent")
    job_id = _completed(render_api)

    response = render_api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    assert response.json()["error"] == "Nothing to export: the selected channels are silent"
    assert not executor.out_dirs[0].exists()


def test_an_unexpected_error_fails_the_job_with_a_generic_text(
    render_api: Api, executor: FakeExecutor, caplog: pytest.LogCaptureFixture
) -> None:
    executor.render_error = MemoryError("out of memory")
    job_id = _completed(render_api)

    job = render_api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"]) == ("failed", "Unexpected error")
    assert "Render job" in caplog.text


# ------------------------------------------------------------------ file download


def test_the_file_is_409_while_the_job_runs(render_api: Api, executor: FakeExecutor) -> None:
    executor.gate.clear()
    job_id = _create(render_api)
    assert executor.entered.wait(WAIT)

    response = render_api.client.get(f"{JOBS}/{job_id}/file")
    executor.gate.set()
    _wait(render_api)

    assert (response.status_code, response.json()["type"]) == (409, "render-not-completed")
    assert_conforms(response, FILE)


def test_the_file_of_a_completed_job_is_served_with_its_length(render_api: Api) -> None:
    job_id = _completed(render_api)

    response = render_api.client.get(f"{JOBS}/{job_id}/file")

    assert_conforms(response, FILE)
    assert (response.status_code, response.content) == (200, OUTPUT)
    assert response.headers["content-type"] == "audio/flac"
    assert response.headers["content-length"] == str(len(OUTPUT))


def test_a_file_that_vanished_is_404(render_api: Api) -> None:
    job_id = _completed(render_api)
    runtime = render_api.client.app.state.runtime  # type: ignore[attr-defined]
    (runtime.paths.render_jobs / job_id / "output.flac").unlink()

    assert render_api.client.get(f"{JOBS}/{job_id}/file").status_code == 404


@pytest.mark.parametrize("suffix", ["", "/file"])
def test_unknown_render_jobs_are_404(render_api: Api, suffix: str) -> None:
    response = render_api.client.get(f"{JOBS}/{'0' * 32}{suffix}")

    assert response.status_code == 404
    assert_conforms(response, ONE + suffix)


# ------------------------------------------------------------------ delete


def test_delete_cancels_a_running_job_and_removes_it(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.gate.clear()
    job_id = _create(render_api)
    assert executor.entered.wait(WAIT)

    response = render_api.client.delete(f"{JOBS}/{job_id}")
    executor.gate.set()
    _wait(render_api)

    assert response.status_code == 204
    assert render_api.client.get(f"{JOBS}/{job_id}").status_code == 404
    assert not executor.out_dirs[0].exists()


def test_delete_of_a_queued_job_means_it_never_renders(
    render_api: Api, executor: FakeExecutor
) -> None:
    executor.gate.clear()
    first = _create(render_api)
    assert executor.entered.wait(WAIT)
    second = _create(render_api)

    render_api.client.delete(f"{JOBS}/{second}")
    executor.gate.set()
    _wait(render_api)

    assert [path.name for path in executor.out_dirs] == [first]
    assert render_api.client.get(f"{JOBS}/{second}").status_code == 404


@pytest.mark.parametrize("job_id", ["0" * 32, "not-an-id"])
def test_delete_is_idempotent(render_api: Api, job_id: str) -> None:
    assert render_api.client.delete(f"{JOBS}/{job_id}").status_code == 204


def test_delete_of_a_completed_job_removes_its_file(render_api: Api) -> None:
    job_id = _completed(render_api)

    assert render_api.client.delete(f"{JOBS}/{job_id}").status_code == 204
    assert render_api.client.get(f"{JOBS}/{job_id}/file").status_code == 404


# ------------------------------------------------------------------ health


def test_health_reports_a_readable_library_and_an_idle_lane(render_api: Api) -> None:
    response = render_api.client.get("/health")

    assert_conforms(response, "/health")
    health = response.json()
    assert health["library"] == {"readable": True, "reason": None}
    assert health["lanes"] == {"render": {"queued": 0, "running": False}}


def test_health_reports_the_render_lane_while_busy(render_api: Api, executor: FakeExecutor) -> None:
    executor.gate.clear()
    _create(render_api)
    assert executor.entered.wait(WAIT)
    _create(render_api)

    lanes = render_api.client.get("/health").json()["lanes"]

    assert lanes == {"render": {"queued": 1, "running": True}}
    executor.gate.set()
    _wait(render_api)


def _app(tmp_path: Path, library: Path | None) -> TestClient:
    settings = WorkerSettings(
        data_dir=tmp_path, separator="fake", render=RenderSettings(library_dir=library)
    )
    return TestClient(main.create_app(settings, ApiFakes().adapters()))


def test_health_reports_a_missing_library_with_the_fixed_reason(tmp_path: Path) -> None:
    with _app(tmp_path, tmp_path / "absent") as client:
        response = client.get("/health")

    assert_conforms(response, "/health")
    assert response.json()["library"] == {"readable": False, "reason": LIBRARY_UNAVAILABLE_TEXT}


# ------------------------------------------------------------------ restart and composition


def test_start_up_fails_render_jobs_interrupted_by_restart(tmp_path: Path) -> None:
    now = datetime(2026, 11, 15, 13, 0, tzinfo=UTC)
    job = RenderJob(render_job_id="a" * 32, status=RenderJobStatus.RUNNING, progress=0.2,
                    created_at=now, updated_at=now)  # fmt: skip
    request = RenderJobRequest.model_validate(body())
    RenderJobStore(DataPaths(tmp_path)).save(StoredRenderJob(job=job, request=request))

    with _app(tmp_path, None) as client:
        response = client.get(f"{JOBS}/{'a' * 32}")

    assert_conforms(response, ONE)
    assert (response.json()["status"], response.json()["error"]) == (
        "failed",
        "interrupted by restart",
    )


def test_the_real_executor_is_composed_from_the_render_factories(tmp_path: Path) -> None:
    library = tmp_path / "library"
    library.mkdir()
    with _app(tmp_path, library) as client:
        response = client.post(JOBS, json=body())

    assert response.status_code == 404
    assert response.json()["detail"] == f"Input not found: {STEM}"


def test_the_sweeper_also_sweeps_render_jobs(render_api: Api) -> None:
    lanes = render_api.client.app.state.runtime.lanes  # type: ignore[attr-defined]

    assert lanes.render.sweep() == []
