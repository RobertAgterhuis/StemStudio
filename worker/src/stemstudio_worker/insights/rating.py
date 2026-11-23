"""Levels, rating and reason codes of one stem (data-model 4.5/4.6, research R-05, FR-003).

Rating, in order: fewer than ``MIN_ACTIVE_SECONDS`` of active audio -> ``unknown`` (``too-short``);
B3 (little activity or a very low level) together with a large bleed index -> ``not-present``;
else the worse dimension (``high`` -> ``poor``, ``medium`` -> ``fair``, ``low`` -> ``good``; a
dimension shown as ``unknown`` is skipped, both unknown -> ``unknown``).
"""

from dataclasses import dataclass
from typing import Final

from stemstudio_worker.insights.thresholds import (
    ARTEFACT_LEVELS,
    BLEED_LEVELS,
    MIN_ACTIVE_SECONDS,
    PHANTOM_ACTIVE_SHARE,
    PHANTOM_BLEED_INDEX,
    PHANTOM_LEVEL_DB,
    G1Status,
    shown_level,
)
from stemstudio_worker.schemas import QualityLevel, QualityRating, QualityReasonCode

_RATING: Final[dict[QualityLevel, QualityRating]] = {
    "low": "good",
    "medium": "fair",
    "high": "poor",
    "unknown": "unknown",
}
_ORDER: Final[dict[QualityLevel, int]] = {"unknown": 0, "low": 1, "medium": 2, "high": 3}
_RAISED: Final = ("medium", "high")


@dataclass(frozen=True, slots=True)
class RatingInputs:
    """What the rating of one stem depends on (levels as shown, after the G1 masking)."""

    active_seconds: float
    active_share: float
    level_db: float
    bleed_index: float
    bleed_level: QualityLevel
    artefact_level: QualityLevel
    co_occurrence: bool


def bleed_level(index: float, *, co_occurrence: bool, status: G1Status) -> QualityLevel:
    """Shown bleed level: thresholds, the co-occurrence cap at ``medium``, then the G1 mask."""
    level = BLEED_LEVELS.level(index)
    if co_occurrence and level == "high":
        level = "medium"
    return shown_level(level, status)


def artefact_level(index: float, *, status: G1Status) -> QualityLevel:
    """Shown artefact level: thresholds, then the G1 mask."""
    return shown_level(ARTEFACT_LEVELS.level(index), status)


def _absent(inputs: RatingInputs) -> bool:
    phantom = inputs.active_share < PHANTOM_ACTIVE_SHARE or inputs.level_db < PHANTOM_LEVEL_DB
    return phantom and inputs.bleed_index >= PHANTOM_BLEED_INDEX


def _reason(inputs: RatingInputs) -> QualityReasonCode:
    bled = inputs.bleed_level in _RAISED
    flawed = inputs.artefact_level in _RAISED
    if bled and flawed:
        return "bleed-and-artefacts"
    if bled:
        return "bleed-together" if inputs.co_occurrence else "bleed"
    return "artefacts" if flawed else "clean"


def rate(inputs: RatingInputs) -> tuple[QualityRating, QualityReasonCode]:
    """Rating and reason code of a stem."""
    if inputs.active_seconds < MIN_ACTIVE_SECONDS:
        return "unknown", "too-short"
    if _absent(inputs):
        return "not-present", "not-present"
    worse = max(inputs.bleed_level, inputs.artefact_level, key=_ORDER.__getitem__)
    return _RATING[worse], _reason(inputs)
