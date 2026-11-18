"""Chord frames -> chart segments (data-model 2.3 steps 3, 4 and 6; research R-04).

With a reliable beat grid the frame posteriors are summed per beat and every beat takes its best
label, so a chord changes only on a beat and lasts at least one beat (FR-011). Without a grid,
runs shorter than 0.5 s merge into the neighbour with the higher confidence. For the ``full``
vocabulary the strongest pitch class of the bass stem adds an inversion when it is the chord's
third, fifth or seventh. The key (step 5) is added afterwards by :mod:`harmony.key`.
"""

import itertools
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.harmony.labels import (
    Chord,
    format_label,
    is_chord,
    is_inversion,
    parse_label,
)
from stemstudio_worker.ports import (
    CHORD_HOP,
    CHORD_SAMPLE_RATE,
    ChordFrames,
    MonoAudio,
    PostProcessOptions,
)
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    MAX_CHORD_SEGMENTS,
    AnalysisWarning,
    AnalysisWarningCode,
    BeatGridTempo,
    ChordChart,
    ChordSegment,
    ChordVocabulary,
)

MIN_UNSNAPPED_MS: Final = 500
"""Shortest segment without a reliable grid (research R-04)."""

FEW_CHORDS: Final = 2
"""Fewer distinct chords than this warn ``few-chords``."""

SILENT_BASS: Final = 1e-6
"""Bass chroma energy below this per segment adds no inversion."""


@dataclass(frozen=True, slots=True)
class _Span:
    """Frames ``[first, last)`` labelled with class ``cls``, from ``start_ms`` to ``end_ms``."""

    first: int
    last: int
    start_ms: int
    end_ms: int
    cls: int


class BeatSnapPostProcessor:
    """``ports.ChordPostProcessor``: beat snapping, merges, slash bass and warnings."""

    def segments(self, frames: ChordFrames, options: PostProcessOptions) -> ChordChart:
        """Build the chart without the key (``key``/``keyRegions`` empty, ``preferFlats`` false)."""
        vocabulary = _vocabulary(frames, options.vocabulary)
        spans = _cap(_merge_equal(_spans(frames, options.grid)))
        bass = _bass_chroma(options.bass) if vocabulary == "full" else None
        segments = [_segment(frames, span, bass) for span in spans]
        no_chords = not any(is_chord(s.label) for s in segments)
        return ChordChart(
            vocabulary=vocabulary,
            engine=frames.engine,
            beat_snapped=options.grid is not None,
            no_chords=no_chords,
            segments=segments,
            warnings=_warnings(frames, options, segments),
        )


def _vocabulary(frames: ChordFrames, requested: ChordVocabulary) -> ChordVocabulary:
    """``simple`` when ``full`` was requested from the majmin-only chroma-hmm engine."""
    return "simple" if frames.engine == "chroma-hmm" else requested


def _spans(frames: ChordFrames, grid: BeatGridTempo | None) -> list[_Span]:
    """Beat-snapped spans, or frame runs with short runs merged."""
    if frames.posteriors.shape[0] == 0:
        return []
    if grid is not None:
        return _beat_spans(frames, grid)
    runs = _frame_runs(frames)
    return _merge_short(frames, runs)


def _ms(seconds: float) -> int:
    return round(seconds * 1000)


def _beat_spans(frames: ChordFrames, grid: BeatGridTempo) -> list[_Span]:
    """One span per beat interval (plus the part before the first beat), best summed label."""
    fs, count = frames.frame_seconds, frames.posteriors.shape[0]
    times = [0.0, *(b for b in grid.beats if 0 < b < count * fs), count * fs]
    cuts: list[tuple[int, float]] = []
    for t in times:
        index = min(count, round(t / fs))
        if not cuts or index > cuts[-1][0]:
            cuts.append((index, t))
    cuts[-1] = (count, count * fs)
    return [
        _Span(i0, i1, _ms(t0), _ms(t1), int(np.argmax(frames.posteriors[i0:i1].sum(axis=0))))
        for (i0, t0), (i1, t1) in itertools.pairwise(cuts)
    ]


def _frame_runs(frames: ChordFrames) -> list[_Span]:
    """Runs of equal frame argmax."""
    best = np.argmax(frames.posteriors, axis=1)
    edges = [0, *(np.flatnonzero(np.diff(best)) + 1).tolist(), len(best)]
    fs = frames.frame_seconds
    return [
        _Span(a, b, _ms(a * fs), _ms(b * fs), int(best[a])) for a, b in itertools.pairwise(edges)
    ]


def _confidence(frames: ChordFrames, first: int, last: int, cls: int) -> float:
    """Mean posterior of ``cls`` over frames ``[first, last)``."""
    return float(np.clip(frames.posteriors[first:last, cls].mean(), 0.0, 1.0))


