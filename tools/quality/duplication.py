"""Duplicated-block detection with jscpd (``npx jscpd@<pinned>``).

Configuration lives in ``tools/quality/.jscpd.json`` (``minLines: 6``). A clone that touches
production code (``src/``, ``worker/src/``, ``generator/src/``, not tests) fails the gate; clones
only between test files, and clones between worker and generator production code (two services
that may not share code), are reported as warnings.

Usage::

    python tools/quality/duplication.py [paths...]

Exit code 0 = no production clone, 1 = clone in production code, 2 = jscpd could not run.
"""

import json
import shutil
import subprocess
import sys
import tempfile
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from common import (
    Finding,
    Severity,
    build_parser,
    count_errors,
    default_paths,
    format_findings,
    is_excluded,
    is_production,
    relative,
)

JSCPD_VERSION = "5.4.2"
"""Pinned jscpd release (MIT)."""

CONFIG_PATH = Path(__file__).resolve().parent / ".jscpd.json"
"""jscpd configuration file."""

Runner = Callable[[list[str], Path], int]
"""Runs a command (argument list) in a working directory and returns its exit code."""


@dataclass(frozen=True, slots=True)
class Clone:
    """A duplicated block found in two places."""

    first: str
    first_line: int
    second: str
    second_line: int
    lines: int

    @property
    def in_production(self) -> bool:
        """True when either side is production code."""
        return is_production(self.first) or is_production(self.second)

    @property
    def cross_service(self) -> bool:
        """True when one side is worker and the other generator production code.

        The two services may not import each other (``dependency_check``), so code they both
        need (environment parsing, contract mirrors) can only be copied; such a clone is a
        warning (features 009/010).
        """
        services = {_service(self.first), _service(self.second)}
        return services == set(SERVICE_ROOTS)


SERVICE_ROOTS = ("worker/src/", "generator/src/")
"""Production roots of the two Python services that may not share code."""

CROSS_SERVICE_NOTE = "(cross-service: worker and generator may not share code)"


def _service(path: str) -> str | None:
    if not is_production(path):
        return None
    return next((root for root in SERVICE_ROOTS if path.startswith(root)), None)


@dataclass(slots=True)
class DuplicationResult:
    """Outcome of a duplication run."""

    clones: list[Clone] = field(default_factory=list)
    findings: list[Finding] = field(default_factory=list)
    duplicated_percentage: float = 0.0
    tool_error: str | None = None


def subprocess_runner(command: list[str], cwd: Path) -> int:
    """Run ``command`` without a shell and return its exit code."""
    completed = subprocess.run(command, cwd=cwd, check=False, capture_output=True)  # noqa: S603
    return completed.returncode


def build_command(npx: str, output_dir: Path, paths: Sequence[Path]) -> list[str]:
    """Return the jscpd argument list (JSON report into ``output_dir``)."""
    return [
        npx,
        "--yes",
        f"jscpd@{JSCPD_VERSION}",
        "--config",
        str(CONFIG_PATH),
        "--output",
        str(output_dir),
        *(str(path) for path in paths),
    ]


def parse_report(report: dict[str, Any], root: Path) -> tuple[list[Clone], float]:
    """Return the clones and the total duplicated-lines percentage of a jscpd JSON report.

    Clones touching an excluded path (e.g. vendored code) are dropped, in case jscpd's own
    ``ignore`` list missed them.
    """
    clones = [_to_clone(entry, root) for entry in report.get("duplicates", [])]
    clones = [c for c in clones if not (is_excluded(c.first) or is_excluded(c.second))]
    total = report.get("statistics", {}).get("total", {})
    return clones, float(total.get("percentage", 0.0))


def _to_clone(entry: dict[str, Any], root: Path) -> Clone:
    first, second = entry["firstFile"], entry["secondFile"]
    return Clone(
        first=_normalise(first["name"], root),
        first_line=int(first["start"]),
        second=_normalise(second["name"], root),
        second_line=int(second["start"]),
        lines=int(entry["lines"]),
    )


def _normalise(name: str, root: Path) -> str:
    """Strip the Windows long-path prefix and make the path repository-relative."""
    return relative(Path(name.removeprefix("\\\\?\\")), root)


def evaluate(clones: Sequence[Clone]) -> list[Finding]:
    """Return one finding per clone: error in production code, warning in test-only code.

    A cross-service clone (worker <-> generator production code) is a warning with a note.
    """
    return [_clone_finding(clone) for clone in clones]


def _clone_finding(clone: Clone) -> Finding:
    message = f"{clone.lines} duplicated lines, also in {clone.second}:{clone.second_line}"
    if clone.cross_service:
        message = f"{message} {CROSS_SERVICE_NOTE}"
    error = clone.in_production and not clone.cross_service
    severity = Severity.ERROR if error else Severity.WARNING
    return Finding("duplication", severity, clone.first, clone.first_line, message, clone.lines)


def run(root: Path, paths: Sequence[Path], runner: Runner = subprocess_runner) -> DuplicationResult:
    """Run jscpd over ``paths`` (default roots when empty) and evaluate the report."""
    npx = shutil.which("npx")
    if npx is None:
        return DuplicationResult(tool_error="npx not found: install Node.js 22 to run jscpd")
    with tempfile.TemporaryDirectory(prefix="jscpd-") as temp:
        output_dir = Path(temp)
        command = build_command(npx, output_dir, paths or default_paths(root))
        exit_code = runner(command, root)
        report_file = output_dir / "jscpd-report.json"
        if exit_code != 0 or not report_file.is_file():
            return DuplicationResult(tool_error=f"jscpd failed (exit code {exit_code})")
        report = json.loads(report_file.read_text(encoding="utf-8"))
    clones, percentage = parse_report(report, root)
    return DuplicationResult(clones, evaluate(clones), percentage)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__).parse_args(argv)
    result = run(args.root, args.paths)
    if result.tool_error:
        sys.stderr.write(f"duplication: {result.tool_error}\n")
        return 2
    errors = count_errors(result.findings)
    lines = [format_findings(result.findings)] if result.findings else []
    lines.append(
        f"duplication: {len(result.clones)} clones, {errors} in production code, "
        f"{result.duplicated_percentage:.2f}% duplicated lines"
    )
    sys.stdout.write("\n".join(lines) + "\n")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
