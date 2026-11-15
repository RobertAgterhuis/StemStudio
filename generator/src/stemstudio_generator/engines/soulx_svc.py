"""SoulX-Singer-SVC behind ``VoiceConverter`` (singer service, ADR-019, 010 research S-05).

The model is built once in fp16 on CUDA from the pinned checkpoint ``<model_dir>/soulx/
model-svc.pt`` (verified against its SHA-256 before ``torch.load``). Its content encoder
asks ``from_pretrained("openai/whisper-base")``; while the model is built that name is mapped
to the pinned local snapshot ``<model_dir>/soulx/whisper-base`` (no hub access, no upstream
change). F0 comes from RMVPE (``F0Extractor`` on the SVC frame grid, 24 kHz / hop 480). A target
with less than 10 % voiced frames fails with ``unvoicedTarget``; a target longer than
``STEMSTUDIO_SVC_MAX_TARGET_SECONDS`` with ``invalidInput``.
"""

import gc
from collections.abc import Callable, Iterator
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final

import numpy as np
import soundfile as sf

from stemstudio_generator.config import TARGET_MIN_VOICED_RATIO, GeneratorSettings
from stemstudio_generator.engines.profiles import SOULX_MODELS, model_statuses
from stemstudio_generator.engines.soulx_api import SoulXApi, import_soulx, load_f0_module
from stemstudio_generator.engines.weights import WeightsDir
from stemstudio_generator.ports import (
    RMVPE_WEIGHTS,
    SOULX_SVC_FILE,
    SOULX_SVC_WEIGHTS,
    SVC_SAMPLE_RATE,
    WHISPER_BASE_DIR,
    WHISPER_BASE_WEIGHTS,
    ConversionInput,
    DeviceUse,
    GeneratorError,
    InvalidInputError,
    ProgressCallback,
    PromptInvalidError,
    Signal,
    UnvoicedTargetError,
    voiced_ratio,
)
from stemstudio_generator.schemas import UNVOICED_TARGET_TEXT, ModelId, ModelStatus

__all__ = [
    "CONVERTING_STAGE",
    "SoulXApi",
    "SoulXConverter",
    "SoulXWeights",
    "load_f0_module",
    "soulx_weights",
]

CONVERTING_STAGE: Final = "Converting voice"
NOT_LOADED_TEXT: Final = "The voice model is not loaded."
TARGET_TOO_LONG_TEXT: Final = "The vocal stem is longer than {limit:g} s ({seconds:.1f} s)."
WHISPER_HUB_ID: Final = "openai/whisper-base"
SVC_HOP: Final = 480
"""Hop of the SVC frame grid at 24 kHz (``soulxsinger.yaml``)."""


@dataclass(frozen=True, slots=True)
class SoulXWeights:
    """The singer's three pinned weight sets.

    Attributes:
        svc: ``soulx/model-svc.pt``.
        rmvpe: ``soulx/rmvpe/rmvpe.pt``.
        whisper: ``soulx/whisper-base/``.
    """

    svc: WeightsDir
    rmvpe: WeightsDir
    whisper: WeightsDir

    def all(self) -> tuple[WeightsDir, ...]:
        """The three sets."""
        return (self.svc, self.rmvpe, self.whisper)


def soulx_weights(model_dir: Path) -> SoulXWeights:
    """The pinned singer weights under ``model_dir``."""
    soulx = model_dir / "soulx"
    return SoulXWeights(
        svc=WeightsDir(soulx, SOULX_SVC_WEIGHTS, only_pinned=True),
        rmvpe=WeightsDir(soulx, RMVPE_WEIGHTS, only_pinned=True),
        whisper=WeightsDir(model_dir / WHISPER_BASE_DIR, WHISPER_BASE_WEIGHTS),
    )


class _LocalPretrained:
    """``from_pretrained`` wrapper mapping the whisper hub id to the local snapshot."""

    def __init__(self, original: Any, local: Path) -> None:
        self._original = original
        self._local = str(local)

    def from_pretrained(self, name: str, *args: Any, **kwargs: Any) -> Any:
        target = self._local if name == WHISPER_HUB_ID else name
        return self._original.from_pretrained(target, *args, **kwargs)


@contextmanager
def _local_whisper(module: Any, local: Path) -> Iterator[None]:
    """Map whisper-base to ``local`` in SoulX's whisper encoder module while building."""
    names = ("WhisperFeatureExtractor", "WhisperModel")
    originals = {name: getattr(module, name) for name in names}
    for name, original in originals.items():
        setattr(module, name, _LocalPretrained(original, local))
    try:
        yield
    finally:
        for name, original in originals.items():
            setattr(module, name, original)


@dataclass(slots=True)
class _Loaded:
    model: Any
    f0: Any


