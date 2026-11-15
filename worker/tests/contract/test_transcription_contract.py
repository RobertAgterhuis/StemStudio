"""Skeleton tests for the feature 008 contract code: worker API 1.6.0, ports, settings.

The code follows the 1.6.0 document (``specs/008-sheet-music/contracts``), which contains the
006 document (1.5.0) unchanged except for ``/health`` (see ``test_lyrics_contract.py``).
"""

from pathlib import Path
from typing import Any, get_args

import pytest
from pydantic import BaseModel, TypeAdapter, ValidationError

from stemstudio_worker.config import TranscriptionSettings, WorkerSettings, load_settings
from stemstudio_worker.ports import (
    BASIC_PITCH_WEIGHTS,
    PIANO_GPU_NEED,
    PIANO_WEIGHTS,
    WHISPER_GPU_NEED,
    DrumHit,
    EngravedScore,
    NoNotesError,
    NotationSettings,
    NoteEvent,
    ScoreDocument,
    Transcription,
    TranscriptionCancelledError,
    TranscriptionError,
    WorkerError,
    is_allowed_library_path,
)
from stemstudio_worker.schemas import (
    BASIC_PITCH_MODEL_ID,
    CONTRACT_VERSION,
    NO_NOTES_TEXT,
    PIANO_MODEL_ID,
    SCORE_FILES,
    SCORE_WARNING_TEXTS,
    BeatGridTempo,
    DrumInputs,
    DrumPiece,
    Health,
    KeyName,
    ManualTempo,
    ModelReadiness,
    ScoreEngine,
    ScoreFileRole,
    ScoreProfile,
    ScoreQuality,
    ScoreResolution,
    ScoreSummary,
    ScoreTitles,
    ScoreWarningCode,
    TempoGrid,
    TranscriptionHealth,
    TranscriptionJob,
    TranscriptionJobRequest,
    TranscriptionResult,
    TranscriptionStage,
)

from .docs import WorkerDocument

V160 = WorkerDocument("008-sheet-music")
_TRACK = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"


def _errors(name: str, instance: object) -> list[str]:
    return V160.errors(name, instance)


def _example(name: str) -> Any:
    return V160.example(name)


def test_contract_version_is_1_6_0() -> None:
    assert CONTRACT_VERSION == V160.root["info"]["version"] == "1.6.0"


@pytest.mark.parametrize(
    ("alias", "schema"),
    [
        (ScoreProfile, "ScoreProfile"),
        (ScoreQuality, "ScoreQuality"),
        (ScoreResolution, "ScoreResolution"),
        (KeyName, "KeyName"),
        (TranscriptionStage, "TranscriptionStage"),
        (ScoreFileRole, "ScoreFileRole"),
        (ScoreWarningCode, "ScoreWarningCode"),
        (ScoreEngine, "ScoreEngine"),
        (ModelReadiness, "ModelReadiness"),
    ],
)
def test_literals_match_the_contract(alias: object, schema: str) -> None:
    assert list(get_args(alias)) == V160.schemas[schema]["enum"]


def test_drum_pieces_and_files_match_the_contract() -> None:
    drum = V160.schemas["DrumInputs"]["properties"]
    file = V160.schemas["TranscriptionFile"]["properties"]

    assert list(get_args(DrumPiece)) == list(drum)
    assert [name for name, _ in SCORE_FILES.values()] == file["fileName"]["enum"]
    assert [media for _, media in SCORE_FILES.values()] == file["contentType"]["enum"]
    assert set(SCORE_WARNING_TEXTS) == set(get_args(ScoreWarningCode))


@pytest.mark.parametrize(
    ("example", "model"),
    [
        ("BassTranscriptionRequest", TranscriptionJobRequest),
        ("DrumsTranscriptionRequest", TranscriptionJobRequest),
        ("PianoTranscriptionRequest", TranscriptionJobRequest),
        ("QueuedTranscriptionJob", TranscriptionJob),
        ("RunningTranscriptionJob", TranscriptionJob),
        ("CompletedTranscriptionJob", TranscriptionJob),
        ("FailedTranscriptionJob", TranscriptionJob),
    ],
)
def test_examples_round_trip(example: str, model: type[BaseModel]) -> None:
    V160.assert_round_trip(example, model)


