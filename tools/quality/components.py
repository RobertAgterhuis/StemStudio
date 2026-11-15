"""Per-component unit metrics: three-band risk distributions, star estimates and clone counts.

The bands match the constitution's limits (unit size <= 15 / 16-30 / > 30 LOC, complexity
<= 5 / 6-10 / > 10, parameters <= 2 / 3-4 / > 4). Stars use the four-band SIG profile of
``risk.py`` and are a house estimate, not a Sigrid rating.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Literal

from common import is_test
from duplication import Clone
from risk import PROPERTIES, RiskProfile, risk_profile
from sig_check import UnitMetrics

Scope = Literal["production", "all", "tests"]
"""Which code of a component is measured."""

THREE_BANDS = {"Unit size (LOC)": (15, 30), "Unit complexity (CCN)": (5, 10)}
"""Constitution bands per property; parameters use ``(2, 4)``."""

WORKER = "worker/src/stemstudio_worker/"
WORKER_CORE = ("__init__.py", "config.py", "main.py", "ports.py", "schemas.py")
"""Worker modules outside the feature packages (composition root, ports, schemas)."""


@dataclass(frozen=True, slots=True)
class Component:
    """A named group of paths (prefixes) measured together."""

    name: str
    prefixes: tuple[str, ...]
    scope: Scope = "production"

    def contains(self, path: str, *, test: bool) -> bool:
        """Tell whether a unit or clone side at ``path`` belongs to this component."""
        if not path.startswith(self.prefixes):
            return False
        return self.scope == "all" or (self.scope == "tests") == test


COMPONENTS: tuple[Component, ...] = (
    Component("Domain", ("src/StemStudio.Domain/",)),
    Component("Contracts", ("src/StemStudio.Contracts/",)),
    Component("Persistence", ("src/StemStudio.Persistence/",)),
    Component("Api", ("src/StemStudio.Api/",)),
    Component("Web", ("src/StemStudio.Web/",)),
    Component("worker/ingest", (WORKER + "ingest/",)),
    Component("worker/metadata", (WORKER + "metadata/",)),
    Component("worker/render", (WORKER + "render/",)),
    Component("worker/separation", (WORKER + "separation/",)),
    Component("worker/detail", (WORKER + "detail/",)),
    Component("worker/devices", (WORKER + "devices/",)),
    Component("worker/analysis", (WORKER + "analysis/",)),
    Component("worker/jobs", (WORKER + "jobs/",)),
    Component("worker/api", (WORKER + "api/",)),
    Component("worker/core", tuple(WORKER + name for name in WORKER_CORE)),
    Component("audio-engine", ("src/audio-engine/",)),
    Component("web-interop", ("src/web-interop/",)),
    Component("e2e", ("e2e/",), scope="all"),
    Component("tools/quality", ("tools/quality/",), scope="all"),
    Component("Test code (all)", ("",), scope="tests"),
)
"""Reported components (production code unless the scope says otherwise)."""


@dataclass(frozen=True, slots=True)
class ComponentRow:
    """Measured values of one component."""

    component: Component
    units: int
    loc: int
    size: RiskProfile
    complexity: RiskProfile
    params: RiskProfile
    stars: tuple[int, int, int]
    clones: int


def component_rows(
    units: Sequence[UnitMetrics],
    clones: Sequence[Clone],
    components: Sequence[Component] = COMPONENTS,
) -> list[ComponentRow]:
    """Return one row per component."""
    return [_row(component, units, clones) for component in components]


def _row(
    component: Component, units: Sequence[UnitMetrics], clones: Sequence[Clone]
) -> ComponentRow:
    own = [u for u in units if component.contains(u.path, test=u.is_test)]
    size, complexity, params = _three_bands(own)
    loc = sum(u.nloc for u in own)
    touched = _clone_count(component, clones)
    return ComponentRow(component, len(own), loc, size, complexity, params, _stars(own), touched)


def _three_bands(units: Sequence[UnitMetrics]) -> list[RiskProfile]:
    return [
        risk_profile(units, metric, THREE_BANDS.get(name, (2, 4))) for name, metric, _ in PROPERTIES
    ]


def _stars(units: Sequence[UnitMetrics]) -> tuple[int, int, int]:
    size, complexity, params = (risk_profile(units, m, bins).stars for _, m, bins in PROPERTIES)
    return size, complexity, params


def _clone_count(component: Component, clones: Sequence[Clone]) -> int:
    return sum(1 for clone in clones if _touches(component, clone))


def _touches(component: Component, clone: Clone) -> bool:
    sides = (clone.first, clone.second)
    return any(component.contains(side, test=is_test(side)) for side in sides)


_HEADER = (
    "## Components\n\n"
    "Share of unit LOC per band. Size <= 15 / 16-30 / > 30 LOC, complexity <= 5 / 6-10 / > 10, "
    "parameters <= 2 / 3-4 / > 4. Stars (size / complexity / interfacing) are a house "
    "estimate from `tools/quality/risk.py`, not a Sigrid rating. Production code only, except "
    "e2e and tools/quality (all code) and the test-code row.\n\n"
    "| Component | Units | LOC | Size % | Complexity % | Params % | Stars (estimate) | Clones |\n"
    "|---|---:|---:|---|---|---|---|---:|"
)


def render_components(rows: Sequence[ComponentRow]) -> str:
    """Return the Markdown components table."""
    lines = [_HEADER]
    lines.extend(
        f"| {r.component.name} | {r.units} | {r.loc} | {_bands(r.size)} | "
        f"{_bands(r.complexity)} | {_bands(r.params)} | "
        f"{' / '.join(str(s) for s in r.stars)} | {r.clones} |"
        for r in rows
    )
    return "\n".join(lines)


def _bands(profile: RiskProfile) -> str:
    return f"{profile.low:.1f} / {profile.moderate:.1f} / {profile.high:.1f}"
