"""Tests of the fake aligner, the Whisper runtime (fake modules) and the factories (006 T040)."""

import sys
import types
from pathlib import Path
from typing import Any

import numpy as np
import pytest

from stemstudio_worker.alignment import create_aligner, create_vocal_loader, whisper_weights
from stemstudio_worker.alignment.audio_prep import VocalAudioLoader
from stemstudio_worker.alignment.fake import FakeAligner
from stemstudio_worker.alignment.stable_ts_aligner import StableTsAligner
from stemstudio_worker.alignment.whisper_runtime import WhisperRuntime
from stemstudio_worker.config import LyricsSettings, WorkerSettings
from stemstudio_worker.ports import WHISPER_TURBO_WEIGHTS, AlignmentError, AlignRequest
from stemstudio_worker.schemas import ALIGNMENT_FAILED_TEXT

from .fakes import TEXT


def _ignore(fraction: float, stage: str) -> None:
    """Drop progress reports."""


def _settings(tmp_path: Path, **changes: Any) -> WorkerSettings:
    return WorkerSettings(data_dir=tmp_path, model_dir=tmp_path / "models", **changes)


# ------------------------------------------------------------------ fake aligner


def test_the_fake_aligner_puts_word_k_at_2000_plus_500_k_ms() -> None:
    audio = np.zeros(16_000 * 8, dtype=np.float32)

    document = FakeAligner("word").align(AlignRequest(audio, TEXT, "en"), "cpu", _ignore)

    spans = [(w.start_ms, w.end_ms) for line in document.lines for w in line.words]
    assert spans == [(2000 + 500 * k, 2500 + 500 * k) for k in range(6)]
    assert [(line.start_ms, line.end_ms) for line in document.lines] == [(2000, 3500), (3500, 5000)]
    assert (document.origin, document.source, document.language) == ("aligned", "user", "en")


def test_the_fake_aligner_defaults_to_english() -> None:
    audio = np.zeros(16_000, dtype=np.float32)

    document = FakeAligner("plain").align(AlignRequest(audio, TEXT), "cpu", _ignore)

    assert document.language == "en"


def test_the_fake_aligner_fails_in_failed_mode() -> None:
    request = AlignRequest(np.zeros(16, dtype=np.float32), TEXT)

    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        FakeAligner("failed").align(request, "cpu", _ignore)


def test_the_fake_aligner_has_nothing_to_release() -> None:
    FakeAligner("word").release()


# ------------------------------------------------------------------ factories


def test_the_fake_stack_gets_the_fake_aligner(tmp_path: Path) -> None:
    aligner = create_aligner(_settings(tmp_path, separator="fake"))

    assert isinstance(aligner, FakeAligner)
    assert isinstance(create_vocal_loader(), VocalAudioLoader)


def test_the_real_stack_gets_stable_ts_with_weights_in_the_model_dir(tmp_path: Path) -> None:
    aligner = create_aligner(_settings(tmp_path))
    weights = whisper_weights(_settings(tmp_path))

    assert isinstance(aligner, StableTsAligner)
    assert weights.root == tmp_path / "models" / "whisper"
    assert (weights.name, weights.file_name) == ("turbo", "large-v3-turbo.pt")
    assert weights.size_bytes == WHISPER_TURBO_WEIGHTS.size_bytes


def test_the_small_model_is_selectable(tmp_path: Path) -> None:
    weights = whisper_weights(_settings(tmp_path, lyrics=LyricsSettings(align_model="small")))

    assert (weights.name, weights.file_name, weights.size_bytes) == ("small", "small.pt", None)


# ------------------------------------------------------------------ Whisper runtime (fake modules)


class _Model:
    device = "cpu"
    dims = types.SimpleNamespace(n_mels=128)

    def detect_language(self, mel: object) -> tuple[None, dict[str, float]]:
        assert mel == ("mel", 128, "cpu")
        return None, {"en": 0.2, "nl": 0.7}


class _Mel:
    def __init__(self, n_mels: int) -> None:
        self.n_mels = n_mels

    def to(self, device: str) -> tuple[str, int, str]:
        return ("mel", self.n_mels, device)


@pytest.fixture
def modules(monkeypatch: pytest.MonkeyPatch) -> dict[str, list[Any]]:
    calls: dict[str, list[Any]] = {"download": [], "load": []}

    def download(url: str, root: str, in_memory: bool) -> str:
        calls["download"].append((url, root, in_memory))
        return f"{root}/x.pt"

    def load_model(name: str, device: str, download_root: str) -> _Model:
        calls["load"].append((name, device, download_root))
        return _Model()

    whisper: Any = types.ModuleType("whisper")
    whisper._MODELS = {"turbo": "https://example.invalid/large-v3-turbo.pt"}
    whisper._download = download
    whisper.pad_or_trim = lambda audio: audio
    whisper.log_mel_spectrogram = lambda audio, n_mels: _Mel(n_mels)
    stable: Any = types.ModuleType("stable_whisper")
    stable.load_model = load_model
    monkeypatch.setitem(sys.modules, "whisper", whisper)
    monkeypatch.setitem(sys.modules, "stable_whisper", stable)
    return calls


def test_the_runtime_downloads_with_whisper_and_loads_with_stable_ts(
    tmp_path: Path, modules: dict[str, list[Any]]
) -> None:
    runtime = WhisperRuntime()

    path = runtime.download("turbo", tmp_path)
    model = runtime.load("turbo", "cuda", tmp_path)

    assert path == tmp_path / "x.pt"
    assert modules["download"] == [
        ("https://example.invalid/large-v3-turbo.pt", str(tmp_path), False)
    ]
    assert modules["load"] == [("turbo", "cuda", str(tmp_path))]
    assert runtime.detect_language(model, np.zeros(16, dtype=np.float32)) == "nl"
