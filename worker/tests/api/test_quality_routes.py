"""Tests of the quality job routes and service (020 T040, worker API 1.11.0, data-model 2.3/2.4)."""

import copy
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.jobs.progress import UNEXPECTED_ERROR
from stemstudio_worker.jobs.quality_service import stage_of
from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import QualityAnalysisError

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .insights_fakes import STEMS, TRACK, WAIT, FakeQualityAnalyzer, write_track

JOBS = "/v1/quality-jobs"
ONE = f"{JOBS}/{{qualityJobId}}"


def _body() -> dict[str, Any]:
    stems = [{"channel": n, "path": f"tracks/{TRACK}/stems/{n}.flac", "kind": "standard"}
             for n in STEMS]  # fmt: skip
    stems += [
        {"channel": "drums.kick", "path": f"tracks/{TRACK}/detail/drums/kick.flac",
         "kind": "detail"},
        {"channel": "drums.rest", "path": f"tracks/{TRACK}/detail/drums/rest.flac",
         "kind": "rest"},
    ]  # fmt: skip
    return {"stems": stems, "titles": {"title": "Example Song", "artist": "Example Band"},
            "detailParents": [{"parent": "drums", "modelResidualDb": -25.8,
                               "reconstructionErrorDb": -170.0}]}  # fmt: skip


@pytest.fixture
def analyzer() -> FakeQualityAnalyzer:
    return FakeQualityAnalyzer()


@pytest.fixture
def api(make_api: ApiFactory, analyzer: FakeQualityAnalyzer) -> Api:
    created = make_api(overrides=AppOverrides(quality_analyzer=analyzer))
    write_track(created.settings.render.library_dir or Path())
    return created


def _library(api: Api) -> Path:
    root = api.settings.render.library_dir
    assert root is not None
    return root.resolve()


def _wait(api: Api) -> None:
    assert api.client.app.state.runtime.lanes.insights.lane.join(WAIT)  # type: ignore[attr-defined]


def _create(api: Api, body: dict[str, Any] | None = None) -> str:
    response = api.client.post(JOBS, json=body or _body())
    assert response.status_code == 202, response.text
    assert_conforms(response, JOBS, "post")
    job_id: str = response.json()["qualityJobId"]
    assert response.headers["location"] == f"{JOBS}/{job_id}"
    return job_id


