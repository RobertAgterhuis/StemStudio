"""Skeleton tests for the 020 contract code: worker API 1.11.0, insights ports, settings.

The code follows the 1.11.0 document (``specs/020-insights-handouts/contracts``), which is 016's
1.9.0 plus the 020 delta without the deferred G2 parts (architect, 020 Phase 0 T001/T003).
"""

from pathlib import Path
from typing import Any, get_args

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import load_settings
from stemstudio_worker.ports import (
    LIBRARY_PATH_PATTERNS,
    QualityAnalysisError,
    QualityAnalyzer,
    QualityCancelledError,
    QualityInputs,
    StemAudio,
    WorkerError,
    is_allowed_library_path,
    is_quality_input_path,
)
from stemstudio_worker.schemas import (
    CONTRACT_VERSION,
    INSIGHTS_NOT_CHECKED,
    ArtefactEstimate,
    ArtefactKind,
    BleedEstimate,
    DetailParentInput,
    Health,
    InsightsHealth,
    LanesHealth,
    QualityInput,
    QualityInputKind,
    QualityJob,
    QualityJobRequest,
    QualityLevel,
    QualityRating,
    QualityReasonCode,
    QualityResult,
    QualityTitles,
    Reliability,
    StemQuality,
)

from .docs import WorkerDocument, changed_entries

V190 = WorkerDocument("016-instrument-views")
V1110 = WorkerDocument("020-insights-handouts")
_TRACK = "tracks/7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f"
_STANDARD = ("vocals", "drums", "bass", "guitar", "piano", "other")
_MODELS: dict[str, type[BaseModel]] = {
    "QualityInput": QualityInput,
    "DetailParentInput": DetailParentInput,
    "QualityTitles": QualityTitles,
    "QualityJobRequest": QualityJobRequest,
    "BleedEstimate": BleedEstimate,
    "ArtefactEstimate": ArtefactEstimate,
    "StemQuality": StemQuality,
    "QualityResult": QualityResult,
    "QualityJob": QualityJob,
    "InsightsHealth": InsightsHealth,
}


def _stems(*extra: dict[str, str], skip: str = "") -> list[dict[str, str]]:
    stems = [
        {"channel": s, "path": f"{_TRACK}/stems/{s}.flac", "kind": "standard"}
        for s in _STANDARD
        if s != skip
    ]
    return [*stems, *extra]


def _request(stems: list[dict[str, str]]) -> dict[str, Any]:
    return {"stems": stems, "detailParents": [], "titles": {"title": "Song"}}


# ---------------------------------------------------------------- document


def test_contract_version_is_1_11_0() -> None:
    assert CONTRACT_VERSION == V1110.root["info"]["version"] == "1.11.0"


def test_1_11_0_contains_1_9_0_except_the_health_additions() -> None:
    assert changed_entries(V190, V1110, "schemas") == {"Health", "LanesHealth"}
    assert changed_entries(V190, V1110, "paths") == {"/health"}
    assert changed_entries(V190, V1110, "responses") == set()
    assert changed_entries(V190, V1110, "examples") == set()
    new_paths = set(V1110.section("paths")) - set(V190.section("paths"))
    assert new_paths == {
        "/v1/quality-jobs",
        "/v1/quality-jobs/{qualityJobId}",
        "/v1/handout-jobs",
        "/v1/handout-jobs/{handoutJobId}",
        "/v1/handout-jobs/{handoutJobId}/file",
    }


def test_g2_parts_are_not_merged() -> None:
    assert not any("reseparation" in path for path in V1110.section("paths"))
    assert not {"ReseparationJob", "SeparationPipelinesHealth"} & set(V1110.schemas)
    assert "separationPipelines" not in V1110.schemas["Health"]["properties"]


@pytest.mark.parametrize("name", sorted(_MODELS))
def test_every_new_schema_has_a_model_with_the_same_fields(name: str) -> None:
    schema = V1110.schemas[name]
    fields = {f.alias or key for key, f in _MODELS[name].model_fields.items()}
    required = {f.alias or key for key, f in _MODELS[name].model_fields.items() if f.is_required()}

    assert fields == set(schema["properties"])
    assert required <= set(schema.get("required", []))


@pytest.mark.parametrize(("model", "schema"), [(Health, "Health"), (LanesHealth, "LanesHealth")])
def test_extended_schemas_match_their_models(model: type[BaseModel], schema: str) -> None:
    fields = {f.alias or key for key, f in model.model_fields.items()}
    assert fields == set(V1110.schemas[schema]["properties"])


@pytest.mark.parametrize(
    ("literal", "schema"),
    [
        (QualityInputKind, "QualityInputKind"),
        (QualityLevel, "QualityLevel"),
        (Reliability, "Reliability"),
        (QualityRating, "QualityRating"),
        (QualityReasonCode, "QualityReasonCode"),
        (ArtefactKind, "ArtefactKind"),
    ],
)
def test_enums_match_the_contract(literal: Any, schema: str) -> None:
    assert list(get_args(literal)) == V1110.schemas[schema]["enum"]


# ---------------------------------------------------------------- examples


