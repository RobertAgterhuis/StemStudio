"""ACE-Step 1.5 behind ``MusicEngine`` (ADR-018 D1/D5/D6, 009 research S-05, 010 S-04/S-07).

The Python API of ACE-Step (``AceStepHandler.initialize_service`` and ``generate_music``, commit
``ca1e85f``) is imported lazily, only in the ``acestep`` image variant. The DiT
``acestep-v15-turbo`` is loaded from the pinned bundle in the shared model cache
(``<model_dir>/acestep/checkpoints``); the low-memory profile offloads DiT, VAE and text encoder
to the CPU. The 1.7B LM planner is loaded only for the ``SONG_PLANNED`` need (bf16, ``pt``
backend, ``acestep_song``) and dropped for every other need; restyles never use it.

ACE-Step reports progress through a callable, partly from a helper thread that swallows
exceptions; a ``Cancelled`` raised by our callback is therefore remembered and re-raised when
``generate_music`` returns. Failed results are mapped to ``GpuOutOfMemoryError`` (CUDA OOM or
ACE-Step's "Insufficient free VRAM" guard) or a generic ``GeneratorError``; the raw text only
goes to the log.
"""

import dataclasses
import gc
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final

import soundfile as sf

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.engines.acestep_api import (
    LOAD_FAILED_TEXT,
    AceStepApi,
    failure,
    import_acestep,
)
from stemstudio_generator.engines.acestep_song import LM_MODEL, init_lm, song_values
from stemstudio_generator.engines.profiles import (
    ACESTEP_MODELS,
    LoadProfile,
    load_profile,
    model_statuses,
)
from stemstudio_generator.engines.weights import WeightsDir
from stemstudio_generator.ports import (
    ACESTEP_CHECKPOINT_DIR,
    ACESTEP_WEIGHTS,
    Cancelled,
    DeviceUse,
    EngineRestyle,
    EngineSong,
    EngineTake,
    GeneratorError,
    ProgressCallback,
)
from stemstudio_generator.schemas import PLANNING_STAGE, ModelId, ModelStatus, SongProfile

__all__ = [
    "DIT_CONFIG",
    "GENERATION_FAILED_TEXT",
    "LM_MODEL",
    "LOAD_FAILED_TEXT",
    "NOT_LOADED_TEXT",
    "AceStepApi",
    "AceStepEngine",
    "import_acestep",
    "restyle_values",
]

DIT_CONFIG: Final = "acestep-v15-turbo"
"""ACE-Step ``config_path`` of the turbo DiT."""

GENERATION_FAILED_TEXT: Final = "ACE-Step could not generate the take; see the generator log."
NOT_LOADED_TEXT: Final = "The engine model is not loaded."
ENGINE_DIR: Final = "acestep"
"""Sub-directory of ``out_dir`` where ACE-Step saves its files before they are renamed."""

PLANNING_END: Final = 0.5
"""ACE-Step's progress value below which the LM planner runs (the DiT reports from 0.51)."""

_MODEL_FILES: Final[dict[ModelId, str]] = {
    "acestep-v15-turbo": "acestep-v15-turbo/model.safetensors",
    "acestep-vae": "vae/diffusion_pytorch_model.safetensors",
    "qwen3-embedding-0.6b": "Qwen3-Embedding-0.6B/model.safetensors",
    "acestep-5hz-lm-1.7b": "acestep-5Hz-lm-1.7B/model.safetensors",
}


class _Progress:
    """ACE-Step's progress callable over our ``ProgressCallback``; remembers a cancel."""

    def __init__(self, on_progress: ProgressCallback, stage: str, planning: bool) -> None:
        self._on_progress = on_progress
        self._stage = stage
        self._planning = planning
        self.cancelled = False

    def __call__(self, value: float, desc: str | None = None, **_: Any) -> None:
        del desc  # ACE-Step's own texts are not shown to users
        planning = self._planning and float(value) < PLANNING_END
        try:
            self._on_progress(float(value), PLANNING_STAGE if planning else self._stage)
        except Cancelled:
            self.cancelled = True
            raise


@dataclass(frozen=True, slots=True)
class _Call:
    """One ``generate_music`` call: parameter values, seeds, take directory, planner use."""

    values: dict[str, Any]
    seeds: list[int]
    out_dir: Path
    planning: bool = False


@dataclass(slots=True)
class _Loaded:
    profile: LoadProfile
    dit: Any
    idle_llm: Any
    lm: Any = None