class SoulXConverter:
    """``VoiceConverter`` over SoulX-Singer-SVC."""

    def __init__(
        self,
        settings: GeneratorSettings,
        api: Callable[[], SoulXApi] = import_soulx,
        weights: SoulXWeights | None = None,
    ) -> None:
        """Use the weights under ``settings.model_dir``; ``api`` imports SoulX on first use."""
        self._weights = weights or soulx_weights(settings.model_dir)
        self._limit = settings.svc_max_target_seconds
        self._import = api
        self._api: SoulXApi | None = None
        self._loaded: _Loaded | None = None

    @property
    def api(self) -> SoulXApi:
        """The SoulX API (imported on first use)."""
        if self._api is None:
            self._api = self._import()
        return self._api

    def required_models(self) -> list[ModelStatus]:
        """SVC checkpoint, RMVPE and whisper-base with presence from the pinned cache."""
        checks: dict[ModelId, Callable[[], bool]] = {
            "soulx-singer-svc": lambda: self._weights.svc.file_present(Path(SOULX_SVC_FILE).name),
            "rmvpe": lambda: self._weights.rmvpe.file_present("rmvpe/rmvpe.pt"),
            "whisper-base": lambda: self._weights.whisper.file_present("model.safetensors"),
        }
        return model_statuses(SOULX_MODELS, lambda model: checks[model]())

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Download (first use), verify and build the model in fp16 on CUDA (once)."""
        del use  # one profile (SVC_FP16)
        for weights in self._weights.all():
            weights.ensure(on_progress)
            weights.verify()
        if self._loaded is None:
            self._loaded = _Loaded(self._build(), self._extractor())

    def convert(self, request: ConversionInput, on_progress: ProgressCallback) -> Signal:
        """Convert the target's timbre to the prompt's (24 kHz mono float32)."""
        loaded = self._require_loaded()
        self._check_target(request.target)
        _duration(request.prompt, PromptInvalidError)
        on_progress(0.0, CONVERTING_STAGE)
        gt_f0 = loaded.f0.process(str(request.target))
        if voiced_ratio(np.asarray(gt_f0, np.float32)) < TARGET_MIN_VOICED_RATIO:
            raise UnvoicedTargetError(UNVOICED_TARGET_TEXT)
        pt_f0 = loaded.f0.process(str(request.prompt))
        vocal = self._infer(loaded.model, request, (pt_f0, gt_f0))
        on_progress(1.0, CONVERTING_STAGE)
        return vocal

    def unload(self) -> None:
        """Drop the model and empty the CUDA cache."""
        if self._loaded is None:
            return
        self._loaded = None
        gc.collect()
        self.api.torch.cuda.empty_cache()

    def _build(self) -> Any:
        api = self.api
        config = api.load_config(str(api.config_path))
        with _local_whisper(api.whisper, self._weights.whisper.root):
            model = api.svc(config).to("cuda")
        checkpoint = api.torch.load(
            str(self._weights.svc.root / Path(SOULX_SVC_FILE).name),
            weights_only=True, map_location="cpu",
        )  # fmt: skip
        model.load_state_dict(checkpoint["state_dict"], strict=True)
        model.half()
        model.mel.float()
        model.eval()
        return model

    def _extractor(self) -> Any:
        return self.api.f0_extractor(
            str(self._weights.rmvpe.root / "rmvpe" / "rmvpe.pt"), device="cuda", is_half=False,
            target_sr=SVC_SAMPLE_RATE, hop_size=SVC_HOP, max_duration=self._limit + 1.0,
            verbose=False,
        )  # fmt: skip

    def _infer(self, model: Any, request: ConversionInput, f0s: tuple[Any, Any]) -> Signal:
        params, torch = request.params, self.api.torch
        torch.manual_seed(0)
        inputs = self._tensors(request, f0s)
        with torch.no_grad():
            audio, _ = model.infer(
                **inputs, auto_shift=params.auto_shift, pitch_shift=params.pitch_shift,
                n_steps=params.steps, cfg=params.cfg, use_fp16=True,
            )  # fmt: skip
        return np.asarray(audio.squeeze().float().cpu().numpy(), dtype=np.float32).reshape(-1)

    def _tensors(self, request: ConversionInput, f0s: tuple[Any, Any]) -> dict[str, Any]:
        """Prompt/target waveforms (24 kHz) and F0 curves as CUDA tensors."""
        api, torch = self.api, self.api.torch
        pt_wav, gt_wav = (
            api.load_wav(str(path), SVC_SAMPLE_RATE).to("cuda")
            for path in (request.prompt, request.target)
        )
        pt_f0, gt_f0 = (
            torch.from_numpy(np.asarray(f0, np.float32)).float().unsqueeze(0).to("cuda")
            for f0 in f0s
        )
        return {"pt_wav": pt_wav, "gt_wav": gt_wav, "pt_f0": pt_f0, "gt_f0": gt_f0}

    def _check_target(self, target: Path) -> None:
        seconds = _duration(target, InvalidInputError)
        if seconds > self._limit:
            raise InvalidInputError(TARGET_TOO_LONG_TEXT.format(limit=self._limit, seconds=seconds))

    def _require_loaded(self) -> _Loaded:
        if self._loaded is None:
            raise GeneratorError(NOT_LOADED_TEXT)
        return self._loaded


def _duration(path: Path, error: type[GeneratorError]) -> float:
    """Length of an audio file; ``error`` when it cannot be read."""
    try:
        return float(sf.info(path).duration)
    except (sf.LibsndfileError, RuntimeError, OSError) as exc:
        raise error("The audio file could not be read.") from exc
