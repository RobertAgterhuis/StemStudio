"""One-command StemStudio quality gate: unit metrics, duplication, dependencies, raw colours.

Usage::

    python tools/quality/run_all.py [paths...] [--report docs/quality/SIG-REPORT.md]
    uv run --project tools/quality python tools/quality/run_all.py --report ...

Exit codes: 0 = pass (warnings allowed), 1 = hard-limit breach, forbidden dependency, cycle or
production clone, 2 = a tool could not run (e.g. Node/npx missing for jscpd).
"""

import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field
from pathlib import Path

import css_tokens_check
import dependency_check
import duplication
import sig_check
from common import Finding, build_parser, count_errors, format_findings, is_test
from report import ReportInput, render

DuplicationRun = Callable[[Path, Sequence[Path]], duplication.DuplicationResult]
"""Signature of ``duplication.run`` (injectable for tests)."""


@dataclass(slots=True)
class GateResult:
    """Combined outcome of all checks."""

    sig: sig_check.SigResult
    dup: duplication.DuplicationResult
    dependency_findings: list[Finding] = field(default_factory=list)
    token_findings: list[Finding] = field(default_factory=list)

    @property
    def findings(self) -> list[Finding]:
        """All findings of all checks."""
        return [
            *self.sig.findings,
            *self.dup.findings,
            *self.dependency_findings,
            *self.token_findings,
        ]

    @property
    def tool_errors(self) -> list[str]:
        """Tool failures (missing Node, jscpd crash)."""
        return [self.dup.tool_error] if self.dup.tool_error else []

    @property
    def exit_code(self) -> int:
        """0 = pass, 1 = violation, 2 = tool error."""
        if self.tool_errors:
            return 2
        return 1 if count_errors(self.findings) else 0


def run(root: Path, paths: Sequence[Path], dup_run: DuplicationRun = duplication.run) -> GateResult:
    """Run every check below ``root``."""
    return GateResult(
        sig=sig_check.run(root, paths),
        dup=dup_run(root, paths),
        dependency_findings=dependency_check.run(root),
        token_findings=css_tokens_check.run(root),
    )


def to_report(result: GateResult) -> str:
    """Render ``result`` as the Markdown SIG report."""
    data = ReportInput(
        production_units=[u for u in result.sig.units if not is_test(u.path)],
        files=result.sig.files,
        total_units=len(result.sig.units),
        duplicated_percentage=None if result.dup.tool_error else result.dup.duplicated_percentage,
        findings=result.findings,
        tool_errors=result.tool_errors,
        units=result.sig.units,
        clones=result.dup.clones,
        exemptions=result.sig.exemptions,
    )
    return render(data)


def summary(result: GateResult) -> str:
    """Return the console summary."""
    errors = count_errors(result.findings)
    lines = [format_findings(result.findings)] if result.findings else []
    lines.extend(f"exemption: {exemption}" for exemption in result.sig.exemptions)
    lines.extend(f"tool error: {error}" for error in result.tool_errors)
    lines.append(
        f"quality gate: {result.sig.files} files, {len(result.sig.units)} units, "
        f"{len(result.dup.clones)} clones, {len(result.sig.exemptions)} exemptions, "
        f"{errors} errors, "
        f"{len(result.findings) - errors} warnings -> exit {result.exit_code}"
    )
    return "\n".join(lines) + "\n"


def main(argv: Sequence[str] | None = None, dup_run: DuplicationRun = duplication.run) -> int:
    """Command-line entry point; returns the exit code."""
    parser = build_parser(__doc__)
    parser.add_argument("--report", type=Path, help="write the Markdown SIG report here")
    args = parser.parse_args(argv)
    result = run(args.root, args.paths, dup_run)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(to_report(result), encoding="utf-8", newline="\n")
    sys.stdout.write(summary(result))
    return result.exit_code


if __name__ == "__main__":
    sys.exit(main())
