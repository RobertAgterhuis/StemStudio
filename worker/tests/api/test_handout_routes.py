"""Tests of the handout job routes and service (020 T074, worker API 1.11.0, data-model 2.2/2.5)."""

import copy
import hashlib
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.handouts.renderer import WeasyHandoutRenderer
from stemstudio_worker.jobs.handout_service import stage_of
from stemstudio_worker.main import AppOverrides, default_handout_renderer
from stemstudio_worker.ports import HANDOUT_FAILED_TEXT, HandoutRenderError

from .conftest import Api, ApiFactory
from .contract import assert_conforms, spec
from .insights_fakes import (
    OUTLINE,
    PDF,
    WAIT,
    FakeHandoutRenderer,
    blank_layout,
    tokens_pdf,
)

JOBS = "/v1/handout-jobs"
ONE = f"{JOBS}/{{handoutJobId}}"
FILE = f"{ONE}/file"
TRACK = "7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f"
ARTWORK = f"tracks/{TRACK}/artwork/auto/cover.jpg"
TAB = "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/tab.pdf"
SCORE = "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf"


def _body() -> dict[str, Any]:
    """The ``BassHandoutRequest`` example of the contract."""
    example: dict[str, Any] = spec()["components"]["examples"]["BassHandoutRequest"]["value"]
    return copy.deepcopy(example)


def _song(body: dict[str, Any]) -> dict[str, Any]:
    song: dict[str, Any] = body["document"]["songs"][0]
    return song


@pytest.fixture
def renderer() -> FakeHandoutRenderer:
    return FakeHandoutRenderer()


@pytest.fixture
def api(make_api: ApiFactory, renderer: FakeHandoutRenderer) -> Api:
    created = make_api(overrides=AppOverrides(handout_renderer=renderer))
    root = _library(created)
    for path in (ARTWORK, TAB, SCORE):
        (root / path).parent.mkdir(parents=True, exist_ok=True)
        (root / path).write_bytes(b"%PDF-1.7" if path.endswith(".pdf") else b"\xff\xd8jpeg")
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
    job_id: str = response.json()["handoutJobId"]
    assert response.headers["location"] == f"{JOBS}/{job_id}"
    return job_id


def test_a_completed_job_reports_the_pdf_and_serves_it(
    api: Api, renderer: FakeHandoutRenderer
) -> None:
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}")
    file = api.client.get(f"{JOBS}/{job_id}/file")

    assert_conforms(job, ONE)
    body = job.json()
    assert (body["status"], body["stage"], body["progress"]) == ("completed", None, 1)
    assert body["result"] == {
        "fileName": "handout.pdf", "contentType": "application/pdf", "sizeBytes": len(PDF),
        "sha256": hashlib.sha256(PDF).hexdigest(), "pages": 5, "outline": list(OUTLINE),
        "warnings": [],
    }  # fmt: skip
    assert_conforms(file, FILE)
    assert file.content == PDF
    assert file.headers["content-length"] == str(len(PDF))


def test_the_renderer_gets_the_real_path_of_every_input(
    api: Api, renderer: FakeHandoutRenderer
) -> None:
    _create(api)
    _wait(api)

    ((request, files),) = renderer.calls

    assert request.paper == "a4"
    root = _library(api)
    assert files == {ARTWORK: root / ARTWORK, TAB: root / TAB, SCORE: root / SCORE}


def test_renderer_warnings_are_reported(api: Api, renderer: FakeHandoutRenderer) -> None:
    from stemstudio_worker.schemas import HandoutWarning  # noqa: PLC0415

    renderer.warnings = (HandoutWarning(code="notation-scaled", text="Bass - score was scaled"),)
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}")

    assert_conforms(job, ONE)
    assert job.json()["result"]["warnings"] == [
        {"code": "notation-scaled", "text": "Bass - score was scaled"}
    ]


@pytest.mark.parametrize(("fraction", "stage"), [(0.0, "layout"), (0.59, "layout"),
                                                 (0.6, "notation"), (0.89, "notation"),
                                                 (0.9, "writing"), (1.0, "writing")])  # fmt: skip
def test_progress_maps_to_the_contract_stages(fraction: float, stage: str) -> None:
    assert stage_of(fraction) == stage


def test_a_running_job_shows_its_stage_and_the_file_is_409(
    api: Api, renderer: FakeHandoutRenderer
) -> None:
    renderer.fractions = [0.7]
    renderer.gate.clear()
    job_id = _create(api)
    assert renderer.entered.wait(WAIT)

    job = api.client.get(f"{JOBS}/{job_id}")
    file = api.client.get(f"{JOBS}/{job_id}/file")
    renderer.gate.set()

    assert (job.json()["status"], job.json()["stage"]) == ("running", "notation")
    assert file.status_code == 409
    assert file.json()["type"].endswith("handout-not-completed")
    assert_conforms(file, FILE)
    _wait(api)


def _other_notice(body: dict[str, Any]) -> None:
    body["document"]["notice"]["footer"] = "Share freely"


def _two_songs(body: dict[str, Any]) -> None:
    body["document"]["songs"].append(copy.deepcopy(_song(body)))


def _setlist_without_block(body: dict[str, Any]) -> None:
    body["document"]["kind"] = "setlist"


def _bad_paper(body: dict[str, Any]) -> None:
    body["paper"] = "a3"


@pytest.mark.parametrize("change", [_other_notice, _two_songs, _setlist_without_block, _bad_paper])
def test_invalid_documents_are_422(api: Api, change: Any) -> None:
    body = _body()
    change(body)

    response = api.client.post(JOBS, json=body)

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


