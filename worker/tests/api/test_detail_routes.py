"""Contract tests of ``/v1/detail-jobs/**`` against worker-api 1.1.0 (T046, FR-007..FR-010)."""

from collections.abc import Iterator
from contextlib import contextmanager
from typing import Any

import pytest

from .conftest import Api
from .contract import assert_conforms, component, spec, validate_schema

JOBS = "/v1/detail-jobs"
JOB = "/v1/detail-jobs/{detailJobId}"
CANCEL = "/v1/detail-jobs/{detailJobId}/cancel"
RESULT = "/v1/detail-jobs/{detailJobId}/result"
STEM = "/v1/detail-jobs/{detailJobId}/stems/{parent}/{name}"
UNKNOWN = "f" * 32
OTHER_VIDEO = "https://www.youtube.com/watch?v=aaaaaaaaaaa"


@contextmanager
def busy_worker(api: Api) -> Iterator[None]:
    """Keep the worker thread busy with a standard job blocked in its download."""
    api.fakes.gate.clear()
    api.post_url(OTHER_VIDEO)
    try:
        yield
    finally:
        api.fakes.gate.set()
        api.wait()


@pytest.mark.parametrize(
    ("example", "schema"),
    [
        ("QueuedDetailJob", "DetailJob"),
        ("RunningDetailJob", "DetailJob"),
        ("FailedDetailJob", "DetailJob"),
        ("CompletedDetailJob", "DetailJob"),
        ("DetailResultExample", "DetailResult"),
    ],
)
def test_contract_examples_are_valid(example: str, schema: str) -> None:
    validate_schema(component(schema), spec()["components"]["examples"][example]["value"])


def test_health_reports_contract_1_1_0(api: Api) -> None:
    response = api.client.get("/health")
    assert_conforms(response, "/health")
    assert response.json()["contractVersion"] == "1.1.0"


def test_create_returns_202_with_location(api: Api) -> None:
    standard = api.completed_job()
    with busy_worker(api):
        response = api.post_detail(standard)
        assert response.status_code == 202
        assert_conforms(response, JOBS, "post")
        body = response.json()
        assert response.headers["location"] == f"/v1/detail-jobs/{body['detailJobId']}"
        assert (body["status"], body["stage"], body["progress"]) == ("queued", "Queued", 0)
        assert body["standardJobId"] == standard


def test_create_again_returns_200_with_the_existing_job(api: Api) -> None:
    standard = api.completed_job()
    first = api.post_detail(standard).json()
    api.wait()
    again = api.post_detail(standard)
    assert again.status_code == 200
    assert_conforms(again, JOBS, "post")
    assert again.json()["detailJobId"] == first["detailJobId"]
    assert again.json()["status"] == "completed"


@pytest.mark.parametrize(
    "body", [{}, {"standardJobId": "XYZ"}, {"standardJobId": "a" * 32, "extra": 1}]
)
def test_create_with_an_invalid_body_returns_400(api: Api, body: dict[str, Any]) -> None:
    response = api.client.post(JOBS, json=body)
    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_create_with_malformed_json_returns_400(api: Api) -> None:
    response = api.client.post(JOBS, content=b"{", headers={"content-type": "application/json"})
    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_create_for_an_unknown_standard_job_returns_404(api: Api) -> None:
    response = api.post_detail(UNKNOWN)
    assert response.status_code == 404
    assert_conforms(response, JOBS, "post")


def test_create_for_a_standard_job_that_is_not_completed_returns_409(api: Api) -> None:
    api.fakes.gate.clear()
    standard = api.post_url().json()["jobId"]
    response = api.post_detail(standard)
    api.fakes.gate.set()
    assert response.status_code == 409
    assert_conforms(response, JOBS, "post")
    assert response.json()["detail"].startswith("Job is in state ")


def test_get_reports_the_completed_job(api: Api) -> None:
    detail_id = api.completed_detail()
    response = api.client.get(f"/v1/detail-jobs/{detail_id}")
    assert_conforms(response, JOB)
    body = response.json()
    assert (body["status"], body["progress"], body["currentParent"]) == ("completed", 100, None)


@pytest.mark.parametrize("detail_id", [UNKNOWN, "not-a-job"])
def test_get_unknown_job_returns_404(api: Api, detail_id: str) -> None:
    response = api.client.get(f"/v1/detail-jobs/{detail_id}")
    assert response.status_code == 404
    assert_conforms(response, JOB)


