"""Levels and rating (data-model 4.5/4.6). Stub for the red commit of T034."""

from dataclasses import dataclass

from stemstudio_worker.insights.thresholds import G1Status
from stemstudio_worker.schemas import QualityLevel, QualityRating, QualityReasonCode


@dataclass(frozen=True, slots=True)
class RatingInputs:
    """Stub."""

    active_seconds: float
    active_share: float
    level_db: float
    bleed_index: float
    bleed_level: QualityLevel
    artefact_level: QualityLevel
    co_occurrence: bool


def bleed_level(index: float, *, co_occurrence: bool, status: G1Status) -> QualityLevel:
    """Stub."""
    raise NotImplementedError


def artefact_level(index: float, *, status: G1Status) -> QualityLevel:
    """Stub."""
    raise NotImplementedError


def rate(inputs: RatingInputs) -> tuple[QualityRating, QualityReasonCode]:
    """Stub."""
    raise NotImplementedError
