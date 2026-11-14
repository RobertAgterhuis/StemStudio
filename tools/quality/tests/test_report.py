import pytest

from common import Finding, Severity
from duplication import Clone
from report import ReportInput, render
from risk import (
    COMPLEXITY_BINS,
    UNIT_SIZE_BINS,
    RiskProfile,
    duplication_stars,
    risk_profile,
)
from sig_check import Exemption, UnitMetrics


def _unit(nloc: int, ccn: int = 1, params: int = 0) -> UnitMetrics:
    return UnitMetrics("src/a.cs", "f", 1, nloc, ccn, params, False)


def test_risk_profile_is_loc_weighted() -> None:
    units = [_unit(10), _unit(20), _unit(40), _unit(70)]
    profile = risk_profile(units, lambda u: u.nloc, UNIT_SIZE_BINS)
    assert profile == RiskProfile(100 * 10 / 140, 100 * 20 / 140, 100 * 40 / 140, 100 * 70 / 140)


def test_empty_profile_is_all_low() -> None:
    assert risk_profile([], lambda u: u.ccn, COMPLEXITY_BINS) == RiskProfile(100.0, 0, 0, 0)


@pytest.mark.parametrize(
    ("profile", "stars"),
    [
        (RiskProfile(100, 0, 0, 0), 5),
        (RiskProfile(80, 20, 0, 0), 4),
        (RiskProfile(80, 10, 9, 1), 4),
        (RiskProfile(70, 30, 0, 0), 3),
        (RiskProfile(55, 25, 15, 5), 2),
        (RiskProfile(40, 60, 0, 0), 1),
        (RiskProfile(90, 0, 0, 10), 2),
        (RiskProfile(85, 0, 0, 15), 1),
    ],
)
def test_star_estimate(profile: RiskProfile, stars: int) -> None:
    assert profile.stars == stars


@pytest.mark.parametrize(("pct", "stars"), [(0.0, 5), (3.0, 5), (4.0, 4), (8, 3), (15, 2), (30, 1)])
def test_duplication_stars(pct: float, stars: int) -> None:
    assert duplication_stars(pct) == stars


def _input(
    findings: list[Finding], *, dup: float | None = 1.0, tools: list[str] | None = None
) -> ReportInput:
    return ReportInput([_unit(10)], 3, 4, dup, findings, tools or [])


def test_render_clean_report() -> None:
    text = render(_input([]))
    assert "**Verdict: PASS** - 0 errors" in text
    assert "| Unit size (LOC) | 100.0 | 0.0 | 0.0 | 0.0 | 5 |" in text
    assert "| Duplication (% lines, all scanned code) | 1.00 | | | | 5 |" in text
    assert text.count("None.") == 2


def test_render_groups_requests_per_owner() -> None:
    findings = [
        Finding("unit-size", Severity.WARNING, "src/StemStudio.Api/B.cs", 9, "long"),
        Finding("complexity", Severity.ERROR, "src/StemStudio.Web/A.cs", 2, "complex"),
        Finding("unit-size", Severity.ERROR, "src/StemStudio.Api/A.cs", 5, "too long"),
    ]
    text = render(_input(findings))
    assert "**Verdict: FAIL** - 2 errors (hard limits), 1 warnings" in text
    backend = text.index("### backend")
    frontend = text.index("### frontend")
    assert backend < frontend
    section = text[backend:frontend]
    assert section.index("[error] `src/StemStudio.Api/A.cs:5`") < section.index("[warning]")
    assert "| error | complexity | `src/StemStudio.Web/A.cs:2` | frontend | complex |" in text


def test_render_tool_error() -> None:
    text = render(_input([], dup=None, tools=["npx not found"]))
    assert "**Verdict: FAIL**" in text
    assert "**Tool error:** npx not found" in text
    assert "| Duplication | n/a | | | | n/a |" in text


def _full(findings: list[Finding]) -> ReportInput:
    units = [_unit(10)]
    clones = [
        Clone("src/StemStudio.Api/A.cs", 3, "src/StemStudio.Api/B.cs", 9, 7),
        Clone("tests/X.Tests/A.cs", 1, "tests/X.Tests/B.cs", 2, 6),
    ]
    exemptions = [Exemption("tests/X.Tests/L.cs", 29, "L::Log", "interface-mandated")]
    return ReportInput(units, 3, 4, 1.0, findings, [], units, clones, exemptions)


def test_render_lists_exemptions_clones_and_dependencies() -> None:
    dependency = Finding("dependency", Severity.ERROR, "StemStudio.Web", 0, "Web -> Persistence")
    text = render(_full([dependency]))
    assert "## Exemptions (1)" in text
    assert "| `tests/X.Tests/L.cs:29` | L::Log | interface-mandated |" in text
    assert "## Clones (2, 1 in production code)" in text
    assert "| 1 | `src/StemStudio.Api/A.cs:3` | `src/StemStudio.Api/B.cs:9` | 7 | backend | yes |" in text
    assert "| 2 | `tests/X.Tests/A.cs:1` | `tests/X.Tests/B.cs:2` | 6 | architect | no |" in text
    assert "## Dependency direction\n\n**FAIL** - 1 errors, 0 warnings" in text
    assert "## Components" in text
    assert "## Top 10 refactor requests" in text


def test_render_estimated_rating_and_clean_sections() -> None:
    text = render(_full([]))
    assert "Estimated maintainability: **5.0 stars**" in text
    assert "not a Sigrid rating" in text
    assert "## Dependency direction\n\n**PASS** - no forbidden edges or cycles." in text
    empty = render(_input([]))
    assert "## Exemptions (0)\n\nNone." in empty
    assert "## Clones (0, 0 in production code)\n\nNone." in empty
    assert "Estimated maintainability: **n/a**" in render(_input([], dup=None))