def test_a_completed_job_rates_every_stem_in_request_order(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    job_id = _create(api)
    _wait(api)

    response = api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    job = response.json()
    assert (job["status"], job["stage"], job["progress"], job["error"]) == (
        "completed", None, 1, None)  # fmt: skip
    channels = [stem["channel"] for stem in job["result"]["stems"]]
    assert channels == [*STEMS, "drums.kick", "drums.rest"]
    assert job["result"]["analyzerVersion"] == "fake-1"
    (inputs,) = analyzer.calls
    assert [(s.channel, s.kind) for s in inputs.stems][-2:] == [("drums.kick", "detail"),
                                                                ("drums.rest", "rest")]  # fmt: skip
    assert inputs.stems[0].path == _library(api) / f"tracks/{TRACK}/stems/vocals.flac"
    assert inputs.detail_parents[0].model_residual_db == -25.8


@pytest.mark.parametrize(("fraction", "stage"), [(0.0, "loading"), (0.19, "loading"),
                                                 (0.2, "analysing"), (0.89, "analysing"),
                                                 (0.9, "rating"), (1.0, "rating")])  # fmt: skip
def test_progress_maps_to_the_contract_stages(fraction: float, stage: str) -> None:
    assert stage_of(fraction) == stage


def test_a_running_job_shows_its_stage_and_progress(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    analyzer.fractions = [0.5]
    analyzer.gate.clear()
    job_id = _create(api)
    assert analyzer.entered.wait(WAIT)

    response = api.client.get(f"{JOBS}/{job_id}")
    analyzer.gate.set()

    assert_conforms(response, ONE)
    assert (response.json()["status"], response.json()["stage"]) == ("running", "analysing")
    assert response.json()["progress"] == 0.5
    _wait(api)


def _without_piano(body: dict[str, Any]) -> dict[str, Any]:
    body["stems"] = [s for s in body["stems"] if s["channel"] != "piano"]
    return body


def _wrong_kind(body: dict[str, Any]) -> dict[str, Any]:
    body["stems"][6]["kind"] = "standard"
    return body


def _twice(body: dict[str, Any]) -> dict[str, Any]:
    body["stems"].append(copy.deepcopy(body["stems"][0]))
    return body


@pytest.mark.parametrize("change", [_without_piano, _wrong_kind, _twice])
def test_bodies_without_the_six_stems_or_with_bad_kinds_are_422(api: Api, change: Any) -> None:
    response = api.client.post(JOBS, json=change(_body()))

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


@pytest.mark.parametrize(
    "path",
    [
        "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/tab.pdf",
        f"tracks/{TRACK}/artwork/auto/cover.jpg",
        "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/mix.flac",
        f"tracks/{TRACK}/stems/../stems/vocals.flac",
    ],
)
def test_paths_outside_the_quality_allow_list_are_400(api: Api, path: str) -> None:
    body = _body()
    body["stems"][0]["path"] = path

    response = api.client.post(JOBS, json=body)

    assert response.status_code == 400
    assert response.json()["type"].endswith("invalid-input-path")
    assert_conforms(response, JOBS, "post")


def test_a_missing_stem_is_404(api: Api) -> None:
    (_library(api) / f"tracks/{TRACK}/stems/piano.flac").unlink()

    response = api.client.post(JOBS, json=_body())

    assert response.status_code == 404
    assert response.json()["type"].endswith("input-not-found")
    assert_conforms(response, JOBS, "post")


def test_a_stem_linked_out_of_the_library_is_400(api: Api, tmp_path: Path) -> None:
    outside = tmp_path / "outside.flac"
    outside.write_bytes(b"fLaC")
    stem = _library(api) / f"tracks/{TRACK}/stems/bass.flac"
    stem.unlink()
    try:
        stem.symlink_to(outside)
    except OSError:
        pytest.skip("symlinks are not available here")

    response = api.client.post(JOBS, json=_body())

    assert response.status_code == 400


def test_an_unknown_job_is_404(api: Api) -> None:
    response = api.client.get(f"{JOBS}/{'0' * 32}")

    assert response.status_code == 404
    assert_conforms(response, ONE)


def test_delete_cancels_a_running_job_and_is_idempotent(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    analyzer.gate.clear()
    job_id = _create(api)
    assert analyzer.entered.wait(WAIT)

    first = api.client.delete(f"{JOBS}/{job_id}")
    analyzer.gate.set()
    _wait(api)
    second = api.client.delete(f"{JOBS}/{job_id}")

    assert (first.status_code, second.status_code) == (204, 204)
    assert api.client.get(f"{JOBS}/{job_id}").status_code == 404
    assert api.client.delete(f"{JOBS}/not-an-id").status_code == 204


def test_an_analysis_error_fails_the_job_with_its_text(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    analyzer.error = QualityAnalysisError("The vocals stem could not be read.")
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"], job["stage"]) == (
        "failed", "The vocals stem could not be read.", None)  # fmt: skip


def test_an_unexpected_error_fails_with_the_generic_text(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    analyzer.error = ZeroDivisionError("boom")
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"]) == ("failed", UNEXPECTED_ERROR)


def test_a_stem_deleted_after_creation_fails_the_job(
    api: Api, analyzer: FakeQualityAnalyzer
) -> None:
    analyzer.gate.clear()
    _create(api)
    assert analyzer.entered.wait(WAIT)
    job_id = _create(api)
    (_library(api) / f"tracks/{TRACK}/stems/other.flac").unlink()
    analyzer.gate.set()
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert job["status"] == "failed"
    assert "other.flac" in job["error"]
    assert len(analyzer.calls) == 1


def test_health_reports_the_insights_lane_and_the_analyzer(api: Api) -> None:
    response = api.client.get("/health")

    assert_conforms(response, "/health")
    body = response.json()
    assert body["lanes"]["insights"] == {"queued": 0, "running": False}
    insights = body["insights"]
    assert (insights["analyzer"], insights["analyzerVersion"]) == ("real", "fake-1")
