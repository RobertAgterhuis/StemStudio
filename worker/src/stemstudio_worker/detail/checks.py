"""Rest stem and plausibility checks of a split (research.md D-04, FR-005, FR-006).

Order per parent: sub-stem gate -> fewer than 2 kept -> nothing further; model residual
(models whose outputs partition the input) or double count (overlapping family models) ->
not reliable; rest = parent - sum(kept); write FLACs; reconstruction check -> not reliable.

The kept stems are quantised to 16 bit first and the rest is computed on the same grid, so the
reconstruction is exact unless the rest clips; the check therefore catches clipping and
implementation errors.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

import numpy as np

from stemstudio_worker.detail.audio_io import PCM_SCALE, Pcm
from stemstudio_worker.detail.folding import output_sum
from stemstudio_worker.detail.gating import relative_db
from stemstudio_worker.ports import DetailAudio
from stemstudio_worker.schemas import ParentOutcomeKind

MIN_KEPT: Final = 2
"""Fewer kept sub-stems than this means "nothing further to separate"."""

MAX_MODEL_RESIDUAL_DB: Final = -15.0
"""Parent minus the sum of all model outputs must stay at or below this (dB rel. parent)."""

MAX_DOUBLE_COUNT_DB: Final = 1.0
"""The kept families together may exceed the parent energy by at most this (dB)."""

MAX_RECONSTRUCTION_DB: Final = -30.0
"""Parent minus (kept + rest) as written must stay at or below this (dB rel. parent)."""

_PCM_MIN: Final = -32768
_PCM_MAX: Final = 32767


@dataclass(frozen=True, slots=True)
class CheckPolicy:
    """Which plausibility checks apply to a model's outputs.

    Attributes:
        model_residual: The outputs partition the input (drums, vocals), so the parent minus
            their sum must be small.
        double_count: The outputs may overlap (instrument families), so the kept sum must not
            exceed the parent.
    """

    model_residual: bool
    double_count: bool


def to_pcm(audio: DetailAudio) -> Pcm:
    """Quantise float audio to 16-bit samples (rounded, clipped to the int16 range)."""
    scaled = np.round(np.asarray(audio, dtype=np.float64) * PCM_SCALE)
    return np.clip(scaled, _PCM_MIN, _PCM_MAX).astype(np.int16)


def from_pcm(pcm: Pcm) -> DetailAudio:
    """Convert 16-bit samples back to float32 audio."""
    return (pcm / PCM_SCALE).astype(np.float32)


def rest_of(parent: DetailAudio, kept: Sequence[Pcm]) -> DetailAudio:
    """Return ``parent - sum(kept)`` in float (quantise it with :func:`to_pcm` before writing)."""
    total = np.sum([pcm.astype(np.int32) for pcm in kept], axis=0) / PCM_SCALE
    return (np.asarray(parent, dtype=np.float64) - total).astype(np.float32)


def model_residual_db(parent: DetailAudio, outputs: Sequence[DetailAudio]) -> float:
    """Energy of ``parent`` minus the sum of every model output, relative to the parent (dB)."""
    return relative_db(parent - output_sum(list(outputs)), parent)


def double_count_db(parent: DetailAudio, kept: Sequence[DetailAudio]) -> float:
    """Energy of the sum of the kept sub-stems relative to the parent (dB)."""
    return relative_db(output_sum(list(kept)), parent)


def pre_write_outcome(
    kept: int, residual_db: float, double_db: float, policy: CheckPolicy
) -> ParentOutcomeKind | None:
    """Decide the outcome before any file is written; None means "write and check".

    Args:
        kept: Number of sub-stems that passed the audibility gate.
        residual_db: :func:`model_residual_db` of the parent.
        double_db: :func:`double_count_db` of the kept sub-stems.
        policy: Which checks apply to the model.
    """
    if kept < MIN_KEPT:
        return ParentOutcomeKind.NOTHING_FURTHER
    if policy.model_residual and residual_db > MAX_MODEL_RESIDUAL_DB:
        return ParentOutcomeKind.NOT_RELIABLE
    if policy.double_count and double_db > MAX_DOUBLE_COUNT_DB:
        return ParentOutcomeKind.NOT_RELIABLE
    return None


def reconstruction_db(parent: DetailAudio, written: Sequence[DetailAudio]) -> float:
    """Energy of ``parent`` minus the sum of the written stems, relative to the parent (dB)."""
    return relative_db(parent - output_sum(list(written)), parent)


def reconstruction_ok(error_db: float) -> bool:
    """Return True when the reconstruction error is at most -30 dB."""
    return error_db <= MAX_RECONSTRUCTION_DB
