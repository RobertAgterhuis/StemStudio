"""SIG unit metrics for C#, Python, TypeScript and Razor ``@code`` blocks (lizard).

Limits (constitution principle II):

* unit size: warn when NLOC > 15, fail when NLOC > 30 (test code: reported as a warning only)
* unit complexity: warn when CCN > 5, fail when CCN > 10
* unit interfacing: fail when a unit has more than 4 parameters
* Razor: warn when an ``@code`` block is longer than 30 lines (move logic to a code-behind)

Usage::

    python tools/quality/sig_check.py [paths...] [--json FILE]

Exit code 0 = no hard breach, 1 = at least one hard breach.
"""

import json
import sys
from collections.abc import Sequence
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

import lizard

from common import (
    Finding,
    Severity,
    build_parser,
    default_paths,
    format_findings,
    is_test,
    iter_source_files,
    relative,
)
from razor import find_code_blocks, to_csharp


@dataclass(frozen=True, slots=True)
class Limits:
    """Soft (warn) and hard (fail) limits per unit."""

    soft_nloc: int = 15
    hard_nloc: int = 30
    soft_ccn: int = 5
    hard_ccn: int = 10
    hard_params: int = 4
    razor_code_lines: int = 30


@dataclass(frozen=True, slots=True)
class UnitMetrics:
    """Measured values of one unit (method, function, lambda with a body)."""

    path: str
    name: str
    line: int
    nloc: int
    ccn: int
    params: int
    is_test: bool


@dataclass(slots=True)
class SigResult:
    """Outcome of a ``sig_check`` run."""

    units: list[UnitMetrics] = field(default_factory=list)
    findings: list[Finding] = field(default_factory=list)
    files: int = 0

    @property
    def errors(self) -> list[Finding]:
        """Hard-limit breaches."""
        return [f for f in self.findings if f.severity is Severity.ERROR]

    @property
    def warnings(self) -> list[Finding]:
        """Soft-limit breaches."""
        return [f for f in self.findings if f.severity is Severity.WARNING]


def measure_source(rel_path: str, text: str) -> list[UnitMetrics]:
    """Measure all units in ``text``; ``rel_path`` selects the language by its suffix."""
    analysed_name, code = rel_path, text
    if rel_path.lower().endswith(".razor"):
        analysed_name, code = rel_path + ".cs", to_csharp(text)
    info: Any = lizard.analyze_file.analyze_source_code(analysed_name, code)
    test = is_test(rel_path)
    return [_to_metrics(rel_path, function, test) for function in info.function_list]


def _to_metrics(rel_path: str, function: Any, test: bool) -> UnitMetrics:
    return UnitMetrics(
        path=rel_path,
        name=str(function.name),
        line=int(function.start_line),
        nloc=int(function.nloc),
        ccn=int(function.cyclomatic_complexity),
        params=int(function.parameter_count),
        is_test=test,
    )


def evaluate_unit(unit: UnitMetrics, limits: Limits) -> list[Finding]:
    """Return the findings for one unit."""
    findings = [_size_finding(unit, limits), _complexity_finding(unit, limits)]
    if unit.params > limits.hard_params:
        message = f"{unit.name} has {unit.params} parameters (max {limits.hard_params})"
        findings.append(_finding("parameters", Severity.ERROR, unit, message))
    return [f for f in findings if f is not None]


def _size_finding(unit: UnitMetrics, limits: Limits) -> Finding | None:
    if unit.nloc <= limits.soft_nloc:
        return None
    if unit.nloc <= limits.hard_nloc:
        message = f"{unit.name} has {unit.nloc} lines of code (limit {limits.soft_nloc})"
        return _finding("unit-size", Severity.WARNING, unit, message)
    message = f"{unit.name} has {unit.nloc} lines of code (limit {limits.hard_nloc})"
    if unit.is_test:
        return _finding("unit-size", Severity.WARNING, unit, message + " (test code: reported)")
    return _finding("unit-size", Severity.ERROR, unit, message)


def _complexity_finding(unit: UnitMetrics, limits: Limits) -> Finding | None:
    if unit.ccn <= limits.soft_ccn:
        return None
    hard = unit.ccn > limits.hard_ccn
    severity = Severity.ERROR if hard else Severity.WARNING
    limit = limits.hard_ccn if hard else limits.soft_ccn
    message = f"{unit.name} has cyclomatic complexity {unit.ccn} (limit {limit})"
    return _finding("complexity", severity, unit, message)


def _finding(check: str, severity: Severity, unit: UnitMetrics, message: str) -> Finding:
    return Finding(check, severity, unit.path, unit.line, message)


def evaluate_razor(rel_path: str, text: str, limits: Limits) -> list[Finding]:
    """Warn about ``@code`` blocks longer than ``limits.razor_code_lines``."""
    return [
        Finding(
            "razor-code",
            Severity.WARNING,
            rel_path,
            block.start_line,
            f"@code block has {block.lines} lines (limit {limits.razor_code_lines}); "
            "move logic to a code-behind or service",
        )
        for block in find_code_blocks(text)
        if block.lines > limits.razor_code_lines
    ]


def run(root: Path, paths: Sequence[Path], limits: Limits | None = None) -> SigResult:
    """Measure every source file under ``paths`` (default roots when empty)."""
    active = limits or Limits()
    result = SigResult()
    for path in iter_source_files(root, paths or default_paths(root)):
        _measure_file(path, root, active, result)
    return result


def _measure_file(path: Path, root: Path, limits: Limits, result: SigResult) -> None:
    rel_path = relative(path, root)
    text = path.read_text(encoding="utf-8", errors="replace")
    units = measure_source(rel_path, text)
    result.files += 1
    result.units.extend(units)
    for unit in units:
        result.findings.extend(evaluate_unit(unit, limits))
    if rel_path.lower().endswith(".razor"):
        result.findings.extend(evaluate_razor(rel_path, text, limits))


def to_json(result: SigResult) -> dict[str, Any]:
    """Return a JSON-serialisable summary of ``result``."""
    return {
        "files": result.files,
        "units": [asdict(unit) for unit in result.units],
        "findings": [asdict(finding) for finding in result.findings],
    }


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    parser = build_parser(__doc__)
    parser.add_argument("--json", type=Path, help="write the full result as JSON")
    args = parser.parse_args(argv)
    result = run(args.root, args.paths)
    if args.json:
        args.json.write_text(json.dumps(to_json(result), indent=2), encoding="utf-8")
    lines = [format_findings(result.findings)] if result.findings else []
    lines.append(
        f"sig_check: {result.files} files, {len(result.units)} units, "
        f"{len(result.errors)} errors, {len(result.warnings)} warnings"
    )
    sys.stdout.write("\n".join(lines) + "\n")
    return 1 if result.errors else 0


if __name__ == "__main__":
    sys.exit(main())
