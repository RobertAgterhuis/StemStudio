"""Skeleton tests for the feature 007 contract code: worker API 1.4.0, ports, settings, vectors."""

import json
import math
import runpy
from pathlib import Path
from typing import Any, get_args

import pytest
import yaml
from jsonschema import Draft202012Validator
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import RenderSettings, WorkerSettings, load_settings
from stemstudio_worker.ports import (
    LIBRARY_PATH_PATTERNS,
    InputNotFoundError,
    InputPathError,
    LibraryReader,
    RenderCancelledError,
    RenderError,
    RenderExecutor,
    RenderProgress,
    WorkerError,
    is_allowed_library_path,
)
from stemstudio_worker.schemas import (
    CONTRACT_VERSION,
    LIBRARY_NOT_CHECKED,
    LIBRARY_UNAVAILABLE_TEXT,
    RENDER_CONTENT_TYPES,
    RESTART_INTERRUPTED_TEXT,
    Health,
    LibraryHealth,
    RenderFormat,
    RenderJob,
    RenderJobRequest,
    RenderJobStatus,
    RenderOutput,
    RenderRange,
    RenderResult,
)

_CONTRACTS = Path(__file__).resolve().parents[2] / "specs" / "007-export-library" / "contracts"
_DOC: dict[str, Any] = yaml.safe_load(
    (_CONTRACTS / "worker-api.openapi.yaml").read_text(encoding="utf-8")
)
_TRACK = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"


def _errors(schema: str, body: object) -> list[str]:
    components = _DOC["components"]
    validator = Draft202012Validator({**components["schemas"][schema], "components": components})
    return [error.message for error in validator.iter_errors(body)]


def _value(example: str) -> Any:
    return _DOC["components"]["examples"][example]["value"]


def test_document_is_1_4_0_and_the_code_follows_the_008_document() -> None:
    assert _DOC["info"]["version"] == "1.4.0"
    assert CONTRACT_VERSION == "1.11.0"  # 020 Phase 0 (contract/test_instrument_views_contract.py)


@pytest.mark.parametrize(
    ("example", "model"),
    [
        ("RawStemRenderRequest", RenderJobRequest),
        ("MixRenderRequest", RenderJobRequest),
        ("QueuedRenderJob", RenderJob),
        ("RunningRenderJob", RenderJob),
        ("CompletedRenderJob", RenderJob),
        ("FailedRenderJob", RenderJob),
    ],
)
def test_render_examples_round_trip(example: str, model: type[BaseModel]) -> None:
    dumped = model.model_validate(_value(example)).model_dump(mode="json", by_alias=True)

    assert dumped == _value(example)
    assert _errors(model.__name__, dumped) == []


@pytest.mark.parametrize("name", ["cpu", "gpu", "gpuUnavailable"])
def test_health_examples_carry_library_and_lanes(name: str) -> None:
    content = _DOC["paths"]["/health"]["get"]["responses"]["200"]["content"]
    example = content["application/json"]["examples"][name]["value"]

    dumped = Health.model_validate(example).model_dump(mode="json", by_alias=True)

    assert dumped["library"] == example["library"]
    assert dumped["lanes"]["render"] == example["lanes"]["render"]


def test_default_health_reports_an_unchecked_library_and_idle_lanes() -> None:
    body = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    assert body["library"] == {"readable": False, "reason": LIBRARY_UNAVAILABLE_TEXT}
    assert body["lanes"]["render"] == {"queued": 0, "running": False}
    assert LibraryHealth(readable=False, reason=LIBRARY_UNAVAILABLE_TEXT) == LIBRARY_NOT_CHECKED


def test_failed_render_example_uses_the_restart_text() -> None:
    assert _value("FailedRenderJob")["error"] == RESTART_INTERRUPTED_TEXT


def test_statuses_and_formats_match_the_contract() -> None:
    schemas = _DOC["components"]["schemas"]

    assert [s.value for s in RenderJobStatus] == schemas["RenderJobStatus"]["enum"]
    assert [s.is_terminal for s in RenderJobStatus] == [False, False, True, True, True]
    assert list(get_args(RenderFormat)) == schemas["RenderFormat"]["enum"]
    content_types = schemas["RenderResult"]["properties"]["contentType"]["enum"]
    assert list(RENDER_CONTENT_TYPES.values()) == content_types


