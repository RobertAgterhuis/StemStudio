"""Tests of the BTC chord recognizer with a fake torch module and a fake runtime (016 T029).

Features: CQT 144 bins, 24 per octave, hop 2048 at 22,050 Hz, ``log(|C| + 1e-6)``, normalised with
the checkpoint's ``(x - mean) / std`` and zero-padded to 108-frame instances. The checkpoint comes
from the model store (pinned URL and SHA-256) per vocabulary and is loaded with
``torch.load(weights_only=True)`` inside ``safe_globals`` with exactly ``ports.BTC_SAFE_GLOBALS``.
The model runs through the injected ``DeviceRunner`` with ``BTC_GPU_NEED``; without weights the
chroma-hmm fallback runs and the frames say so (``fallback-engine``); progress per instance.
"""

import ast
import contextlib
import importlib
from collections.abc import Iterator
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.harmony import btc
from stemstudio_worker.harmony.btc import (
    BINS_PER_OCTAVE,
    FRAME_SECONDS,
    N_BINS,
    TIMESTEP,
    BtcModel,
    BtcParts,
    BtcRecognizer,
    TorchBtcRuntime,
    btc_labels,
    cqt_features,
    instances,
    model_config,
    safe_globals,
)
from stemstudio_worker.ports import (
    BTC_GPU_NEED,
    BTC_LARGE_VOCA_WEIGHTS,
    BTC_MAJMIN_WEIGHTS,
    BTC_SAFE_GLOBALS,
    CHORD_HOP,
    CHORD_SAMPLE_RATE,
    AnalysisCancelledError,
    AnalysisError,
    ChordFrames,
    DeviceUse,
    GpuNeed,
    TranscriptionError,
    WeightsFile,
)
from stemstudio_worker.schemas import ChordVocabulary

SR = CHORD_SAMPLE_RATE


def _sine(seconds: float, hz: float = 440.0) -> npt.NDArray[np.float32]:
    t = np.arange(int(seconds * SR)) / SR
    return (0.3 * np.sin(2 * np.pi * hz * t)).astype(np.float32)


# ------------------------------------------------------------------ fakes


@dataclass
class FakeModels:
    """A model store that hands out ``<root>/<model_id>.pt`` or fails like ``ModelStore``."""

    root: Path
    missing: bool = False
    requested: list[WeightsFile] = field(default_factory=list)

    def ensure(self, weights: WeightsFile) -> Path:
        self.requested.append(weights)
        if self.missing:
            raise TranscriptionError("The transcription model could not be downloaded.")
        path = self.root / f"{weights.model_id}.pt"
        path.write_bytes(b"weights")
        return path

    def ready(self, weights: WeightsFile) -> bool:
        return not self.missing


class FakeRuntime:
    """Stands in for the torch runtime: one-hot class 1 per frame, records every call."""

    def __init__(self, mean: float = 2.0, std: float = 4.0) -> None:
        self.mean, self.std = mean, std
        self.loads: list[tuple[Path, int, str]] = []
        self.windows: list[tuple[npt.NDArray[np.float32], str]] = []
        self.error: Exception | None = None

    def load(self, checkpoint: Path, classes: int, device: str) -> BtcModel:
        self.loads.append((checkpoint, classes, device))
        return BtcModel(network=("net", classes), mean=self.mean, std=self.std)

    def infer(
        self, model: BtcModel, window: npt.NDArray[np.float32], device: str
    ) -> npt.NDArray[np.float32]:
        if self.error is not None:
            raise self.error
        self.windows.append((window, device))
        out = np.zeros((window.shape[0], model.network[1]), dtype=np.float32)
        out[:, 1] = 1.0
        return out


