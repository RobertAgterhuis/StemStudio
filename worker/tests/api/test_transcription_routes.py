"""Tests of the transcription job routes (008 T038, FR-001, FR-003, FR-011; worker 1.6.0).

The fake stack's fake transcriber (data-model 2.6) plays on the request grid; most cases use a
fake notator and engraver, one runs the real notation and Verovio engraving on the fake notes
(only CairoSVG is replaced: it needs libcairo). Every response is checked against the contract.
"""

from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.engraving import VerovioScoreEngraver
from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import NoNotesError, TranscriptionError
from stemstudio_worker.schemas import MUSIC_FONT_MISSING_TEXT, NO_NOTES_TEXT

from .conftest import Api, ApiFactory
from .contract import Response, assert_conforms
from .render_fakes import FakeExecutor, body as render_body
from .score_fakes import (
    BASS,
    PDF,
    TRACK,
    WAIT,
    FakeEngraver,
    FakeNotator,
    blank_pdf_page,
    request_body,
    write_stem,
)

JOBS = "/v1/transcription-jobs"
ONE = f"{JOBS}/{{transcriptionJobId}}"
FILE = f"{ONE}/files/{{role}}"


@pytest.fixture
def notator() -> FakeNotator:
    return FakeNotator()


@pytest.fixture
def engraver() -> FakeEngraver:
    return FakeEngraver()


@pytest.fixture
def score_api(make_api: ApiFactory, notator: FakeNotator, engraver: FakeEngraver) -> Api:
    api = make_api(overrides=AppOverrides(notator=notator, score_engraver=engraver))
    write_stem(api.settings.render.library_dir)  # type: ignore[arg-type]
    return api


def _runtime(api: Api) -> Any:
    return api.client.app.state.runtime  # type: ignore[attr-defined]


def _wait(api: Api) -> None:
    assert _runtime(api).lanes.transcription.join(WAIT)


def _post(api: Api, body: dict[str, Any]) -> Response:
    response = api.client.post(JOBS, json=body)
    assert_conforms(response, JOBS, "post")
    return response


def _completed(api: Api, body: dict[str, Any] | None = None) -> dict[str, Any]:
    response = _post(api, body or request_body())
    assert response.status_code == 202, response.json()
    _wait(api)
    job = api.client.get(f"{JOBS}/{response.json()['transcriptionJobId']}")
    assert_conforms(job, ONE)
    result: dict[str, Any] = job.json()
    return result


# ------------------------------------------------------------------ create and run


def test_a_bass_job_is_queued_and_completes_with_files_and_summary(
    score_api: Api, notator: FakeNotator, engraver: FakeEngraver
) -> None:
    response = _post(score_api, request_body())
    job_id = response.json()["transcriptionJobId"]
    _wait(score_api)
    job = score_api.client.get(f"{JOBS}/{job_id}").json()

    assert response.status_code == 202
    assert response.headers["location"] == f"{JOBS}/{job_id}"
    assert (response.json()["status"], response.json()["stage"]) in (
        ("queued", None),
        ("running", "loading-model"),
    )
    assert (job["status"], job["stage"], job["progress"], job["error"]) == (
        "completed",
        None,
        1,
        None,
    )
    assert job["device"]["kind"] == "cpu"
    summary = job["result"]["summary"]
    assert (summary["profile"], summary["engine"], summary["quality"]) == ("bass", "fake", None)
    assert (summary["bars"], summary["pages"], summary["resolution"], summary["bpm"]) == (
        3,
        2,
        "sixteenth",
        120,
    )
    assert summary["notes"] == notator.calls[0][0] > 0
    assert engraver.calls == [("Fake Song - bass (sheet music)", None)]
    assert [f["role"] for f in job["result"]["files"]] == ["pdf", "musicxml", "midi"]