def _notation(path: str) -> dict[str, Any]:
    body = _body()
    _song(body)["notation"][0]["pdfPath"] = path
    return body


def _artwork(path: str) -> dict[str, Any]:
    body = _body()
    _song(body)["artworkPath"] = path
    return body


@pytest.mark.parametrize(
    "body",
    [
        _notation(f"tracks/{TRACK}/stems/bass.flac"),
        _notation("assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/../tab.pdf"),
        _notation("assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/score.musicxml"),
        _notation("/etc/passwd"),
        _notation("assets\\1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6\\tab.pdf"),
        _artwork(f"tracks/{TRACK}/artwork/auto/thumb.jpg"),
        _artwork(TAB),
    ],
)
def test_paths_outside_the_handout_allow_list_are_400(api: Api, body: dict[str, Any]) -> None:
    response = api.client.post(JOBS, json=body)

    assert response.status_code == 400
    assert response.json()["type"].endswith("invalid-input-path")
    assert_conforms(response, JOBS, "post")


def test_a_missing_notation_pdf_is_404(api: Api) -> None:
    (_library(api) / SCORE).unlink()

    response = api.client.post(JOBS, json=_body())

    assert response.status_code == 404
    assert response.json()["type"].endswith("input-not-found")
    assert_conforms(response, JOBS, "post")


def test_a_notation_pdf_linked_out_of_the_library_is_400(api: Api, tmp_path: Path) -> None:
    outside = tmp_path / "outside.pdf"
    outside.write_bytes(b"%PDF")
    link = _library(api) / TAB
    link.unlink()
    try:
        link.symlink_to(outside)
    except OSError:
        pytest.skip("symlinks are not available here")

    assert api.client.post(JOBS, json=_body()).status_code == 400


def test_a_folder_with_a_pdf_name_is_404(api: Api) -> None:
    (_library(api) / TAB).unlink()
    (_library(api) / TAB).mkdir()

    assert api.client.post(JOBS, json=_body()).status_code == 404


def test_a_job_without_inputs_needs_no_library(
    make_api: ApiFactory, renderer: FakeHandoutRenderer
) -> None:
    api = make_api(overrides=AppOverrides(handout_renderer=renderer))
    body = _body()
    _song(body).update(artworkPath=None, notation=[])

    job_id = _create(api, body)
    _wait(api)

    assert api.client.get(f"{JOBS}/{job_id}").json()["status"] == "completed"
    assert renderer.calls[0][1] == {}


def test_an_unknown_job_and_its_file_are_404(api: Api) -> None:
    assert api.client.get(f"{JOBS}/{'0' * 32}").status_code == 404
    response = api.client.get(f"{JOBS}/{'0' * 32}/file")

    assert response.status_code == 404
    assert_conforms(response, FILE)


def test_delete_cancels_a_running_job_and_removes_it(
    api: Api, renderer: FakeHandoutRenderer
) -> None:
    renderer.gate.clear()
    job_id = _create(api)
    assert renderer.entered.wait(WAIT)

    first = api.client.delete(f"{JOBS}/{job_id}")
    renderer.gate.set()
    _wait(api)

    assert (first.status_code, api.client.delete(f"{JOBS}/{job_id}").status_code) == (204, 204)
    assert api.client.get(f"{JOBS}/{job_id}").status_code == 404
    assert not (api.settings.data_dir / "handout-jobs" / job_id).exists()


def test_a_completed_job_whose_pdf_vanished_is_404(api: Api) -> None:
    job_id = _create(api)
    _wait(api)
    (api.settings.data_dir / "handout-jobs" / job_id / "handout.pdf").unlink()

    assert api.client.get(f"{JOBS}/{job_id}/file").status_code == 404


@pytest.mark.parametrize(("error", "text"), [
    (HandoutRenderError(), HANDOUT_FAILED_TEXT),
    (HandoutRenderError("A notation file could not be read: Bass"),
     "A notation file could not be read: Bass"),
])  # fmt: skip
def test_a_render_error_fails_the_job_with_its_text(
    api: Api, renderer: FakeHandoutRenderer, error: Exception, text: str
) -> None:
    renderer.error = error
    job_id = _create(api)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["error"], job["stage"], job["result"]) == ("failed", text, None,
                                                                          None)  # fmt: skip
    assert not (api.settings.data_dir / "handout-jobs" / job_id).exists()


def test_the_default_renderer_is_the_weasyprint_renderer() -> None:
    assert isinstance(default_handout_renderer(AppOverrides()), WeasyHandoutRenderer)
    fake = FakeHandoutRenderer()
    assert default_handout_renderer(AppOverrides(handout_renderer=fake)) is fake


def test_a_notation_pdf_over_the_parse_limit_completes_the_job_with_a_warning(
    make_api: ApiFactory,
) -> None:
    api = make_api(overrides=AppOverrides(handout_renderer=WeasyHandoutRenderer(
        layout=blank_layout)))  # fmt: skip
    root = _library(api)
    for path, data in ((TAB, tokens_pdf(2_100_000)), (SCORE, tokens_pdf(30))):
        (root / path).parent.mkdir(parents=True, exist_ok=True)
        (root / path).write_bytes(data)
    body = _body()
    _song(body)["artworkPath"] = None

    job_id = _create(api, body)
    _wait(api)

    job = api.client.get(f"{JOBS}/{job_id}")
    assert_conforms(job, ONE)
    assert job.json()["status"] == "completed", job.json()["error"]
    assert job.json()["result"]["warnings"] == [
        {
            "code": "notation-unreadable",
            "text": "A notation file could not be read: Bass - tablature",
        }
    ]