def test_result_lists_one_outcome_per_standard_stem(api: Api) -> None:
    detail_id = api.completed_detail()
    response = api.client.get(f"/v1/detail-jobs/{detail_id}/result")
    assert_conforms(response, RESULT)
    body = response.json()
    assert [p["parent"] for p in body["parents"]] == [
        "vocals", "drums", "bass", "guitar", "piano", "other",
    ]  # fmt: skip
    outcomes = {p["parent"]: p["outcome"] for p in body["parents"]}
    assert outcomes == {
        "vocals": "split", "drums": "split", "bass": "not-supported",
        "guitar": "nothing-further", "piano": "not-reliable", "other": "split",
    }  # fmt: skip
    drums = body["parents"][1]
    assert drums["stems"][0]["path"] == f"/v1/detail-jobs/{detail_id}/stems/drums/kick"


def test_queued_job_result_and_stems_return_409_and_cancel_works(api: Api) -> None:
    standard = api.completed_job()
    with busy_worker(api):
        detail_id = api.post_detail(standard).json()["detailJobId"]
        result = api.client.get(f"/v1/detail-jobs/{detail_id}/result")
        stem = api.client.get(f"/v1/detail-jobs/{detail_id}/stems/drums/kick")
        cancel = api.client.post(f"/v1/detail-jobs/{detail_id}/cancel")
        again = api.client.post(f"/v1/detail-jobs/{detail_id}/cancel")
    assert (result.status_code, stem.status_code) == (409, 409)
    assert_conforms(result, RESULT)
    assert_conforms(stem, STEM)
    assert cancel.status_code == 200
    assert_conforms(cancel, CANCEL, "post")
    assert (cancel.json()["status"], again.json()["status"]) == ("cancelled", "cancelled")
    assert api.client.get(f"/v1/detail-jobs/{detail_id}").json()["status"] == "cancelled"


def test_cancel_of_a_completed_job_returns_409(api: Api) -> None:
    detail_id = api.completed_detail()
    response = api.client.post(f"/v1/detail-jobs/{detail_id}/cancel")
    assert response.status_code == 409
    assert_conforms(response, CANCEL, "post")
    assert response.json()["detail"] == "Detail job is in state completed."


def test_cancel_of_an_unknown_job_returns_404(api: Api) -> None:
    response = api.client.post(f"/v1/detail-jobs/{UNKNOWN}/cancel")
    assert response.status_code == 404
    assert_conforms(response, CANCEL, "post")


def test_result_of_an_unknown_job_returns_404(api: Api) -> None:
    response = api.client.get(f"/v1/detail-jobs/{UNKNOWN}/result")
    assert response.status_code == 404
    assert_conforms(response, RESULT)


def test_detail_stem_streams_flac(api: Api) -> None:
    detail_id = api.completed_detail()
    response = api.client.get(f"/v1/detail-jobs/{detail_id}/stems/drums/kick")
    assert response.status_code == 200
    assert_conforms(response, STEM)
    assert response.headers["content-type"] == "audio/flac"
    assert int(response.headers["content-length"]) == len(response.content)
    assert response.content.startswith(b"fLaC")


@pytest.mark.parametrize(
    ("parent", "name"),
    [("bass", "rest"), ("drums", "lead"), ("guitar", "rest"), ("drums", "nope"), ("x", "kick")],
)
def test_unknown_or_unoffered_detail_stems_return_404(api: Api, parent: str, name: str) -> None:
    detail_id = api.completed_detail()
    response = api.client.get(f"/v1/detail-jobs/{detail_id}/stems/{parent}/{name}")
    assert response.status_code == 404
    assert_conforms(response, STEM)


def test_detail_stem_missing_on_disk_returns_404(api: Api) -> None:
    detail_id = api.completed_detail()
    runtime = api.client.app.state.runtime  # type: ignore[attr-defined]
    record = runtime.details.get(detail_id)
    runtime.paths.detail_stem(record.job.audio_sha256, "drums", "kick").unlink()
    response = api.client.get(f"/v1/detail-jobs/{detail_id}/stems/drums/kick")
    assert response.status_code == 404


def test_detail_stem_of_an_unknown_job_returns_404(api: Api) -> None:
    response = api.client.get(f"/v1/detail-jobs/{UNKNOWN}/stems/drums/kick")
    assert response.status_code == 404
    assert_conforms(response, STEM)
