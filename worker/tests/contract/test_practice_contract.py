"""Skeleton tests for the feature 014 contract code: worker API 1.8.0, practice ports, settings.

The code follows the 1.8.0 document (``specs/014-practice-flow/contracts``), which contains the
013 document (1.7.0) unchanged except for the additive ``Health``/``LanesHealth`` fields (and their
``/health`` examples) and one more allow-list line in the ``createRenderJob`` description.
"""

from datetime import UTC, datetime
from pathlib import Path
from typing import Any, get_args

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import load_settings
from stemstudio_worker.ports import (
    DETAIL_PATH_PATTERN,
    SCORE_MUSICXML_PATTERN,
    STEM_PATH_PATTERN,
    DocumentError,
    ScoreView,
    ScoreViewError,
    ScoreViewRenderer,
    SectionProgress,
    SetlistDocument,
    SetlistDocumentRenderer,
    StemFiles,
    StructureAnalyzer,
    StructureError,
    WorkerError,
    is_allowed_library_path,
)
from stemstudio_worker.schemas import (
    DOCUMENT_FAILED_TEXT,
    SCORE_VIEW_FAILED_TEXT,
    SECTION_WARNING_TEXTS,
    AnalyzerInfo,
    BarBox,
    BeatGridTempo,
    DetailPath,
    DetectedSection,
    Health,
    RenderJobStatus,
    ScoreViewJob,
    ScoreViewJobRequest,
    ScoreViewResult,
    SectionJob,
    SectionJobRequest,
    SectionKind,
    SectionResult,
    SectionWarning,
    SectionWarningCode,
    SetlistDocumentJob,
    SetlistDocumentRequest,
    SetlistSongContent,
    StemPaths,
    StructureAnalyzerName,
    StructureHealth,
    TempoGrid,
)

from .docs import WorkerDocument, changed_entries

V170 = WorkerDocument("013-library-care")
V180 = WorkerDocument("014-practice-flow")
_TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"
_ASSET = "9a8b7c6d-1111-4222-8333-444455556666"
_JOB = "0123456789abcdef0123456789abcdef"
_NOW = datetime(2026, 11, 17, 10, 0, tzinfo=UTC)
_NEW_PATHS = {
    "/v1/section-jobs",
    "/v1/section-jobs/{jobId}",
    "/v1/score-view-jobs",
    "/v1/score-view-jobs/{jobId}",
    "/v1/score-view-jobs/{jobId}/pages/{page}",
    "/v1/setlist-document-jobs",
    "/v1/setlist-document-jobs/{jobId}",
    "/v1/setlist-document-jobs/{jobId}/file",
}
# Contract schema name -> pydantic model (``Warning`` is ``SectionWarning`` in Python).
_MODELS: dict[str, type[BaseModel]] = {
    "StemPaths": StemPaths,
    "DetailPath": DetailPath,
    "SectionJobRequest": SectionJobRequest,
    "DetectedSection": DetectedSection,
    "Warning": SectionWarning,
    "SectionResult": SectionResult,
    "SectionJob": SectionJob,
    "ScoreViewJobRequest": ScoreViewJobRequest,
    "BarBox": BarBox,
    "ScoreViewResult": ScoreViewResult,
    "ScoreViewJob": ScoreViewJob,
    "SetlistSongContent": SetlistSongContent,
    "SetlistDocumentRequest": SetlistDocumentRequest,
    "SetlistDocumentJob": SetlistDocumentJob,
    "StructureHealth": StructureHealth,
}


def _section(kind: SectionKind, start: float, end: float) -> dict[str, Any]:
    return {
        "kind": kind,
        "label": kind.title(),
        "startSeconds": start,
        "endSeconds": end,
        "cluster": "A",
        "instrument": None,
        "confidence": 0.5,
    }


def _result(*sections: dict[str, Any]) -> dict[str, Any]:
    return {
        "analyzer": {"name": "fake", "version": "1.0.0"},
        "snap": "time",
        "sections": list(sections),
        "warnings": [],
    }


def _bar(index: int, page: int, start: float, end: float) -> dict[str, Any]:
    return {
        "index": index,
        "number": index + 1,
        "page": page,
        "x": 0.1,
        "y": 0.1,
        "width": 0.2,
        "height": 0.05,
        "startSeconds": start,
        "endSeconds": end,
    }