def _join(left: _Span, right: _Span, cls: int) -> _Span:
    return _Span(left.first, right.last, left.start_ms, right.end_ms, cls)


def _absorb(frames: ChordFrames, spans: list[_Span], index: int) -> None:
    """Merge ``spans[index]`` into the neighbour with the higher confidence (ties: previous)."""
    score = {
        n: _confidence(frames, spans[n].first, spans[n].last, spans[n].cls)
        for n in (index - 1, index + 1)
        if 0 <= n < len(spans)
    }
    target = max(score, key=lambda n: (score[n], -n))
    low, high = sorted((index, target))
    spans[low : high + 1] = [_join(spans[low], spans[high], spans[target].cls)]


def _merge_short(frames: ChordFrames, spans: list[_Span]) -> list[_Span]:
    """Merge runs shorter than ``MIN_UNSNAPPED_MS`` (shortest first) into a neighbour."""
    while len(spans) > 1:
        index = min(range(len(spans)), key=lambda i: spans[i].end_ms - spans[i].start_ms)
        if spans[index].end_ms - spans[index].start_ms >= MIN_UNSNAPPED_MS:
            break
        _absorb(frames, spans, index)
    return spans


def _merge_equal(spans: list[_Span]) -> list[_Span]:
    """Merge consecutive spans of the same class."""
    merged: list[_Span] = []
    for span in spans:
        if merged and merged[-1].cls == span.cls:
            merged[-1] = _join(merged[-1], span, span.cls)
        else:
            merged.append(span)
    return merged


def _cap(spans: list[_Span]) -> list[_Span]:
    """Join the shortest span with a neighbour until at most ``MAX_CHORD_SEGMENTS`` remain."""
    while len(spans) > MAX_CHORD_SEGMENTS:
        index = min(range(len(spans) - 1), key=lambda i: spans[i].end_ms - spans[i].start_ms)
        longer = max(index, index + 1, key=lambda i: spans[i].end_ms - spans[i].start_ms)
        spans[index : index + 2] = [_join(spans[index], spans[index + 1], spans[longer].cls)]
    return spans


def _bass_chroma(bass: MonoAudio | None) -> npt.NDArray[np.float64] | None:
    """Unnormalised ``(12, frames)`` chroma of the bass stem at the chord frame rate."""
    if bass is None:
        return None
    chroma = librosa.feature.chroma_cqt(
        y=np.asarray(bass, dtype=np.float32),
        sr=CHORD_SAMPLE_RATE,
        hop_length=CHORD_HOP,
        norm=None,
        tuning=0.0,
    )
    return np.asarray(chroma, dtype=np.float64)


def _with_bass(chord: Chord, bass: npt.NDArray[np.float64] | None, span: _Span) -> Chord:
    """Add the inversion the bass stem plays during ``span`` (data-model 2.3 step 4)."""
    if bass is None or chord.root is None or chord.quality is None:
        return chord
    energy = bass[:, span.first : span.last].sum(axis=1)
    if energy.sum() < SILENT_BASS:
        return chord
    strongest = int(np.argmax(energy))
    if is_inversion(chord.root, chord.quality, strongest):
        return Chord(chord.root, chord.quality, strongest)
    return chord


def _segment(
    frames: ChordFrames, span: _Span, bass: npt.NDArray[np.float64] | None
) -> ChordSegment:
    """The contract segment of one span."""
    label = frames.labels[span.cls]
    chord = _with_bass(parse_label(label), bass, span)
    if chord.root is not None and chord.quality is not None:
        label = format_label(chord.root, chord.quality, chord.bass)
    return ChordSegment(
        start_ms=span.start_ms,
        end_ms=span.end_ms,
        label=label,
        root=chord.root,
        quality=chord.quality,
        bass=chord.bass if chord.bass != chord.root else None,
        confidence=_confidence(frames, span.first, span.last, span.cls),
    )


def _warning(code: AnalysisWarningCode) -> AnalysisWarning:
    return AnalysisWarning(code=code, text=ANALYSIS_WARNING_TEXTS[code])


def _warnings(
    frames: ChordFrames, options: PostProcessOptions, segments: Sequence[ChordSegment]
) -> list[AnalysisWarning]:
    """``fallback-engine``, ``vocabulary-reduced``, ``grid-unreliable``, ``few-chords``."""
    distinct = {s.label.split("/")[0] for s in segments if is_chord(s.label)}
    flags: list[tuple[AnalysisWarningCode, bool]] = [
        ("fallback-engine", frames.fallback),
        ("vocabulary-reduced", options.vocabulary == "full" and frames.engine == "chroma-hmm"),
        ("grid-unreliable", options.grid is None),
        ("few-chords", 0 < len(distinct) < FEW_CHORDS),
    ]
    return [_warning(code) for code, raised in flags if raised]
