"""ACE-Step 1.5 behind ``MusicEngine`` (ADR-018 D1/D5/D6, 009 research S-05).

The Python API of ACE-Step (``AceStepHandler.initialize_service`` and ``generate_music``, commit
``ca1e85f``) is imported lazily, only in the ``acestep`` image variant. The DiT
``acestep-v15-turbo`` is loaded from the pinned bundle in the shared model cache
(``<model_dir>/acestep/checkpoints``); the low-memory profile offloads DiT, VAE and text encoder
to the CPU. Restyles never initialise the language model.

ACE-Step reports progress through a callable, partly from a helper thread that swallows
exceptions; a ``Cancelled`` raised by our callback is therefore remembered and re-raised when
``generate_music`` returns. Failed results are mapped to ``GpuOutOfMemoryError`` (CUDA OOM or
ACE-Step's "Insufficient free VRAM" guard) or a generic ``GeneratorError``; the raw text only
goes to the log.
"""

import gc
import logging
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final

import soundfile as sf

from stemstudio_generator.config import GeneratorSettings
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
    GpuOutOfMemoryError,
    ProgressCallback,
)
from stemstudio_generator.schemas import ModelId, ModelStatus

DIT_CONFIG: Final = "acestep-v15-turbo"
"""ACE-Step ``config_path`` of the turbo DiT."""

LOAD_FAILED_TEXT: Final = "The ACE-Step model could not be loaded; see the generator log."
GENERATION_FAILED_TEXT: Final = "ACE-Step could not generate the take; see the generator log."
NOT_LOADED_TEXT: Final = "The engine model is not loaded."
ENGINE_DIR: Final = "acestep"
"""Sub-directory of ``out_dir`` where ACE-Step saves its files before they are renamed."""

_OOM_MARKERS: Final = ("out of memory", "insufficient free vram")
_MODEL_FILES: Final[dict[ModelId, str]] = {
    "acestep-v15-turbo": "acestep-v15-turbo/model.safetensors",
    "acestep-vae": "vae/diffusion_pytorch_model.safetensors",
    "qwen3-embedding-0.6b": "Qwen3-Embedding-0.6B/model.safetensors",
    "acestep-5hz-lm-1.7b": "acestep-5Hz-lm-1.7B/model.safetensors",
}
_LOG: Final = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class AceStepApi:
    """The parts of ACE-Step's Python API the engine uses (replaced by a fake in tests).

    Attributes:
        handler: ``acestep.handler.AceStepHandler``.
        llm: ``acestep.llm_inference.LLMHandler``.
        params: ``acestep.inference.GenerationParams``.
        config: ``acestep.inference.GenerationConfig``.
        generate: ``acestep.inference.generate_music``.
        torch: The ``torch`` module (cache, dtypes).
    """

    handler: Any
    llm: Any
    params: Any
    config: Any
    generate: Any
    torch: Any


def import_acestep() -> AceStepApi:
    """Import ACE-Step and torch (``acestep`` image variant only)."""
    import torch  # noqa: PLC0415 - lazy: GPU image variant only
    from acestep.handler import AceStepHandler  # noqa: PLC0415
    from acestep.inference import (  # noqa: PLC0415
        GenerationConfig,
        GenerationParams,
        generate_music,
    )
    from acestep.llm_inference import LLMHandler  # noqa: PLC0415

    return AceStepApi(
        handler=AceStepHandler, llm=LLMHandler, params=GenerationParams,
        config=GenerationConfig, generate=generate_music, torch=torch,
    )  # fmt: skip


class _Progress:
    """ACE-Step's progress callable over our ``ProgressCallback``; remembers a cancel."""

    def __init__(self, on_progress: ProgressCallback, stage: str) -> None:
        self._on_progress = on_progress
        self._stage = stage
        self.cancelled = False

    def __call__(self, value: float, desc: str | None = None, **_: Any) -> None:
        del desc  # ACE-Step's own texts are not shown to users
        try:
            self._on_progress(float(value), self._stage)
        except Cancelled:
            self.cancelled = True
            raise


@dataclass(frozen=True, slots=True)
class _Call:
    """One ``generate_music`` call: parameter values, seeds and the take directory."""

    values: dict[str, Any]
    seeds: list[int]
    out_dir: Path