def _view(*bars: dict[str, Any]) -> dict[str, Any]:
    return {
        "pages": [{"page": 1, "widthPx": 840, "heightPx": 1188}],
        "bars": list(bars),
        "engine": {"name": "verovio", "version": "6.4.0"},
    }


def _song(position: int = 1) -> dict[str, Any]:
    return {"position": position, "title": "Song", "durationSeconds": 200.0}


def _document(*songs: dict[str, Any]) -> dict[str, Any]:
    return {
        "title": "Band night",
        "generatedAt": "2026-11-17T10:00:00Z",
        "options": {
            "includeLyrics": True,
            "includeSections": True,
            "includeNotes": True,
            "pageBreakPerSong": True,
        },
        "songs": list(songs),
    }


# ---------------------------------------------------------------- document


def test_contract_version_is_1_8_0() -> None:
    assert V180.root["info"]["version"] == "1.8.0"  # current: 1.9.0 (016)


def test_1_8_0_contains_1_7_0_except_the_additive_health_fields() -> None:
    assert changed_entries(V170, V180, "schemas") == {"Health", "LanesHealth"}
    assert changed_entries(V170, V180, "paths") == {"/health", "/v1/render-jobs"}
    for section in ("examples", "responses", "parameters"):
        assert changed_entries(V170, V180, section) == set(), section
    assert set(V180.section("paths")) - set(V170.section("paths")) == _NEW_PATHS


def test_health_and_lanes_only_gain_the_practice_fields() -> None:
    old, new = V170.schemas, V180.schemas
    for name, added in (("Health", "structure"), ("LanesHealth", "practice")):
        assert set(new[name]["properties"]) - set(old[name]["properties"]) == {added}
        assert set(new[name]["required"]) - set(old[name]["required"]) == {added}
        assert all(new[name]["properties"][k] == v for k, v in old[name]["properties"].items())


@pytest.mark.parametrize("name", sorted(_MODELS))
def test_every_new_schema_has_a_model_with_the_same_fields(name: str) -> None:
    schema = V180.schemas[name]
    fields = {f.alias or key for key, f in _MODELS[name].model_fields.items()}
    required = {f.alias or key for key, f in _MODELS[name].model_fields.items() if f.is_required()}

    assert fields == set(schema["properties"])
    assert required <= set(schema.get("required", []))


def test_warning_codes_have_texts_and_match_the_contract() -> None:
    codes = V180.schemas["Warning"]["properties"]["code"]["enum"]

    assert set(codes) == set(get_args(SectionWarningCode)) == set(SECTION_WARNING_TEXTS)
    assert all(0 < len(text) <= 300 for text in SECTION_WARNING_TEXTS.values())


def test_section_kinds_match_the_contract() -> None:
    kinds = V180.schemas["DetectedSection"]["properties"]["kind"]["enum"]

    assert kinds == list(get_args(SectionKind))


# ---------------------------------------------------------------- examples round-trip


def test_section_request_example_round_trips() -> None:
    example = V180.example("SectionJobRequestSixStems")
    model = SectionJobRequest.model_validate(example)

    assert model.model_dump(mode="json", by_alias=True, exclude_unset=True) == example
    assert model.max_sections == 64
    assert V180.errors("SectionJobRequest", example) == []


def test_completed_section_job_example_round_trips() -> None:
    example = V180.example("SectionJobCompleted")
    model = SectionJob.model_validate(example)

    dumped = model.model_dump(mode="json", by_alias=True)
    assert dumped["result"] == example["result"]
    assert dumped["status"] == "completed"
    assert V180.errors("SectionJob", dumped) == []


@pytest.mark.parametrize(
    ("example", "schema"),
    [
        ("SectionJobQueued", "SectionJob"),
        ("SectionJobFailed", "SectionJob"),
        ("ScoreViewJobRequestBass", "ScoreViewJobRequest"),
        ("ScoreViewJobCompleted", "ScoreViewJob"),
        ("SetlistDocumentRequestTwoSongs", "SetlistDocumentRequest"),
        ("SetlistDocumentJobCompleted", "SetlistDocumentJob"),
    ],
)
def test_backend_fake_examples_round_trip(example: str, schema: str) -> None:
    value = V180.example(example)
    dumped = _MODELS[schema].model_validate(value).model_dump(mode="json", by_alias=True)

    assert V180.errors(schema, value) == []
    assert dumped == value


