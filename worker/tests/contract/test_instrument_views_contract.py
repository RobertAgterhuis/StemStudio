"""Skeleton tests for the 016 contract code: worker API 1.9.0, instrument-view ports, settings.

The code follows the 1.9.0 document (``specs/016-instrument-views/contracts``), which contains the
014 document (1.8.0) unchanged except for the additive score, ``Health`` and ``LanesHealth`` fields
(and their examples).
"""

from typing import Any, get_args

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import InstrumentAnalysisSettings, load_settings
from stemstudio_worker.ports import (
    BTC_GPU_NEED,
    BTC_LARGE_VOCA_WEIGHTS,
    BTC_MAJMIN_WEIGHTS,
    CHORD_HOP,
    CHORD_SAMPLE_RATE,
    GM_DRUM_MAP,
    TUNINGS,
    WRITTEN_INSTRUMENTS,
    AnalysisCancelledError,
    AnalysisError,
    NoMelodyError,
    WorkerError,
)
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    CONTRACT_VERSION,
    INSTRUMENT_ANALYSIS_REQUEST,
    NO_MELODY_TEXT,
    SCORE_WARNING_TEXTS,
    AnalysisKey,
    AnalysisWarning,
    AnalysisWarningCode,
    ChordChart,
    ChordQuality,
    ChordSegment,
    ChordsJobRequest,
    DrumHitEvent,
    DrumHits,
    DrumHitsJobRequest,
    Health,
    InstrumentAnalysisHealth,
    InstrumentAnalysisJob,
    KeyRegion,
    PartSettings,
    PartSummary,
    ScoreChordSymbol,
    ScoreProfile,
    ScoreSummary,
    ScoreWarningCode,
    TablatureSettings,
    TablatureSummary,
    TranscriptionJobRequest,
    TuningId,
    VocalPitch,
    VocalPitchJobRequest,
    WrittenInstrument,
)

from .docs import WorkerDocument, changed_entries

V180 = WorkerDocument("014-practice-flow")
V190 = WorkerDocument("016-instrument-views")
_JOB = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8"
_STEM = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/guitar.flac"
_KICK = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail/drums/kick.flac"
_MODELS: dict[str, type[BaseModel]] = {
    "ChordsJobRequest": ChordsJobRequest,
    "DrumHitsJobRequest": DrumHitsJobRequest,
    "VocalPitchJobRequest": VocalPitchJobRequest,
    "InstrumentAnalysisJob": InstrumentAnalysisJob,
    "ChordSegment": ChordSegment,
    "AnalysisKey": AnalysisKey,
    "KeyRegion": KeyRegion,
    "AnalysisWarning": AnalysisWarning,
    "ChordChart": ChordChart,
    "DrumHitEvent": DrumHitEvent,
    "DrumHits": DrumHits,
    "VocalPitch": VocalPitch,
    "TablatureSettings": TablatureSettings,
    "PartSettings": PartSettings,
    "ScoreChordSymbol": ScoreChordSymbol,
    "TablatureSummary": TablatureSummary,
    "PartSummary": PartSummary,
    "InstrumentAnalysisHealth": InstrumentAnalysisHealth,
}
_CHANGED = {
    "TranscriptionJobRequest",
    "ScoreSummary",
    "ScoreProfile",
    "ScoreWarningCode",
    "Health",
    "LanesHealth",
}


def _segment(start: int, end: int, label: str = "C:maj") -> dict[str, Any]:
    root = None if label == "N" else 0
    quality = None if label == "N" else "maj"
    return {
        "startMs": start,
        "endMs": end,
        "label": label,
        "root": root,
        "quality": quality,
        "bass": None,
        "confidence": 0.9,
    }


def _chart(
    *segments: dict[str, Any], regions: list[dict[str, Any]] | None = None
) -> dict[str, Any]:
    return {
        "vocabulary": "simple",
        "engine": "btc",
        "segments": list(segments),
        "keyRegions": regions or [],
    }


def _score_request(profile: ScoreProfile, **extra: Any) -> dict[str, Any]:
    body: dict[str, Any] = {
        "profile": profile,
        "grid": {"kind": "manual", "bpm": 120, "beatsPerBar": 4, "firstDownbeatSeconds": 0},
        "titles": {"title": "Song", "subtitle": None, "instrumentName": "Part"},
        **extra,
    }
    if profile == "drums":
        body["drumInputs"] = {"kick": _KICK}
    else:
        body["input"] = {"path": _STEM}
    return body


