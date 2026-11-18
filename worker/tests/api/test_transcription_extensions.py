"""The 1.9.0 extensions of the transcription jobs (016 T077, DELTA 1, FR-020, FR-023, FR-051).

``tablature``, ``part``, ``chords``, ``preferFlats`` and ``singlePiece`` reach the notator's
``NotationSettings``; the invalid combinations answer 422; the ``ScoreSummary`` carries the
notator's ``tablature``, ``part`` and ``chordsIncluded`` facts; ``singlePiece`` suppresses the
``missing-drum-piece`` warnings. An 008 request (none of the fields) keeps the 008 settings.
"""

from collections.abc import Sequence
from dataclasses import replace
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import DrumHit, NotationSettings, NoteEvent, ScoreDocument
from stemstudio_worker.schemas import (
    PartSettings,
    PartSummary,
    ScoreChordSymbol,
    TablatureSettings,
    TablatureSummary,
)

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .score_fakes import TRACK, WAIT, FakeEngraver, FakeNotator, request_body, write_stem

JOBS = "/v1/transcription-jobs"
ONE = f"{JOBS}/{{transcriptionJobId}}"
GUITAR = f"{TRACK}/stems/guitar.flac"
TAB = {"tuning": "bass-4-standard", "capo": 0, "layout": "notation-and-tab"}
PART = {"instrument": "double-bass", "clef": "auto"}
CHORDS = [{"startMs": 0, "root": 0, "quality": "maj", "bass": None},
          {"startMs": 2000, "root": 10, "quality": "7", "bass": 2}]  # fmt: skip
TAB_SUMMARY = TablatureSummary(tuning="bass-4-standard", strings=4, capo=0,
                               layout="notation-and-tab", octave_moved=1, notes_dropped=0,
                               wide_stretches=0)  # fmt: skip
PART_SUMMARY = PartSummary(instrument="double-bass", name="Double Bass", interval="P8",
                           written_key=None, clef="bass", out_of_range=2)  # fmt: skip


class ExtendedNotator(FakeNotator):
    """The fake notator whose documents carry the 1.9.0 facts of the request's settings."""

    def notate(self, notes: Sequence[NoteEvent], settings: NotationSettings) -> ScoreDocument:
        return _with_facts(super().notate(notes, settings), settings)

    def notate_drums(self, hits: Sequence[DrumHit], settings: NotationSettings) -> ScoreDocument:
        return _with_facts(super().notate_drums(hits, settings), settings)


def _with_facts(document: ScoreDocument, settings: NotationSettings) -> ScoreDocument:
    return replace(document,
                   tablature=TAB_SUMMARY if settings.tablature is not None else None,
                   part=PART_SUMMARY if settings.part is not None else None,
                   chords_included=bool(settings.chords))  # fmt: skip


@pytest.fixture
def notator() -> ExtendedNotator:
    return ExtendedNotator()


@pytest.fixture
def score_api(make_api: ApiFactory, notator: ExtendedNotator) -> Api:
    api = make_api(overrides=AppOverrides(notator=notator, score_engraver=FakeEngraver()))
    library = api.settings.render.library_dir
    assert library is not None
    write_stem(library)
    write_stem(library, GUITAR)
    return api


def _completed(api: Api, body: dict[str, Any]) -> dict[str, Any]:
    response = api.client.post(JOBS, json=body)
    assert response.status_code == 202, response.json()
    assert_conforms(response, JOBS, "post")
    assert api.client.app.state.runtime.lanes.transcription.join(WAIT)  # type: ignore[attr-defined]
    job = api.client.get(f"{JOBS}/{response.json()['transcriptionJobId']}")
    assert_conforms(job, ONE)
    result: dict[str, Any] = job.json()
    return result


def test_tablature_part_chords_and_prefer_flats_reach_the_notator(
    score_api: Api, notator: ExtendedNotator
) -> None:
    body = request_body(tablature=TAB, part=PART, chords=CHORDS, preferFlats=True)

    job = _completed(score_api, body)

    settings = notator.calls[0][1]
    assert job["status"] == "completed"
    assert settings.tablature == TablatureSettings.model_validate(TAB)
    assert settings.part == PartSettings.model_validate(PART)
    assert settings.chords == tuple(ScoreChordSymbol.model_validate(c) for c in CHORDS)
    assert settings.prefer_flats is True and settings.single_piece is None


