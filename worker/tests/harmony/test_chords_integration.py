"""Integration test of the real BTC chord model (016 T063, SC-001, SC-010, ADR-031).

Downloads both BTC checkpoints from the pinned commit (SHA-256 checked by the model store) into
``STEMSTUDIO_MODEL_DIR`` (default: a temporary folder), loads them with ``torch.load(
weights_only=True)`` and exactly ``ports.BTC_SAFE_GLOBALS``, and runs the majmin model on 8 threads
over a synthetic chord set: every major and minor triad (24 chords, 2 s each, harmonic tones with
the root an octave below). It records the BTC forward time per audio second (Phase 0 could not
measure it) and checks the majmin frame accuracy; with the beat-analysis post-processing and key
estimator installed (``harmony.postprocess``, ``harmony.key``) it also checks the time-weighted
majmin accuracy of the chart (SC-001, >= 85 %) and the key of a test set (SC-010, >= 90 %).

Run: ``uv run pytest -m integration tests/harmony/test_chords_integration.py -s``.
"""

import importlib
import os
import time
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.harmony.btc import BtcParts, BtcRecognizer, TorchBtcRuntime
from stemstudio_worker.main import ChordModelStore
from stemstudio_worker.ports import (
    BTC_LARGE_VOCA_WEIGHTS,
    BTC_MAJMIN_WEIGHTS,
    CHORD_SAMPLE_RATE,
    AnalysisError,
    ChordFrames,
    PostProcessOptions,
    TranscriptionError,
)
from stemstudio_worker.schemas import BeatGridTempo

SR = CHORD_SAMPLE_RATE
CHORD_SECONDS = 2.0
THREADS = 8
NAMES = ("C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B")
MAJMIN_FRAME_TARGET = 0.70
"""Raw argmax frames of the synthetic set (before beat snapping and merging)."""
CHART_TARGET = 0.85
KEY_TARGET = 0.90
SECONDS_PER_AUDIO_SECOND_BUDGET = 0.2
"""Generous CPU budget; Phase 0 estimated < 2 s for 5 minutes (0.007 s/s)."""


def _tone(midi: int, seconds: float) -> npt.NDArray[np.float64]:
    t = np.arange(int(seconds * SR)) / SR
    hz = 440.0 * 2 ** ((midi - 69) / 12)
    partials = sum(np.sin(2 * np.pi * hz * k * t) / k for k in range(1, 6) if hz * k < SR / 2)
    envelope = np.minimum(1.0, np.minimum(t, seconds - t) * 20)
    return np.asarray(partials * envelope)


def _triad(root: int, minor: bool) -> npt.NDArray[np.float64]:
    third = 3 if minor else 4
    notes = [48 + root, 60 + root, 60 + root + third, 60 + root + 7]
    return np.asarray(sum(_tone(n, CHORD_SECONDS) for n in notes))


def chord_set() -> tuple[npt.NDArray[np.float32], list[tuple[int, bool]]]:
    """Every major and minor triad, 2 s each; the ground truth per chord."""
    truth = [(root, minor) for minor in (False, True) for root in range(12)]
    audio = np.concatenate([_triad(root, minor) for root, minor in truth])
    return (0.2 * audio / np.max(np.abs(audio))).astype(np.float32), truth


def _label(root: int, minor: bool) -> str:
    return f"{NAMES[root]}:{'min' if minor else 'maj'}"


