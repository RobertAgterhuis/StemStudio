"""A fake of the SoulX-Singer-SVC Python API (model, config, wav loader, RMVPE, whisper)."""

from dataclasses import dataclass, field
from pathlib import Path
from types import SimpleNamespace
from typing import Any

import numpy as np

from stemstudio_generator.engines.soulx_svc import SoulXApi


@dataclass
class SoulXRecorder:
    """Shared record of every call the adapter makes."""

    built: list[Any] = field(default_factory=list)
    loads: list[tuple[str, dict[str, Any]]] = field(default_factory=list)
    infers: list[dict[str, Any]] = field(default_factory=list)
    f0_inits: list[dict[str, Any]] = field(default_factory=list)
    rmvpe_inits: list[dict[str, Any]] = field(default_factory=list)
    whisper_names: list[str] = field(default_factory=list)
    seeds: list[int] = field(default_factory=list)
    emptied: int = 0
    voiced: float = 0.8
    """Share of voiced F0 frames the fake extractor returns."""
    infer_error: BaseException | None = None


class _Tensor:
    """Just enough of a torch tensor for the adapter."""

    def __init__(self, data: np.ndarray, recorder: SoulXRecorder) -> None:
        self.data = data
        self.recorder = recorder
        self.device = "cpu"
        self.dtype = "float32"

    def to(self, device: str) -> "_Tensor":
        self.device = device
        return self

    def float(self) -> "_Tensor":
        return self

    def unsqueeze(self, dim: int) -> "_Tensor":
        return _Tensor(np.expand_dims(self.data, dim), self.recorder)

    def squeeze(self) -> "_Tensor":
        return _Tensor(np.squeeze(self.data), self.recorder)

    def cpu(self) -> "_Tensor":
        return self

    def numpy(self) -> np.ndarray:
        return self.data


class _Model:
    def __init__(self, recorder: SoulXRecorder, config: Any) -> None:
        self.recorder = recorder
        self.config = config
        self.mel = SimpleNamespace(float=lambda: None)
        self.state: list[str] = []
        recorder.built.append(config)
        _whisper_names(recorder)

    def to(self, device: str) -> "_Model":
        self.state.append(f"to {device}")
        return self

    def load_state_dict(self, state: Any, strict: bool) -> None:
        self.state.append(f"state strict={strict}")

    def half(self) -> "_Model":
        self.state.append("half")
        return self

    def eval(self) -> "_Model":
        self.state.append("eval")
        return self

    def infer(self, **kwargs: Any) -> tuple[_Tensor, int]:
        self.recorder.infers.append(kwargs)
        if self.recorder.infer_error is not None:
            raise self.recorder.infer_error
        frames = kwargs["gt_wav"].data.shape[-1]
        return _Tensor(np.full((1, frames), 0.25, np.float32), self.recorder), 0


def _whisper_names(recorder: SoulXRecorder) -> None:
    """What SoulX's ``WhisperEncoder()`` asks ``from_pretrained`` for (hub id, patched)."""
    module = _WHISPER_MODULES[id(recorder)]
    module.WhisperFeatureExtractor.from_pretrained("openai/whisper-base")
    module.WhisperModel.from_pretrained("openai/whisper-base")


_WHISPER_MODULES: dict[int, Any] = {}


class _Pretrained:
    def __init__(self, recorder: SoulXRecorder) -> None:
        self.recorder = recorder

    def from_pretrained(self, name: str, *args: Any, **kwargs: Any) -> object:
        self.recorder.whisper_names.append(str(name))
        return object()


class _F0Extractor:
    recorder: SoulXRecorder

    def __init__(self, model_path: str, device: str = "cpu", **kwargs: Any) -> None:
        self.recorder.f0_inits.append({"model_path": model_path, "device": device, **kwargs})

    def process(self, audio_path: str, **kwargs: Any) -> np.ndarray:
        frames = 500
        voiced = round(frames * self.recorder.voiced)
        f0: np.ndarray = np.r_[np.full(voiced, 220.0), np.zeros(frames - voiced)]
        return f0.astype(np.float32)


class _Rmvpe:
    recorder: SoulXRecorder

    def __init__(self, model_path: str, is_half: bool, device: str | None = None) -> None:
        self.recorder.rmvpe_inits.append(
            {"model_path": model_path, "is_half": is_half, "device": device}
        )

    def infer_from_audio(self, audio: np.ndarray, thred: float = 0.03) -> np.ndarray:
        frames = audio.size // 160 + 1
        f0: np.ndarray = np.where(np.arange(frames) % 2 == 0, 200.0, 0.0).astype(np.float32)
        return f0


def fake_soulx(recorder: SoulXRecorder) -> SoulXApi:
    """A ``SoulXApi`` over fakes."""
    whisper = SimpleNamespace(
        WhisperFeatureExtractor=_Pretrained(recorder), WhisperModel=_Pretrained(recorder)
    )
    _WHISPER_MODULES[id(recorder)] = whisper

    def load_wav(path: str, sample_rate: int) -> _Tensor:
        return _Tensor(np.zeros((1, sample_rate * 12), np.float32), recorder)

    def manual_seed(seed: int) -> None:
        recorder.seeds.append(seed)

    def empty_cache() -> None:
        recorder.emptied += 1

    def load(path: str, **kwargs: Any) -> dict[str, Any]:
        recorder.loads.append((str(path), kwargs))
        return {"state_dict": {}}

    torch = SimpleNamespace(
        load=load,
        from_numpy=lambda data: _Tensor(data, recorder),
        manual_seed=manual_seed,
        no_grad=_NoGrad,
        cuda=SimpleNamespace(empty_cache=empty_cache),
    )
    return SoulXApi(
        svc=lambda config: _Model(recorder, config),
        load_config=lambda path: {"config": str(path)},
        load_wav=load_wav,
        f0_extractor=type("F0", (_F0Extractor,), {"recorder": recorder}),
        rmvpe=type("Rmvpe", (_Rmvpe,), {"recorder": recorder}),
        load_16k=lambda path: np.zeros(16_000 * 3, np.float32),
        torch=torch,
        whisper=whisper,
        config_path=Path("/opt/soulx-singer/soulxsinger/config/soulxsinger.yaml"),
    )


class _NoGrad:
    def __enter__(self) -> None:
        return None

    def __exit__(self, *args: object) -> None:
        return None
