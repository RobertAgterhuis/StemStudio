"""Deterministic transcribers of the fake stack (``STEMSTUDIO_TRANSCRIBER=fake``, data-model 2.6).

One note per beat of the grid, from the input start to its end (beats before 0 are skipped):
pitched profiles walk the C-major scale inside the profile range, starting at the C nearest the
middle of the range and wrapping back to it; piano adds a C-major triad (C3-E3-G3) in the bass
staff on every bar, lasting the bar; drums play kick on beats 1 and 3, snare on 2 and 4 and
hi-hat on every eighth. Bar positions count from the beat nearest the first downbeat. Notation
and engraving stay real, so E2E tests check bars and files on real output.
"""

import statistics
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import soundfile as sf

from stemstudio_worker.ports import (
    DrumHit,
    NoteEvent,
    Transcription,
    TranscriptionError,
    TranscriptionProgress,
)
from stemstudio_worker.schemas import (
    BeatGridTempo,
    DrumPiece,
    ManualTempo,
    RenderRange,
    ScoreEngine,
    ScoreProfile,
)
from stemstudio_worker.transcription.audio import AUDIO_UNREADABLE_TEXT

TRIAD: Final = (48, 52, 55)
"""C-major triad of the fake piano's bass staff."""

_C_MAJOR = frozenset({0, 2, 4, 5, 7, 9, 11})
_LEVEL = 0.8
_POSITIONS: Final[dict[DrumPiece, frozenset[int]]] = {
    "kick": frozenset({0, 2}), "snare": frozenset({1, 3}),
}  # fmt: skip


@dataclass(frozen=True, slots=True)
class FakeBeat:
    """One beat inside the input: its time, its end and its position in the bar (0 = downbeat)."""

    time: float
    end: float
    position: int


def _beat_times(grid: BeatGridTempo | ManualTempo, duration: float) -> tuple[list[float], int]:
    """Every beat time and the index of the first downbeat's beat."""
    if isinstance(grid, BeatGridTempo):
        beats = grid.beats
        first = min(range(len(beats)), key=lambda i: abs(beats[i] - grid.downbeats[0]))
        return beats, first
    period = 60.0 / grid.bpm
    before = int(grid.first_downbeat_seconds // period)
    count = before + int((duration - grid.first_downbeat_seconds) // period) + 2
    start = grid.first_downbeat_seconds - before * period
    return [start + i * period for i in range(count)], before


def grid_beats(grid: BeatGridTempo | ManualTempo, duration: float) -> list[FakeBeat]:
    """The beats of ``grid`` in ``[0, duration)``; each ends at the next beat (or the input end)."""
    times, first = _beat_times(grid, duration)
    beats = []
    for index, time in enumerate(times):
        if 0 <= time < duration:
            following = times[index + 1] if index + 1 < len(times) else duration
            position = (index - first) % grid.beats_per_bar
            beats.append(FakeBeat(time, min(following, duration), position))
    return beats


def _period(beats: list[FakeBeat]) -> float:
    return statistics.median(b.end - b.time for b in beats) if beats else 0.5


def _duration(audio_path: Path, window: RenderRange | None) -> float:
    if window is not None:
        return window.end_seconds - window.start_seconds
    try:
        return float(sf.info(audio_path).duration)
    except (OSError, sf.LibsndfileError) as exc:
        raise TranscriptionError(AUDIO_UNREADABLE_TEXT) from exc


def _scale(pitch_range: tuple[int, int]) -> list[int]:
    """The C-major pitches from the C nearest the range middle up to the range top."""
    low, high = pitch_range
    middle = (low + high) / 2
    start = min((c for c in range(0, 128, 12) if low <= c <= high), key=lambda c: abs(c - middle),
                default=low)  # fmt: skip
    return [p for p in range(start, high + 1) if p % 12 in _C_MAJOR]


class FakePitchTranscriber:
    """``PitchTranscriber`` of the fake stack for one job (profile range and grid)."""

    def __init__(
        self, profile: ScoreProfile, pitch_range: tuple[int, int], grid: BeatGridTempo | ManualTempo
    ) -> None:
        """Play the scale of ``pitch_range`` on ``grid`` (piano: plus a triad per bar)."""
        self._profile = profile
        self._scale = _scale(pitch_range)
        self._grid = grid

    @property
    def engine(self) -> ScoreEngine:
        """``fake``."""
        return "fake"

    @property
    def attribution(self) -> str | None:
        """None."""
        return None

    def transcribe(
        self, audio_path: Path, window: RenderRange | None, progress: TranscriptionProgress
    ) -> Transcription:
        """One scale note per beat (and the piano triads)."""
        beats = grid_beats(self._grid, _duration(audio_path, window))
        notes = [NoteEvent(b.time, b.end, self._scale[i % len(self._scale)], _LEVEL)
                 for i, b in enumerate(beats)]  # fmt: skip
        if self._profile == "piano":
            notes += self._triads(beats)
        progress(1.0)
        return Transcription(notes=tuple(sorted(notes, key=lambda n: (n.start, n.pitch))))

    def _triads(self, beats: list[FakeBeat]) -> list[NoteEvent]:
        if not beats:
            return []
        starts = [b.time for b in beats if b.position == 0]
        bars = zip(starts, [*starts[1:], beats[-1].end], strict=True)
        return [NoteEvent(start, end, pitch, _LEVEL) for start, end in bars for pitch in TRIAD]


class FakeDrumTranscriber:
    """``DrumTranscriber`` of the fake stack for one job's grid."""

    def __init__(self, grid: BeatGridTempo | ManualTempo) -> None:
        """Play the fake groove on ``grid``."""
        self._grid = grid

    def transcribe(
        self,
        inputs: Mapping[DrumPiece, Path],
        window: RenderRange | None,
        progress: TranscriptionProgress,
    ) -> tuple[DrumHit, ...]:
        """Kick 1 and 3, snare 2 and 4, eighth hi-hats; only for the given pieces."""
        pieces = list(inputs)
        beats = grid_beats(self._grid, _duration(inputs[pieces[0]], window)) if pieces else []
        hits: list[DrumHit] = []
        for index, piece in enumerate(pieces):
            hits += self._hits(piece, beats)
            progress((index + 1) / len(pieces))
        return tuple(sorted(hits, key=lambda h: (h.time, h.piece)))

    @staticmethod
    def _hits(piece: DrumPiece, beats: list[FakeBeat]) -> list[DrumHit]:
        times = _eighths(beats) if piece == "hihat" else _on_positions(beats, piece)
        return [DrumHit(time, piece, _LEVEL) for time in times]


def _eighths(beats: list[FakeBeat]) -> list[float]:
    """Every beat and the half-beat after it, inside the input."""
    if not beats:
        return []
    half, end = _period(beats) / 2, beats[-1].end
    return sorted({t for b in beats for t in (b.time, b.time + half) if t < end})


def _on_positions(beats: list[FakeBeat], piece: DrumPiece) -> list[float]:
    """Kick on bar positions 0 and 2, snare on 1 and 3, nothing for the other pieces."""
    wanted = _POSITIONS.get(piece, frozenset())
    return [b.time for b in beats if b.position in wanted]