# ---------------------------------------------------------------- document


def test_contract_version_is_1_9_0() -> None:
    # 020 Phase 0: the code follows 1.11.0, which contains 1.9.0 (test_insights_contract.py).
    assert V190.root["info"]["version"] == "1.9.0"
    assert CONTRACT_VERSION == "1.11.0"


def test_1_9_0_contains_1_8_0_except_the_additive_fields() -> None:
    assert changed_entries(V180, V190, "schemas") == _CHANGED
    assert changed_entries(V180, V190, "paths") == {"/health"}
    assert changed_entries(V180, V190, "examples") == {"CompletedTranscriptionJob"}
    assert changed_entries(V180, V190, "responses") == set()
    new_paths = set(V190.section("paths")) - set(V180.section("paths"))
    assert new_paths == {
        "/v1/instrument-analysis-jobs",
        "/v1/instrument-analysis-jobs/{analysisJobId}",
    }


@pytest.mark.parametrize("name", sorted(_MODELS))
def test_every_new_schema_has_a_model_with_the_same_fields(name: str) -> None:
    schema = V190.schemas[name]
    fields = {f.alias or key for key, f in _MODELS[name].model_fields.items()}
    required = {f.alias or key for key, f in _MODELS[name].model_fields.items() if f.is_required()}

    assert fields == set(schema["properties"])
    assert required <= set(schema.get("required", []))


@pytest.mark.parametrize(
    ("model", "schema"),
    [
        (TranscriptionJobRequest, "TranscriptionJobRequest"),
        (ScoreSummary, "ScoreSummary"),
        (Health, "Health"),
    ],
)
def test_extended_schemas_match_their_models(model: type[BaseModel], schema: str) -> None:
    fields = {f.alias or key for key, f in model.model_fields.items()}
    later = {"insights"} if model is Health else set()  # 1.11.0 (020)
    assert fields == set(V190.schemas[schema]["properties"]) | later


@pytest.mark.parametrize(
    ("literal", "schema"),
    [
        (ScoreProfile, "ScoreProfile"),
        (ScoreWarningCode, "ScoreWarningCode"),
        (ChordQuality, "ChordQuality"),
        (TuningId, "TuningId"),
        (WrittenInstrument, "WrittenInstrument"),
    ],
)
def test_enums_match_the_contract(literal: Any, schema: str) -> None:
    assert list(get_args(literal)) == V190.schemas[schema]["enum"]


def test_warning_texts_cover_every_code() -> None:
    assert set(ANALYSIS_WARNING_TEXTS) == set(get_args(AnalysisWarningCode))
    assert (
        list(get_args(AnalysisWarningCode))
        == V190.schemas["AnalysisWarning"]["properties"]["code"]["enum"]
    )
    assert set(SCORE_WARNING_TEXTS) == set(get_args(ScoreWarningCode))
    assert all(0 < len(text) <= 300 for text in [*ANALYSIS_WARNING_TEXTS.values(), NO_MELODY_TEXT])


# ---------------------------------------------------------------- examples


@pytest.mark.parametrize("name", ["chords", "drumHits", "vocalPitch"])
def test_request_examples_validate(name: str) -> None:
    operation = V190.section("paths")["/v1/instrument-analysis-jobs"]["post"]
    example = operation["requestBody"]["content"]["application/json"]["examples"][name]["value"]

    request = INSTRUMENT_ANALYSIS_REQUEST.validate_python(example)

    assert request.kind == example["kind"]
    assert request.input_paths()


@pytest.mark.parametrize("name", ["runningChords", "completedChords", "failedPitch"])
def test_job_examples_validate(name: str) -> None:
    operation = V190.section("paths")["/v1/instrument-analysis-jobs/{analysisJobId}"]["get"]
    example = operation["responses"]["200"]["content"]["application/json"]["examples"][name][
        "value"
    ]

    job = InstrumentAnalysisJob.model_validate(example)

    assert job.model_dump(by_alias=True, mode="json", exclude_none=False) == example