def test_the_summary_carries_the_tablature_part_and_chord_facts(score_api: Api) -> None:
    job = _completed(score_api, request_body(tablature=TAB, part=PART, chords=CHORDS))

    summary = job["result"]["summary"]
    assert summary["tablature"] == TAB_SUMMARY.model_dump(mode="json", by_alias=True)
    assert summary["part"] == PART_SUMMARY.model_dump(mode="json", by_alias=True)
    assert summary["chordsIncluded"] is True


def test_an_008_request_keeps_the_008_settings_and_an_empty_extension_summary(
    score_api: Api, notator: ExtendedNotator
) -> None:
    job = _completed(score_api, request_body())

    settings = notator.calls[0][1]
    assert (settings.tablature, settings.part, settings.chords) == (None, None, ())
    assert (settings.prefer_flats, settings.single_piece) == (False, None)
    summary = job["result"]["summary"]
    assert (summary["tablature"], summary["part"], summary["chordsIncluded"]) == (None, None, False)


def test_a_guitar_tab_with_capo_reaches_the_notator(
    score_api: Api, notator: ExtendedNotator
) -> None:
    tab = {"tuning": "guitar-drop-d", "capo": 3, "layout": "tab-only"}
    body = request_body(profile="guitar", input={"path": GUITAR}, tablature=tab)

    _completed(score_api, body)

    assert notator.calls[0][1].tablature == TablatureSettings.model_validate(tab)


def _drum_body(library: Path, pieces: tuple[str, ...], **changes: Any) -> dict[str, Any]:
    inputs = {}
    for piece in pieces:
        inputs[piece] = f"{TRACK}/detail/drums/{piece}.flac"
        write_stem(library, inputs[piece])
    return request_body("DrumsTranscriptionRequest", drumInputs=inputs, **changes)


def test_a_single_drum_piece_has_no_missing_piece_warnings(
    score_api: Api, notator: ExtendedNotator
) -> None:
    library = score_api.settings.render.library_dir
    assert library is not None

    job = _completed(score_api, _drum_body(library, ("snare",), singlePiece="snare"))

    assert notator.calls[0][1].single_piece == "snare"
    assert [w["code"] for w in job["result"]["summary"]["warnings"]] == []


def test_without_single_piece_the_missing_pieces_are_still_warned(score_api: Api) -> None:
    library = score_api.settings.render.library_dir
    assert library is not None

    job = _completed(score_api, _drum_body(library, ("snare",)))

    codes = [w["code"] for w in job["result"]["summary"]["warnings"]]
    assert codes == ["missing-drum-piece"] * 4


@pytest.mark.parametrize(
    "changes",
    [
        {"profile": "vocals", "tablature": TAB},
        {"tablature": {**TAB, "tuning": "guitar-standard"}},
        {"tablature": {**TAB, "capo": 2}},
        {"tablature": {**TAB, "tuning": "ukulele"}},
        {"part": {"instrument": "kazoo", "clef": "auto"}},
        {"profile": "piano", "part": {"instrument": "concert", "clef": "treble"}},
        {"chords": CHORDS},
        {"chords": [{"startMs": 0, "root": 12, "quality": "maj", "bass": None}]},
        {"singlePiece": "kick"},
        {"preferFlats": "yes"},
    ],
)
def test_invalid_extension_combinations_answer_422(score_api: Api, changes: dict[str, Any]) -> None:
    body = request_body(**changes)

    response = score_api.client.post(JOBS, json=body)

    assert response.status_code == 422, changes
    assert_conforms(response, JOBS, "post")


def test_drum_extensions_that_do_not_fit_answer_422(score_api: Api) -> None:
    library = score_api.settings.render.library_dir
    assert library is not None
    two = _drum_body(library, ("kick", "snare"), singlePiece="kick")
    with_part = _drum_body(library, ("kick",), part=PART)

    for body in (two, with_part):
        assert score_api.client.post(JOBS, json=body).status_code == 422
