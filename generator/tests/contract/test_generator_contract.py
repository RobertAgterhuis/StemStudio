"""Skeleton contract tests (architect, 009 T003 + 010 T003): schemas.py against generator 1.1.0.

The code follows the 1.1.0 document (``specs/010-lyrics-to-song/contracts``), which contains
009's 1.0.0 document plus the song, voice-conversion and voice-sample additions; the subset test
pins exactly which 1.0.0 entries 1.1.0 changes.
"""

from typing import Any, get_args

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_generator.schemas import (
    CONTRACT_VERSION,
    CREATE_GENERATION_REQUEST,
    GenerationErrorCode,
    GenerationKind,
    GenerationStatus,
    GeneratorEngine,
    GeneratorHealth,
    GeneratorProblemCode,
    ModelId,
    Problem,
    SongProfile,
    VocalLabel,
    VocalLanguage,
    VoiceSampleCheck,
    VoiceSampleIssue,
)
from stemstudio_generator.schemas import Generation as GenerationModel

from .docs import V100, V110, changed_entries

GENERATION_EXAMPLES = [
    "QueuedRestyle", "WaitingForGpuRestyle", "RunningRestyle", "CompletedRestyle",
    "FailedOutOfMemory", "CancelledRestyle", "QueuedSong", "RunningSong", "CompletedSong",
    "CompletedInstrumental", "CompletedVoiceConversion", "FailedUnvoicedTarget",
]  # fmt: skip
HEALTH_EXAMPLES = ["ReadyHealth", "NoGpuHealth", "SingerReadyHealth"]
REQUEST_EXAMPLES = ["SongRequest", "InstrumentalSongRequest"]
UPLOAD_EXAMPLES = ["RangeRestyleUpload", "InPlaceRestyleUpload", "VoiceConversionUpload"]
ENUMS: list[tuple[Any, str]] = [
    (GenerationKind, "GenerationKind"), (GenerationStatus, "GenerationStatus"),
    (GenerationErrorCode, "GenerationErrorCode"), (GeneratorProblemCode, "GeneratorProblemCode"),
    (GeneratorEngine, "GeneratorEngine"), (ModelId, "ModelId"), (VocalLabel, "VocalLabel"),
    (SongProfile, "SongProfile"), (VoiceSampleIssue, "VoiceSampleIssue"),
    (VocalLanguage, "VocalLanguage"),
]  # fmt: skip


def dump(model: BaseModel) -> Any:
    return model.model_dump(mode="json", by_alias=True)


def test_contract_version_matches_the_document() -> None:
    assert CONTRACT_VERSION == V110.root["info"]["version"] == "1.1.0"
    assert V100.root["info"]["version"] == "1.0.0"


@pytest.mark.parametrize(("literal", "schema"), ENUMS)
def test_enums_match_the_contract(literal: Any, schema: str) -> None:
    assert list(get_args(literal)) == V110.section("schemas")[schema]["enum"]


@pytest.mark.parametrize("name", GENERATION_EXAMPLES)
def test_generation_examples_round_trip(name: str) -> None:
    dumped = dump(GenerationModel.model_validate(V110.example(name)))

    assert dumped == V110.example(name)
    assert V110.errors("Generation", dumped) == []


@pytest.mark.parametrize("name", HEALTH_EXAMPLES)
def test_health_examples_round_trip(name: str) -> None:
    dumped = dump(GeneratorHealth.model_validate(V110.example(name)))

    assert dumped == V110.example(name)
    assert V110.errors("GeneratorHealth", dumped) == []


@pytest.mark.parametrize("name", ["VoiceSampleOk", "VoiceSampleUnvoiced"])
def test_voice_sample_examples_round_trip(name: str) -> None:
    dumped = dump(VoiceSampleCheck.model_validate(V110.example(name)))

    assert dumped == V110.example(name)
    assert V110.errors("VoiceSampleCheck", dumped) == []


@pytest.mark.parametrize("name", REQUEST_EXAMPLES)
def test_song_request_examples_round_trip(name: str) -> None:
    dumped = dump(CREATE_GENERATION_REQUEST.validate_python(V110.example(name)))

    assert dumped == V110.example(name)
    assert V110.errors("CreateGenerationRequest", dumped) == []


