"""Skeleton tests for the feature 006 contract code: worker API 1.5.0, ports, settings.

Features 006 and 008 share one Phase 0: the code is at 1.6.0 (the 008 document). The 007
document must be contained in the 1.5.0 document and the 1.5.0 document in the 1.6.0 document
unchanged except for ``/health`` (``Health``, ``LanesHealth``), so an owner implementing against
the 006 document and one implementing against the 008 document produce one consistent system.
"""

from pathlib import Path
from typing import Any, get_args

import numpy as np
import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import LyricsSettings, WorkerSettings, load_settings
from stemstudio_worker.ports import (
    ALIGN_SAMPLE_RATE,
    COLLAPSE_OVERLAP_SHARE,
    COLLAPSE_SHORT_WORD_MS,
    COLLAPSE_SHORT_WORD_SHARE,
    WHISPER_GPU_NEED,
    WHISPER_TURBO_WEIGHTS,
    Aligner,
    AlignmentCancelledError,
    AlignmentError,
    AlignRequest,
    LyricsError,
    LyricsLookup,
    LyricsQuery,
    LyricsUnavailableError,
    LyricsUnusableError,
    ProgressCallback,
    WorkerError,
    estimate_alignment_seconds,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_NOT_CHECKED,
    LYRICS_REQUEST,
    LYRICS_UNREACHABLE_TEXT,
    LYRICS_UNUSABLE_TEXT,
    VOCAL_STEM_SILENT_TEXT,
    AlignAuto,
    AlignLyricsRequest,
    AlignModelName,
    Health,
    LookupLyricsRequest,
    LyricsAlignmentHealth,
    LyricsJob,
    LyricsJobMode,
    LyricsJobState,
    LyricsOrigin,
    LyricsOutcome,
    LyricsResult,
    LyricsSource,
    SyncLevel,
    TimedLyrics,
    WordOrigin,
)

from .docs import WorkerDocument, changed_entries

V150 = WorkerDocument("006-synced-lyrics-karaoke")
_JOB = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c"


def _errors(name: str, instance: object) -> list[str]:
    return V150.errors(name, instance)


def _example(name: str) -> Any:
    return V150.example(name)


def _document_dump(example: str) -> dict[str, Any]:
    return _example(example)["lyrics"]  # type: ignore[no-any-return]


def test_document_versions() -> None:
    versions = [
        WorkerDocument(f).root["info"]["version"] for f in ("007-export-library", "008-sheet-music")
    ]

    assert (versions[0], V150.root["info"]["version"], versions[1]) == ("1.4.0", "1.5.0", "1.6.0")


@pytest.mark.parametrize("section", ["paths", "schemas", "responses", "examples", "parameters"])
@pytest.mark.parametrize(
    ("older", "newer", "allowed"),
    [
        ("007-export-library", "006-synced-lyrics-karaoke", ["/health", "Health"]),
        ("006-synced-lyrics-karaoke", "008-sheet-music", ["/health", "Health", "LanesHealth"]),
    ],
)
def test_newer_documents_contain_older_ones_unchanged_except_health(
    section: str, older: str, newer: str, allowed: list[str]
) -> None:
    assert changed_entries(WorkerDocument(older), WorkerDocument(newer), section) <= set(allowed)


@pytest.mark.parametrize(
    ("alias", "schema"),
    [
        (LyricsJobMode, "LyricsJobMode"),
        (LyricsOutcome, "LyricsOutcome"),
        (SyncLevel, "SyncLevel"),
        (WordOrigin, "WordOrigin"),
        (LyricsOrigin, "LyricsOrigin"),
        (LyricsSource, "LyricsSource"),
    ],
)
def test_literals_match_the_contract(alias: object, schema: str) -> None:
    assert list(get_args(alias)) == V150.schemas[schema]["enum"]


def test_health_literals_match_the_contract() -> None:
    properties = V150.schemas["LyricsAlignmentHealth"]["properties"]

    assert list(get_args(AlignModelName)) == properties["model"]["enum"]
    assert list(get_args(AlignAuto)) == properties["auto"]["enum"]


def test_job_states_match_the_contract() -> None:
    enum = V150.schemas["LyricsJobState"]["enum"]

    assert [state.value for state in LyricsJobState] == enum
    assert [state.is_terminal for state in LyricsJobState] == [False, False, True, True, True]


