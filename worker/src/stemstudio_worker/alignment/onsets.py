"""Onset refinement of aligned word starts (006 SC-003, qa finding on the sung clip).

Whisper places the word after a held note inside the held vowel (340-580 ms early on the qa
sung clip, stable-ts default options). Each word start is moved to the nearest vocal onset
(librosa spectral-flux onsets, backtracked to the energy minimum) between ``SNAP_BEFORE_S``
before and ``SNAP_AFTER_S`` after it; starts without an onset in that window stay. On the qa
clip this lifts SC-003 from 14/22 to 20/22 word starts within 300 ms.
"""

from collections.abc import Sequence

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.alignment.mapping import AlignedWord
from stemstudio_worker.ports import ALIGN_SAMPLE_RATE

SNAP_BEFORE_S = 0.05
"""A start moves at most this far earlier (Whisper rarely places a start too late)."""

SNAP_AFTER_S = 0.6
"""A start moves at most this far later (the held-vowel error was up to 580 ms)."""

HOP = 160
"""Onset analysis hop: 10 ms at 16 kHz."""


def vocal_onsets(audio: npt.NDArray[np.float32]) -> npt.NDArray[np.float64]:
    """Onset times (s) of a mono 16 kHz vocal stem."""
    envelope = librosa.onset.onset_strength(y=audio, sr=ALIGN_SAMPLE_RATE, hop_length=HOP)
    onsets: npt.NDArray[np.float64] = librosa.onset.onset_detect(
        onset_envelope=envelope, sr=ALIGN_SAMPLE_RATE, hop_length=HOP, units="time",
        backtrack=True,
    )  # fmt: skip
    return onsets


def _snapped(start: float, onsets: npt.NDArray[np.float64]) -> float:
    """The onset nearest to ``start`` inside the window, else ``start``."""
    near = onsets[(onsets >= start - SNAP_BEFORE_S) & (onsets <= start + SNAP_AFTER_S)]
    return float(near[np.argmin(np.abs(near - start))]) if near.size else start


def snap_starts(words: Sequence[AlignedWord], onsets: npt.NDArray[np.float64]) -> list[AlignedWord]:
    """Move every start to its onset; starts never decrease, a word keeps a positive length."""
    snapped: list[AlignedWord] = []
    floor = 0.0
    for word in words:
        start = max(_snapped(word.start, onsets), floor)
        end = word.end if word.end > start else start + (word.end - word.start)
        snapped.append(AlignedWord(word.text, start, end, word.probability))
        floor = start
    return snapped
