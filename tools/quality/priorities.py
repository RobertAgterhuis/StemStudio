"""Rank soft-limit warnings by impact so owners know what to refactor first.

Impact = excess over the soft limit x check weight. A branch above the complexity limit weighs
three lines above the size limit, because it adds a test path. Production code is ranked before
test code, because the SIG maintainability model rates production code.
"""

from collections.abc import Sequence

from common import Finding, Severity, is_production

CHECK_WEIGHTS = {
    "complexity": 3.0,
    "parameters": 2.0,
    "unit-size": 1.0,
    "duplication": 1.0,
    "razor-code": 0.5,
    "raw-colour": 1.0,
}
"""Weight per check; unknown checks (``exemption``, ``dependency``) weigh 0."""

REMEDIES = {
    "complexity": "replace branches with guard clauses, a lookup table or a small strategy",
    "unit-size": "extract the steps into well-named private methods",
    "razor-code": "move the `@code` logic to a `.razor.cs` code-behind or a service",
    "duplication": "extract a shared helper, fixture or builder",
    "parameters": "introduce a parameter object (record)",
    "raw-colour": "replace the literal with a design token `var(--ss-...)` from tokens.css",
}
"""Concrete refactoring per check."""


def impact(finding: Finding) -> float:
    """Return the impact score of ``finding``."""
    return finding.excess * CHECK_WEIGHTS.get(finding.check, 0.0)


def top_warnings(findings: Sequence[Finding], count: int = 10) -> list[Finding]:
    """Return the ``count`` top warnings: production code first, then impact, path and line."""
    warnings = [f for f in findings if f.severity is Severity.WARNING]
    return sorted(warnings, key=_rank)[:count]


def _rank(finding: Finding) -> tuple[bool, float, str, int]:
    return (not is_production(finding.path), -impact(finding), finding.path, finding.line)


_INTRO = (
    "Impact = excess over the soft limit x weight (complexity 3, unit size 1, duplication 1, "
    "Razor `@code` 0.5). Production code is ranked first, because the SIG maintainability "
    "model rates production code.\n\n"
)
_TABLE_HEAD = [
    "| # | Owner | Location | Finding | Impact | Request |",
    "|---:|---|---|---|---:|---|",
]


def render_top(findings: Sequence[Finding], count: int = 10) -> str:
    """Return the Markdown top-``count`` refactor-request table."""
    title = f"## Top {count} refactor requests (soft warnings by impact)\n\n" + _INTRO
    top = top_warnings(findings, count)
    if not top:
        return title + "None."
    rows = [_row(rank, f) for rank, f in enumerate(top, start=1)]
    return title + "\n".join([*_TABLE_HEAD, *rows])


def _row(rank: int, finding: Finding) -> str:
    return (
        f"| {rank} | {finding.owner} | `{finding.path}:{finding.line}` | {finding.message} "
        f"| {impact(finding):.0f} | {REMEDIES.get(finding.check, '')} |"
    )