def test_render_output_defaults() -> None:
    output = RenderOutput(format="mp3")

    assert (output.bit_depth, output.mp3_bitrate_kbps, output.dither) == (24, 320, True)
    assert output.model_dump(by_alias=True)["mp3BitrateKbps"] == 320
    with pytest.raises(ValidationError):
        RenderOutput.model_validate({"format": "ogg"})
    with pytest.raises(ValidationError):
        RenderOutput.model_validate({"format": "flac", "bitDepth": 32})


@pytest.mark.parametrize(("start", "end"), [(0.0, 0.4), (10.0, 10.0), (12.0, 10.0), (-1.0, 5.0)])
def test_render_range_needs_half_a_second(start: float, end: float) -> None:
    with pytest.raises(ValidationError):
        RenderRange(start_seconds=start, end_seconds=end)


def test_render_range_accepts_exactly_half_a_second() -> None:
    assert RenderRange(start_seconds=1.0, end_seconds=1.5).end_seconds == 1.5


@pytest.mark.parametrize(
    "change",
    [
        {"inputs": []},
        {"masterGain": 1.5},
        {"inputs": [{"path": "x", "gain": 2, "pan": 0}]},
        {"inputs": [{"path": "x", "gain": 1, "pan": -1.5}]},
        {"output": {"format": "flac", "mp3BitrateKbps": 128}},
    ],
)
def test_render_request_limits(change: dict[str, object]) -> None:
    with pytest.raises(ValidationError):
        RenderJobRequest.model_validate({**_value("RawStemRenderRequest"), **change})


def test_render_request_accepts_64_inputs_and_default_metadata() -> None:
    inputs = [{"path": "x", "gain": 1, "pan": 0}] * 64
    request = RenderJobRequest.model_validate(
        {"inputs": inputs, "masterGain": 1, "output": {"format": "wav"}}
    )

    assert (len(request.inputs), request.range, request.metadata.title) == (64, None, None)
    with pytest.raises(ValidationError):
        RenderJobRequest.model_validate(
            {"inputs": [*inputs, inputs[0]], "masterGain": 1, "output": {"format": "wav"}}
        )


def test_render_result_limits() -> None:
    result = _value("CompletedRenderJob")["result"]

    with pytest.raises(ValidationError):
        RenderResult.model_validate({**result, "appliedGainDb": 0.5})
    with pytest.raises(ValidationError):
        RenderResult.model_validate({**result, "fileName": "../output.mp3"})


# ------------------------------------------------------------------ ports


@pytest.mark.parametrize(
    "path",
    [
        f"tracks/{_TRACK}/stems/drums.flac",
        f"tracks/{_TRACK}/detail/drums/kick.flac",
        f"tracks/{_TRACK}/detail/other/acoustic-guitar.flac",
        f"assets/{_TRACK}/audio.mp3",
        f"assets/{_TRACK}/audio-2.wav",
    ],
)
def test_allowed_library_paths(path: str) -> None:
    assert is_allowed_library_path(path)


@pytest.mark.parametrize(
    "path",
    [
        f"tracks/{_TRACK.upper()}/stems/drums.flac",
        f"tracks/{_TRACK}/stems/../../stemstudio.db",
        f"/tracks/{_TRACK}/stems/drums.flac",
        f"tracks\\{_TRACK}\\stems\\drums.flac",
        f"tracks/{_TRACK}/stems/kazoo.flac",
        f"tracks/{_TRACK}/stems/drums.wav",
        f"tracks/{_TRACK}/detail/bass/rest.flac",
        f"tracks/{_TRACK}/detail/drums/Kick.flac",
        f"assets/{_TRACK}/audio.ogg",
        f"tracks/{_TRACK}/stems/drums.flac\n",
        "stemstudio.db",
    ],
)
def test_rejected_library_paths(path: str) -> None:
    assert not is_allowed_library_path(path)


def test_there_are_four_path_patterns() -> None:
    # 1.8.0 (014) adds assets/<uuid>/score.musicxml for score-view jobs.
    assert len(LIBRARY_PATH_PATTERNS) == 4


def test_render_errors() -> None:
    missing = InputNotFoundError(f"tracks/{_TRACK}/stems/drums.flac")

    assert (str(missing), missing.path) == (
        f"Input not found: tracks/{_TRACK}/stems/drums.flac",
        f"tracks/{_TRACK}/stems/drums.flac",
    )
    for error in (InputPathError, InputNotFoundError, RenderCancelledError):
        assert issubclass(error, RenderError)
    assert issubclass(RenderError, WorkerError)