def frame_accuracy(frames: ChordFrames, truth: list[tuple[int, bool]]) -> float:
    """Share of frames whose argmax is the true chord (frames inside the 2 s blocks)."""
    best = np.argmax(frames.posteriors, axis=1)
    hits = total = 0
    for index, label in enumerate(best):
        chord = int(index * frames.frame_seconds // CHORD_SECONDS)
        if chord < len(truth):
            total += 1
            hits += frames.labels[label] == _label(*truth[chord])
    return hits / total


def _model_dir(tmp_path_factory: pytest.TempPathFactory) -> Path:
    configured = os.environ.get("STEMSTUDIO_MODEL_DIR")
    return Path(configured) if configured else tmp_path_factory.mktemp("models")


def _optional(module: str) -> Any:
    try:
        return importlib.import_module(f"stemstudio_worker.harmony.{module}")
    except ImportError:
        return None


def _store_or_skip(tmp_path_factory: pytest.TempPathFactory) -> ChordModelStore:
    """The model store with both BTC weights present; skips when offline."""
    store = ChordModelStore(_model_dir(tmp_path_factory))
    for weights in (BTC_MAJMIN_WEIGHTS, BTC_LARGE_VOCA_WEIGHTS):
        try:
            path = store.ensure(weights)
        except TranscriptionError as error:
            pytest.skip(f"BTC weights {weights.model_id} are not available locally: {error}")
        assert path.stat().st_size == weights.size_bytes
    return store


@pytest.mark.integration
def test_real_btc_recognises_the_triad_set_and_records_its_speed(
    tmp_path_factory: pytest.TempPathFactory,
) -> None:
    store = _store_or_skip(tmp_path_factory)
    recognizer = BtcRecognizer(
        BtcParts(store, FakeDeviceRunner("cpu"), TorchBtcRuntime(THREADS), _NoFallback())
    )
    audio, truth = chord_set()

    frames, per_second = _timed(recognizer, audio)
    accuracy = frame_accuracy(frames, truth)
    full = recognizer.recognize(audio[: SR * 4], "full", lambda _f: None)
    print(f"\nBTC majmin: {per_second:.4f} s per audio second on {THREADS} threads; "  # noqa: T201
          f"frames {accuracy:.1%}; large-voca classes {full.posteriors.shape[1]}")  # fmt: skip

    assert frames.engine == "btc" and not frames.fallback
    assert full.posteriors.shape[1] == 170
    assert accuracy >= MAJMIN_FRAME_TARGET
    assert per_second <= SECONDS_PER_AUDIO_SECOND_BUDGET
    _check_chart_and_key(recognizer, frames, truth)


def _timed(recognizer: BtcRecognizer, audio: npt.NDArray[np.float32]) -> tuple[ChordFrames, float]:
    """The majmin frames of ``audio`` and the seconds per audio second (model loaded first)."""
    recognizer.recognize(audio[: SR * 2], "simple", lambda _f: None)
    started = time.perf_counter()
    frames = recognizer.recognize(audio, "simple", lambda _f: None)
    return frames, (time.perf_counter() - started) / (len(audio) / SR)


def _check_chart_and_key(
    recognizer: BtcRecognizer, frames: ChordFrames, truth: list[tuple[int, bool]]
) -> None:
    """SC-001 and SC-010 with the beat-analysis post-processing and key, when installed."""
    post, key = _optional("postprocess"), _optional("key")
    if post is None or key is None:
        print("harmony.postprocess / harmony.key not installed: chart and key not checked")  # noqa: T201
        return
    processor, estimator = post.BeatSnapPostProcessor(), key.HmmKeyEstimator()
    grid = beat_grid(len(truth) * CHORD_SECONDS)
    share = chart_share(processor.segments(frames, PostProcessOptions(grid, "simple")), truth)
    keys = [_key_found(recognizer, (processor, estimator), tonic, minor)
            for tonic, minor in KEY_SET]  # fmt: skip
    print(f"chart majmin time {share:.1%}; keys {sum(keys)}/{len(keys)}")  # noqa: T201
    assert share >= CHART_TARGET
    assert sum(keys) / len(keys) >= KEY_TARGET


def beat_grid(seconds: float) -> BeatGridTempo:
    """The reliable 120 BPM 4/4 grid of the synthetic set (one chord per bar)."""
    beats = [i * 0.5 for i in range(int(seconds / 0.5))]
    return BeatGridTempo(bpm=120, beats_per_bar=4, beats=beats, downbeats=beats[::4])


def chart_share(chart: Any, truth: list[tuple[int, bool]]) -> float:
    """Time share of the chart whose label is the true chord of its 2 s block."""
    block = int(CHORD_SECONDS * 1000)
    hits = 0
    for index, chord in enumerate(truth):
        start, end = index * block, (index + 1) * block
        hits += sum(max(0, min(s.end_ms, end) - max(s.start_ms, start))
                    for s in chart.segments if s.label == _label(*chord))  # fmt: skip
    return hits / (len(truth) * block)


KEY_SET = [(tonic, False) for tonic in range(12)] + [(tonic, True) for tonic in (9, 2, 4, 0, 7)]
"""Twelve major keys and five minor keys."""


def _cadence(tonic: int, minor: bool) -> npt.NDArray[np.float32]:
    """I IV V I (i iv V i in minor), 2 s per chord, twice."""
    steps = [(0, minor), (5, minor), (7, False), (0, minor)] * 2
    audio = np.concatenate([_triad((tonic + step) % 12, quality) for step, quality in steps])
    return (0.2 * audio / np.max(np.abs(audio))).astype(np.float32)


def _key_found(recognizer: BtcRecognizer, parts: tuple[Any, Any], tonic: int, minor: bool) -> bool:
    processor, estimator = parts
    frames = recognizer.recognize(_cadence(tonic, minor), "simple", lambda _f: None)
    grid = beat_grid(8 * CHORD_SECONDS)
    chart = processor.segments(frames, PostProcessOptions(grid, "simple"))
    found = estimator.estimate(frames.chroma, chart.segments, grid).key
    mode = "minor" if minor else "major"
    return found is not None and (found.tonic, found.mode) == (tonic, mode)


class _NoFallback:
    """The integration run must use BTC itself."""

    engine = "chroma-hmm"

    def recognize(self, *_args: object) -> ChordFrames:
        raise AnalysisError("BTC weights unavailable in the integration run")
