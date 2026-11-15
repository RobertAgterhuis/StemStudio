"""ACE-Step 1.5 engine adapter over a fake ``acestep`` API (009 T034, FR-013, ADR-018 D5/D6)."""

import hashlib
import sys
from dataclasses import replace
from pathlib import Path
from types import ModuleType
from typing import Any

import pytest

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.engines.acestep_engine import (
    LOAD_FAILED_TEXT,
    NOT_LOADED_TEXT,
    AceStepEngine,
    import_acestep,
)
from stemstudio_generator.engines.weights import WeightsDir, write_manifest
from stemstudio_generator.ports import (
    ACESTEP_CHECKPOINT_DIR,
    ACESTEP_WEIGHTS,
    COVER_TURBO,
    COVER_TURBO_LOWMEM,
    Cancelled,
    DeviceUse,
    EngineRestyle,
    GeneratorError,
    GpuNeed,
    GpuOutOfMemoryError,
    MusicEngine,
    WeightsPin,
)
from stemstudio_generator.schemas import JobDevice, RestyleParams

from .acestep_fake import Recorder, Result, fake_api

RESTYLE = RestyleParams(
    caption="Jazz trio",
    audio_cover_strength=0.55,
    inference_steps=8,
    shift=3.0,
    seeds=[11, 22],
    mode="range",
    range_start_seconds=0.0,
    range_end_seconds=20.0,
)


def _use(need: GpuNeed = COVER_TURBO) -> DeviceUse:
    return DeviceUse(need=need, device=JobDevice(kind="gpu"), free_memory_mib=14_000)


def _pin_files(root: Path) -> WeightsPin:
    """Write tiny pinned files and return a pin with their hashes."""
    files = []
    for name in ("acestep-v15-turbo/model.safetensors", "acestep-5Hz-lm-1.7B/model.safetensors"):
        data = name.encode()
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_bytes(data)
        files.append((name, hashlib.sha256(data).hexdigest()))
    for name in (
        "vae/diffusion_pytorch_model.safetensors",
        "Qwen3-Embedding-0.6B/model.safetensors",
    ):
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_bytes(b"w")
    write_manifest(root, ACESTEP_WEIGHTS.revision)
    return WeightsPin(ACESTEP_WEIGHTS.repo, ACESTEP_WEIGHTS.revision, tuple(files))


def _engine(tmp_path: Path, recorder: Recorder) -> AceStepEngine:
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "models")
    root = settings.model_dir / ACESTEP_CHECKPOINT_DIR
    weights = WeightsDir(root, _pin_files(root))
    return AceStepEngine(settings, lambda: fake_api(recorder), weights)


def _request(tmp_path: Path, params: RestyleParams = RESTYLE) -> EngineRestyle:
    source = tmp_path / "source.wav"
    source.write_bytes(b"RIFF")
    out = tmp_path / "work"
    out.mkdir(exist_ok=True)
    return EngineRestyle(source=source, params=params, out_dir=out)


def _progress() -> tuple[list[tuple[float, str]], Any]:
    seen: list[tuple[float, str]] = []
    return seen, lambda fraction, stage: seen.append((fraction, stage))


def test_the_engine_satisfies_the_port_and_lists_the_bundle(tmp_path: Path) -> None:
    engine = _engine(tmp_path, Recorder())

    assert isinstance(engine, MusicEngine)
    models = engine.required_models()
    assert [m.id for m in models] == [
        "acestep-v15-turbo", "acestep-vae", "qwen3-embedding-0.6b", "acestep-5hz-lm-1.7b",
    ]  # fmt: skip
    assert all(m.present for m in models)
    assert {m.revision for m in models} == {ACESTEP_WEIGHTS.revision}


def test_models_are_missing_without_the_pinned_cache(tmp_path: Path) -> None:
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "empty")
    engine = AceStepEngine(settings, lambda: fake_api(Recorder()))

    assert not any(m.present for m in engine.required_models())


def test_standard_load_initialises_the_turbo_dit_on_cuda_without_the_lm(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)

    engine.load(_use(), lambda fraction, stage: None)
    engine.load(_use(), lambda fraction, stage: None)

    assert len(recorder.inits) == 1
    init = recorder.inits[0]
    assert init["project_root"] == str(tmp_path / "models" / "acestep")
    assert init["config_path"] == "acestep-v15-turbo"
    assert init["device"] == "cuda"
    assert (init["offload_to_cpu"], init["offload_dit_to_cpu"]) == (False, False)
    assert (init["compile_model"], init["quantization"], init["use_flash_attention"]) == (
        False, None, False,
    )  # fmt: skip
    assert recorder.llm_inits == []


