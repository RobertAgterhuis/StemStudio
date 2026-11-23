"""Skeleton tests for the 020 handout contract code: worker API 1.11.0 handout jobs and ports.

Architect, 020 Phase 0 T001: models of ``HandoutJobRequest`` and ``HandoutJob``, the fixed notice
texts (data-model 6.6), the handout path allow-list (data-model 2.2) and the renderer port.
"""

from typing import Any, get_args

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.ports import (
    HANDOUT_FAILED_TEXT,
    NOTICE_FOOTER,
    NOTICE_FULL,
    HandoutCancelledError,
    HandoutRenderer,
    HandoutRenderError,
    NotationUnreadableError,
    RenderedHandout,
    WorkerError,
    artwork_omitted_text,
    is_handout_input_path,
    notation_omitted_text,
    notation_unreadable_text,
)
from stemstudio_worker.schemas import (
    HANDOUT_NOTICE_FOOTER,
    HANDOUT_NOTICE_FULL,
    HandoutDocument,
    HandoutFamily,
    HandoutJob,
    HandoutJobRequest,
    HandoutKind,
    HandoutLyrics,
    HandoutNotation,
    HandoutNotice,
    HandoutResult,
    HandoutSection,
    HandoutSong,
    HandoutWarning,
    HandoutWarningCode,
    IngredientCode,
    LyricChord,
    LyricLine,
    NotationKind,
    Paper,
    SetlistBlock,
    SetlistEntryBlock,
    SongFacts,
)

from .docs import WorkerDocument, field_mismatch

V1110 = WorkerDocument("020-insights-handouts")
_ASSET = "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6"
_TRACK = "tracks/7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f"
_MODELS: dict[str, type[BaseModel]] = {
    "HandoutNotice": HandoutNotice,
    "SetlistEntryBlock": SetlistEntryBlock,
    "SetlistBlock": SetlistBlock,
    "SongFacts": SongFacts,
    "HandoutSection": HandoutSection,
    "LyricChord": LyricChord,
    "LyricLine": LyricLine,
    "HandoutLyrics": HandoutLyrics,
    "HandoutNotation": HandoutNotation,
    "HandoutSong": HandoutSong,
    "HandoutDocument": HandoutDocument,
    "HandoutJobRequest": HandoutJobRequest,
    "HandoutWarning": HandoutWarning,
    "HandoutResult": HandoutResult,
    "HandoutJob": HandoutJob,
}


def _request() -> dict[str, Any]:
    request: dict[str, Any] = V1110.example("BassHandoutRequest")
    return request


def _setlist(request: dict[str, Any], songs: int) -> dict[str, Any]:
    document = request["document"]
    document["kind"] = "setlist"
    document["setlist"] = {
        "name": "Friday set",
        "notes": None,
        "entries": [
            {
                "position": i + 1,
                "title": "Example Song",
                "artist": None,
                "key": None,
                "tempoBpm": None,
                "durationSeconds": None,
                "notes": None,
            }
            for i in range(songs)
        ],
    }
    document["songs"] = document["songs"] * songs
    return request


# ---------------------------------------------------------------- document


@pytest.mark.parametrize("name", sorted(_MODELS))
def test_every_handout_schema_has_a_model_with_the_same_fields(name: str) -> None:
    assert field_mismatch(V1110, name, _MODELS[name]) == []


@pytest.mark.parametrize(
    ("literal", "schema"),
    [
        (HandoutKind, "HandoutKind"),
        (HandoutFamily, "HandoutFamily"),
        (IngredientCode, "IngredientCode"),
        (Paper, "Paper"),
        (NotationKind, "NotationKind"),
        (HandoutWarningCode, "HandoutWarningCode"),
    ],
)
def test_enums_match_the_contract(literal: Any, schema: str) -> None:
    assert list(get_args(literal)) == V1110.schemas[schema]["enum"]


def test_handout_examples_round_trip() -> None:
    V1110.assert_round_trip("BassHandoutRequest", HandoutJobRequest)
    V1110.assert_round_trip("CompletedHandoutJob", HandoutJob)


def test_notice_texts_are_the_fixed_texts_of_the_contract_example() -> None:
    notice = _request()["document"]["notice"]

    assert (notice["footer"], notice["full"]) == (HANDOUT_NOTICE_FOOTER, HANDOUT_NOTICE_FULL)
    assert (NOTICE_FOOTER, NOTICE_FULL) == (HANDOUT_NOTICE_FOOTER, HANDOUT_NOTICE_FULL)


# ---------------------------------------------------------------- request rules