def test_default_health_validates_against_the_document() -> None:
    health = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    assert health["lanes"]["practice"] == {"queued": 0, "running": False}
    assert health["structure"] == {"analyzer": "stems-novelty", "version": "1.0.0"}
    assert WorkerDocument("016-instrument-views").errors("Health", health) == []  # 1.9.0 fields


def test_health_examples_validate() -> None:
    for name, example in V180.health_examples().items():
        assert V180.errors("Health", example) == [], name
        assert Health.model_validate(example).structure.analyzer == "stems-novelty"


@pytest.mark.parametrize(
    ("model", "body"),
    [
        (ScoreViewResult, _view(_bar(0, 1, 0.0, 2.0), _bar(1, 1, 2.0, 4.0))),
        (SetlistDocumentRequest, _document(_song(1), _song(2))),
        (
            ScoreViewJobRequest,
            {
                "musicxml": f"assets/{_ASSET}/score.musicxml",
                "grid": {"kind": "manual", "bpm": 120, "beatsPerBar": 4, "firstDownbeatSeconds": 0},
                "durationSeconds": 30,
            },
        ),
    ],
)
def test_built_bodies_validate_in_both(model: type[BaseModel], body: dict[str, Any]) -> None:
    schema = next(name for name, m in _MODELS.items() if m is model)

    model.model_validate(body)
    assert V180.errors(schema, body) == []


# ---------------------------------------------------------------- rules


def test_section_result_must_be_contiguous() -> None:
    SectionResult.model_validate(_result(_section("intro", 0, 8), _section("verse", 8, 24)))
    with pytest.raises(ValidationError, match="contiguous"):
        SectionResult.model_validate(_result(_section("intro", 0, 8), _section("verse", 9, 24)))
    with pytest.raises(ValidationError, match="contiguous"):
        SectionResult.model_validate(_result(_section("intro", 8, 8.0)))


def test_score_view_bars_must_be_contiguous_and_on_a_listed_page() -> None:
    with pytest.raises(ValidationError, match="contiguous"):
        ScoreViewResult.model_validate(_view(_bar(0, 1, 0, 2), _bar(1, 1, 2.5, 4)))
    with pytest.raises(ValidationError, match="listed pages"):
        ScoreViewResult.model_validate(_view(_bar(0, 2, 0, 2)))


@pytest.mark.parametrize(
    "change",
    [
        {"maxSections": 65},
        {"durationSeconds": 0.5},
        {"details": [{"channel": "bass.fretless", "path": "x"}]},
        {"stems": {"vocals": "a", "drums": "b", "bass": "c"}},
        {"extra": True},
    ],
)
def test_invalid_section_requests_are_rejected(change: dict[str, Any]) -> None:
    body = {**V180.example("SectionJobRequestSixStems"), **change}

    with pytest.raises(ValidationError):
        SectionJobRequest.model_validate(body)
    assert V180.errors("SectionJobRequest", body) != []


def test_setlist_limits() -> None:
    with pytest.raises(ValidationError):
        SetlistDocumentRequest.model_validate(_document())
    with pytest.raises(ValidationError):
        SetlistDocumentRequest.model_validate(_document(*[_song(1)] * 101))
    long_line = {**_song(), "lyrics": {"lines": ["x" * 501], "shortened": False}}
    with pytest.raises(ValidationError):
        SetlistSongContent.model_validate(long_line)


# ---------------------------------------------------------------- ports and settings


