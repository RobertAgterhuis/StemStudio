"""Song key and key regions: a 24-state HMM over chroma and chord evidence (research R-13).

Units are beats of a reliable grid (1 s steps without one) over the non-``N`` part of the song.
Per unit, the beat chroma smoothed over +-``smooth_beats`` units and the chord-evidence
histogram (root, third and fifth weighted by duration, +-2 bars) are each correlated with the 24
rotated key profiles (mean of Krumhansl-Kessler and Temperley); the two correlations are averaged.
Viterbi decodes ``exp(beta * score)`` with ``p_stay`` per unit (Phase 0 start values: beta 3,
``p_stay`` 0.995, +-8 beats). Regions shorter than 8 bars (16 s) merge into the neighbour that
fits them better. Confidences are mean softmax probabilities at temperature 0.1.
"""

import itertools
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Any, Final

import librosa
import numpy as np
import numpy.typing as npt

from stemstudio_worker.harmony.labels import chord_tones, is_chord, prefer_flats
from stemstudio_worker.ports import CHORD_HOP, CHORD_SAMPLE_RATE, KeyEstimate
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    MAX_KEY_REGIONS,
    AnalysisKey,
    AnalysisWarning,
    BeatGridTempo,
    ChordChart,
    ChordSegment,
    KeyAlternative,
    KeyMode,
    KeyRegion,
)

Floats = npt.NDArray[np.float64]

_KK_MAJOR: Final = (6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88)
_KK_MINOR: Final = (6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17)
_TKP_MAJOR: Final = (0.748, 0.06, 0.488, 0.082, 0.67, 0.46, 0.096, 0.715, 0.104, 0.366, 0.057, 0.4)
_TKP_MINOR: Final = (
    0.712,
    0.084,
    0.474,
    0.618,
    0.049,
    0.46,
    0.105,
    0.747,
    0.404,
    0.067,
    0.133,
    0.33,
)

CONFIDENCE_BETA: Final = 10.0
"""Inverse softmax temperature of the confidences (temperature 0.1, research R-13)."""

UNCERTAIN: Final = 0.5
"""Global confidence below this warns ``key-uncertain``."""

ALTERNATIVE_MARGIN: Final = 0.1
"""The relative key is named when its mean probability is this close."""

REGION_BARS: Final = 8
"""Shortest key region in bars of the grid."""

UNGRIDDED_REGION_UNITS: Final = 16
"""Shortest key region without a grid (1 s units)."""

EVIDENCE_BARS: Final = 2
"""Chord evidence is collected over +-2 bars (4 bars around the unit)."""

_UNGRIDDED_BAR_UNITS: Final = 4
_MODES: Final[tuple[KeyMode, KeyMode]] = ("major", "minor")


def _profiles() -> Floats:
    """``(24, 12)`` centred unit-norm key profiles; row ``tonic * 2 + (0 major, 1 minor)``."""
    rows = []
    for tonic in range(12):
        for kk, tkp in ((_KK_MAJOR, _TKP_MAJOR), (_KK_MINOR, _TKP_MINOR)):
            mean = np.asarray(kk) / sum(kk) + np.asarray(tkp) / sum(tkp)
            rows.append(np.roll(mean, tonic))
    return _centred(np.asarray(rows, dtype=np.float64))


def _centred(rows: Floats) -> Floats:
    """Rows minus their mean, scaled to unit norm (zero rows stay zero)."""
    centred = rows - rows.mean(axis=-1, keepdims=True)
    norms = np.linalg.norm(centred, axis=-1, keepdims=True)
    return np.asarray(centred / np.maximum(norms, 1e-12), dtype=np.float64)


@dataclass(frozen=True, slots=True)
class _Units:
    """Analysis units: boundaries in seconds and the bar length in units."""

    edges: Floats
    bar: int

    @property
    def count(self) -> int:
        return len(self.edges) - 1

    def ms(self, index: int) -> int:
        return round(float(self.edges[index]) * 1000)


@dataclass(frozen=True, slots=True)
class _Run:
    """Units ``[first, last)`` in key state ``state``."""

    first: int
    last: int
    state: int