@pytest.mark.parametrize(
    ("example", "model"),
    [
        ("LookupLyricsRequest", LookupLyricsRequest),
        ("AlignLyricsRequest", AlignLyricsRequest),
        ("QueuedLyricsLookupJob", LyricsJob),
        ("QueuedLyricsAlignJob", LyricsJob),
        ("RunningLyricsAlignJob", LyricsJob),
        ("CompletedLyricsLookupJob", LyricsJob),
        ("FailedLyricsAlignJob", LyricsJob),
        ("CancelledLyricsAlignJob", LyricsJob),
        ("WordSyncedLyricsResult", LyricsResult),
        ("LineSyncedLyricsResult", LyricsResult),
        ("PlainLyricsResult", LyricsResult),
        ("AlignedLyricsResult", LyricsResult),
        ("InstrumentalLyricsResult", LyricsResult),
        ("NotFoundLyricsResult", LyricsResult),
        ("DisabledLyricsResult", LyricsResult),
    ],
)
def test_examples_round_trip(example: str, model: type[BaseModel]) -> None:
    V150.assert_round_trip(example, model)


@pytest.mark.parametrize(
    ("example", "mode"), [("LookupLyricsRequest", "lookup"), ("AlignLyricsRequest", "align")]
)
def test_requests_are_selected_by_mode(example: str, mode: str) -> None:
    request = LYRICS_REQUEST.validate_python(_example(example))

    assert request.mode == mode
    assert _errors("CreateLyricsJobRequest", _example(example)) == []


