"""SoulX-Singer-SVC adapter and the RMVPE pitch tracker over fake modules (010 T041, T040)."""

import hashlib
from dataclasses import replace
from pathlib import Path

import numpy as np
import pytest

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.engines.rmvpe import RmvpePitchTracker
from stemstudio_generator.engines.soulx_svc import (
    CONVERTING_STAGE,
    SoulXConverter,
    SoulXWeights,
)
from stemstudio_generator.engines.weights import WeightsDir, write_manifest
from stemstudio_generator.ports import (
    RMVPE_FILE,
    RMVPE_WEIGHTS,
    SOULX_SVC_FILE,
    SOULX_SVC_WEIGHTS,
    SVC_FP16,
    WHISPER_BASE_DIR,
    WHISPER_BASE_WEIGHTS,
    ConversionInput,
    DeviceUse,
    GeneratorError,
    InvalidInputError,
    PitchTracker,
    PromptInvalidError,
    UnvoicedTargetError,
    VoiceConverter,
    WeightsPin,
)
from stemstudio_generator.schemas import JobDevice, VoiceConversionParams

from ..signals import tone, write
from .soulx_fake import SoulXRecorder, fake_soulx

PARAMS = VoiceConversionParams(
    pitch_shift=2, auto_shift=False, steps=24, cfg=2.5, profile_name="Me"
)
USE = DeviceUse(need=SVC_FP16, device=JobDevice(kind="gpu"), free_memory_mib=14_000)


def _pin(root: Path, name: str, base: WeightsPin) -> WeightsPin:
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(name.encode())
    return WeightsPin(
        base.repo, base.revision, ((name, hashlib.sha256(name.encode()).hexdigest()),)
    )


def _weights(model_dir: Path) -> SoulXWeights:
    soulx = model_dir / "soulx"
    svc = _pin(soulx, "model-svc.pt", SOULX_SVC_WEIGHTS)
    rmvpe = _pin(soulx, "rmvpe/rmvpe.pt", RMVPE_WEIGHTS)
    write_manifest(soulx, SOULX_SVC_WEIGHTS.revision)
    (soulx / "REVISION").write_text(
        f"model-svc.pt {svc.repo} {svc.revision}\nrmvpe/rmvpe.pt {rmvpe.repo} {rmvpe.revision}\n"
    )
    whisper = model_dir / WHISPER_BASE_DIR
    whisper.mkdir(parents=True)
    (whisper / "model.safetensors").write_bytes(b"w")
    write_manifest(whisper, WHISPER_BASE_WEIGHTS.revision)
    return SoulXWeights(
        svc=WeightsDir(soulx, svc, only_pinned=True),
        rmvpe=WeightsDir(soulx, rmvpe, only_pinned=True),
        whisper=WeightsDir(whisper, WHISPER_BASE_WEIGHTS),
    )


def _converter(tmp_path: Path, recorder: SoulXRecorder) -> SoulXConverter:
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "models")
    return SoulXConverter(settings, lambda: fake_soulx(recorder), _weights(settings.model_dir))


def _request(tmp_path: Path, seconds: float = 12.0) -> ConversionInput:
    target = write(tmp_path / "target.wav", tone(seconds, 24_000), 24_000)
    prompt = write(tmp_path / "prompt.wav", tone(20.0, 24_000), 24_000)
    return ConversionInput(target=target, prompt=prompt, params=PARAMS)


def _loaded(
    tmp_path: Path, recorder: SoulXRecorder | None = None
) -> tuple[SoulXConverter, SoulXRecorder]:
    recorder = recorder or SoulXRecorder()
    converter = _converter(tmp_path, recorder)
    converter.load(USE, lambda fraction, stage: None)
    return converter, recorder


def test_the_converter_satisfies_the_port_and_lists_the_singer_models(tmp_path: Path) -> None:
    converter = _converter(tmp_path, SoulXRecorder())

    assert isinstance(converter, VoiceConverter)
    models = converter.required_models()
    assert [(m.id, m.present) for m in models] == [
        ("soulx-singer-svc", True), ("rmvpe", True), ("whisper-base", True),
    ]  # fmt: skip


def test_models_are_missing_without_the_cache(tmp_path: Path) -> None:
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "none")
    converter = SoulXConverter(settings, lambda: fake_soulx(SoulXRecorder()))

    assert not any(m.present for m in converter.required_models())


def test_load_builds_the_model_once_in_fp16_on_cuda_with_the_local_whisper(
    tmp_path: Path,
) -> None:
    converter, recorder = _loaded(tmp_path)
    converter.load(USE, lambda fraction, stage: None)

    assert len(recorder.built) == 1
    [(path, kwargs)] = recorder.loads
    assert path.endswith(SOULX_SVC_FILE)
    assert kwargs == {"weights_only": False, "map_location": "cpu"}
    whisper_dir = str(tmp_path / "models" / WHISPER_BASE_DIR)
    assert recorder.whisper_names == [whisper_dir, whisper_dir]
    [f0] = recorder.f0_inits
    assert f0["model_path"].endswith(RMVPE_FILE)
    assert (f0["device"], f0["is_half"], f0["target_sr"], f0["hop_size"]) == (
        "cuda",
        False,
        24_000,
        480,
    )
    assert f0["max_duration"] > 480


