"""The BTC chord model (Park et al. 2019) behind the ``ChordRecognizer`` port (ADR-031).

Features follow the BTC run config: a CQT of 144 bins, 24 per octave, hop 2048 at 22,050 Hz
(92.88 ms per frame), ``log(|C| + 1e-6)``, normalised with the checkpoint's ``mean``/``std`` and
zero-padded into instances of 108 frames. Each instance runs through the vendored
self-attention layers; the softmax of the output projection gives the class posteriors.

The checkpoint per vocabulary (``simple`` -> ``btc-majmin``, ``full`` -> ``btc-large-voca``) comes
from the model store (pinned URL, SHA-256 checked, never a shell download) and is loaded with
``torch.load(weights_only=True)`` allowing only ``ports.BTC_SAFE_GLOBALS``. The model runs through
the 003 ``DeviceRunner`` with ``BTC_GPU_NEED``. When the weights are unavailable the injected
fallback recognizer (chroma-hmm) runs and the frames carry ``fallback=True`` (``fallback-engine``).
"""

import importlib
from collections.abc import Callable
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any, Final, Protocol

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import (
    BTC_GPU_NEED,
    BTC_LARGE_VOCA_WEIGHTS,
    BTC_MAJMIN_WEIGHTS,
    BTC_SAFE_GLOBALS,
    CHORD_HOP,
    CHORD_SAMPLE_RATE,
    AnalysisError,
    AnalysisProgress,
    ChordFrames,
    ChordRecognizer,
    DeviceRunner,
    MonoAudio,
    TranscriptionError,
    WeightsFile,
)
from stemstudio_worker.schemas import ChordEngine, ChordVocabulary, ModelReadiness

N_BINS: Final = 144
"""CQT bins of the BTC features."""

BINS_PER_OCTAVE: Final = 24
"""CQT resolution (quarter tones)."""

TIMESTEP: Final = 108
"""Frames per model instance (10 s)."""

FRAME_SECONDS: Final = CHORD_HOP / CHORD_SAMPLE_RATE
"""Frame hop in seconds (92.88 ms)."""

NO_AUDIO_TEXT: Final = "The harmonic stems contain no audio."
"""Failure text of an empty mix."""

_FLOOR: Final = 1e-6
_VENDOR: Final = "stemstudio_worker.harmony.vendor.btc"
_WEIGHTS: Final[dict[ChordVocabulary, WeightsFile]] = {
    "simple": BTC_MAJMIN_WEIGHTS, "full": BTC_LARGE_VOCA_WEIGHTS,
}  # fmt: skip
_RUN_CONFIG: Final[dict[str, Any]] = {
    "feature_size": N_BINS, "timestep": TIMESTEP, "input_dropout": 0.2, "layer_dropout": 0.2,
    "attention_dropout": 0.2, "relu_dropout": 0.2, "num_layers": 8, "num_heads": 4,
    "hidden_size": 128, "total_key_depth": 128, "total_value_depth": 128, "filter_size": 128,
    "loss": "ce", "probs_out": False,
}  # fmt: skip

Floats = npt.NDArray[np.float32]


def _torch() -> Any:
    return importlib.import_module("torch")


def _labels_module() -> Any:
    return importlib.import_module(f"{_VENDOR}.labels")


def btc_labels(vocabulary: ChordVocabulary) -> tuple[str, ...]:
    """Harte label per model class in column order (``C`` -> ``C:maj``; ``N`` and ``X`` kept)."""
    vendored = _labels_module()
    if vocabulary == "simple":
        names = list(vendored.idx2chord)
    else:
        table = vendored.idx2voca_chord()
        names = [table[i] for i in range(len(table))]
    return tuple(_harte(name) for name in names)


def _harte(name: str) -> str:
    return name if ":" in name or name in ("N", "X") else f"{name}:maj"


def model_config(classes: int) -> dict[str, Any]:
    """The ``model`` section of the BTC run config with ``num_chords = classes``."""
    return {**_RUN_CONFIG, "num_chords": classes}


def cqt_features(audio: MonoAudio) -> tuple[Floats, Floats]:
    """``(frames, 144)`` log-CQT and ``(frames, 12)`` chroma of ``audio`` at 22,050 Hz.

    Raises:
        AnalysisError: ``audio`` is empty.
    """
    if audio.size == 0:
        raise AnalysisError(NO_AUDIO_TEXT)
    magnitude = np.abs(librosa.cqt(audio, sr=CHORD_SAMPLE_RATE, hop_length=CHORD_HOP,
                                   n_bins=N_BINS, bins_per_octave=BINS_PER_OCTAVE))  # fmt: skip
    chroma = librosa.feature.chroma_cqt(C=magnitude, bins_per_octave=BINS_PER_OCTAVE, n_chroma=12)
    features = np.log(magnitude + _FLOOR).T.astype(np.float32)
    return features, np.nan_to_num(chroma.T).astype(np.float32)


def instances(features: Floats, mean: float, std: float) -> Floats:
    """``(n, 108, 144)`` normalised instances, the last one zero-padded."""
    normalised = (features - mean) / std
    pad = (-normalised.shape[0]) % TIMESTEP
    padded = np.pad(normalised, ((0, pad), (0, 0)), mode="constant")
    return padded.reshape(-1, TIMESTEP, features.shape[1]).astype(np.float32)


def safe_globals() -> list[tuple[Any, str]]:
    """``(object, qualified name)`` of every ``BTC_SAFE_GLOBALS`` entry for ``safe_globals``."""
    allowed = []
    for name in BTC_SAFE_GLOBALS:
        module, _, attribute = name.rpartition(".")
        allowed.append((getattr(importlib.import_module(module), attribute), name))
    return allowed