def test_the_files_are_served_per_role_with_their_content_types(score_api: Api) -> None:
    job = _completed(score_api)
    job_id = job["transcriptionJobId"]

    for role, media, data in (("pdf", "application/pdf", PDF),
                              ("musicxml", "application/vnd.recordare.musicxml+xml",
                               b"<score-partwise/>"),
                              ("midi", "audio/midi", b"MThd")):  # fmt: skip
        response = score_api.client.get(f"{JOBS}/{job_id}/files/{role}")
        assert_conforms(response, FILE)
        assert response.headers["content-type"].split(";")[0] == media
        assert response.content == data
        assert response.headers["content-length"] == str(len(data))


def test_files_of_unknown_jobs_or_roles_answer_404(score_api: Api) -> None:
    job_id = _completed(score_api)["transcriptionJobId"]

    unknown = score_api.client.get(f"{JOBS}/{'e' * 32}/files/pdf")
    role = score_api.client.get(f"{JOBS}/{job_id}/files/score")

    assert_conforms(unknown, FILE)
    assert (unknown.status_code, role.status_code) == (404, 404)
    assert score_api.client.get(f"{JOBS}/{'e' * 32}").status_code == 404


def test_stages_and_progress_follow_the_pipeline(
    score_api: Api, notator: FakeNotator, engraver: FakeEngraver
) -> None:
    seen: list[tuple[str, float]] = []

    def snapshot() -> None:
        job = _runtime(score_api).lanes.transcription.get(job_ids[0])
        seen.append((job.stage, job.progress))

    notator.on_call = engraver.on_call = snapshot
    job_ids = [_post(score_api, request_body()).json()["transcriptionJobId"]]
    _wait(score_api)

    assert seen == [("notating", 0.8), ("engraving", 0.9)]


def test_a_detail_stem_input_adds_the_bleed_warning(score_api: Api) -> None:
    detail = f"{TRACK}/detail/vocals/lead.flac"
    write_stem(score_api.settings.render.library_dir, detail)  # type: ignore[arg-type]

    job = _completed(score_api, request_body(profile="vocals", input={"path": detail}))

    codes = [w["code"] for w in job["result"]["summary"]["warnings"]]
    assert codes == ["detail-bleed"]
    assert job["result"]["summary"]["resolution"] == "eighth"


def test_at_most_eight_warnings_are_kept(score_api: Api, notator: FakeNotator) -> None:
    warning = {"code": "notes-cleaned", "text": "9 very short or out-of-range notes were removed."}
    notator.warnings = tuple(ScoreWarningOf(warning) for _ in range(8))
    detail = f"{TRACK}/detail/vocals/lead.flac"
    write_stem(score_api.settings.render.library_dir, detail)  # type: ignore[arg-type]

    job = _completed(score_api, request_body(profile="vocals", input={"path": detail}))

    assert len(job["result"]["summary"]["warnings"]) == 8


def ScoreWarningOf(value: dict[str, str]) -> Any:  # noqa: N802 - reads like the model it builds
    from stemstudio_worker.schemas import ScoreWarning  # noqa: PLC0415

    return ScoreWarning.model_validate(value)


# ------------------------------------------------------------------ validation and failures


@pytest.mark.parametrize(
    "changes",
    [
        {"profile": "drums"},
        {"drumInputs": {"kick": f"{TRACK}/detail/drums/kick.flac"}},
        {"grid": {"kind": "manual", "bpm": 10, "beatsPerBar": 4, "firstDownbeatSeconds": 0}},
        {"keyOverride": "H major"},
    ],
)
def test_invalid_requests_answer_422(score_api: Api, changes: dict[str, Any]) -> None:
    response = _post(score_api, request_body(**changes))

    assert response.status_code == 422


def test_bad_and_missing_input_paths_answer_400_and_404(score_api: Api) -> None:
    bad = _post(score_api, request_body(input={"path": "../etc/passwd"}))
    missing = _post(score_api, request_body(input={"path": f"{TRACK}/stems/piano.flac"}))

    assert (bad.status_code, bad.json()["type"]) == (400, "invalid-input-path")
    assert (missing.status_code, missing.json()["type"]) == (404, "input-not-found")


