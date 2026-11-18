"""Deterministic instrument analyses of the fake stack (feature 016, research R-11, data-model 2.7).

* Chords (:class:`FakeChordAnalyzer`): one chord per bar on the request grid (bars at its
  downbeats, then at its tempo; 120 BPM 4/4 from 0.5 s without a grid), ``C G Am F`` for bars 1-8
  and ``D A Bm G`` from bar 9 (``full``: ``C:maj7 G:7 A:min7 F:maj/3`` and the same a tone
  higher), confidence 0.9; key C major (0.9) with a D major region from bar 9.
* Drum hits (:class:`FakeDrumHitsTranscriber`): the 008 fake groove at 120 BPM from 0.5 s (kick
  on 1 and 3, snare on 2 and 4, eighth hi-hats) plus a crash on beat 1 of every fourth bar.
* Reference melody (:class:`FakePitchAnalyzer`): one note per beat at 120 BPM from 0.5 s, beat
  ``k`` sings ``FAKE_WALK[k % 14]`` (the global beat index), voiced only inside the 006 fake
  lyric lines; this is the melody of the qa fake-microphone fixtures.

None of them reads audio content; durations come from the files or the sample count.
"""

import itertools
import math
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Final

import numpy as np
import soundfile as sf

from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs, ChordOutcome
from stemstudio_worker.lyrics.fake import FAKE_LINES
from stemstudio_worker.lyrics.interpolate import line_end_ms
from stemstudio_worker.ports import (
    PITCH_SAMPLE_RATE,
    AnalysisError,
    AnalysisProgress,
    DrumHit,
    MonoAudio,
    NoMelodyError,
    TranscriptionProgress,
)
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    MAX_PITCH_FRAMES,
    AnalysisKey,
    AnalysisWarning,
    BeatGridTempo,
    ChordChart,
    ChordEngine,
    ChordQuality,
    ChordSegment,
    ChordsJobRequest,
    ChordVocabulary,
    DrumPiece,
    InstrumentAnalysisModels,
    KeyRegion,
    ManualTempo,
    PitchEngine,
    PitchRange,
    RenderRange,
    ScoreEngine,
    VocalPitch,
    VocalSource,
)
from stemstudio_worker.transcription.audio import AUDIO_UNREADABLE_TEXT
from stemstudio_worker.transcription.fake import FakeDrumTranscriber

FAKE_TEMPO: Final = ManualTempo(bpm=120, beats_per_bar=4, first_downbeat_seconds=0.5)
"""The fake time base without a grid: 120 BPM in 4/4 from 0.5 s."""

FAKE_WALK: Final = (60, 62, 64, 65, 67, 69, 71, 72, 71, 69, 67, 65, 64, 62)
"""MIDI notes of the fake melody; beat ``k`` sings ``FAKE_WALK[k % 14]``."""

CONFIDENCE: Final = 0.9
"""Confidence of every fake chord and key."""

SECOND_KEY_BAR: Final = 8
"""Zero-based bar where the fake song moves to D major (bar 9)."""

_Chord = tuple[str, int, ChordQuality, int | None]
_PROGRESSIONS: Final[Mapping[ChordVocabulary, tuple[tuple[_Chord, ...], tuple[_Chord, ...]]]] = {
    "simple": (
        (("C:maj", 0, "maj", None), ("G:maj", 7, "maj", None), ("A:min", 9, "min", None),
         ("F:maj", 5, "maj", None)),
        (("D:maj", 2, "maj", None), ("A:maj", 9, "maj", None), ("B:min", 11, "min", None),
         ("G:maj", 7, "maj", None)),
    ),
    "full": (
        (("C:maj7", 0, "maj7", None), ("G:7", 7, "7", None), ("A:min7", 9, "min7", None),
         ("F:maj/3", 5, "maj", 9)),
        (("D:maj7", 2, "maj7", None), ("A:7", 9, "7", None), ("B:min7", 11, "min7", None),
         ("G:maj/3", 7, "maj", 11)),
    ),
}  # fmt: skip
_BEAT: Final = 60.0 / FAKE_TEMPO.bpm
_START: Final = FAKE_TEMPO.first_downbeat_seconds
_BAR_BEATS: Final = FAKE_TEMPO.beats_per_bar
_LEVEL: Final = 0.8