class FallbackRecognizer:
    """The chroma-hmm stand-in: one-hot ``N`` frames."""

    def __init__(self) -> None:
        self.calls: list[ChordVocabulary] = []

    @property
    def engine(self) -> str:
        return "chroma-hmm"

    def recognize(self, audio: Any, vocabulary: ChordVocabulary, progress: Any) -> ChordFrames:
        self.calls.append(vocabulary)
        progress(1.0)
        return ChordFrames(
            posteriors=np.ones((3, 1), dtype=np.float32),
            labels=("N",),
            frame_seconds=FRAME_SECONDS,
            chroma=np.zeros((3, 12), dtype=np.float32),
            engine="chroma-hmm",
        )


class RecordingRunner(FakeDeviceRunner):
    """The fake device runner that remembers the need it was given."""

    def __init__(self, mode: str = "cpu") -> None:
        super().__init__(mode)  # type: ignore[arg-type]
        self.needs: list[GpuNeed] = []

    def run[T](self, work: Any, need: GpuNeed) -> tuple[T, DeviceUse]:
        self.needs.append(need)
        return super().run(work, need)


def _recognizer(
    tmp_path: Path, runtime: FakeRuntime | None = None, missing: bool = False, mode: str = "cpu"
) -> tuple[BtcRecognizer, FakeModels, RecordingRunner, FallbackRecognizer]:
    models, runner, fallback = (
        FakeModels(tmp_path, missing),
        RecordingRunner(mode),
        FallbackRecognizer(),
    )
    recognizer = BtcRecognizer(BtcParts(models, runner, runtime or FakeRuntime(), fallback))
    return recognizer, models, runner, fallback


# ------------------------------------------------------------------ features


def test_the_feature_constants_follow_the_btc_run_config() -> None:
    assert (N_BINS, BINS_PER_OCTAVE, TIMESTEP) == (144, 24, 108)
    assert FRAME_SECONDS == CHORD_HOP / CHORD_SAMPLE_RATE
    assert round(FRAME_SECONDS * 1000, 2) == 92.88