class HmmKeyEstimator:
    """``ports.KeyEstimator``: global key and key regions (research R-13)."""

    def __init__(self, *, beta: float = 3.0, p_stay: float = 0.995, smooth_beats: int = 8) -> None:
        """Create the estimator with the Phase 0 start values (beta 3, 0.995, +-8 beats)."""
        self._beta = beta
        self._transition = librosa.sequence.transition_loop(24, p_stay)
        self._smooth = smooth_beats
        self._profiles = _profiles()

    def estimate(
        self,
        chroma: npt.NDArray[np.float32],
        segments: Sequence[ChordSegment],
        grid: BeatGridTempo | None,
    ) -> KeyEstimate:
        """Estimate the global key and regions >= 8 bars (16 s without a grid)."""
        chords = [s for s in segments if is_chord(s.label)]
        if not chords:
            return KeyEstimate(None)
        units = _units(chords[0].start_ms / 1000, chords[-1].end_ms / 1000, grid)
        scores = self._scores(chroma, chords, units)
        probs = _softmax(CONFIDENCE_BETA * scores)
        runs = _regions(self._decode(scores), probs, _min_units(units, grid))
        regions = tuple(_region(r, probs, units) for r in runs)
        return KeyEstimate(_global(runs, probs, units), regions)

    def _decode(self, scores: Floats) -> list[_Run]:
        """Runs of equal key states of the Viterbi path over ``softmax(beta * scores)``."""
        return _runs(librosa.sequence.viterbi(_softmax(self._beta * scores), self._transition))

    def _scores(
        self, chroma: npt.NDArray[np.float32], chords: Sequence[ChordSegment], units: _Units
    ) -> Floats:
        """``(24, units)`` mean of the chroma and the chord-evidence profile correlations."""
        smoothed = _smoothed(_unit_chroma(chroma, units), self._smooth)
        evidence = _evidence(chords, units)
        chroma_term = self._profiles @ _centred(smoothed).T
        chord_term = self._profiles @ _centred(evidence).T
        has_evidence = np.any(evidence > 0, axis=1)
        return np.where(has_evidence, 0.5 * chroma_term + 0.5 * chord_term, chroma_term)


def _units(start: float, end: float, grid: BeatGridTempo | None) -> _Units:
    """Beats of the grid inside ``(start, end)``, or 1 s steps, framed by start and end."""
    if grid is None:
        inner = np.arange(start + 1.0, end, 1.0)
        return _Units(np.asarray([start, *inner, end]), _UNGRIDDED_BAR_UNITS)
    beats = [b for b in grid.beats if start < b < end]
    return _Units(np.asarray([start, *beats, end]), grid.beats_per_bar)


def _min_units(units: _Units, grid: BeatGridTempo | None) -> int:
    return UNGRIDDED_REGION_UNITS if grid is None else REGION_BARS * units.bar


def _unit_chroma(chroma: npt.NDArray[np.float32], units: _Units) -> Floats:
    """``(units, 12)`` mean chroma per unit (L1-normalised; empty units are zero)."""
    frame_s = CHORD_HOP / CHORD_SAMPLE_RATE
    out = np.zeros((units.count, 12))
    for index in range(units.count):
        first = int(units.edges[index] / frame_s)
        last = max(first + 1, int(units.edges[index + 1] / frame_s))
        block = chroma[first:last]
        if len(block):
            out[index] = block.mean(axis=0)
    return out / np.maximum(out.sum(axis=1, keepdims=True), 1e-12)


def _smoothed(rows: Floats, half: int) -> Floats:
    """Moving mean over +-``half`` rows (shorter at the edges)."""
    sums = np.vstack([np.zeros((1, rows.shape[1])), np.cumsum(rows, axis=0)])
    index = np.arange(len(rows))
    low, high = np.maximum(0, index - half), np.minimum(len(rows), index + half + 1)
    return np.asarray((sums[high] - sums[low]) / (high - low)[:, None], dtype=np.float64)


def _evidence(chords: Sequence[ChordSegment], units: _Units) -> Floats:
    """``(units, 12)`` root/third/fifth histogram of the chords within +-2 bars of each unit."""
    centres = (units.edges[:-1] + units.edges[1:]) / 2
    reach = EVIDENCE_BARS * units.bar * float(np.diff(units.edges).mean())
    out = np.zeros((units.count, 12))
    spans = [
        (c.start_ms / 1000, c.end_ms / 1000, list(chord_tones(c.root, c.quality)[:3]))
        for c in chords
        if c.root is not None and c.quality is not None
    ]
    for start, end, tones in spans:
        low = np.maximum(centres - reach, start)
        overlap = np.clip(np.minimum(centres + reach, end) - low, 0.0, None)
        out[:, tones] += overlap[:, None]
    return out


def _softmax(scores: Floats) -> Floats:
    """Column-wise softmax of ``(states, units)``."""
    shifted = np.exp(scores - scores.max(axis=0, keepdims=True))
    return np.asarray(shifted / shifted.sum(axis=0, keepdims=True), dtype=np.float64)


