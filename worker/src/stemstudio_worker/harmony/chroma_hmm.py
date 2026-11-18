"""chroma-hmm: the CPU fallback chord recognizer (research R-03, data-model 2.3 step 2).

``librosa.feature.chroma_cqt`` at the BTC frame rate (hop 2048 at 22,050 Hz, 92.88 ms), cosine
similarity to 24 major/minor triad templates plus "N" for frames below -50 dBFS, and a Viterbi
pass over a 25-state self-loop HMM. Major/minor only: a ``full`` request yields the ``simple``
classes; the post-processor then writes ``vocabulary-reduced``.
"""

from typing import Final

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.harmony.labels import chord_tones, simple_labels
from stemstudio_worker.ports import (
    CHORD_HOP,
    CHORD_SAMPLE_RATE,
    AnalysisProgress,
    ChordFrames,
    MonoAudio,
)
from stemstudio_worker.schemas import ChordEngine, ChordQuality, ChordVocabulary

SILENCE_DB: Final = -50.0
"""Frames whose RMS is below this level (dBFS) are "N"."""

SELF_TRANSITION: Final = 0.9
"""Probability of staying in a state per frame (research R-03)."""

N_SIMILARITY: Final = 0.05
"""Similarity of "N" in a sounding frame (keeps the state reachable)."""

_CLASSES: Final = 25
_MAJMIN: Final[tuple[ChordQuality, ...]] = ("maj", "min")

MIN_SAMPLES: Final = 4 * CHORD_SAMPLE_RATE
"""Shortest CQT input (4 s); the lowest octaves of the CQT need it."""


def _templates() -> npt.NDArray[np.float64]:
    """``(24, 12)`` unit-norm triad templates in ``simple_labels()`` order."""
    rows = np.zeros((_CLASSES - 1, 12))
    for root in range(12):
        for offset, quality in enumerate(_MAJMIN):
            rows[root * 2 + offset, list(chord_tones(root, quality))] = 1.0
    return rows / np.linalg.norm(rows, axis=1, keepdims=True)


class ChromaHmmRecognizer:
    """``ports.ChordRecognizer`` of the ``chroma-hmm`` engine (no model, CPU only)."""

    def __init__(self, *, self_transition: float = SELF_TRANSITION) -> None:
        """Create the recognizer; ``self_transition`` is the HMM state self-loop probability."""
        self._transition = librosa.sequence.transition_loop(_CLASSES, self_transition)
        self._templates = _templates()

    @property
    def engine(self) -> ChordEngine:
        """Engine name recorded in the chart."""
        return "chroma-hmm"

    def recognize(
        self,
        audio: MonoAudio,
        vocabulary: ChordVocabulary,  # noqa: ARG002 - majmin only; the post-processor reduces
        progress: AnalysisProgress,
    ) -> ChordFrames:
        """Recognise major/minor chords frame by frame (``audio`` at ``CHORD_SAMPLE_RATE``)."""
        progress(0.0)
        chroma, silent = _features(np.asarray(audio, dtype=np.float32))
        progress(0.5)
        states = librosa.sequence.viterbi(self._emissions(chroma, silent), self._transition)
        posteriors = np.zeros((len(states), _CLASSES), dtype=np.float32)
        posteriors[np.arange(len(states)), states] = 1.0
        progress(1.0)
        return ChordFrames(
            posteriors=posteriors,
            labels=simple_labels(),
            frame_seconds=CHORD_HOP / CHORD_SAMPLE_RATE,
            chroma=np.ascontiguousarray(chroma.T, dtype=np.float32),
            engine="chroma-hmm",
        )

    def _emissions(
        self, chroma: npt.NDArray[np.float64], silent: npt.NDArray[np.bool_]
    ) -> npt.NDArray[np.float64]:
        """``(25, frames)`` emission probabilities from template cosine similarity."""
        norms = np.linalg.norm(chroma, axis=0, keepdims=True)
        unit = chroma / np.maximum(norms, 1e-12)
        similarity = np.clip(self._templates @ unit, 0.0, None)
        none = np.full((1, chroma.shape[1]), N_SIMILARITY)
        scores = np.vstack([similarity, none])
        scores[:, silent] = 0.0
        scores[-1, silent] = 1.0
        return np.asarray(scores / scores.sum(axis=0, keepdims=True), dtype=np.float64)


def _features(
    samples: npt.NDArray[np.float32],
) -> tuple[npt.NDArray[np.float64], npt.NDArray[np.bool_]]:
    """``(12, frames)`` chroma and the per-frame silence flags (``1 + len // hop`` frames).

    Inputs shorter than ``MIN_SAMPLES`` are padded with silence for the CQT and cut back.
    """
    count = 1 + len(samples) // CHORD_HOP
    samples = np.pad(samples, (0, max(0, MIN_SAMPLES - len(samples))))
    chroma = librosa.feature.chroma_cqt(
        y=samples, sr=CHORD_SAMPLE_RATE, hop_length=CHORD_HOP, tuning=0.0
    )
    rms = librosa.feature.rms(y=samples, frame_length=2 * CHORD_HOP, hop_length=CHORD_HOP)[0]
    silent = librosa.amplitude_to_db(rms[:count], ref=1.0, amin=1e-10) < SILENCE_DB
    return np.asarray(chroma[:, :count], dtype=np.float64), np.asarray(silent, dtype=np.bool_)