@dataclass(frozen=True, slots=True)
class BtcModel:
    """A loaded checkpoint: the network and its feature normalisation."""

    network: Any
    mean: float
    std: float


class BtcRuntime(Protocol):
    """The torch operations of the recognizer (``TorchBtcRuntime``; a fake in unit tests)."""

    def load(self, checkpoint: Path, classes: int, device: str) -> BtcModel:
        """Build the network for ``classes`` from ``checkpoint`` on ``device``."""
        ...

    def infer(self, model: BtcModel, window: Floats, device: str) -> Floats:
        """``(108, classes)`` posteriors of one normalised ``(108, 144)`` instance."""
        ...


class TorchBtcRuntime:
    """``BtcRuntime`` over torch and the vendored ``BTC_model``."""

    def __init__(self, threads: int) -> None:
        """Cap torch's intra-op threads at ``threads`` (``STEMSTUDIO_ANALYSIS_THREADS``)."""
        self._threads = threads

    def load(self, checkpoint: Path, classes: int, device: str) -> BtcModel:
        """Load the state dict with ``weights_only=True`` and only the allow-listed globals."""
        torch = _torch()
        torch.set_num_threads(self._threads)
        network = importlib.import_module(f"{_VENDOR}.btc_model").BTC_model(
            config=model_config(classes)
        )
        with torch.serialization.safe_globals(safe_globals()):
            state = torch.load(checkpoint, map_location=device, weights_only=True)
        network.load_state_dict(state["model"])
        return BtcModel(network.to(device).eval(), float(state["mean"]), float(state["std"]))

    def infer(self, model: BtcModel, window: Floats, device: str) -> Floats:
        """Self-attention layers, output projection and softmax without gradients."""
        torch = _torch()
        with torch.no_grad():
            batch = torch.from_numpy(window[None, :, :]).float().to(device)
            hidden, _weights = model.network.self_attn_layers(batch)
            logits = model.network.output_layer.output_projection(hidden)
            probabilities = torch.softmax(logits, dim=-1)
        return np.asarray(probabilities.detach().cpu().numpy()[0], dtype=np.float32)


class WeightsSource(Protocol):
    """Hands out verified weight files (``main.ChordModelStore`` over the 008 model store)."""

    def ensure(self, weights: WeightsFile) -> Path:
        """The verified file of ``weights`` (``TranscriptionError`` when unavailable)."""
        ...

    def ready(self, weights: WeightsFile) -> bool:
        """Whether a verified copy is cached."""
        ...


@dataclass(frozen=True, slots=True)
class BtcParts:
    """What the BTC recognizer runs on.

    Attributes:
        models: Hands out the verified checkpoints.
        devices: The 003 device runner.
        runtime: The torch operations.
        fallback: The recognizer that runs without weights (chroma-hmm).
    """

    models: WeightsSource
    devices: DeviceRunner
    runtime: BtcRuntime
    fallback: ChordRecognizer


class BtcRecognizer:
    """``ChordRecognizer`` over BTC with a chroma-hmm fallback when the weights are missing."""

    def __init__(self, parts: BtcParts) -> None:
        """Fetch checkpoints, run the runtime on the device runner; else run the fallback."""
        self._models = parts.models
        self._devices = parts.devices
        self._runtime = parts.runtime
        self._fallback = parts.fallback
        self._loaded: dict[tuple[Path, str], BtcModel] = {}

    @property
    def engine(self) -> ChordEngine:
        """``btc``."""
        return "btc"

    @property
    def fallback(self) -> ChordRecognizer:
        """The recognizer that runs without weights (chroma-hmm)."""
        return self._fallback

    def models_ready(self) -> dict[str, ModelReadiness]:
        """``Health.instrumentAnalysis.models``: readiness per BTC model id."""
        return {w.model_id: "ready" if self._models.ready(w) else "not-downloaded"
                for w in _WEIGHTS.values()}  # fmt: skip

    def recognize(
        self, audio: MonoAudio, vocabulary: ChordVocabulary, progress: AnalysisProgress
    ) -> ChordFrames:
        """Posteriors per 92.88 ms frame on the device the runner picks.

        Raises:
            AnalysisError: The mix is empty (or the fallback cannot run).
            AnalysisCancelledError: Propagated from ``progress``.
        """
        try:
            checkpoint = self._models.ensure(_WEIGHTS[vocabulary])
        except TranscriptionError:
            frames = self._fallback.recognize(audio, vocabulary, progress)
            return replace(frames, fallback=True)
        features, chroma = cqt_features(audio)
        labels = btc_labels(vocabulary)
        work = self._work((checkpoint, len(labels)), features, progress)
        posteriors, use = self._devices.run(work, BTC_GPU_NEED)
        return ChordFrames(posteriors=posteriors[: features.shape[0]], labels=labels,
                           frame_seconds=FRAME_SECONDS, chroma=chroma, engine="btc",
                           device=use)  # fmt: skip

    def release(self) -> None:
        """Drop the loaded models (idle hook)."""
        self._loaded.clear()

    def _work(
        self, model_file: tuple[Path, int], features: Floats, progress: AnalysisProgress
    ) -> Callable[[str], Floats]:
        """The device step: ``model_file`` is the checkpoint and its class count."""

        def work(device: str) -> Floats:
            model = self._model(*model_file, device)
            windows = instances(features, model.mean, model.std)
            outputs = []
            for index, window in enumerate(windows, 1):
                outputs.append(self._runtime.infer(model, window, device))
                progress(index / len(windows))
            return np.concatenate(outputs).astype(np.float32)

        return work

    def _model(self, checkpoint: Path, classes: int, device: str) -> BtcModel:
        key = (checkpoint, device)
        if key not in self._loaded:
            self._loaded[key] = self._runtime.load(checkpoint, classes, device)
        return self._loaded[key]