def _ms(seconds: float) -> int:
    return round(seconds * 1000)


def _duration(paths: Sequence[Path]) -> float:
    try:
        return max(float(sf.info(path).duration) for path in paths)
    except (OSError, sf.LibsndfileError) as exc:
        raise AnalysisError(AUDIO_UNREADABLE_TEXT) from exc


def bar_starts(grid: BeatGridTempo | None, duration: float) -> list[float]:
    """Bar start times inside ``[0, duration)``: the grid's downbeats, then its bar length."""
    if grid is None:
        known, length = [_START], _BAR_BEATS * _BEAT
    else:
        known = [d for d in grid.downbeats if d >= 0] or [grid.bar_one_downbeat]
        length = grid.beats_per_bar * 60.0 / grid.bpm
    starts = [t for t in known if t < duration]
    following = (starts[-1] if starts else known[-1]) + length
    while starts and following < duration:
        starts.append(following)
        following += length
    return starts


def _segment(span: tuple[int, int], chord: _Chord | None) -> ChordSegment:
    if chord is None:
        return ChordSegment(start_ms=span[0], end_ms=span[1], label="N", confidence=CONFIDENCE)
    label, root, quality, bass = chord
    return ChordSegment(start_ms=span[0], end_ms=span[1], label=label, root=root,
                        quality=quality, bass=bass, confidence=CONFIDENCE)  # fmt: skip


def _chord(vocabulary: ChordVocabulary, bar: int) -> _Chord:
    section = _PROGRESSIONS[vocabulary][0 if bar < SECOND_KEY_BAR else 1]
    return section[bar % len(section)]


def fake_segments(
    starts: Sequence[float], duration: float, vocabulary: ChordVocabulary
) -> list[ChordSegment]:
    """``N`` before the first bar, then one chord per bar up to ``duration``."""
    end = _ms(duration)
    first = _ms(starts[0]) if starts else end
    segments = [_segment((0, first), None)] if first > 0 else []
    bounds = [_ms(t) for t in starts] + [end]
    for bar, span in enumerate(itertools.pairwise(bounds)):
        segments.append(_segment(span, _chord(vocabulary, bar)))
    return segments


def fake_key_regions(starts: Sequence[float], duration: float) -> list[KeyRegion]:
    """C major from the first bar, D major from bar 9 (when the song reaches it)."""
    if not starts:
        return []
    bounds = [_ms(starts[0])]
    if len(starts) > SECOND_KEY_BAR:
        bounds.append(_ms(starts[SECOND_KEY_BAR]))
    bounds.append(_ms(duration))
    tonics = (0, 2)
    return [KeyRegion(start_ms=a, end_ms=b, tonic=tonics[i], mode="major", confidence=CONFIDENCE)
            for i, (a, b) in enumerate(itertools.pairwise(bounds))]  # fmt: skip


class FakeChordAnalyzer:
    """``ChordAnalyzer`` of the fake stack (``STEMSTUDIO_CHORD_ENGINE=fake``)."""

    @property
    def default_engine(self) -> ChordEngine:
        """``fake``."""
        return "fake"

    def models(self) -> InstrumentAnalysisModels:
        """No BTC weights are used by the fake."""
        return InstrumentAnalysisModels()

    def analyze(
        self, request: ChordsJobRequest, inputs: ChordInputs, progress: AnalysisProgress
    ) -> ChordOutcome:
        """The fake progression over the longest harmonic input; progress 1.0 once."""
        duration = _duration(inputs.harmonic)
        starts = bar_starts(request.grid, duration)
        segments = fake_segments(starts, duration, request.vocabulary)
        regions = fake_key_regions(starts, duration)
        key = AnalysisKey(tonic=0, mode="major", confidence=CONFIDENCE) if regions else None
        chart = ChordChart(vocabulary=request.vocabulary, engine="fake", key=key,
                           key_regions=regions, beat_snapped=request.grid is not None,
                           no_chords=not regions, segments=segments)  # fmt: skip
        progress(1.0)
        return ChordOutcome(chart=chart)