class _Library:
    """Structural ``LibraryReader`` over a fixed root."""

    def __init__(self, root: Path) -> None:
        self._root = root

    def status(self) -> LibraryHealth:
        return LibraryHealth(readable=True)

    def resolve(self, path: str) -> Path:
        if not is_allowed_library_path(path):
            raise InputPathError(path)
        return self._root / path


class _Executor:
    """Structural ``RenderExecutor`` that reports both passes and returns the example result."""

    def __init__(self, library: LibraryReader) -> None:
        self._library = library

    def validate(self, request: RenderJobRequest) -> None:
        for item in request.inputs:
            self._library.resolve(item.path)

    def render(
        self, request: RenderJobRequest, out_dir: Path, progress: RenderProgress
    ) -> RenderResult:
        progress(0.5)
        progress(1.0)
        return RenderResult.model_validate(_value("CompletedRenderJob")["result"])


def test_render_ports_are_structural(tmp_path: Path) -> None:
    executor: RenderExecutor = _Executor(_Library(tmp_path))
    request = RenderJobRequest.model_validate(_value("MixRenderRequest"))
    seen: list[float] = []

    executor.validate(request)
    result = executor.render(request, tmp_path, seen.append)

    assert (seen, result.applied_gain_db, isinstance(seen.append, RenderProgress)) == (
        [0.5, 1.0],
        -1.8,
        True,
    )
    outside = request.inputs[0].model_copy(update={"path": "x"})
    bad = request.model_copy(update={"inputs": [outside]})
    with pytest.raises(InputPathError):
        executor.validate(bad)


# ------------------------------------------------------------------ settings


def test_render_settings_defaults() -> None:
    assert load_settings({}).render == RenderSettings() == WorkerSettings(data_dir=Path("d")).render
    assert (RenderSettings().library_dir, RenderSettings().block_seconds) == (None, 10.0)


def test_render_settings_from_the_environment() -> None:
    render = load_settings(
        {"STEMSTUDIO_LIBRARY_DIR": "/library", "STEMSTUDIO_RENDER_BLOCK_SECONDS": "0.25"}
    ).render

    assert render == RenderSettings(library_dir=Path("/library"), block_seconds=0.25)


@pytest.mark.parametrize("value", ["0", "-1", "ten"])
def test_invalid_block_seconds_are_rejected(value: str) -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_RENDER_BLOCK_SECONDS"):
        load_settings({"STEMSTUDIO_RENDER_BLOCK_SECONDS": value})


# ------------------------------------------------------------------ render-vectors.json


def _vectors() -> dict[str, Any]:
    loaded: dict[str, Any] = json.loads(
        (_CONTRACTS / "render-vectors.json").read_text(encoding="utf-8")
    )
    return loaded


def test_vectors_are_generated_by_the_committed_generator() -> None:
    generator = runpy.run_path(str(_CONTRACTS / "generate_render_vectors.py"))

    assert (_CONTRACTS / "render-vectors.json").read_text(encoding="utf-8") == generator["render"]()
    assert _vectors()["version"] == "1.0.0"


@pytest.mark.parametrize("case", _vectors()["pan"], ids=lambda case: str(case["pan"]))
def test_pan_matrices_follow_the_stereo_panner_law(case: dict[str, Any]) -> None:
    pan: float = case["pan"]
    theta = (pan + 1 if pan <= 0 else pan) * math.pi / 2
    left, right = (1.0, 0.25)
    if pan <= 0:
        expected = (left + right * math.cos(theta), right * math.sin(theta))
    else:
        expected = (left * math.cos(theta), right + left * math.sin(theta))

    (ll, lr), (rl, rr) = case["matrix"]
    actual = (ll * left + lr * right, rl * left + rr * right)

    assert actual == pytest.approx(expected, abs=1e-8)


def _gate_inputs() -> list[dict[str, Any]]:
    return [i for case in _vectors()["gate"] for i in case["expected"].get("inputs", [])]


def test_gate_vectors_only_use_allowed_paths() -> None:
    inputs = _gate_inputs()
    gains = sorted(i["gain"] for i in inputs)
    pans = sorted(i["pan"] for i in inputs)

    assert inputs
    assert all(map(is_allowed_library_path, (i["path"] for i in inputs)))
    assert (gains[0] >= 0, gains[-1] <= 1, pans[0] >= -1, pans[-1] <= 1) == (True,) * 4
