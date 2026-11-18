"""Tests of the score-view job routes (014 T076, worker-practice delta, FR-014)."""

from typing import Any

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import ScoreViewError
from stemstudio_worker.schemas import SCORE_VIEW_FAILED_TEXT

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .practice_fakes import ASSET, MUSICXML, TRACK, WAIT, FakeScoreViews, write_library

JOBS = "/v1/score-view-jobs"
ONE = f"{JOBS}/{{jobId}}"
PAGE = f"{ONE}/pages/{{page}}"


def _body(**changes: Any) -> dict[str, Any]:
    body: dict[str, Any] = {
        "musicxml": MUSICXML,
        "grid": {"kind": "manual", "bpm": 120, "beatsPerBar": 4, "firstDownbeatSeconds": 0},
        "durationSeconds": 8,
    }
    body.update(changes)
    return body


@pytest.fixture
def views() -> FakeScoreViews:
    return FakeScoreViews()


@pytest.fixture
def api(make_api: ApiFactory, views: FakeScoreViews) -> Api:
    started = make_api(overrides=AppOverrides(score_view_renderer=views))
    write_library(started.settings.render.library_dir)  # type: ignore[arg-type]
    return started


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.practice.lane.join(WAIT)  # type: ignore[attr-defined]


def _create(api: Api, **changes: Any) -> str:
    response = api.client.post(JOBS, json=_body(**changes))
    assert response.status_code == 202, response.text
    assert_conforms(response, JOBS, "post")
    job_id: str = response.json()["jobId"]
    return job_id


def test_a_completed_job_has_pages_bars_and_engine(api: Api, views: FakeScoreViews) -> None:
    job_id = _create(api)
    _wait(api)

    response = api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    result = response.json()["result"]
    assert result["pages"] == [{"page": 1, "widthPx": 840, "heightPx": 1188},
                               {"page": 2, "widthPx": 840, "heightPx": 1188}]  # fmt: skip
    assert [bar["endSeconds"] for bar in result["bars"]] == [2.0, 8.0]
    assert result["engine"] == {"name": "verovio", "version": "6.4.0"}
    assert views.inputs == [b"<score-partwise/>"]


def test_pages_are_served_as_svg(api: Api) -> None:
    job_id = _create(api)
    _wait(api)

    response = api.client.get(f"{JOBS}/{job_id}/pages/2")

    assert response.status_code == 200
    assert response.headers["content-type"].startswith("image/svg+xml")
    assert response.text == "<svg>2</svg>"
    for page in (3, 0):
        missing = api.client.get(f"{JOBS}/{job_id}/pages/{page}")
        assert missing.status_code == 404
    assert_conforms(api.client.get(f"{JOBS}/{job_id}/pages/3"), PAGE)


def test_pages_before_completion_are_409(api: Api, views: FakeScoreViews) -> None:
    views.gate.clear()
    job_id = _create(api)
    assert views.entered.wait(WAIT)

    response = api.client.get(f"{JOBS}/{job_id}/pages/1")

    assert response.status_code == 409
    assert_conforms(response, PAGE)
    assert response.json()["type"] == "job-not-completed"
    views.gate.set()
    _wait(api)


@pytest.mark.parametrize(
    "path",
    [f"assets/{ASSET}/score.pdf", f"assets/{ASSET}/other.musicxml",
     f"tracks/{TRACK}/stems/bass.flac", f"assets/{ASSET}/../{ASSET}/score.musicxml"],
)  # fmt: skip
def test_only_score_musicxml_files_are_allowed(api: Api, path: str) -> None:
    response = api.client.post(JOBS, json=_body(musicxml=path))

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_a_missing_score_is_404(api: Api) -> None:
    root = api.settings.render.library_dir
    assert root is not None
    (root / MUSICXML).unlink()

    response = api.client.post(JOBS, json=_body())

    assert response.status_code == 404


def test_schema_errors_are_422(api: Api) -> None:
    response = api.client.post(JOBS, json=_body(grid={"kind": "beat-grid", "bpm": 120}))

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


def test_a_layout_error_fails_the_job_and_keeps_no_pages(api: Api, views: FakeScoreViews) -> None:
    views.error = ScoreViewError()
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"]) == ("failed", SCORE_VIEW_FAILED_TEXT)
    assert api.client.get(f"{JOBS}/{job_id}/pages/1").status_code == 409


def test_delete_removes_the_job_and_its_pages(api: Api) -> None:
    job_id = _create(api)
    _wait(api)

    assert api.client.delete(f"{JOBS}/{job_id}").status_code == 204

    assert api.client.get(f"{JOBS}/{job_id}/pages/1").status_code == 404
    assert api.client.delete(f"{JOBS}/{'x' * 32}").status_code == 400
