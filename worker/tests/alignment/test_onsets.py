"""Tests of the onset refinement of aligned word starts (006 T041, qa finding on SC-003).

Whisper places the word after a held note inside the held vowel (340-580 ms early on the qa sung
clip); each word start is moved to the nearest vocal onset from 50 ms before to 600 ms after it.
"""

from pathlib import Path

import numpy as np
import numpy.typing as npt

from stemstudio_worker.alignment.mapping import AlignedWord
from stemstudio_worker.alignment.onsets import snap_starts, vocal_onsets
from stemstudio_worker.alignment.stable_ts_aligner import StableTsAligner
from stemstudio_worker.alignment.weights import WhisperWeights
from stemstudio_worker.ports import AlignRequest

from .fakes import TEXT, FakeRuntime, spread

RATE = 16_000


def _bursts(starts: list[float], seconds: float = 6.0) -> npt.NDArray[np.float32]:
    """Silence with a 300 ms tone burst (distinct pitch each) from every start."""
    audio = np.zeros(int(seconds * RATE), dtype=np.float32)
    t = np.arange(int(0.3 * RATE)) / RATE
    for index, start in enumerate(starts):
        first = int(start * RATE)
        audio[first : first + t.size] = 0.5 * np.sin(2 * np.pi * (220 + 40 * index) * t)
    return audio


def _word(text: str, start: float, end: float) -> AlignedWord:
    return AlignedWord(text, start, end, 0.9)


def test_onsets_are_found_at_the_note_starts() -> None:
    onsets = vocal_onsets(_bursts([0.5, 1.5, 2.25, 4.0]))

    assert len(onsets) == 4
    assert np.max(np.abs(onsets - np.array([0.5, 1.5, 2.25, 4.0]))) < 0.04


def test_silence_has_no_onsets() -> None:
    assert vocal_onsets(np.zeros(RATE, dtype=np.float32)).size == 0


def test_a_start_inside_a_held_note_moves_to_the_next_onset() -> None:
    onsets = np.array([1.0, 2.5])
    words = [_word("held", 1.0, 2.0), _word("next", 2.0, 2.6)]

    snapped = snap_starts(words, onsets)

    assert [(w.start, w.end) for w in snapped] == [(1.0, 2.0), (2.5, 2.6)]


def test_a_start_moved_past_its_end_keeps_its_length() -> None:
    snapped = snap_starts([_word("short", 2.0, 2.1)], np.array([2.5]))

    assert snapped[0].start == 2.5
    assert abs(snapped[0].end - 2.6) < 1e-9


def test_starts_move_at_most_50_ms_earlier_and_600_ms_later() -> None:
    words = [_word("a", 1.0, 1.2), _word("b", 3.0, 3.2), _word("c", 5.0, 5.2)]

    snapped = snap_starts(words, np.array([0.9, 0.96, 3.7, 5.55]))

    assert [w.start for w in snapped] == [0.96, 3.0, 5.55]


def test_the_nearest_onset_wins_and_starts_never_decrease() -> None:
    words = [_word("a", 1.0, 1.5), _word("b", 1.05, 1.5)]

    snapped = snap_starts(words, np.array([1.02, 1.4]))

    assert [w.start for w in snapped] == [1.02, 1.02]


def test_the_aligner_refines_word_starts_against_the_vocal_onsets(tmp_path: Path) -> None:
    root = tmp_path / "whisper"
    root.mkdir()
    (root / "large-v3-turbo.pt").write_bytes(b"weights")
    aligner = StableTsAligner(WhisperWeights("turbo", root, "large-v3-turbo.pt", 7), FakeRuntime())
    audio = _bursts([1.0, 1.4, 1.8, 2.5, 2.8, 3.2])

    document = aligner.align(AlignRequest(audio, TEXT, "en"), "cpu", lambda f, s: None)

    starts = [w.start_ms for line in document.lines for w in line.words]
    assert spread().words[3].start == 2.2
    assert starts[:3] == [1000, 1400, 1800]
    assert abs((starts[3] or 0) - 2500) <= 40