@pytest.mark.parametrize(
    "body",
    [
        {"mode": "lookup", "title": "Fake Song"},
        {"mode": "align", "standardJobId": _JOB},
        {"mode": "align", "standardJobId": _JOB, "text": ""},
        {"mode": "align", "standardJobId": _JOB, "text": "x" * 20_001},
        {"mode": "align", "standardJobId": _JOB, "text": "ok", "language": "EN"},
        {"mode": "dance", "title": "Fake Song", "durationSeconds": 10},
    ],
)
def test_invalid_requests_are_rejected(body: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        LYRICS_REQUEST.validate_python(body)
    assert _errors("CreateLyricsJobRequest", body) != []


def test_control_characters_are_rejected_by_the_model() -> None:
    with pytest.raises(ValidationError, match="control characters"):
        AlignLyricsRequest(standard_job_id=_JOB, text="bad\x07bell")


def test_align_text_allows_tabs_and_line_breaks() -> None:
    request = AlignLyricsRequest(standard_job_id=_JOB, text="one\ttwo\r\nthree")

    assert request.text == "one\ttwo\r\nthree"


def test_refine_must_be_line_synced() -> None:
    word = TimedLyrics.model_validate(_document_dump("WordSyncedLyricsResult"))
    line = TimedLyrics.model_validate(_document_dump("LineSyncedLyricsResult"))

    assert AlignLyricsRequest(standard_job_id=_JOB, text="a", refine=line).refine == line
    with pytest.raises(ValidationError, match="line-synced"):
        AlignLyricsRequest(standard_job_id=_JOB, text="a", refine=word)


def _with_word(change: dict[str, Any]) -> dict[str, Any]:
    document = _document_dump("WordSyncedLyricsResult")
    words = document["lines"][0]["words"]
    words[1] = {**words[1], **change}
    return document


@pytest.mark.parametrize(
    "change",
    [
        {"startMs": None},
        {"endMs": 11400},
        {"origin": None},
        {"startMs": 10000},
        {"endMs": 13300},
        {"startMs": 11400, "endMs": 14500},
    ],
    ids=[
        "missing-start",
        "zero-length",
        "no-origin",
        "before-previous",
        "outside-line",
        "too-long",
    ],
)
def test_timed_document_invariants(change: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        TimedLyrics.model_validate(_with_word(change))


def test_line_starts_must_not_decrease() -> None:
    document = _document_dump("WordSyncedLyricsResult")
    document["lines"].reverse()

    with pytest.raises(ValidationError, match="must not decrease"):
        TimedLyrics.model_validate(document)


def test_plain_documents_have_no_times() -> None:
    document = _document_dump("PlainLyricsResult")
    document["lines"][1]["words"][0]["startMs"] = 14000

    with pytest.raises(ValidationError, match="no times"):
        TimedLyrics.model_validate(document)


def test_instrumental_documents_have_no_lines() -> None:
    document = {
        **_document_dump("InstrumentalLyricsResult"),
        "lines": _document_dump("PlainLyricsResult")["lines"],
    }

    with pytest.raises(ValidationError, match="instrumental"):
        TimedLyrics.model_validate(document)


@pytest.mark.parametrize(
    ("outcome", "example"),
    [
        ("found", None),
        ("not-found", "WordSyncedLyricsResult"),
        ("instrumental", "WordSyncedLyricsResult"),
        ("found", "InstrumentalLyricsResult"),
    ],
)
def test_result_outcome_matches_the_document(outcome: str, example: str | None) -> None:
    lyrics = None if example is None else _document_dump(example)

    with pytest.raises(ValidationError, match="carry a document"):
        LyricsResult.model_validate({"outcome": outcome, "lyrics": lyrics})


def test_health_examples_and_default() -> None:
    examples = [example["lyricsAlignment"] for example in V150.health_examples().values()]
    body = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    dumped = [LyricsAlignmentHealth.model_validate(e).model_dump(by_alias=True) for e in examples]

    assert dumped == examples
    assert body["lyricsAlignment"] == ALIGNMENT_NOT_CHECKED.model_dump(mode="json", by_alias=True)
    assert body["lyricsAlignment"]["available"] is False


def test_fixed_texts_match_the_examples() -> None:
    assert _example("FailedLyricsAlignJob")["error"] == VOCAL_STEM_SILENT_TEXT
    assert str(LyricsUnavailableError()) == LYRICS_UNREACHABLE_TEXT
    assert str(LyricsUnusableError()) == LYRICS_UNUSABLE_TEXT
    for error in (
        LyricsUnavailableError,
        LyricsUnusableError,
        AlignmentError,
        AlignmentCancelledError,
    ):
        assert issubclass(error, LyricsError)
    assert issubclass(LyricsError, WorkerError)


def test_whisper_constants_follow_the_phase_0_measurement() -> None:
    assert WHISPER_GPU_NEED.memory_mib == 5120
    assert WHISPER_GPU_NEED.memory_mib % 256 == 0
    assert WHISPER_TURBO_WEIGHTS.size_bytes == 1_617_941_637
    assert WHISPER_TURBO_WEIGHTS.sha256 in WHISPER_TURBO_WEIGHTS.url
    assert ALIGN_SAMPLE_RATE == 16_000
    assert (COLLAPSE_SHORT_WORD_MS, COLLAPSE_SHORT_WORD_SHARE, COLLAPSE_OVERLAP_SHARE) == (
        20,
        0.35,
        0.25,
    )


@pytest.mark.parametrize(
    ("device", "cached", "expected"),
    [("gpu", True, 28.0), ("gpu", False, 168.0), ("cpu", True, 225.0), ("cpu", False, 365.0)],
)
def test_alignment_estimate(device: str, cached: bool, expected: float) -> None:
    assert estimate_alignment_seconds(262.0, device, model_cached=cached) == expected  # type: ignore[arg-type]


class _Lookup:
    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        progress(1.0, "Searching lyrics")
        return LyricsResult(outcome="not-found")


class _Aligner:
    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:
        return TimedLyrics(
            sync_level="plain", origin="none", source="user", plain_text=request.text
        )


def test_ports_accept_simple_fakes() -> None:
    lookup: LyricsLookup = _Lookup()
    aligner: Aligner = _Aligner()
    stages: list[str] = []
    request = AlignRequest(audio=np.zeros(ALIGN_SAMPLE_RATE, dtype=np.float32), text="La")

    result = lookup.lookup(
        LyricsQuery(title="Fake Song", duration_seconds=10.0), lambda _f, s: stages.append(s)
    )

    assert result.outcome == "not-found"
    assert stages == ["Searching lyrics"]
    assert aligner.align(request, "cpu", lambda _f, _s: None).plain_text == "La"
    assert request.language is None
    assert request.refine is None


def test_settings_defaults_and_environment() -> None:
    assert WorkerSettings(data_dir=Path("d")).lyrics == LyricsSettings()
    loaded = load_settings(
        {
            "STEMSTUDIO_ALIGN_MODEL": "small",
            "STEMSTUDIO_ALIGN_AUTO": "Never",
            "STEMSTUDIO_FAKE_LYRICS": "plain",
        }
    ).lyrics

    assert loaded == LyricsSettings(align_model="small", align_auto="never", fake_mode="plain")


@pytest.mark.parametrize(
    "name", ["STEMSTUDIO_ALIGN_MODEL", "STEMSTUDIO_ALIGN_AUTO", "STEMSTUDIO_FAKE_LYRICS"]
)
def test_settings_reject_unknown_values(name: str) -> None:
    with pytest.raises(ValueError, match=name):
        load_settings({name: "bogus"})
