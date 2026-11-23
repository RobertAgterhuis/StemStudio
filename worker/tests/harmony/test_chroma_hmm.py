"""chroma-hmm fallback recognizer on rendered triads (016 T017, research R-03, SC-001 fallback)."""

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.harmony import labels
from stemstudio_worker.harmony.chroma_hmm import ChromaHmmRecognizer
from stemstudio_worker.harmony.postprocess import BeatSnapPostProcessor
from stemstudio_worker.ports import (
    CHORD_SAMPLE_RATE,
    AnalysisCancelledError,
    ChordFrames,
    PostProcessOptions,
)

from .conftest import grid_of

SR = CHORD_SAMPLE_RATE
CHORD_S = 2.0
PROGRESSION = ("C:maj", "G:maj", "A:min", "F:maj", "D:min", "E:maj", "A#:maj", "F#:min")


def _note(midi: int, seconds: float) -> npt.NDArray[np.float64]:
    t = np.arange(round(seconds * SR)) / SR
    hz = 440.0 * 2 ** ((midi - 69) / 12)
    return sum((0.6**k) * np.sin(2 * np.pi * (k + 1) * hz * t) for k in range(4))  # type: ignore[return-value]


def render(progression: tuple[str, ...], seconds: float = CHORD_S) -> npt.NDArray[np.float32]:
    """Every chord as a close-position triad around middle C plus its root an octave lower."""
    parts = []
    for label in progression:
        chord = labels.parse_label(label)
        assert chord.root is not None and chord.quality is not None
        tones = [48 + chord.root] + [
            60 + (chord.root + s) % 12 for s in (0, 4 if chord.quality == "maj" else 3, 7)
        ]
        parts.append(sum(_note(m, seconds) for m in tones))
    audio = np.concatenate(parts)
    return np.asarray(0.5 * audio / np.max(np.abs(audio)), dtype=np.float32)


def _recognize(audio: npt.NDArray[np.float32], vocabulary: str = "simple") -> ChordFrames:
    return ChromaHmmRecognizer().recognize(audio, vocabulary, lambda _p: None)  # type: ignore[arg-type]


def _frame_labels(frames: ChordFrames) -> list[str]:
    return [frames.labels[i] for i in np.argmax(frames.posteriors, axis=1)]


def test_rendered_triads_are_recognised_at_least_75_percent_of_the_time() -> None:
    frames = _recognize(render(PROGRESSION))
    found = _frame_labels(frames)
    per_chord = round(CHORD_S / frames.frame_seconds)
    expected = [label for label in PROGRESSION for _ in range(per_chord)]
    hits = sum(a == b for a, b in zip(found, expected, strict=False))
    assert hits / len(expected) >= 0.75


def test_frames_are_one_hot_over_the_25_simple_classes() -> None:
    frames = _recognize(render(PROGRESSION[:2]))
    assert frames.labels == labels.simple_labels()
    assert frames.posteriors.dtype == np.float32
    assert set(np.unique(frames.posteriors)) <= {0.0, 1.0}
    assert np.all(frames.posteriors.sum(axis=1) == 1.0)
    assert frames.chroma.shape == (frames.posteriors.shape[0], 12)
    assert frames.engine == "chroma-hmm"
    assert (frames.device, frames.fallback) == (None, False)


def test_frame_rate_is_92_88_ms() -> None:
    frames = _recognize(render(PROGRESSION[:2]))
    assert frames.frame_seconds == pytest.approx(0.09288, abs=1e-5)
    assert frames.posteriors.shape[0] == 1 + round(4.0 * SR) // 2048


def test_silence_is_no_chord() -> None:
    audio = np.concatenate([np.zeros(2 * SR, np.float32), render(("C:maj",))])
    found = _frame_labels(_recognize(audio))
    assert found[2:18] == ["N"] * 16
    assert found[-10:] == ["C:maj"] * 10


def test_full_request_gives_a_simple_chart_with_vocabulary_reduced() -> None:
    frames = _recognize(render(PROGRESSION[:4]), "full")
    chart = BeatSnapPostProcessor().segments(
        frames, PostProcessOptions(grid=grid_of(120, 8.0, first=0.0), vocabulary="full")
    )
    assert chart.vocabulary == "simple"
    assert [w.code for w in chart.warnings] == ["vocabulary-reduced"]
    assert [s.label for s in chart.segments] == list(PROGRESSION[:4])


def test_progress_reports_start_and_end() -> None:
    seen: list[float] = []
    ChromaHmmRecognizer().recognize(render(("C:maj",)), "simple", seen.append)
    assert seen[0] == 0.0
    assert seen[-1] == 1.0


def test_cancellation_from_progress_propagates() -> None:
    def cancel(_value: float) -> None:
        raise AnalysisCancelledError

    with pytest.raises(AnalysisCancelledError):
        ChromaHmmRecognizer().recognize(render(("C:maj",)), "simple", cancel)


def test_engine_name_is_chroma_hmm() -> None:
    assert ChromaHmmRecognizer().engine == "chroma-hmm"


def test_empty_audio_gives_no_chord_frames() -> None:
    frames = _recognize(np.zeros(0, np.float32))
    assert set(_frame_labels(frames)) <= {"N"}