def test_the_low_memory_profile_offloads_dit_vae_and_text_encoder(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)

    engine.load(_use(), lambda fraction, stage: None)
    engine.load(_use(COVER_TURBO_LOWMEM), lambda fraction, stage: None)

    assert len(recorder.inits) == 2
    assert (recorder.inits[1]["offload_to_cpu"], recorder.inits[1]["offload_dit_to_cpu"]) == (
        True, True,
    )  # fmt: skip


def test_a_failed_initialisation_is_a_readable_error(tmp_path: Path) -> None:
    recorder = Recorder(init_result=("Error initializing model: boom\nTraceback ...", False))

    with pytest.raises(GeneratorError) as caught:
        _engine(tmp_path, recorder).load(_use(), lambda fraction, stage: None)

    assert str(caught.value) == LOAD_FAILED_TEXT
    assert not isinstance(caught.value, GpuOutOfMemoryError)


def test_an_initialisation_out_of_memory_is_an_oom(tmp_path: Path) -> None:
    recorder = Recorder(init_result=("Error initializing model: CUDA out of memory", False))

    with pytest.raises(GpuOutOfMemoryError):
        _engine(tmp_path, recorder).load(_use(), lambda fraction, stage: None)


def test_restyle_maps_a_range_to_a_cover_with_one_take_per_seed(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)
    request = _request(tmp_path)

    takes = engine.restyle(request, lambda fraction, stage: None)

    p, c, save_dir = recorder.calls[0]
    assert p.values == {
        "task_type": "cover", "src_audio": str(request.source), "caption": "Jazz trio",
        "audio_cover_strength": 0.55, "inference_steps": 8, "shift": 3.0, "thinking": False,
        "use_cot_metas": False, "use_cot_caption": False, "use_cot_language": False,
    }  # fmt: skip
    assert c.values == {
        "batch_size": 2, "use_random_seed": False, "seeds": [11, 22], "audio_format": "flac",
    }  # fmt: skip
    assert save_dir == str(request.out_dir / "acestep")
    assert [(t.index, t.seed) for t in takes] == [(1, 11), (2, 22)]
    assert [t.path for t in takes] == [
        request.out_dir / "take-1.flac",
        request.out_dir / "take-2.flac",
    ]
    assert all(t.path.is_file() for t in takes)
    assert takes[0].duration_seconds == pytest.approx(2.0)
    assert recorder.llm_inits == []


def test_progress_is_forwarded_with_our_stage_text(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)
    seen, on_progress = _progress()

    engine.restyle(_request(tmp_path), on_progress)

    assert seen == [(0.3, "Generating 2 takes"), (0.9, "Generating 2 takes")]


def test_one_take_has_a_singular_stage(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)
    seen, on_progress = _progress()

    engine.restyle(_request(tmp_path, RESTYLE.model_copy(update={"seeds": [5]})), on_progress)

    assert seen[0][1] == "Generating take 1 of 1"


def test_restyle_before_load_is_an_error(tmp_path: Path) -> None:
    with pytest.raises(GeneratorError, match=NOT_LOADED_TEXT):
        _engine(tmp_path, Recorder()).restyle(_request(tmp_path), lambda fraction, stage: None)


def test_a_cancel_raised_in_the_progress_callback_is_kept_even_if_swallowed(
    tmp_path: Path,
) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)

    def cancel(fraction: float, stage: str) -> None:
        raise Cancelled

    with pytest.raises(Cancelled):
        engine.restyle(_request(tmp_path), cancel)


def test_a_cancel_swallowed_by_acestep_is_still_reported(tmp_path: Path) -> None:
    recorder = Recorder(result=Result(success=False, audios=[], error="Cancelled"))
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)
    calls: list[int] = []

    def cancel_once(fraction: float, stage: str) -> None:
        calls.append(1)
        if len(calls) == 1:
            raise Cancelled

    with pytest.raises(Cancelled):
        engine.restyle(_request(tmp_path), _Swallow(cancel_once))