def test_the_whisper_patch_is_removed_after_the_build(tmp_path: Path) -> None:
    recorder = SoulXRecorder()
    converter = _converter(tmp_path, recorder)
    whisper = converter.api.whisper
    original = whisper.WhisperModel

    converter.load(USE, lambda fraction, stage: None)

    assert whisper.WhisperModel is original


def test_convert_maps_the_parameters_to_infer(tmp_path: Path) -> None:
    converter, recorder = _loaded(tmp_path)
    seen: list[tuple[float, str]] = []

    vocal = converter.convert(_request(tmp_path), lambda f, s: seen.append((f, s)))

    [call] = recorder.infers
    assert (call["auto_shift"], call["pitch_shift"], call["n_steps"], call["cfg"]) == (
        False,
        2,
        24,
        2.5,
    )
    assert call["use_fp16"] is True
    assert call["pt_wav"].device == call["gt_wav"].device == "cuda"
    assert call["gt_f0"].data.shape == (1, 500)
    assert recorder.seeds == [0]
    assert vocal.dtype == np.float32
    assert vocal.ndim == 1
    assert seen == [(0.0, CONVERTING_STAGE), (1.0, CONVERTING_STAGE)]


def test_a_target_longer_than_the_limit_is_invalid_input(tmp_path: Path) -> None:
    recorder = SoulXRecorder()
    settings = replace(
        GeneratorSettings(), model_dir=tmp_path / "models", svc_max_target_seconds=10.0
    )
    converter = SoulXConverter(settings, lambda: fake_soulx(recorder), _weights(settings.model_dir))
    converter.load(USE, lambda fraction, stage: None)

    with pytest.raises(InvalidInputError, match="longer than 10 s"):
        converter.convert(_request(tmp_path, 12.0), lambda fraction, stage: None)


def test_a_target_below_ten_percent_voiced_is_unvoiced(tmp_path: Path) -> None:
    converter, _ = _loaded(tmp_path, SoulXRecorder(voiced=0.05))

    with pytest.raises(UnvoicedTargetError):
        converter.convert(_request(tmp_path), lambda fraction, stage: None)


def test_an_unreadable_prompt_is_prompt_invalid(tmp_path: Path) -> None:
    converter, _ = _loaded(tmp_path)
    request = _request(tmp_path)
    request.prompt.write_bytes(b"broken")

    with pytest.raises(PromptInvalidError):
        converter.convert(request, lambda fraction, stage: None)


def test_an_unreadable_target_is_invalid_input(tmp_path: Path) -> None:
    converter, _ = _loaded(tmp_path)
    request = _request(tmp_path)
    request.target.write_bytes(b"broken")

    with pytest.raises(InvalidInputError):
        converter.convert(request, lambda fraction, stage: None)


def test_convert_before_load_is_an_error(tmp_path: Path) -> None:
    with pytest.raises(GeneratorError, match="not loaded"):
        _converter(tmp_path, SoulXRecorder()).convert(_request(tmp_path), print)


def test_infer_errors_propagate_for_the_runner(tmp_path: Path) -> None:
    converter, _ = _loaded(tmp_path, SoulXRecorder(infer_error=RuntimeError("CUDA out of memory")))

    with pytest.raises(RuntimeError, match="out of memory"):
        converter.convert(_request(tmp_path), lambda fraction, stage: None)


def test_unload_drops_the_model_and_empties_the_cache(tmp_path: Path) -> None:
    converter, recorder = _loaded(tmp_path)

    converter.unload()
    converter.unload()

    assert recorder.emptied == 1
    converter.load(USE, lambda fraction, stage: None)
    assert len(recorder.built) == 2


def test_rmvpe_tracker_runs_on_the_cpu_at_10_ms_frames(tmp_path: Path) -> None:
    recorder = SoulXRecorder()
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "models")
    tracker = RmvpePitchTracker(settings, lambda: fake_soulx(recorder))

    f0 = tracker.f0(tmp_path / "sample.flac")
    tracker.f0(tmp_path / "sample.flac")

    assert isinstance(tracker, PitchTracker)
    assert f0.dtype == np.float32
    assert f0.size == 3 * 100 + 1
    [init] = recorder.rmvpe_inits
    assert (init["device"], init["is_half"]) == ("cpu", False)
    assert init["model_path"].endswith(RMVPE_FILE)


def test_import_soulx_loads_f0_extraction_by_file_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    from stemstudio_generator.engines import soulx_svc  # noqa: PLC0415

    source = tmp_path / "soulx-src"
    tools = source / "preprocess" / "tools"
    tools.mkdir(parents=True)
    (tools / "f0_extraction.py").write_text("class F0Extractor: ...\nclass RMVPE: ...\n")

    module = soulx_svc.load_f0_module(source)

    assert hasattr(module, "F0Extractor")
    assert hasattr(module, "RMVPE")
    with pytest.raises(GeneratorError):
        soulx_svc.load_f0_module(tmp_path / "missing")
