"""Constants of the quality measures, their calibration and the G1 reliability table.

The algorithm constants are calibrated on the V1 synthetic validation (research.md R-04,
``tests/insights/test_validation_v1.py``). Any change of a constant bumps ``ANALYZER_VERSION``,
which marks earlier reports as outdated (data-model 5.1).
"""

from dataclasses import dataclass
from typing import Final, Literal

from stemstudio_worker.schemas import QualityInputKind, QualityLevel, Reliability, StemName

ANALYZER_VERSION: Final = "1.0.0"
"""``analyzerVersion`` of every result of the signal analyzer."""


@dataclass(frozen=True, slots=True)
class Levels:
    """Level thresholds of one dimension: ``low < low_below <= medium < high_from <= high``."""

    low_below: float
    high_from: float

    def level(self, index: float) -> QualityLevel:
        """``low``, ``medium`` or ``high`` for an index."""
        if index < self.low_below:
            return "low"
        return "medium" if index < self.high_from else "high"


# bleed (4.2/4.3, Q1) ---------------------------------------------------------------

LOUD_RANGE_DB: Final = 20.0
"""A cell of the source is *loud* within this range below the source's band peak."""

FLOOR_PERCENTILE: Final = 5.0
"""Percentile of ``P_i / P_j`` over the source's loud cells: the ratio floor (Q1)."""

MAX_GAIN: Final = 1.0
"""Upper bound of the per-band bleed gain ``g*`` (Q3, pending confirmation)."""

CO_OCCURRENCE_RATIO: Final = 2.0
"""Full fit / guarded fit above this flags co-occurrence (data-model 4.3)."""

CO_OCCURRENCE_PAIRS: Final[frozenset[frozenset[StemName]]] = frozenset(
    frozenset(pair)
    for pair in (("drums", "bass"), ("guitar", "other"), ("piano", "other"), ("guitar", "piano"))
)
"""Pairs of instruments that often play together; their level is capped at ``medium``."""

BLEED_LEVELS: Final = Levels(0.0065, 0.053)
"""Bleed index (energy share) thresholds; V1 2026-11-23, seeds 7/11/23: rho >= 0.91, 87-93 %."""

# artefacts (4.4) ---------------------------------------------------------------

DROPOUT_NEIGHBOURHOOD_FRAMES: Final = 43
"""Median window of the dropout measure: +-0.5 s of 23 ms blocks."""

DROPOUT_DEPTH_DB: Final = 30.0
"""A dropout block lies this far below the neighbourhood median."""

DROPOUT_EDGE_DB: Final = 25.0
"""A gating hole falls and recovers by at least this much within two blocks (46 ms).

Natural decays fall slowly and are therefore not holes; this judges every stem against its
own envelope, the same way for every family (orchestrator Q2).
"""

DROPOUT_MAX_FRAMES: Final = 13
"""A hole recovers within 0.3 s."""

MIX_FLOOR: Final = 1e-3
"""Floor of the mix's dropout share in the stem/mix ratio."""

ARTEFACT_K: Final = 50.0
"""Normalisation ``x / (x + K)`` of the dropout ratio."""

ARTEFACT_LEVELS: Final = Levels(0.35, 0.62)
"""Artefact index thresholds, calibrated on V1 (2026-11-23: rho 0.96, 100 %)."""

MODEL_RESIDUAL_DB: Final = -20.0
"""Detail stems get the kind ``model-residual`` when the parent's residual is above this."""

REST_SHARE: Final = 0.25
"""Detail and rest stems get ``rest-share`` when the parent's rest energy share is above this."""

# rating (4.5) ---------------------------------------------------------------

MIN_ACTIVE_SECONDS: Final = 10.0
"""Less active audio rates ``unknown`` (``too-short``); ``ports.QUALITY_MIN_ACTIVE_SECONDS``."""

PHANTOM_ACTIVE_SHARE: Final = 0.08
PHANTOM_LEVEL_DB: Final = -30.0
PHANTOM_BLEED_INDEX: Final = 0.5
"""B3: (active share below or level below) and bleed index at least -> ``not-present``."""

# reliability (4.5, G1) ---------------------------------------------------------------

Dimension = Literal["bleed", "artefacts"]
G1Status = Literal["not-measured", "passed", "failed"]

G1_STEM_TYPES: Final[tuple[StemName, ...]] = ("vocals", "drums", "bass", "other")
"""Stem types that V2 (MUSDB18-HQ: four sources) can validate; the others never can."""

G1_RESULTS: Final[dict[tuple[str, Dimension], G1Status]] = {
    (stem, dimension): "not-measured"
    for stem in G1_STEM_TYPES
    for dimension in ("bleed", "artefacts")
}
"""Outcome of gate G1 per stem type and dimension (Phase 0: not run, dataset pending)."""


def g1_status(channel: str, kind: QualityInputKind, dimension: Dimension) -> G1Status:
    """G1 outcome for a stem; detail, rest, guitar and piano stems are never measured."""
    if kind != "standard":
        return "not-measured"
    return G1_RESULTS.get((channel, dimension), "not-measured")


def reliability(channel: str, kind: QualityInputKind, dimension: Dimension) -> Reliability:
    """``validated`` only for a standard stem type whose dimension passed G1."""
    return "validated" if g1_status(channel, kind, dimension) == "passed" else "indicative"


def shown_level(level: QualityLevel, status: G1Status) -> QualityLevel:
    """A dimension that failed G1 shows only ``high``; low and medium become ``unknown``."""
    if status == "failed" and level in ("low", "medium"):
        return "unknown"
    return level