@pytest.mark.parametrize("field", ["footer", "full"])
def test_changed_notice_text_is_rejected(field: str) -> None:
    request = _request()
    request["document"]["notice"][field] += " (edited)"

    with pytest.raises(ValidationError, match="fixed notice text"):
        HandoutJobRequest.model_validate(request)


def test_song_handout_has_exactly_one_song_and_no_setlist() -> None:
    two = _request()
    two["document"]["songs"] *= 2
    with pytest.raises(ValidationError, match="exactly one song"):
        HandoutJobRequest.model_validate(two)
    with_block = _setlist(_request(), 1)
    with_block["document"]["kind"] = "song"
    with pytest.raises(ValidationError, match="setlist"):
        HandoutJobRequest.model_validate(with_block)


def test_setlist_handout_needs_its_block_and_at_most_fifty_songs() -> None:
    assert HandoutJobRequest.model_validate(_setlist(_request(), 50)).document.kind == "setlist"
    with pytest.raises(ValidationError):
        HandoutJobRequest.model_validate(_setlist(_request(), 51))
    missing = _setlist(_request(), 2)
    missing["document"]["setlist"] = None
    with pytest.raises(ValidationError, match="setlist"):
        HandoutJobRequest.model_validate(missing)


@pytest.mark.parametrize(
    "symbol", ["Am", "Bb", "F#m7", "C/G", "Ebmaj7/Bb", "N.C.", "Dsus4", "G7(b9)"]
)
def test_chord_symbols_use_ascii_accidentals(symbol: str) -> None:
    assert LyricChord(symbol=symbol, char_index=0).symbol == symbol


@pytest.mark.parametrize("symbol", ["H", "B♭", "C/H", "", "c", "Am/G/E"])
def test_invalid_chord_symbols_are_rejected(symbol: str) -> None:
    with pytest.raises(ValidationError):
        LyricChord(symbol=symbol, char_index=0)


def test_time_signature_pattern() -> None:
    facts = _request()["document"]["songs"][0]["facts"]

    assert SongFacts.model_validate(facts | {"timeSignature": "12/8"}).time_signature == "12/8"
    with pytest.raises(ValidationError):
        SongFacts.model_validate(facts | {"timeSignature": "4/3"})


# ---------------------------------------------------------------- ports and paths


@pytest.mark.parametrize(
    ("path", "allowed"),
    [
        (f"{_ASSET}/score.pdf", True),
        (f"{_ASSET}/tab-2.pdf", True),
        (f"{_TRACK}/artwork/auto/cover.jpg", True),
        (f"{_TRACK}/artwork/user/cover.jpg", True),
        (f"{_TRACK}/artwork/user/thumb.jpg", False),
        (f"{_TRACK}/stems/vocals.flac", False),
        (f"{_ASSET}/../secrets.pdf", False),
        (f"{_ASSET}/Score.pdf", False),
        ("/etc/passwd", False),
    ],
)
def test_handout_inputs_accept_only_notation_pdfs_and_artwork(path: str, allowed: bool) -> None:
    assert is_handout_input_path(path) is allowed


def test_handout_request_paths_are_listed() -> None:
    request = HandoutJobRequest.model_validate(_request())

    assert request.input_paths() == (
        f"{_TRACK}/artwork/auto/cover.jpg",
        f"{_ASSET}/tab.pdf",
        "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf",
    )


def test_renderer_port_is_structural() -> None:
    class Fake:
        def render(self, request: HandoutJobRequest, files: Any, progress: Any) -> RenderedHandout:
            progress(1.0)
            return RenderedHandout(pdf=b"%PDF-1.7", pages=1, outline=("Song",), warnings=())

    renderer: HandoutRenderer = Fake()
    request = HandoutJobRequest.model_validate(_request())

    assert isinstance(renderer, HandoutRenderer)
    assert renderer.render(request, {}, lambda _: None).pages == 1


def test_handout_errors_and_texts() -> None:
    assert issubclass(HandoutCancelledError, HandoutRenderError)
    assert issubclass(NotationUnreadableError, HandoutRenderError)
    assert issubclass(HandoutRenderError, WorkerError)
    assert HANDOUT_FAILED_TEXT == "The handout could not be written"
    assert notation_unreadable_text("Bass - tablature") == (
        "A notation file could not be read: Bass - tablature"
    )
    assert notation_omitted_text("Bass - tablature") == (
        "A notation file was left out because the handout reached its size limit: Bass - tablature"
    )
    assert artwork_omitted_text("Blue Bossa") == (
        "The cover picture was left out because it is not a JPEG or is larger than 40 megapixels:"
        " Blue Bossa"
    )