def test_allow_list_has_the_score_musicxml_and_named_patterns() -> None:
    assert is_allowed_library_path(f"assets/{_ASSET}/score.musicxml")
    assert SCORE_MUSICXML_PATTERN.fullmatch(f"assets/{_ASSET}/score.musicxml")
    assert not SCORE_MUSICXML_PATTERN.fullmatch(f"assets/{_ASSET}/score.pdf")
    assert not is_allowed_library_path(f"assets/{_ASSET}/../score.musicxml")
    assert STEM_PATH_PATTERN.fullmatch(f"tracks/{_TRACK}/stems/vocals.flac")
    assert DETAIL_PATH_PATTERN.fullmatch(f"tracks/{_TRACK}/detail/guitar/electric-guitar.flac")
    assert not STEM_PATH_PATTERN.fullmatch(f"tracks/{_TRACK}/detail/guitar/electric-guitar.flac")


def test_errors_are_worker_errors_with_readable_defaults() -> None:
    assert issubclass(StructureError, WorkerError)
    assert str(ScoreViewError()) == SCORE_VIEW_FAILED_TEXT
    assert str(DocumentError()) == DOCUMENT_FAILED_TEXT
    assert str(DocumentError("DejaVu Sans is missing.")) == "DejaVu Sans is missing."


class _Analyzer:
    name: StructureAnalyzerName = "fake"
    version: str = "1.0.0"

    def analyze(
        self, request: SectionJobRequest, files: StemFiles, progress: SectionProgress
    ) -> SectionResult:
        progress(1.0)
        return SectionResult(
            analyzer=AnalyzerInfo(name="fake", version="1.0.0"),
            snap="time",
            sections=[
                DetectedSection.model_validate(_section("other", 0, request.duration_seconds))
            ],
            warnings=[],
        )


class _ScoreRenderer:
    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView:
        bar = BarBox.model_validate(_bar(0, 1, 0, duration_seconds))
        return ScoreView(pages=("<svg/>",), bars=(bar,), engine_version="6.4.0")


class _DocumentRenderer:
    def render(self, request: SetlistDocumentRequest) -> SetlistDocument:
        return SetlistDocument(pdf=b"%PDF-1.4", pages=len(request.songs))


def test_fakes_satisfy_the_protocols() -> None:
    analyzer, scores, documents = _Analyzer(), _ScoreRenderer(), _DocumentRenderer()
    assert isinstance(analyzer, StructureAnalyzer)
    assert isinstance(scores, ScoreViewRenderer)
    assert isinstance(documents, SetlistDocumentRenderer)

    seen: list[float] = []
    request = SectionJobRequest.model_validate(V180.example("SectionJobRequestSixStems"))
    result = analyzer.analyze(request, {"vocals": Path("v.flac")}, seen.append)
    view = scores.render(
        b"<score-partwise/>",
        BeatGridTempo.model_validate(
            {"bpm": 120, "beatsPerBar": 4, "beats": [0, 0.5], "downbeats": [0]}
        ),
        4.0,
    )
    document = documents.render(SetlistDocumentRequest.model_validate(_document(_song())))

    assert (seen, result.sections[0].end_seconds) == ([1.0], 64)
    assert (view.page_width_px, view.page_height_px, view.bars[0].end_seconds) == (840, 1188, 4.0)
    assert document.pages == 1


@pytest.mark.parametrize(
    ("env", "expected"),
    [
        ({}, "real"),
        ({"STEMSTUDIO_SEPARATOR": "fake"}, "fake"),
        ({"STEMSTUDIO_SEPARATOR": "fake", "STEMSTUDIO_STRUCTURE_ANALYZER": "real"}, "real"),
        ({"STEMSTUDIO_STRUCTURE_ANALYZER": "FAKE"}, "fake"),
    ],
)
def test_structure_analyzer_setting(env: dict[str, str], expected: str) -> None:
    assert load_settings(env).structure_analyzer == expected


def test_structure_analyzer_setting_rejects_unknown_values() -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_STRUCTURE_ANALYZER"):
        load_settings({"STEMSTUDIO_STRUCTURE_ANALYZER": "allin1"})


def test_job_models_default_to_no_error() -> None:
    job = SetlistDocumentJob(
        job_id=_JOB, status=RenderJobStatus.QUEUED, progress=0, created_at=_NOW
    )

    assert (job.error, job.pages) == (None, None)
    assert (
        ScoreViewJob(
            job_id=_JOB, status=RenderJobStatus.RUNNING, progress=0.5, created_at=_NOW
        ).result
        is None
    )
