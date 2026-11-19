"""SIG-style risk profiles and indicative star ratings.

Risk categories follow the SIG/TUViT maintainability model (lines of code per unit, McCabe
complexity, parameters per unit). The star thresholds below are a house heuristic calibrated to
the constitution's limits, not SIG's benchmark; the project uses no Sigrid rating (ADR-038).
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass

from sig_check import UnitMetrics

UNIT_SIZE_BINS = (15, 30, 60)
"""Upper bounds of low / moderate / high risk; above the last bound is very high."""

COMPLEXITY_BINS = (5, 10, 25)
PARAMETER_BINS = (2, 4, 6)

STAR_THRESHOLDS = (
    (5, (15.0, 5.0, 0.0)),
    (4, (25.0, 10.0, 2.0)),
    (3, (35.0, 15.0, 5.0)),
    (2, (50.0, 25.0, 10.0)),
)
"""Max % of code that is moderate-or-worse, high-or-worse and very high, per star level."""

DUPLICATION_STARS = ((5, 3.0), (4, 5.0), (3, 10.0), (2, 20.0))
"""Max % duplicated lines per star level."""


@dataclass(frozen=True, slots=True)
class RiskProfile:
    """Share of code (NLOC-weighted, %) per risk category: low, moderate, high, very high."""

    low: float
    moderate: float
    high: float
    very_high: float

    @property
    def stars(self) -> int:
        """Indicative star rating (1-5) from the cumulative profile."""
        measured = (
            self.moderate + self.high + self.very_high,
            self.high + self.very_high,
            self.very_high,
        )
        for stars, maxima in STAR_THRESHOLDS:
            if all(value <= limit for value, limit in zip(measured, maxima, strict=True)):
                return stars
        return 1


Metric = Callable[[UnitMetrics], int]
"""Selects the measured value of a unit (NLOC, CCN or parameters)."""


def risk_profile(units: Sequence[UnitMetrics], value: Metric, bins: Sequence[int]) -> RiskProfile:
    """Return the NLOC-weighted risk profile of ``value`` over ``units`` (empty -> all low).

    With fewer than three ``bins`` the unused upper categories stay 0.
    """
    totals = [0, 0, 0, 0]
    for unit in units:
        totals[_category(value(unit), bins)] += unit.nloc
    weight = sum(totals) or 1
    low, moderate, high, very_high = (100.0 * total / weight for total in totals)
    if not units:
        low = 100.0
    return RiskProfile(low, moderate, high, very_high)


def _category(measured: int, bins: Sequence[int]) -> int:
    return next((index for index, bound in enumerate(bins) if measured <= bound), len(bins))


def duplication_stars(percentage: float) -> int:
    """Indicative star rating (1-5) for a duplicated-lines percentage."""
    return next((stars for stars, limit in DUPLICATION_STARS if percentage <= limit), 1)


def nloc(unit: UnitMetrics) -> int:
    """Unit size metric."""
    return unit.nloc


def ccn(unit: UnitMetrics) -> int:
    """Unit complexity metric."""
    return unit.ccn


def params(unit: UnitMetrics) -> int:
    """Unit interfacing metric."""
    return unit.params


PROPERTIES: tuple[tuple[str, Metric, tuple[int, ...]], ...] = (
    ("Unit size (LOC)", nloc, UNIT_SIZE_BINS),
    ("Unit complexity (CCN)", ccn, COMPLEXITY_BINS),
    ("Unit interfacing (params)", params, PARAMETER_BINS),
)
"""Name, metric and SIG bins of the three unit properties."""


def profiles(units: Sequence[UnitMetrics]) -> list[tuple[str, RiskProfile]]:
    """Return the unit size, complexity and interfacing risk profiles of ``units``."""
    return [(name, risk_profile(units, metric, bins)) for name, metric, bins in PROPERTIES]