class _Swallow:
    """Progress callback whose exceptions ACE-Step's progress thread would swallow."""

    def __init__(self, inner: Any) -> None:
        self.inner = inner

    def __call__(self, fraction: float, stage: str) -> None:
        self.inner(fraction, stage)


@pytest.mark.parametrize(
    "error", ["CUDA out of memory. Tried to allocate", "Insufficient free VRAM: need 1.4 GB"]
)
def test_a_failed_result_with_an_oom_text_is_an_oom(tmp_path: Path, error: str) -> None:
    recorder = Recorder(result=Result(success=False, audios=[], error=error))
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)

    with pytest.raises(GpuOutOfMemoryError):
        engine.restyle(_request(tmp_path), lambda fraction, stage: None)


def test_any_other_failed_result_is_a_generic_engine_error(tmp_path: Path) -> None:
    recorder = Recorder(result=Result(success=False, audios=[], error="secret path /x/y"))
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)

    with pytest.raises(GeneratorError) as caught:
        engine.restyle(_request(tmp_path), lambda fraction, stage: None)

    assert "secret" not in str(caught.value)


def test_a_result_without_files_is_an_engine_error(tmp_path: Path) -> None:
    recorder = Recorder(result=Result(success=True, audios=[{"path": "", "params": {}}]))
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)

    with pytest.raises(GeneratorError):
        engine.restyle(_request(tmp_path, RESTYLE.model_copy(update={"seeds": [5]})), print)


def test_exceptions_from_acestep_propagate_for_the_runner(tmp_path: Path) -> None:
    recorder = Recorder(raise_in_generate=RuntimeError("CUDA out of memory"))
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)

    with pytest.raises(RuntimeError, match="out of memory"):
        engine.restyle(_request(tmp_path), lambda fraction, stage: None)


def test_unload_drops_the_handlers_and_empties_the_cache(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(), lambda fraction, stage: None)
    torch = engine.api.torch

    engine.unload()
    engine.unload()

    assert torch.cuda.emptied == 1
    with pytest.raises(GeneratorError, match=NOT_LOADED_TEXT):
        engine.restyle(_request(tmp_path), lambda fraction, stage: None)
    engine.load(_use(), lambda fraction, stage: None)
    assert len(recorder.inits) == 2


def test_load_downloads_missing_weights_with_progress(tmp_path: Path) -> None:
    recorder = Recorder()
    settings = replace(GeneratorSettings(), model_dir=tmp_path / "models")
    events: list[str] = []

    class Weights:
        def present(self) -> bool:
            return False

        def file_present(self, name: str) -> bool:
            return False

        def ensure(self, on_progress: Any) -> None:
            events.append("ensure")
            on_progress(0.5, "Downloading model (5.0 of 10.0 GB)")

        def verify(self) -> None:
            events.append("verify")

    engine = AceStepEngine(settings, lambda: fake_api(recorder), Weights())  # type: ignore[arg-type]
    seen, on_progress = _progress()

    engine.load(_use(), on_progress)

    assert events == ["ensure", "verify"]
    assert (0.5, "Downloading model (5.0 of 10.0 GB)") in seen


def test_import_acestep_reads_the_real_module_names(monkeypatch: pytest.MonkeyPatch) -> None:
    modules = {
        "acestep": ModuleType("acestep"),
        "acestep.handler": ModuleType("acestep.handler"),
        "acestep.inference": ModuleType("acestep.inference"),
        "acestep.llm_inference": ModuleType("acestep.llm_inference"),
        "torch": ModuleType("torch"),
    }
    modules["acestep.handler"].AceStepHandler = "H"  # type: ignore[attr-defined]
    modules["acestep.inference"].GenerationParams = "P"  # type: ignore[attr-defined]
    modules["acestep.inference"].GenerationConfig = "C"  # type: ignore[attr-defined]
    modules["acestep.inference"].generate_music = "G"  # type: ignore[attr-defined]
    modules["acestep.llm_inference"].LLMHandler = "L"  # type: ignore[attr-defined]
    for name, module in modules.items():
        monkeypatch.setitem(sys.modules, name, module)

    api = import_acestep()

    assert (api.handler, api.params, api.config, api.generate, api.llm) == ("H", "P", "C", "G", "L")
    assert api.torch is modules["torch"]
