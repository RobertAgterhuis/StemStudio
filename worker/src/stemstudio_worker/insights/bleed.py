"""Bleed estimate: leakage share B2 with the ratio-floor guard (data-model 4.2/4.3).

For a target stem i and a source stem j, per mel band b over j's active frames:

* ``g_b = max(0, <P_i, P_j> / <P_j, P_j>)`` (closed-form non-negative least squares);
* ``floor_b`` = the ``FLOOR_PERCENTILE``-th percentile of ``P_i / P_j`` over j's *loud* cells
  (active and within ``LOUD_RANGE_DB`` of j's band peak). Bleed is a constant-ratio copy, so
  ``P_i >= a^2 P_j`` in every loud cell of j; instruments that merely play together have cells
  where j is loud and i is quiet, which pulls the floor down (Q1 refinement, 2026-11-23,
  replacing the quiet-frame fit, whose value could never exceed -15 dB);
* ``g*_b = min(g_b, floor_b, MAX_GAIN)``: a stem cannot hold more of a source than the source's
  own stem has (a quiet copy of i in j is spill of i, not bleed of j into i; Q3);
* leakage ``E = sum_b g*_b sum_t P_j``; share ``E / sum P_i``.

The index is the largest share over the sources; ``guard_ratio`` (full fit / guarded fit) above
``CO_OCCURRENCE_RATIO`` flags co-occurrence for the four pairs of instruments that often play
together, which caps the level at ``medium``.
"""

from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from typing import cast

import numpy as np
import numpy.typing as npt

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.insights.thresholds import (
    CO_OCCURRENCE_PAIRS,
    CO_OCCURRENCE_RATIO,
    FLOOR_PERCENTILE,
    LOUD_RANGE_DB,
    MAX_GAIN,
)
from stemstudio_worker.schemas import STANDARD_STEMS, StemName

__all__ = [
    "CO_OCCURRENCE_RATIO",
    "BleedMeasure",
    "BleedSource",
    "PairFit",
    "bleed_sources",
    "family_of",
    "fit_pair",
    "measure_bleed",
]

Float64 = npt.NDArray[np.float64]


@dataclass(frozen=True, slots=True)
class PairFit:
    """Leakage share of one source in one target.

    Attributes:
        share: Guarded share (``g*``), 0..1: the bleed index candidate.
        full_share: Share of the unguarded fit (``g``), 0..1.
    """

    share: float
    full_share: float

    @property
    def guard_ratio(self) -> float:
        """Full fit over guarded fit (1 when both are 0, infinite when only the guard is 0)."""
        if self.share > 0:
            return self.full_share / self.share
        return float("inf") if self.full_share > 0 else 1.0


@dataclass(frozen=True, slots=True)
class BleedSource:
    """A stem that may have bled into the target, with the standard stem it belongs to."""

    family: StemName
    spectrogram: Spectrogram


@dataclass(frozen=True, slots=True)
class BleedMeasure:
    """Bleed of one stem before levels: index, main source and the co-occurrence flag."""

    index: float
    main_source: StemName | None
    co_occurrence: bool


def family_of(channel: str) -> StemName:
    """The standard stem a channel belongs to (``drums.kick`` -> ``drums``)."""
    return cast("StemName", channel.split(".", 1)[0])


def _band_floor(ratio: Float64, loud: npt.NDArray[np.bool_]) -> Float64:
    floor = np.zeros(ratio.shape[1])
    for band in np.flatnonzero(loud.any(axis=0)):
        floor[band] = np.percentile(ratio[loud[:, band], band], FLOOR_PERCENTILE)
    return floor


def _loud_cells(source: Float64, active: npt.NDArray[np.bool_]) -> npt.NDArray[np.bool_]:
    level = 10.0 * np.log10(source + 1e-12)
    peak = np.where(active[:, None], level, -np.inf).max(axis=0)
    return np.asarray(active[:, None] & (level >= peak - LOUD_RANGE_DB) & (source > 0))


def _band_gain(target: Float64, source: Float64) -> Float64:
    """``g_b = max(0, <P_i, P_j> / <P_j, P_j>)`` per band."""
    energy = np.sum(source * source, axis=0)
    return np.maximum(0.0, np.sum(target * source, axis=0) / np.where(energy > 0, energy, 1.0))


def fit_pair(target: Spectrogram, source: Spectrogram) -> PairFit:
    """Leakage share of ``source`` in ``target`` (frames beyond the shorter one are ignored)."""
    frames = min(target.frames, source.frames)
    p_i = target.power[:frames].astype(np.float64)
    p_j = source.power[:frames].astype(np.float64)
    active = source.active()[:frames]
    total = float(p_i.sum())
    if total <= 0 or not active.any():
        return PairFit(0.0, 0.0)
    g = _band_gain(p_i[active], p_j[active])
    floor = _band_floor(p_i / np.where(p_j > 0, p_j, 1.0), _loud_cells(p_j, active))
    mass = np.sum(p_j[active], axis=0)
    guarded = float(np.sum(np.minimum(np.minimum(g, floor), MAX_GAIN) * mass)) / total
    return PairFit(min(1.0, guarded), min(1.0, float(np.sum(g * mass)) / total))


def measure_bleed(family: str, target: Spectrogram, sources: Sequence[BleedSource]) -> BleedMeasure:
    """Largest leakage share over ``sources`` for a target of standard stem ``family``."""
    fits = [(fit_pair(target, source.spectrogram), source.family) for source in sources]
    best = max(fits, key=lambda item: item[0].share, default=None)
    if best is None or best[0].share <= 0:
        return BleedMeasure(0.0, None, co_occurrence=False)
    fit, main = best
    paired = frozenset((family, main)) in CO_OCCURRENCE_PAIRS
    return BleedMeasure(
        fit.share, main, co_occurrence=paired and fit.guard_ratio > CO_OCCURRENCE_RATIO
    )


def bleed_sources(
    channel: str, standard: Mapping[str, Spectrogram], details: Mapping[str, Spectrogram]
) -> list[BleedSource]:
    """Sources of a stem: every other standard stem, plus the siblings of a detail stem.

    A detail stem is part of its parent, so the parent itself is not a source; its siblings
    (the other detail stems of the same parent, ``rest`` included) count as the parent.
    """
    family = family_of(channel)
    others = [name for name in STANDARD_STEMS if name != family]
    sources = [BleedSource(name, standard[name]) for name in others]
    return sources + _siblings(channel, details) if "." in channel else sources


def _siblings(channel: str, details: Mapping[str, Spectrogram]) -> list[BleedSource]:
    family = family_of(channel)
    names = [name for name in details if name != channel and family_of(name) == family]
    return [BleedSource(family, details[name]) for name in names]