class AceStepEngine:
    """``MusicEngine`` over ACE-Step 1.5."""

    def __init__(
        self,
        settings: GeneratorSettings,
        api: Callable[[], AceStepApi] = import_acestep,
        weights: WeightsDir | None = None,
    ) -> None:
        """Use the bundle under ``settings.model_dir``; ``api`` imports ACE-Step on first load."""
        self._root = settings.model_dir / ACESTEP_CHECKPOINT_DIR
        self._weights = weights or WeightsDir(self._root, ACESTEP_WEIGHTS)
        self._import = api
        self._api: AceStepApi | None = None
        self._loaded: _Loaded | None = None

    @property
    def api(self) -> AceStepApi:
        """The ACE-Step API (imported on first use)."""
        if self._api is None:
            self._api = self._import()
        return self._api

    def required_models(self) -> list[ModelStatus]:
        """The ACE-Step bundle with presence from the pinned cache."""
        return model_statuses(
            ACESTEP_MODELS, lambda model: self._weights.file_present(_MODEL_FILES[model])
        )

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Download (first use), verify and load DiT (and LM) for ``use.need``."""
        self._weights.ensure(on_progress)
        self._weights.verify()
        profile = load_profile(use.need)
        loaded = self._loaded
        if loaded is None or loaded.profile.cpu_offload != profile.cpu_offload:
            self.unload()
            loaded = _Loaded(profile, self._init_dit(profile), self.api.llm())
            self._loaded = loaded
        self._set_lm(loaded, wanted=profile.language_model)
        loaded.profile = profile

    def restyle(self, request: EngineRestyle, on_progress: ProgressCallback) -> list[EngineTake]:
        """``cover`` of the range, or ``repaint`` of the range in place (one take per seed)."""
        call = _Call(restyle_values(request), list(request.params.seeds), request.out_dir)
        return self._generate(call, on_progress)

    def song(self, request: EngineSong, on_progress: ProgressCallback) -> list[EngineTake]:
        """``text2music``; planned only when requested and the LM is loaded."""
        planned = request.planned and self._require_loaded().lm is not None
        values = song_values(request, planned)
        call = _Call(values, list(request.params.seeds), request.out_dir, planning=planned)
        profile: SongProfile = "planned" if planned else "direct"
        return [dataclasses.replace(t, profile=profile) for t in self._generate(call, on_progress)]

    def unload(self) -> None:
        """Drop the handlers (DiT and LM) and empty the CUDA cache."""
        if self._loaded is None:
            return
        self._set_lm(self._loaded, wanted=False)
        self._loaded = None
        gc.collect()
        self.api.torch.cuda.empty_cache()

    def _set_lm(self, loaded: _Loaded, *, wanted: bool) -> None:
        if wanted and loaded.lm is None:
            loaded.lm = init_lm(self.api, str(self._root))
        elif not wanted and loaded.lm is not None:
            loaded.lm.unload()
            loaded.lm = None

    def _init_dit(self, profile: LoadProfile) -> Any:
        dit = self.api.handler()
        status, ok = dit.initialize_service(
            project_root=str(self._root.parent), config_path=DIT_CONFIG, device="cuda",
            use_flash_attention=False, compile_model=False, offload_to_cpu=profile.cpu_offload,
            offload_dit_to_cpu=profile.cpu_offload, quantization=None, use_mlx_dit=False,
        )  # fmt: skip
        if not ok:
            raise failure(str(status), LOAD_FAILED_TEXT)
        return dit

    def _generate(self, call: _Call, on_progress: ProgressCallback) -> list[EngineTake]:
        progress = _Progress(on_progress, _stage(len(call.seeds)), call.planning)
        result = self._run(call, progress)
        if not result.success:
            raise failure(str(result.error), GENERATION_FAILED_TEXT)
        audios = enumerate(result.audios, start=1)
        return [_take(index, audio, call.seeds, call.out_dir) for index, audio in audios]

    def _run(self, call: _Call, progress: _Progress) -> Any:
        loaded, api = self._require_loaded(), self.api
        save_dir = call.out_dir / ENGINE_DIR
        save_dir.mkdir(parents=True, exist_ok=True)
        config = _config(api, call.seeds)
        llm = loaded.lm if loaded.lm is not None else loaded.idle_llm
        try:
            return api.generate(
                loaded.dit, llm, api.params(**call.values), config,
                save_dir=str(save_dir), progress=progress,
            )  # fmt: skip
        finally:
            if progress.cancelled:
                raise Cancelled

    def _require_loaded(self) -> _Loaded:
        if self._loaded is None:
            raise GeneratorError(NOT_LOADED_TEXT)
        return self._loaded


def restyle_values(request: EngineRestyle) -> dict[str, Any]:
    """``GenerationParams`` of a restyle (009 research S-05; the LM is never used)."""
    params = request.params
    values: dict[str, Any] = {
        "task_type": "cover" if params.mode == "range" else "repaint",
        "src_audio": str(request.source), "caption": params.caption,
        "audio_cover_strength": params.audio_cover_strength,
        "inference_steps": params.inference_steps, "shift": params.shift, "thinking": False,
        "use_cot_metas": False, "use_cot_caption": False, "use_cot_language": False,
    }  # fmt: skip
    if params.mode == "inPlace":
        values |= {
            "repainting_start": params.range_start_seconds,
            "repainting_end": params.range_end_seconds, "chunk_mask_mode": "explicit",
        }  # fmt: skip
    return values


def _config(api: AceStepApi, seeds: list[int]) -> Any:
    """``GenerationConfig``: one take per seed, fixed seeds, FLAC."""
    return api.config(
        batch_size=len(seeds), use_random_seed=False, seeds=seeds, audio_format="flac"
    )


def _stage(takes: int) -> str:
    return "Generating take 1 of 1" if takes == 1 else f"Generating {takes} takes"


def _take(index: int, audio: dict[str, Any], seeds: list[int], out_dir: Path) -> EngineTake:
    """Rename ACE-Step's file to ``take-<index>.flac`` and describe it."""
    saved = str(audio.get("path") or "")
    if not saved or not Path(saved).is_file():
        raise GeneratorError(GENERATION_FAILED_TEXT)
    path = Path(saved).replace(out_dir / f"take-{index}.flac")
    seed = int(audio.get("params", {}).get("seed", seeds[index - 1]))
    return EngineTake(index=index, seed=seed, path=path, duration_seconds=sf.info(path).duration)
