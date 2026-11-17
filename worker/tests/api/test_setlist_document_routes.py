"""Tests of the setlist document job routes (014 T086, worker-practice delta, FR-018)."""

from typing import Any

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import DocumentError
from stemstudio_worker.schemas import DOCUMENT_FAILED_TEXT

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .practice_fakes import PDF, WAIT, FakeDocuments

JOBS = "/v1/setlist-document-jobs"
ONE = f"{JOBS}/{{jobId}}"
FILE = f"{ONE}/file"


def _song(position: int) -> dict[str, Any]:
    return {"position": position, "title": f"Song {position}", "durationSeconds": 200}


def _body(**changes: Any) -> dict[str, Any]:
    body: dict[str, Any] = {
        "title": "Band night", "note": "", "generatedAt": "2026-11-16T18:00:00Z",
        "options": {"includeLyrics": True, "includeSections": True, "includeNotes": True,
                    "pageBreakPerSong": True},
        "songs": [_song(1), _song(2)],
    }  # fmt: skip
    body.update(changes)
    return body


@pytest.fixture
def documents() -> FakeDocuments:
    return FakeDocuments()


@pytest.fixture
def api(make_api: ApiFactory, documents: FakeDocuments) -> Api:
    return make_api(overrides=AppOverrides(setlist_renderer=documents))


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.practice.lane.join(WAIT)  # type: ignore[attr-defined]


def _create(api: Api, **changes: Any) -> str:
    response = api.client.post(JOBS, json=_body(**changes))
    assert response.status_code == 202, response.text
    assert_conforms(response, JOBS, "post")
    assert response.headers["location"] == f"{JOBS}/{response.json()['jobId']}"
    job_id: str = response.json()["jobId"]
    return job_id


def test_a_completed_job_has_its_page_count_and_serves_the_pdf(api: Api) -> None:
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}")
    file = api.client.get(f"{JOBS}/{job_id}/file")

    assert_conforms(job, ONE)
    assert (job.json()["status"], job.json()["pages"]) == ("completed", 2)
    assert file.status_code == 200
    assert file.headers["content-type"] == "application/pdf"
    assert file.content == PDF


def test_the_file_before_completion_is_409(api: Api, documents: FakeDocuments) -> None:
    documents.gate.clear()
    job_id = _create(api)
    assert documents.entered.wait(WAIT)

    response = api.client.get(f"{JOBS}/{job_id}/file")

    assert response.status_code == 409
    assert_conforms(response, FILE)
    documents.gate.set()
    _wait(api)


@pytest.mark.parametrize(
    "changes",
    [
        {"songs": [_song(n % 100 + 1) for n in range(101)]},
        {"songs": [{**_song(1), "lyrics": {"lines": ["x" * 501], "shortened": False}}]},
        {"songs": []},
        {"title": ""},
    ],
)
def test_limits_are_422(api: Api, changes: dict[str, Any]) -> None:
    response = api.client.post(JOBS, json=_body(**changes))

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


def test_a_renderer_error_fails_the_job(api: Api, documents: FakeDocuments) -> None:
    documents.error = DocumentError()
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"], job["pages"]) == ("failed", DOCUMENT_FAILED_TEXT, None)


def test_delete_removes_the_pdf_and_unknown_jobs_are_404(api: Api) -> None:
    job_id = _create(api)
    _wait(api)

    assert api.client.delete(f"{JOBS}/{job_id}").status_code == 204

    assert api.client.get(f"{JOBS}/{job_id}/file").status_code == 404
    response = api.client.get(f"{JOBS}/{job_id}")
    assert response.status_code == 404
    assert_conforms(response, ONE)


def test_a_completed_job_whose_file_vanished_is_404(api: Api) -> None:
    job_id = _create(api)
    _wait(api)
    runtime = api.client.app.state.runtime  # type: ignore[attr-defined]
    runtime.lanes.practice.documents.file(job_id).unlink()

    assert api.client.get(f"{JOBS}/{job_id}/file").status_code == 404