def _runs(states: npt.NDArray[np.integer[Any]]) -> list[_Run]:
    """Runs of equal decoded states."""
    edges = [0, *(np.flatnonzero(np.diff(states)) + 1).tolist(), len(states)]
    return [_Run(a, b, int(states[a])) for a, b in itertools.pairwise(edges)]


def _regions(runs: list[_Run], probs: Floats, min_units: int) -> list[_Run]:
    """Merge runs shorter than ``min_units`` (shortest first), then equal neighbours; cap at 50."""
    while len(runs) > 1:
        index = min(range(len(runs)), key=lambda i: runs[i].last - runs[i].first)
        if runs[index].last - runs[index].first >= min_units and len(runs) <= MAX_KEY_REGIONS:
            break
        _absorb(runs, index, probs)
    return runs


def _absorb(runs: list[_Run], index: int, probs: Floats) -> None:
    """Give ``runs[index]`` to the neighbour whose key fits its units better, then join equals."""
    run = runs[index]
    fit = {
        n: float(probs[runs[n].state, run.first : run.last].mean())
        for n in (index - 1, index + 1)
        if 0 <= n < len(runs)
    }
    target = max(fit, key=lambda n: (fit[n], -n))
    low, high = sorted((index, target))
    runs[low : high + 1] = [_Run(runs[low].first, runs[high].last, runs[target].state)]
    _join_equal(runs)


def _join_equal(runs: list[_Run]) -> None:
    """Join neighbouring runs of the same state in place."""
    merged: list[_Run] = []
    for run in runs:
        if merged and merged[-1].state == run.state:
            merged[-1] = _Run(merged[-1].first, run.last, run.state)
        else:
            merged.append(run)
    runs[:] = merged


def _key_of(state: int) -> tuple[int, KeyMode]:
    return state // 2, _MODES[state % 2]


def _relative(state: int) -> int:
    """State of the relative minor (of a major key) or major (of a minor key)."""
    tonic, mode = _key_of(state)
    return ((tonic + 9) % 12) * 2 + 1 if mode == "major" else ((tonic + 3) % 12) * 2


def _region(run: _Run, probs: Floats, units: _Units) -> KeyRegion:
    tonic, mode = _key_of(run.state)
    confidence = float(probs[run.state, run.first : run.last].mean())
    return KeyRegion(
        start_ms=units.ms(run.first),
        end_ms=units.ms(run.last),
        tonic=tonic,
        mode=mode,
        confidence=min(1.0, confidence),
    )


def _global(runs: Sequence[_Run], probs: Floats, units: _Units) -> AnalysisKey:
    """The key with the longest total duration, its confidence and the relative alternative."""
    state = _longest_state(runs, units)
    mask = np.zeros(units.count, dtype=np.bool_)
    for run in (r for r in runs if r.state == state):
        mask[run.first : run.last] = True
    confidence = float(probs[state, mask].mean())
    alternative = float(probs[_relative(state), mask].mean())
    tonic, mode = _key_of(state)
    close = confidence - alternative <= ALTERNATIVE_MARGIN
    other = _alternative(_relative(state)) if close else None
    return AnalysisKey(tonic=tonic, mode=mode, confidence=min(1.0, confidence), alternative=other)


def _longest_state(runs: Sequence[_Run], units: _Units) -> int:
    """The key state with the longest total duration (ties: the lower state)."""
    totals: dict[int, int] = {}
    for run in runs:
        totals[run.state] = totals.get(run.state, 0) + units.ms(run.last) - units.ms(run.first)
    return max(totals, key=lambda s: (totals[s], -s))


def _alternative(state: int) -> KeyAlternative:
    tonic, mode = _key_of(state)
    return KeyAlternative(tonic=tonic, mode=mode)


def apply_key(chart: ChordChart, estimate: KeyEstimate) -> ChordChart:
    """Add the key (data-model 2.3 step 5): ``key``, ``keyRegions``, ``preferFlats``, warning."""
    key = estimate.key
    warnings = list(chart.warnings)
    if key is not None and key.confidence < UNCERTAIN:
        text = ANALYSIS_WARNING_TEXTS["key-uncertain"].format(n=round(key.confidence * 100))
        warnings.append(AnalysisWarning(code="key-uncertain", text=text))
    return chart.model_copy(
        update={
            "key": key,
            "key_regions": list(estimate.regions) if key is not None else [],
            "prefer_flats": key is not None and prefer_flats(key.tonic, key.mode),
            "warnings": warnings,
        }
    )
