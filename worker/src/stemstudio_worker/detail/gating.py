"""Energy gates of the detail phase (pure numpy, research.md D-04).

* **Parent gate** (FR-003): a standard stem is split only when at least 8 % of its mono frames
  (2048 samples, hop 512) are louder than -40 dBFS; otherwise it is "(almost) silent".
* **Sub-stem gate** (FR-004): a model output is kept only when its energy is at least -20 dB
  relative to the parent and at least 5 % of its frames exceed max(-50 dBFS, parent level - 25 dB).

Changing any constant changes results and must bump the detail ``separatorVersion``.
"""

from dataclasses import dataclass
from typing import Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import DetailAudio

FRAME: Final = 2048
"""Frame length in samples (mono mix-down)."""

HOP: Final = 512
"""Hop between frames in samples."""

SILENCE_DB: Final = -160.0
"""Level reported for digital silence (and the floor of every dB value)."""

PARENT_ACTIVE_DBFS: Final = -40.0
"""A parent frame is active above this level."""

PARENT_MIN_ACTIVE_SHARE: Final = 0.08
"""Minimum share of active frames for a parent to be split (real >= 0.66, bleed <= 0.064)."""

SUB_MIN_REL_DB: Final = -20.0
"""Minimum energy of a sub-stem relative to its parent."""

SUB_FLOOR_DBFS: Final = -50.0
"""Absolute floor of the sub-stem frame threshold."""

SUB_FLOOR_BELOW_PARENT_DB: Final = 25.0
"""The sub-stem frame threshold lies this far below the parent level (unless under the floor)."""

SUB_MIN_ACTIVE_SHARE: Final = 0.05
"""Minimum share of sub-stem frames above the threshold."""

_TINY: Final = 1e-8
_TINY_ENERGY: Final = 1e-16

Levels = npt.NDArray[np.float64]
"""Per-frame levels in dBFS."""


@dataclass(frozen=True, slots=True)
class ParentReading:
    """Gate measurement of one parent stem.

    Attributes:
        level_dbfs: RMS level of the mono mix-down (<= 0).
        active_share: Share of frames above :data:`PARENT_ACTIVE_DBFS`.
    """

    level_dbfs: float
    active_share: float

    @property
    def active(self) -> bool:
        """Whether the parent passes the gate (see :func:`parent_active`)."""
        return parent_active(self.active_share)


def _mono(audio: DetailAudio) -> npt.NDArray[np.float64]:
    return np.asarray(audio, dtype=np.float64).mean(axis=0)


def _to_db(power: npt.NDArray[np.float64]) -> Levels:
    """Mean-square power -> dB with :data:`SILENCE_DB` as floor."""
    rms = np.sqrt(np.maximum(power, 0.0))
    return np.maximum(20.0 * np.log10(np.maximum(rms, _TINY)), SILENCE_DB)


def frame_levels(audio: DetailAudio) -> Levels:
    """Return the RMS level (dBFS) of every frame of the mono mix-down of ``audio``.

    Inputs shorter than one frame are zero-padded to one frame.
    """
    mono = _mono(audio)
    if mono.size < FRAME:
        mono = np.pad(mono, (0, FRAME - mono.size))
    cumulative = np.concatenate(([0.0], np.cumsum(mono * mono)))
    starts = np.arange(1 + (mono.size - FRAME) // HOP) * HOP
    return _to_db((cumulative[starts + FRAME] - cumulative[starts]) / FRAME)


def level_dbfs(audio: DetailAudio) -> float:
    """Return the RMS level (dBFS, <= 0) of the mono mix-down of ``audio``."""
    mono = _mono(audio)
    power = np.array([float(np.mean(mono * mono))]) if mono.size else np.zeros(1)
    return min(float(_to_db(power)[0]), 0.0)


def active_share(levels: Levels, threshold_dbfs: float) -> float:
    """Return the share of ``levels`` strictly above ``threshold_dbfs``."""
    return float(np.mean(levels > threshold_dbfs))


def parent_active(share: float) -> bool:
    """Return True when a parent with ``share`` active frames is split (>= 8 %)."""
    return share >= PARENT_MIN_ACTIVE_SHARE


def read_parent(audio: DetailAudio) -> ParentReading:
    """Measure the level and the active-frame share of a parent stem."""
    share = active_share(frame_levels(audio), PARENT_ACTIVE_DBFS)
    return ParentReading(level_dbfs=level_dbfs(audio), active_share=share)


def relative_db(audio: DetailAudio, reference: DetailAudio) -> float:
    """Energy of ``audio`` relative to ``reference`` in dB (:data:`SILENCE_DB` if either is 0)."""
    energy = float(np.sum(np.square(audio, dtype=np.float64)))
    ref = float(np.sum(np.square(reference, dtype=np.float64)))
    if energy <= _TINY_ENERGY or ref <= _TINY_ENERGY:
        return SILENCE_DB
    return max(10.0 * float(np.log10(energy / ref)), SILENCE_DB)


def sub_active_floor(parent_level_dbfs: float) -> float:
    """Frame threshold of the sub-stem gate: max(-50 dBFS, parent level - 25 dB)."""
    return max(SUB_FLOOR_DBFS, parent_level_dbfs - SUB_FLOOR_BELOW_PARENT_DB)


def sub_stem_audible(sub: DetailAudio, parent: DetailAudio, reading: ParentReading) -> bool:
    """Return True when the sub-stem ``sub`` of ``parent`` passes the audibility gate.

    Args:
        sub: The sub-stem audio.
        parent: The parent audio (energy reference).
        reading: The parent's gate reading (level for the frame threshold).
    """
    if relative_db(sub, parent) < SUB_MIN_REL_DB:
        return False
    share = active_share(frame_levels(sub), sub_active_floor(reading.level_dbfs))
    return share >= SUB_MIN_ACTIVE_SHARE