@pytest.mark.parametrize("name", UPLOAD_EXAMPLES)
def test_upload_params_parts_round_trip(name: str) -> None:
    params = V110.example(name)["params"]
    dumped = dump(CREATE_GENERATION_REQUEST.validate_python(params))

    assert dumped == params
    assert V110.errors("CreateGenerationRequest", dumped) == []


def test_problem_examples_parse_and_keep_their_code() -> None:
    for example in V110.problem_examples():
        problem = Problem.model_validate(example)

        assert dump(problem) == {"type": "about:blank", "detail": None, **example}


def test_problem_without_code_leaves_it_out() -> None:
    assert "code" not in dump(Problem(title="Not found", status=404))


def test_v100_restyle_examples_stay_valid_for_the_code() -> None:
    for name in ["QueuedRestyle", "CompletedRestyle", "FailedOutOfMemory", "CancelledRestyle"]:
        model = GenerationModel.model_validate(V100.example(name))
        assert V100.errors("Generation", V100.example(name)) == []
        assert model.takes == [] or model.takes[0].vocal is None


def test_v110_changes_only_the_listed_v100_entries() -> None:
    expected = {
        "paths": {
            "/health", "/v1/generations", "/v1/generations/{generationId}",
            "/v1/generations/{generationId}/takes/{index}",
        },
        "schemas": {
            "GeneratorProblemCode", "GeneratorEngine", "GeneratorHealth", "ModelId",
            "GenerationKind", "GenerationErrorCode", "CreateGenerationRequest",
            "CreateGenerationUpload", "GeneratedTake", "Generation",
        },
        "responses": {"GpuRequired"},
        "examples": {"ReadyHealth", "NoGpuHealth", "CompletedRestyle"},
        "parameters": set(),
    }  # fmt: skip

    for section, names in expected.items():
        assert changed_entries(V100, V110, section) == names, section


def test_take_audio_path_is_unchanged_but_documents_the_voice_tags() -> None:
    older = V100.section("paths")["/v1/generations/{generationId}/takes/{index}"]
    newer = V110.section("paths")["/v1/generations/{generationId}/takes/{index}"]

    assert older["get"]["responses"] == newer["get"]["responses"]
    assert "AI_VOICE" in newer["get"]["description"]


# ------------------------------------------------------------------ cross-field rules


def restyle(**changes: Any) -> dict[str, Any]:
    example: dict[str, Any] = V110.example("RangeRestyleUpload")["params"]
    example["restyle"].update(changes)
    return example


@pytest.mark.parametrize(("start", "end"), [(0, 9.9), (0, 480.5), (100, 50)])
def test_restyle_range_must_be_10_to_480_seconds(start: float, end: float) -> None:
    with pytest.raises(ValidationError, match="between 10 and 480 seconds"):
        CREATE_GENERATION_REQUEST.validate_python(
            restyle(rangeStartSeconds=start, rangeEndSeconds=end)
        )


def test_restyle_needs_one_or_two_seeds() -> None:
    with pytest.raises(ValidationError):
        CREATE_GENERATION_REQUEST.validate_python(restyle(seeds=[1, 2, 3]))


def test_generation_needs_exactly_the_member_of_its_kind() -> None:
    song = V110.example("QueuedSong")
    mixed = {**song, "restyle": V110.example("QueuedRestyle")["restyle"]}

    with pytest.raises(ValidationError, match="exactly the member of kind song"):
        GenerationModel.model_validate(mixed)


def test_generation_error_iff_failed() -> None:
    failed = V110.example("FailedOutOfMemory")

    with pytest.raises(ValidationError, match="error must be set exactly"):
        GenerationModel.model_validate({**failed, "status": "completed"})


def test_health_ready_iff_gpu() -> None:
    health = V110.example("NoGpuHealth")

    with pytest.raises(ValidationError, match="ready must be true exactly"):
        GeneratorHealth.model_validate({**health, "ready": True})


@pytest.mark.parametrize(
    "changes",
    [{"ok": False}, {"reason": "unvoiced"}, {"message": "text"}],
)
def test_voice_sample_reason_iff_not_ok(changes: dict[str, Any]) -> None:
    with pytest.raises(ValidationError, match="reason and message"):
        VoiceSampleCheck.model_validate({**V110.example("VoiceSampleOk"), **changes})