@pytest.mark.parametrize("name", ["cpu", "gpu"])
def test_health_examples_validate(name: str) -> None:
    operation = V190.section("paths")["/health"]["get"]
    example = operation["responses"]["200"]["content"]["application/json"]["examples"][name][
        "value"
    ]

    health = Health.model_validate(example)

    assert health.instrument_analysis.models.btc_majmin == "ready"
    assert health.lanes.instrument_analysis.running is False


def test_health_defaults_report_the_lane_and_engines() -> None:
    body = Health(separator="fake", device="cpu").model_dump(by_alias=True, mode="json")

    assert body["lanes"]["instrumentAnalysis"] == {"queued": 0, "running": False}
    assert body["instrumentAnalysis"]["models"] == {
        "btc-majmin": "not-downloaded",
        "btc-large-voca": "not-downloaded",
    }


# ---------------------------------------------------------------- validation


def test_chord_chart_needs_contiguous_segments_and_regions() -> None:
    ChordChart.model_validate(_chart(_segment(0, 500, "N"), _segment(500, 900)))

    with pytest.raises(ValidationError, match="contiguous"):
        ChordChart.model_validate(_chart(_segment(0, 500), _segment(600, 900)))
    with pytest.raises(ValidationError, match="contiguous"):
        region = {"startMs": 600, "endMs": 500, "tonic": 0, "mode": "major", "confidence": 1}
        ChordChart.model_validate(_chart(_segment(0, 500), regions=[region]))


def test_drum_hits_must_be_in_time_order() -> None:
    hit = {"piece": "kick", "strength": 1}
    DrumHits.model_validate(
        {"engine": "fake", "pieces": ["kick"], "hits": [{**hit, "timeMs": 1}, {**hit, "timeMs": 1}]}
    )

    with pytest.raises(ValidationError, match="time order"):
        DrumHits.model_validate(
            {
                "engine": "fake",
                "pieces": ["kick"],
                "hits": [{**hit, "timeMs": 2}, {**hit, "timeMs": 1}],
            }
        )


def test_vocal_pitch_limits() -> None:
    with pytest.raises(ValidationError):
        VocalPitch.model_validate(
            {"frames": [12_701], "source": "vocals", "engine": "fake", "voicedRatio": 1}
        )
    with pytest.raises(ValidationError):
        VocalPitch.model_validate(
            {"hopMs": 20, "frames": [], "source": "vocals", "engine": "fake", "voicedRatio": 0}
        )


def test_chords_request_takes_at_most_four_inputs() -> None:
    body = {"kind": "chords", "inputs": [_STEM] * 5, "vocabulary": "simple"}

    with pytest.raises(ValidationError):
        INSTRUMENT_ANALYSIS_REQUEST.validate_python(body)


@pytest.mark.parametrize(
    "extra",
    [
        {"tablature": {"tuning": "guitar-drop-d", "capo": 2, "layout": "tab-only"}, "chords": []},
        {"part": {"instrument": "alto-sax-eb", "clef": "treble"}, "preferFlats": True},
    ],
)
def test_valid_score_extensions_on_guitar(extra: dict[str, Any]) -> None:
    request = TranscriptionJobRequest.model_validate(_score_request("guitar", **extra))

    assert request.input_paths() == [_STEM]


@pytest.mark.parametrize(
    ("profile", "extra", "reason"),
    [
        ("vocals", {"tablature": {"tuning": "guitar-standard"}}, "guitar and bass only"),
        ("bass", {"tablature": {"tuning": "guitar-standard"}}, "does not match"),
        ("bass", {"tablature": {"tuning": "bass-5-standard", "capo": 1}}, "capo"),
        ("drums", {"part": {"instrument": "concert"}}, "drums"),
        ("piano", {"part": {"instrument": "concert", "clef": "bass"}}, "clef"),
        ("bass", {"chords": [{"startMs": 0, "root": 0, "quality": "maj"}]}, "chords"),
        ("drums", {"singlePiece": "snare"}, "singlePiece"),
        ("guitar", {"singlePiece": "kick"}, "singlePiece"),
    ],
)
def test_score_extension_combinations_are_rejected(
    profile: ScoreProfile, extra: dict[str, Any], reason: str
) -> None:
    with pytest.raises(ValidationError, match=reason):
        TranscriptionJobRequest.model_validate(_score_request(profile, **extra))