class FakeDrumHitsTranscriber:
    """``DrumTranscriber`` of the fake drum trainer: the 008 groove plus crashes."""

    @property
    def engine(self) -> ScoreEngine:
        """``fake`` (``DrumHits.engine``)."""
        return "fake"

    def transcribe(
        self,
        inputs: Mapping[DrumPiece, Path],
        window: RenderRange | None,
        progress: TranscriptionProgress,
    ) -> tuple[DrumHit, ...]:
        """The groove from 0.5 s; a crash on every fourth downbeat when cymbals are given."""
        if not inputs:
            return ()
        hits = [h for h in FakeDrumTranscriber(FAKE_TEMPO).transcribe(inputs, window, progress)
                if h.time >= _START]  # fmt: skip
        if "cymbals" in inputs:
            hits += self._crashes(_duration([inputs["cymbals"]]))
        return tuple(sorted(hits, key=lambda h: (h.time, h.piece)))

    @staticmethod
    def _crashes(duration: float) -> list[DrumHit]:
        every = 4 * _BAR_BEATS * _BEAT
        count = math.ceil(max(duration - _START, 0.0) / every)
        return [DrumHit(_START + i * every, "cymbals", _LEVEL) for i in range(count)]


def fake_lines_ms(duration_ms: int) -> list[tuple[int, int]]:
    """``(start, end)`` of every 006 fake lyric line over a track of ``duration_ms``."""
    step = duration_ms // (len(FAKE_LINES) + 1)
    starts = [step * (i + 1) for i in range(len(FAKE_LINES))]
    nexts: list[int | None] = [*starts[1:], None]
    return [(start, line_end_ms(text, start, following))
            for start, text, following in zip(starts, FAKE_LINES, nexts, strict=True)]  # fmt: skip


def _cents(time_ms: int, lines: Sequence[tuple[int, int]]) -> int:
    if time_ms < _ms(_START) or not any(start <= time_ms < end for start, end in lines):
        return 0
    beat = (time_ms - _ms(_START)) // _ms(_BEAT)
    return FAKE_WALK[beat % len(FAKE_WALK)] * 100


class FakePitchAnalyzer:
    """``PitchAnalyzer`` of the fake stack (``STEMSTUDIO_PITCH_ENGINE=fake``)."""

    @property
    def engine(self) -> PitchEngine:
        """``fake``."""
        return "fake"

    def analyze(
        self, audio: MonoAudio, source: VocalSource, progress: AnalysisProgress
    ) -> VocalPitch:
        """The fake melody over the length of ``audio`` (at ``PITCH_SAMPLE_RATE``).

        Raises:
            NoMelodyError: The audio is too short to reach a sung line.
        """
        count = min(math.ceil(len(audio) * 100 / PITCH_SAMPLE_RATE), MAX_PITCH_FRAMES)
        lines = fake_lines_ms(count * 10)
        frames = [_cents(index * 10, lines) for index in range(count)]
        voiced = np.array([f for f in frames if f > 0])
        if voiced.size == 0:
            raise NoMelodyError
        progress(1.0)
        low, high = (int(np.percentile(voiced, q)) for q in (5, 95))
        return VocalPitch(frames=frames, source=source, engine="fake",
                          voiced_ratio=voiced.size / count,
                          range=PitchRange(low_cents=low, high_cents=high),
                          warnings=_source_warnings(source))  # fmt: skip


def _source_warnings(source: VocalSource) -> list[AnalysisWarning]:
    if source != "vocals.lead":
        return []
    return [AnalysisWarning(code="detail-bleed", text=ANALYSIS_WARNING_TEXTS["detail-bleed"])]