def test_no_notes_fails_the_job_with_the_fixed_text(score_api: Api, notator: FakeNotator) -> None:
    notator.error = NoNotesError()

    job = _completed(score_api)

    assert (job["status"], job["error"], job["result"]) == ("failed", NO_NOTES_TEXT, None)
    files = score_api.client.get(f"{JOBS}/{job['transcriptionJobId']}/files/pdf")
    assert_conforms(files, FILE)
    assert (files.status_code, files.json()["type"]) == (409, "transcription-not-completed")


def test_an_engraving_error_fails_the_job_with_its_text(
    score_api: Api, engraver: FakeEngraver
) -> None:
    engraver.error = TranscriptionError(MUSIC_FONT_MISSING_TEXT)

    job = _completed(score_api)

    assert (job["status"], job["error"]) == ("failed", MUSIC_FONT_MISSING_TEXT)


def test_an_unexpected_error_fails_with_unexpected_error(
    score_api: Api, notator: FakeNotator
) -> None:
    notator.error = RuntimeError("boom")

    assert _completed(score_api)["error"] == "Unexpected error"


# ------------------------------------------------------------------ lane, delete, health


def test_the_transcribe_lane_does_not_wait_for_a_blocked_render_or_separation(
    make_api: ApiFactory, notator: FakeNotator, engraver: FakeEngraver
) -> None:
    executor = FakeExecutor()
    executor.gate.clear()
    api = make_api(overrides=AppOverrides(notator=notator, score_engraver=engraver,
                                          render_executor=executor))  # fmt: skip
    write_stem(api.settings.render.library_dir)  # type: ignore[arg-type]
    write_stem(api.settings.render.library_dir, f"{TRACK}/stems/drums.flac")  # type: ignore[arg-type]
    api.fakes.gate.clear()
    api.post_url()
    api.client.post("/v1/render-jobs", json=render_body())
    assert executor.entered.wait(WAIT)

    job = _completed(api)

    assert job["status"] == "completed"
    executor.gate.set()
    api.fakes.gate.set()


def test_delete_removes_a_job_and_its_files_and_is_idempotent(score_api: Api) -> None:
    job_id = _completed(score_api)["transcriptionJobId"]

    first = score_api.client.delete(f"{JOBS}/{job_id}")
    again = score_api.client.delete(f"{JOBS}/{job_id}")

    assert (first.status_code, again.status_code) == (204, 204)
    assert score_api.client.get(f"{JOBS}/{job_id}").status_code == 404
    assert not (score_api.settings.data_dir / "transcription-jobs" / job_id).exists()


def test_health_reports_the_transcription_models_font_and_lane(score_api: Api) -> None:
    response = score_api.client.get("/health")

    assert_conforms(response, "/health")
    body = response.json()
    assert body["transcription"]["models"] == {
        "basic-pitch-icassp2022": "not-downloaded", "piano-crnn-note-pedal": "not-downloaded",
    }  # fmt: skip
    assert body["transcription"]["font"] == {"leipzig": False}
    assert body["lanes"]["transcribe"] == {"queued": 0, "running": False}


# ------------------------------------------------------------------ real notation + engraving


def test_the_real_notation_and_engraving_run_on_the_fake_notes(make_api: ApiFactory) -> None:
    engraver = VerovioScoreEngraver(font_check=lambda: True, svg_to_pdf=blank_pdf_page)
    api = make_api(overrides=AppOverrides(score_engraver=engraver))
    write_stem(api.settings.render.library_dir)  # type: ignore[arg-type]

    job = _completed(api)

    assert job["status"] == "completed", job["error"]
    summary = job["result"]["summary"]
    assert summary["bars"] >= 2
    assert summary["timeSignature"] == "4/4"
    xml = api.client.get(f"{JOBS}/{job['transcriptionJobId']}/files/musicxml").content
    assert b"<score-partwise" in xml


def test_inputs_are_read_from_the_library(score_api: Api, notator: FakeNotator) -> None:
    library: Path = score_api.settings.render.library_dir  # type: ignore[assignment]

    _completed(score_api)

    assert (library / BASS).is_file()
    assert notator.calls[0][1].duration_seconds == pytest.approx(4.5, abs=0.01)