def test_single_piece_drums_request_is_valid() -> None:
    request = TranscriptionJobRequest.model_validate(_score_request("drums", singlePiece="kick"))

    assert request.single_piece == "kick"


def test_piano_part_with_auto_clef_is_valid() -> None:
    request = TranscriptionJobRequest.model_validate(
        _score_request("piano", part={"instrument": "horn-f"})
    )

    assert request.part is not None and request.part.clef == "auto"


# ---------------------------------------------------------------- ports and settings


def test_catalogues_match_the_contract_and_data_model() -> None:
    assert list(TUNINGS) == list(get_args(TuningId))
    assert list(WRITTEN_INSTRUMENTS) == list(get_args(WrittenInstrument))
    assert TUNINGS["guitar-drop-d"].pitches == (38, 45, 50, 55, 59, 64)
    assert all(
        len(t.pitches) == (4 if "bass-4" in t.tuning_id else 5 if "bass-5" in t.tuning_id else 6)
        for t in TUNINGS.values()
    )
    alto = WRITTEN_INSTRUMENTS["alto-sax-eb"]
    assert (alto.interval, alto.semitones, alto.default_clef) == ("M6", 9, "treble")
    assert WRITTEN_INSTRUMENTS["clarinet-eb"].semitones == -3
    assert dict(GM_DRUM_MAP) == {"kick": 36, "snare": 38, "toms": 47, "hihat": 42, "cymbals": 49}


def test_btc_weights_and_constants() -> None:
    assert BTC_MAJMIN_WEIGHTS.model_id == "btc-majmin" and BTC_MAJMIN_WEIGHTS.licence == "MIT"
    assert BTC_LARGE_VOCA_WEIGHTS.url.endswith("/test/btc_model_large_voca.pt")
    assert "2682317be668032e6e4b269ded36adaa2ad57df0" in BTC_MAJMIN_WEIGHTS.url
    assert BTC_GPU_NEED.memory_mib >= 256
    assert round(1000 * CHORD_HOP / CHORD_SAMPLE_RATE, 2) == 92.88


def test_analysis_errors() -> None:
    assert str(NoMelodyError()) == NO_MELODY_TEXT
    assert issubclass(AnalysisCancelledError, AnalysisError) and issubclass(
        AnalysisError, WorkerError
    )


@pytest.mark.parametrize(
    ("env", "expected"),
    [
        ({}, InstrumentAnalysisSettings("btc", "pyin", 4)),
        ({"STEMSTUDIO_SEPARATOR": "fake"}, InstrumentAnalysisSettings("fake", "fake", 4)),
        (
            {
                "STEMSTUDIO_CHORD_ENGINE": "chroma-hmm",
                "STEMSTUDIO_PITCH_ENGINE": "YIN",
                "STEMSTUDIO_ANALYSIS_THREADS": "8",
            },
            InstrumentAnalysisSettings("chroma-hmm", "yin", 8),
        ),
        ({"STEMSTUDIO_ANALYSIS_THREADS": " "}, InstrumentAnalysisSettings("btc", "pyin", 4)),
    ],
)
def test_instrument_analysis_settings(
    env: dict[str, str], expected: InstrumentAnalysisSettings
) -> None:
    assert load_settings(env).instrument_analysis == expected


@pytest.mark.parametrize(
    "env",
    [
        {"STEMSTUDIO_CHORD_ENGINE": "chordino"},
        {"STEMSTUDIO_ANALYSIS_THREADS": "0"},
        {"STEMSTUDIO_ANALYSIS_THREADS": "x"},
    ],
)
def test_invalid_instrument_analysis_settings(env: dict[str, str]) -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_"):
        load_settings(env)


def test_job_id_and_result_kind() -> None:
    job = InstrumentAnalysisJob.model_validate(
        {
            "analysisJobId": _JOB,
            "kind": "drum-hits",
            "status": "completed",
            "progress": 1,
            "result": {"kind": "drum-hits", "engine": "fake", "pieces": [], "hits": []},
            "createdAt": "2026-11-18T10:00:00Z",
            "updatedAt": "2026-11-18T10:00:01Z",
        }
    )

    assert isinstance(job.result, DrumHits)