def test_cqt_features_are_log_magnitudes_of_144_bins_at_hop_2048_with_a_chroma() -> None:
    audio = _sine(3.0)

    features, chroma = cqt_features(audio)

    frames = 1 + len(audio) // CHORD_HOP
    assert features.shape == (frames, N_BINS)
    assert chroma.shape == (frames, 12)
    assert features.dtype == np.float32 and chroma.dtype == np.float32
    assert features.min() >= np.log(1e-6) - 1e-3
    assert int(np.argmax(chroma[frames // 2])) == 9  # A


def test_cqt_features_match_librosa_with_the_btc_parameters() -> None:
    import librosa  # noqa: PLC0415

    audio = _sine(2.0, 220.0)
    expected = librosa.cqt(audio, sr=SR, n_bins=144, bins_per_octave=24, hop_length=2048)

    features, _ = cqt_features(audio)

    np.testing.assert_allclose(features, np.log(np.abs(expected) + 1e-6).T, rtol=1e-4, atol=1e-4)


def test_silence_gives_the_floor_value_and_no_error() -> None:
    features, chroma = cqt_features(np.zeros(SR, dtype=np.float32))

    assert np.allclose(features, np.log(1e-6), atol=1e-3)
    assert np.all(np.isfinite(chroma))


def test_empty_audio_fails_readably() -> None:
    with pytest.raises(AnalysisError, match="no audio"):
        cqt_features(np.zeros(0, dtype=np.float32))


def test_instances_normalise_and_zero_pad_to_108_frames() -> None:
    features = np.full((250, N_BINS), 6.0, dtype=np.float32)

    windows = instances(features, mean=2.0, std=4.0)

    assert windows.shape == (3, TIMESTEP, N_BINS)
    assert np.all(windows.reshape(-1, N_BINS)[:250] == 1.0)
    assert np.all(windows.reshape(-1, N_BINS)[250:] == 0.0)


def test_an_exact_multiple_of_108_frames_gets_no_extra_instance() -> None:
    windows = instances(np.zeros((216, N_BINS), dtype=np.float32), mean=0.0, std=1.0)

    assert windows.shape == (2, TIMESTEP, N_BINS)


# ------------------------------------------------------------------ labels and config


def test_majmin_labels_are_the_25_btc_classes_in_harte_syntax() -> None:
    labels = btc_labels("simple")

    assert len(labels) == 25
    assert labels[:4] == ("C:maj", "C:min", "C#:maj", "C#:min")
    assert labels[22:] == ("B:maj", "B:min", "N")


def test_large_vocabulary_labels_are_170_classes_with_x_and_n_last() -> None:
    labels = btc_labels("full")

    assert len(labels) == 170
    assert labels[:3] == ("C:min", "C:maj", "C:dim")
    assert labels[13] == "C:sus4"
    assert labels[14] == "C#:min"
    assert labels[167] == "B:sus4"
    assert labels[168:] == ("X", "N")


def test_the_model_config_is_the_run_config_with_the_class_count() -> None:
    config = model_config(170)

    assert config["num_chords"] == 170
    assert (config["feature_size"], config["timestep"], config["num_layers"]) == (144, 108, 8)
    assert (config["num_heads"], config["hidden_size"], config["filter_size"]) == (4, 128, 128)
    assert config["probs_out"] is False


# ------------------------------------------------------------------ the recognizer


def test_simple_uses_the_majmin_checkpoint_on_the_device_runner_with_btc_gpu_need(
    tmp_path: Path,
) -> None:
    runtime = FakeRuntime()
    recognizer, models, runner, _ = _recognizer(tmp_path, runtime)

    frames = recognizer.recognize(_sine(12.0), "simple", lambda _f: None)

    assert models.requested == [BTC_MAJMIN_WEIGHTS]
    assert runner.needs == [BTC_GPU_NEED]
    assert runtime.loads == [(tmp_path / "btc-majmin.pt", 25, "cpu")]
    assert frames.engine == "btc" and not frames.fallback
    assert frames.labels == btc_labels("simple")
    assert frames.frame_seconds == FRAME_SECONDS
    assert frames.device == DeviceUse(kind="cpu", torch_device="cpu")


def test_full_uses_the_large_vocabulary_checkpoint(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    recognizer, models, _, _ = _recognizer(tmp_path, runtime)

    frames = recognizer.recognize(_sine(2.0), "full", lambda _f: None)

    assert models.requested == [BTC_LARGE_VOCA_WEIGHTS]
    assert runtime.loads[0][1] == 170
    assert frames.labels == btc_labels("full")
    assert frames.posteriors.shape[1] == 170


def test_posteriors_are_trimmed_to_the_audio_frames_and_match_the_chroma(tmp_path: Path) -> None:
    recognizer, *_ = _recognizer(tmp_path)
    audio = _sine(12.0)

    frames = recognizer.recognize(audio, "simple", lambda _f: None)

    count = 1 + len(audio) // CHORD_HOP
    assert frames.posteriors.shape == (count, 25)
    assert frames.chroma.shape == (count, 12)
    assert frames.posteriors.dtype == np.float32
    assert np.all(frames.posteriors[:, 1] == 1.0)


def test_each_instance_is_normalised_with_the_checkpoint_mean_and_std(tmp_path: Path) -> None:
    runtime = FakeRuntime(mean=1.5, std=3.0)
    recognizer, *_ = _recognizer(tmp_path, runtime)
    audio = _sine(12.0)

    recognizer.recognize(audio, "simple", lambda _f: None)

    features, _ = cqt_features(audio)
    expected = instances(features, mean=1.5, std=3.0)
    assert len(runtime.windows) == expected.shape[0] == 2
    for (window, device), want in zip(runtime.windows, expected, strict=True):
        np.testing.assert_allclose(window, want, rtol=1e-6)
        assert device == "cpu"


def test_progress_is_reported_per_instance_up_to_one(tmp_path: Path) -> None:
    recognizer, *_ = _recognizer(tmp_path)
    seen: list[float] = []

    recognizer.recognize(_sine(25.0), "simple", seen.append)

    assert seen == pytest.approx([1 / 3, 2 / 3, 1.0])


def test_a_cancel_from_the_progress_callback_propagates(tmp_path: Path) -> None:
    recognizer, *_ = _recognizer(tmp_path)

    def cancel(_fraction: float) -> None:
        raise AnalysisCancelledError("deleted")

    with pytest.raises(AnalysisCancelledError):
        recognizer.recognize(_sine(2.0), "simple", cancel)


def test_the_model_is_loaded_once_per_checkpoint_and_device(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    recognizer, *_ = _recognizer(tmp_path, runtime)

    recognizer.recognize(_sine(1.0), "simple", lambda _f: None)
    recognizer.recognize(_sine(1.0), "simple", lambda _f: None)
    recognizer.recognize(_sine(1.0), "full", lambda _f: None)

    assert [load[1] for load in runtime.loads] == [25, 170]


def test_release_drops_the_loaded_models(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    recognizer, *_ = _recognizer(tmp_path, runtime)
    recognizer.recognize(_sine(1.0), "simple", lambda _f: None)

    recognizer.release()
    recognizer.recognize(_sine(1.0), "simple", lambda _f: None)

    assert len(runtime.loads) == 2


def test_a_gpu_run_is_reported_with_the_gpu_device(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    recognizer, *_ = _recognizer(tmp_path, runtime, mode="gpu")

    frames = recognizer.recognize(_sine(1.0), "simple", lambda _f: None)

    assert frames.device is not None and frames.device.kind == "gpu"
    assert runtime.loads[0][2] == "cuda"


def test_model_errors_propagate_so_the_runner_can_retry(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    runtime.error = RuntimeError("CUDA out of memory")
    recognizer, *_ = _recognizer(tmp_path, runtime)

    with pytest.raises(RuntimeError, match="out of memory"):
        recognizer.recognize(_sine(1.0), "simple", lambda _f: None)


def test_unavailable_weights_fall_back_to_chroma_hmm_with_the_fallback_flag(
    tmp_path: Path,
) -> None:
    runtime = FakeRuntime()
    recognizer, _, runner, fallback = _recognizer(tmp_path, runtime, missing=True)
    seen: list[float] = []

    frames = recognizer.recognize(_sine(1.0), "full", seen.append)

    assert fallback.calls == ["full"]
    assert frames.engine == "chroma-hmm" and frames.fallback
    assert frames.device is None
    assert runtime.loads == [] and runner.needs == []
    assert seen == [1.0]


def test_the_engine_is_btc(tmp_path: Path) -> None:
    recognizer, *_ = _recognizer(tmp_path)

    assert recognizer.engine == "btc"


def test_readiness_per_model_comes_from_the_store(tmp_path: Path) -> None:
    ready, _, _, _ = _recognizer(tmp_path)
    missing, _, _, _ = _recognizer(tmp_path, missing=True)

    assert ready.models_ready() == {"btc-majmin": "ready", "btc-large-voca": "ready"}
    assert missing.models_ready() == {
        "btc-majmin": "not-downloaded",
        "btc-large-voca": "not-downloaded",
    }


# ------------------------------------------------------------------ the torch runtime (fake torch)


class _FakeTensor:
    """Just enough of a tensor for the runtime: wraps a numpy array."""

    def __init__(self, array: npt.NDArray[Any]) -> None:
        self.array = array
        self.devices: list[str] = []

    def float(self) -> "_FakeTensor":
        return _FakeTensor(self.array.astype(np.float32))

    def to(self, device: str) -> "_FakeTensor":
        self.devices.append(device)
        return self

    def detach(self) -> "_FakeTensor":
        return self

    def cpu(self) -> "_FakeTensor":
        return self

    def numpy(self) -> npt.NDArray[Any]:
        return self.array

    def __getitem__(self, index: int) -> "_FakeTensor":
        return _FakeTensor(self.array[index])


class _FakeNetwork:
    """The vendored ``BTC_model`` stand-in: logits favour class 2."""

    def __init__(self, config: dict[str, Any]) -> None:
        self.config = config
        self.state: Any = None
        self.device = "unset"
        self.evaluated = False
        self.output_layer = self
        self.inputs: list[_FakeTensor] = []

    def load_state_dict(self, state: Any) -> None:
        self.state = state

    def to(self, device: str) -> "_FakeNetwork":
        self.device = device
        return self

    def eval(self) -> "_FakeNetwork":
        self.evaluated = True
        return self

    def self_attn_layers(self, batch: _FakeTensor) -> tuple[_FakeTensor, list[Any]]:
        self.inputs.append(batch)
        return batch, []

    def output_projection(self, hidden: _FakeTensor) -> _FakeTensor:
        logits = np.zeros((*hidden.array.shape[:2], self.config["num_chords"]), dtype=np.float32)
        logits[..., 2] = 5.0
        return _FakeTensor(logits)


class _FakeSerialization:
    def __init__(self, torch: "_FakeTorch") -> None:
        self._torch = torch

    @contextlib.contextmanager
    def safe_globals(self, allowed: list[Any]) -> Iterator[None]:
        self._torch.allowed = allowed
        self._torch.inside = True
        try:
            yield
        finally:
            self._torch.inside = False


class _FakeTorch:
    """The torch functions the runtime uses, recording how they were called."""

    def __init__(self) -> None:
        self.serialization = _FakeSerialization(self)
        self.allowed: list[Any] | None = None
        self.inside = False
        self.loads: list[tuple[Path, dict[str, Any], bool]] = []
        self.threads: list[int] = []

    def set_num_threads(self, threads: int) -> None:
        self.threads.append(threads)

    def load(self, path: Path, **kwargs: Any) -> dict[str, Any]:
        self.loads.append((path, kwargs, self.inside))
        return {"model": {"w": 1}, "mean": np.float64(1.25), "std": np.float64(2.5)}

    @contextlib.contextmanager
    def no_grad(self) -> Iterator[None]:
        yield

    def from_numpy(self, array: npt.NDArray[Any]) -> _FakeTensor:
        return _FakeTensor(array)

    def softmax(self, tensor: _FakeTensor, dim: int) -> _FakeTensor:
        shifted = np.exp(tensor.array - tensor.array.max(axis=dim, keepdims=True))
        return _FakeTensor(shifted / shifted.sum(axis=dim, keepdims=True))


@pytest.fixture
def fake_torch(monkeypatch: pytest.MonkeyPatch) -> _FakeTorch:
    torch = _FakeTorch()
    monkeypatch.setattr(btc, "_torch", lambda: torch)
    vendored = importlib.import_module("stemstudio_worker.harmony.vendor.btc.btc_model")
    monkeypatch.setattr(vendored, "BTC_model", _FakeNetwork)
    return torch


def test_torch_load_uses_weights_only_inside_the_allow_listed_globals(
    tmp_path: Path, fake_torch: _FakeTorch
) -> None:
    checkpoint = tmp_path / "btc.pt"

    model = TorchBtcRuntime(8).load(checkpoint, 25, "cpu")

    assert fake_torch.loads == [(checkpoint, {"map_location": "cpu", "weights_only": True}, True)]
    assert fake_torch.allowed == safe_globals()
    assert fake_torch.threads == [8]
    assert (model.mean, model.std) == (1.25, 2.5)
    assert isinstance(model.mean, float)
    network = model.network
    assert network.config == model_config(25)
    assert (network.state, network.device, network.evaluated) == ({"w": 1}, "cpu", True)


def test_infer_returns_softmax_posteriors_of_one_instance(fake_torch: _FakeTorch) -> None:
    runtime = TorchBtcRuntime(4)
    model = runtime.load(Path("btc.pt"), 25, "cuda")
    window = np.zeros((TIMESTEP, N_BINS), dtype=np.float32)

    posteriors = runtime.infer(model, window, "cuda")

    assert posteriors.shape == (TIMESTEP, 25)
    assert posteriors.dtype == np.float32
    np.testing.assert_allclose(posteriors.sum(axis=1), 1.0, rtol=1e-5)
    assert np.all(np.argmax(posteriors, axis=1) == 2)
    assert model.network.inputs[0].array.shape == (1, TIMESTEP, N_BINS)
    assert model.network.inputs[0].devices == ["cuda"]


def test_safe_globals_are_exactly_the_three_numpy_names() -> None:
    allowed = safe_globals()

    assert [name for _, name in allowed] == list(BTC_SAFE_GLOBALS)
    assert allowed[1][0] is np.dtype
    assert allowed[2][0] is np.dtypes.Float64DType
    assert callable(allowed[0][0])


def test_the_real_torch_refuses_a_global_outside_the_allow_list(tmp_path: Path) -> None:
    import torch  # noqa: PLC0415

    checkpoint = tmp_path / "evil.pt"
    torch.save({"model": {}, "mean": np.float64(0.0), "std": np.float64(1.0), "x": Path("p")},
               checkpoint)  # fmt: skip

    with pytest.raises(Exception, match=r"(?i)weights_only|unsupported global"):
        TorchBtcRuntime(1).load(checkpoint, 25, "cpu")


def test_the_real_torch_loads_a_plain_checkpoint_inside_the_allow_list(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import torch  # noqa: PLC0415

    vendored = importlib.import_module("stemstudio_worker.harmony.vendor.btc.btc_model")
    monkeypatch.setattr(vendored, "BTC_model", _RealTinyNetwork)
    checkpoint = tmp_path / "ok.pt"
    torch.save({"model": {}, "mean": 0.5, "std": 2.0}, checkpoint)  # the real BTC files: T063
    threads = torch.get_num_threads()

    model = TorchBtcRuntime(1).load(checkpoint, 25, "cpu")
    torch.set_num_threads(threads)

    assert (model.mean, model.std) == (0.5, 2.0)


class _RealTinyNetwork:
    def __init__(self, config: dict[str, Any]) -> None:
        self.config = config

    def load_state_dict(self, state: Any) -> None:
        self.state = state

    def to(self, device: str) -> "_RealTinyNetwork":
        return self

    def eval(self) -> "_RealTinyNetwork":
        return self


# ------------------------------------------------------------------ security rules


def _imports(path: Path) -> set[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"))
    names = {alias.name.split(".")[0] for node in ast.walk(tree)
             if isinstance(node, ast.Import) for alias in node.names}  # fmt: skip
    return names | {(node.module or "").split(".")[0] for node in ast.walk(tree)
                    if isinstance(node, ast.ImportFrom)}  # fmt: skip


def test_no_mir_eval_pretty_midi_pandas_or_pyrubberband_in_btc_or_its_vendor() -> None:
    folder = Path(btc.__file__).parent
    files = [folder / "btc.py", *sorted((folder / "vendor" / "btc").glob("*.py"))]
    banned = {"mir_eval", "pretty_midi", "pandas", "pyrubberband", "yaml", "subprocess"}

    assert len(files) >= 3
    for path in files:
        assert not _imports(path) & banned, path


def test_the_vendor_has_the_mit_licence_and_names_the_commit() -> None:
    folder = Path(btc.__file__).parent / "vendor" / "btc"

    assert "MIT License" in (folder / "LICENSE").read_text(encoding="utf-8")
    for path in sorted(folder.glob("*.py")):
        text = path.read_text(encoding="utf-8")
        if path.name != "__init__.py":
            assert "2682317be668032e6e4b269ded36adaa2ad57df0" in text.splitlines()[0], path


def test_no_weights_only_false_and_no_unpinned_download_in_btc() -> None:
    folder = Path(btc.__file__).parent
    for path in [folder / "btc.py", *sorted((folder / "vendor" / "btc").glob("*.py"))]:
        text = path.read_text(encoding="utf-8")
        assert "weights_only=False" not in text, path
        assert "urlopen" not in text and "requests" not in text, path