def test_quality_examples_round_trip() -> None:
    V1110.assert_round_trip("QualityRequest", QualityJobRequest)
    V1110.assert_round_trip("CompletedQualityJob", QualityJob)


@pytest.mark.parametrize("name", ["cpu", "gpu"])
def test_health_examples_validate(name: str) -> None:
    example = V1110.health_examples()[name]

    health = Health.model_validate(example)

    assert health.insights.analyzer == "real"
    assert health.lanes.insights.queued == 0


def test_health_defaults_report_the_insights_lane() -> None:
    dumped = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    assert dumped["insights"] == INSIGHTS_NOT_CHECKED.model_dump(mode="json", by_alias=True)
    assert dumped["lanes"]["insights"] == {"queued": 0, "running": False}
    assert V1110.errors("Health", dumped) == []


# ---------------------------------------------------------------- request rules


def test_quality_request_needs_each_standard_stem_once() -> None:
    kick = {"channel": "drums.kick", "path": f"{_TRACK}/detail/drums/kick.flac", "kind": "detail"}

    assert len(QualityJobRequest.model_validate(_request(_stems(kick))).stems) == 7
    with pytest.raises(ValidationError, match="six standard stems"):
        QualityJobRequest.model_validate(_request(_stems(kick, skip="piano")))
    with pytest.raises(ValidationError, match="more than once"):
        QualityJobRequest.model_validate(_request(_stems(kick, kick)))


@pytest.mark.parametrize(
    ("channel", "kind"),
    [
        ("drums.kick", "standard"),
        ("drums", "detail"),
        ("drums.kick", "rest"),
        ("drums.rest", "detail"),
    ],
)
def test_quality_input_kind_must_match_the_channel(channel: str, kind: str) -> None:
    extra = {"channel": channel, "path": f"{_TRACK}/detail/drums/kick.flac", "kind": kind}
    with pytest.raises(ValidationError):
        QualityJobRequest.model_validate(
            _request([*_stems(skip="drums" if "." not in channel else ""), extra])
        )


def test_quality_request_limits() -> None:
    many = [
        {"channel": f"other.s{i}", "path": f"{_TRACK}/detail/other/s{i}.flac", "kind": "detail"}
        for i in range(59)
    ]
    with pytest.raises(ValidationError):
        QualityJobRequest.model_validate(_request(_stems(*many)))


# ---------------------------------------------------------------- ports and paths


@pytest.mark.parametrize(
    ("path", "allowed"),
    [
        (f"{_TRACK}/stems/vocals.flac", True),
        (f"{_TRACK}/detail/drums/kick.flac", True),
        ("assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/score.pdf", False),
        (f"{_TRACK}/stems/../stems/vocals.flac", False),
    ],
)
def test_quality_inputs_accept_only_stem_paths(path: str, allowed: bool) -> None:
    assert is_quality_input_path(path) is allowed


def test_general_allow_list_is_unchanged() -> None:
    assert len(LIBRARY_PATH_PATTERNS) == 4
    assert not is_allowed_library_path("assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/score.pdf")


def test_quality_analyzer_port_is_structural(tmp_path: Path) -> None:
    class Fake:
        version = "fake-1"

        def analyze(self, inputs: QualityInputs, progress: Any) -> QualityResult:
            progress(1.0)
            return QualityResult(analyzer_version=self.version, duration_seconds=0, stems=[])

    inputs = QualityInputs(
        stems=(StemAudio("vocals", "standard", tmp_path / "v.flac"),), detail_parents=()
    )
    analyzer: QualityAnalyzer = Fake()

    assert isinstance(analyzer, QualityAnalyzer)
    assert analyzer.analyze(inputs, lambda _: None).analyzer_version == "fake-1"


def test_quality_errors() -> None:
    assert issubclass(QualityCancelledError, QualityAnalysisError)
    assert issubclass(QualityAnalysisError, WorkerError)


# ---------------------------------------------------------------- settings


@pytest.mark.parametrize(
    ("env", "analyzer", "threads"),
    [
        ({}, "real", 4),
        ({"STEMSTUDIO_SEPARATOR": "fake"}, "fake", 4),
        ({"STEMSTUDIO_SEPARATOR": "fake", "STEMSTUDIO_QUALITY_ANALYZER": "real"}, "real", 4),
        ({"STEMSTUDIO_QUALITY_ANALYZER": "FAKE", "STEMSTUDIO_INSIGHTS_THREADS": "16"}, "fake", 16),
        ({"STEMSTUDIO_INSIGHTS_THREADS": " "}, "real", 4),
    ],
)
def test_insights_settings(env: dict[str, str], analyzer: str, threads: int) -> None:
    settings = load_settings(env).insights

    assert (settings.quality_analyzer, settings.threads) == (analyzer, threads)


@pytest.mark.parametrize(
    "env",
    [
        {"STEMSTUDIO_QUALITY_ANALYZER": "gpu"},
        {"STEMSTUDIO_INSIGHTS_THREADS": "0"},
        {"STEMSTUDIO_INSIGHTS_THREADS": "17"},
        {"STEMSTUDIO_INSIGHTS_THREADS": "two"},
    ],
)
def test_invalid_insights_settings(env: dict[str, str]) -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_"):
        load_settings(env)