def test_example_paths_pass_the_library_guard() -> None:
    requests = [
        TranscriptionJobRequest.model_validate(_example(name))
        for name in (
            "BassTranscriptionRequest",
            "DrumsTranscriptionRequest",
            "PianoTranscriptionRequest",
        )
    ]
    paths = [path for request in requests for path in request.input_paths()]

    assert len(paths) == 7
    assert all(is_allowed_library_path(path) for path in paths)


def test_failed_example_uses_the_fixed_text() -> None:
    assert _example("FailedTranscriptionJob")["error"] == NO_NOTES_TEXT == str(NoNotesError())


def _bass(**change: Any) -> dict[str, Any]:
    return {**_example("BassTranscriptionRequest"), **change}


@pytest.mark.parametrize(
    "body",
    [
        _bass(input=None),
        _bass(drumInputs={"kick": f"tracks/{_TRACK}/detail/drums/kick.flac"}),
        _bass(profile="drums"),
        _bass(profile="drums", input=None, drumInputs={}),
        _bass(keyOverride="H major"),
        _bass(
            grid={
                "kind": "beat-grid",
                "bpm": 120,
                "beatsPerBar": 4,
                "beats": [1.0, 0.5],
                "downbeats": [0.5],
            }
        ),
        _bass(grid={"kind": "manual", "bpm": 20, "beatsPerBar": 4, "firstDownbeatSeconds": 0}),
        _bass(grid={"kind": "manual", "bpm": 120, "beatsPerBar": 13, "firstDownbeatSeconds": 0}),
        _bass(grid={"kind": "swing", "bpm": 120}),
        _bass(titles={"title": ""}),
        _bass(range={"startSeconds": 3.0, "endSeconds": 3.2}),
    ],
    ids=[
        "pitched-without-input",
        "pitched-with-drums",
        "drums-with-input",
        "drums-without-pieces",
        "unknown-key",
        "beats-not-ascending",
        "slow-tempo",
        "too-many-beats-per-bar",
        "unknown-grid",
        "empty-title",
        "short-range",
    ],
)
def test_invalid_requests_are_rejected(body: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        TranscriptionJobRequest.model_validate(body)


def test_drum_inputs_keep_the_piece_order_and_skip_missing_pieces() -> None:
    inputs = DrumInputs(cymbals="c.flac", kick="k.flac")

    assert inputs.pieces() == {"kick": "k.flac", "cymbals": "c.flac"}
    assert list(inputs.pieces()) == ["kick", "cymbals"]


def test_tempo_grid_is_selected_by_kind() -> None:
    adapter: TypeAdapter[BeatGridTempo | ManualTempo] = TypeAdapter(TempoGrid)
    manual = adapter.validate_python(
        {"kind": "manual", "bpm": 96, "beatsPerBar": 3, "firstDownbeatSeconds": 0.4}
    )
    beats = {"bpm": 120, "beatsPerBar": 4, "beats": [-0.5, 0.0], "downbeats": [0.0]}
    grid = adapter.validate_python({"kind": "beat-grid", **beats})

    assert isinstance(manual, ManualTempo)
    assert isinstance(grid, BeatGridTempo)
    assert grid.beats == [-0.5, 0.0]


def test_request_without_inputs_lists_no_paths() -> None:
    request = TranscriptionJobRequest.model_construct(input=None, drum_inputs=None)  # type: ignore[call-arg]

    assert request.input_paths() == []


def test_result_files_must_follow_the_fixed_order() -> None:
    result = _example("CompletedTranscriptionJob")["result"]
    result["files"].reverse()

    with pytest.raises(ValidationError, match="pdf, musicxml, midi"):
        TranscriptionResult.model_validate(result)


def test_summary_accepts_drums_without_key() -> None:
    summary = {
        **_example("CompletedTranscriptionJob")["result"]["summary"],
        "profile": "drums",
        "key": None,
    }

    assert ScoreSummary.model_validate(summary).key is None
    assert _errors("ScoreSummary", summary) == []


@pytest.mark.parametrize("name", ["cpu", "gpu", "gpuUnavailable"])
def test_health_examples_round_trip(name: str) -> None:
    example = V160.health_examples()[name]

    assert Health.model_validate(example).model_dump(mode="json", by_alias=True) == example


def test_default_health_is_valid_and_reports_nothing_ready() -> None:
    body = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    assert body["transcription"] == TranscriptionHealth().model_dump(by_alias=True)
    assert body["transcription"]["models"] == {
        BASIC_PITCH_MODEL_ID: "not-downloaded",
        PIANO_MODEL_ID: "not-downloaded",
    }
    assert body["lanes"]["transcribe"] == {"queued": 0, "running": False}
    assert _errors("Health", body) == []


def test_weights_and_gpu_needs_follow_the_phase_0_measurements() -> None:
    assert (BASIC_PITCH_WEIGHTS.model_id, BASIC_PITCH_WEIGHTS.size_bytes) == (
        BASIC_PITCH_MODEL_ID,
        230_444,
    )
    assert (PIANO_WEIGHTS.model_id, PIANO_WEIGHTS.size_bytes) == (PIANO_MODEL_ID, 171_966_578)
    assert BASIC_PITCH_WEIGHTS.url.startswith(
        "https://raw.githubusercontent.com/spotify/basic-pitch/v0.4.0/"
    )
    assert PIANO_GPU_NEED.memory_mib == 1280
    assert PIANO_GPU_NEED.memory_mib % 256 == WHISPER_GPU_NEED.memory_mib % 256 == 0


def test_errors_form_one_hierarchy() -> None:
    assert issubclass(NoNotesError, TranscriptionError)
    assert issubclass(TranscriptionCancelledError, TranscriptionError)
    assert issubclass(TranscriptionError, WorkerError)


def test_notation_settings_shape() -> None:
    grid = ManualTempo(bpm=120, beats_per_bar=4, first_downbeat_seconds=0)
    titles = ScoreTitles(title="Fake Song - bass (sheet music)")
    settings = NotationSettings("bass", grid, "sixteenth", None, titles, 10.0)

    assert settings.titles.subtitle is None
    assert settings.grid.kind == "manual"


def test_port_value_shapes() -> None:
    document = ScoreDocument(b"<score/>", b"MThd", None, 120, "4/4", 5, 8)

    assert Transcription(notes=(NoteEvent(0.0, 0.5, 40, 0.8),)).device is None
    assert DrumHit(0.5, "snare", 0.9).piece == "snare"
    assert document.warnings == ()
    assert EngravedScore(pdf=b"%PDF", pages=1).pages == 1


def test_settings_defaults_and_environment() -> None:
    assert WorkerSettings(data_dir=Path("d")).transcription == TranscriptionSettings()
    assert load_settings({}).transcription == TranscriptionSettings(transcriber="real", threads=8)
    assert load_settings({"STEMSTUDIO_SEPARATOR": "fake"}).transcription.transcriber == "fake"
    loaded = load_settings(
        {
            "STEMSTUDIO_SEPARATOR": "fake",
            "STEMSTUDIO_TRANSCRIBER": "Real",
            "STEMSTUDIO_TRANSCRIBE_THREADS": "16",
        }
    )

    assert loaded.transcription == TranscriptionSettings(transcriber="real", threads=16)


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("STEMSTUDIO_TRANSCRIBER", "onnx"),
        ("STEMSTUDIO_TRANSCRIBE_THREADS", "0"),
        ("STEMSTUDIO_TRANSCRIBE_THREADS", "65"),
        ("STEMSTUDIO_TRANSCRIBE_THREADS", "eight"),
    ],
)
def test_settings_reject_invalid_values(name: str, value: str) -> None:
    with pytest.raises(ValueError, match=name):
        load_settings({name: value})