@dataclass(slots=True)
class _Loaded:
    profile: LoadProfile
    dit: Any
    llm: Any


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
        """Download (first use), verify and load the DiT for ``use.need`` (no-op when loaded)."""
        self._weights.ensure(on_progress)
        self._weights.verify()
        profile = load_profile(use.need)
        if self._loaded is not None and self._loaded.profile == profile:
            return
        self.unload()
        self._loaded = _Loaded(profile, self._init_dit(profile), self.api.llm())

    def restyle(self, request: EngineRestyle, on_progress: ProgressCallback) -> list[EngineTake]:
        """``cover`` of the range (one take per seed)."""
        params = request.params
        values = {
            "task_type": "cover", "src_audio": str(request.source), "caption": params.caption,
            "audio_cover_strength": params.audio_cover_strength,
            "inference_steps": params.inference_steps, "shift": params.shift, "thinking": False,
            "use_cot_metas": False, "use_cot_caption": False, "use_cot_language": False,
        }  # fmt: skip
        return self._generate(_Call(values, list(params.seeds), request.out_dir), on_progress)

    def song(self, request: EngineSong, on_progress: ProgressCallback) -> list[EngineTake]:
        """``text2music`` (010 US1)."""
        raise NotImplementedError

    def unload(self) -> None:
        """Drop the handlers and empty the CUDA cache."""
        if self._loaded is None:
            return
        self._loaded = None
        gc.collect()
        self.api.torch.cuda.empty_cache()

    def _init_dit(self, profile: LoadProfile) -> Any:
        dit = self.api.handler()
        status, ok = dit.initialize_service(
            project_root=str(self._root.parent), config_path=DIT_CONFIG, device="cuda",
            use_flash_attention=False, compile_model=False, offload_to_cpu=profile.cpu_offload,
            offload_dit_to_cpu=profile.cpu_offload, quantization=None, use_mlx_dit=False,
        )  # fmt: skip
        if not ok:
            raise _failure(str(status), LOAD_FAILED_TEXT)
        return dit

    def _generate(self, call: _Call, on_progress: ProgressCallback) -> list[EngineTake]:
        result = self._run(call, _Progress(on_progress, _stage(len(call.seeds))))
        if not result.success:
            raise _failure(str(result.error), GENERATION_FAILED_TEXT)
        audios = enumerate(result.audios, start=1)
        return [_take(index, audio, call.seeds, call.out_dir) for index, audio in audios]

    def _run(self, call: _Call, progress: _Progress) -> Any:
        loaded, api = self._require_loaded(), self.api
        save_dir = call.out_dir / ENGINE_DIR
        save_dir.mkdir(parents=True, exist_ok=True)
        config = api.config(
            batch_size=len(call.seeds), use_random_seed=False, seeds=call.seeds, audio_format="flac"
        )
        try:
            return api.generate(
                loaded.dit, loaded.llm, api.params(**call.values), config,
                save_dir=str(save_dir), progress=progress,
            )  # fmt: skip
        finally:
            if progress.cancelled:
                raise Cancelled

    def _require_loaded(self) -> _Loaded:
        if self._loaded is None:
            raise GeneratorError(NOT_LOADED_TEXT)
        return self._loaded


def _stage(takes: int) -> str:
    return "Generating take 1 of 1" if takes == 1 else f"Generating {takes} takes"


def _failure(text: str, readable: str) -> GeneratorError:
    """``GpuOutOfMemoryError`` for OOM texts, else ``readable``; the raw text is logged."""
    _LOG.warning("ACE-Step failure: %s", text)
    lowered = text.lower()
    if any(marker in lowered for marker in _OOM_MARKERS):
        return GpuOutOfMemoryError()
    return GeneratorError(readable)


def _take(index: int, audio: dict[str, Any], seeds: list[int], out_dir: Path) -> EngineTake:
    """Rename ACE-Step's file to ``take-<index>.flac`` and describe it."""
    saved = str(audio.get("path") or "")
    if not saved or not Path(saved).is_file():
        raise GeneratorError(GENERATION_FAILED_TEXT)
    path = Path(saved).replace(out_dir / f"take-{index}.flac")
    seed = int(audio.get("params", {}).get("seed", seeds[index - 1]))
    return EngineTake(index=index, seed=seed, path=path, duration_seconds=sf.info(path).duration)
