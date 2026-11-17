"""Duplicated-block detection with jscpd (``npx jscpd@<pinned>``).

Configuration lives in ``tools/quality/.jscpd.json`` (``minLines: 6``). A clone that touches
production code (``src/``, ``worker/src/``, ``generator/src/``, not tests) fails the gate; clones
only between test files, clones between worker and generator production code (two services
that may not share code), clones touching a listed data copy (``DATA_COPIES``, feature 011) and
clones touching a listed mirror (``MIRRORS``, feature 011) are reported as warnings.

Usage::

    python tools/quality/duplication.py [paths...]

Exit code 0 = no production clone, 1 = clone in production code, 2 = jscpd could not run.

"Could not run" covers every case in which no trustworthy measurement exists: ``npx`` missing,
a non-zero jscpd exit code (for example a path that does not exist, a cold npm cache without
network, a crash), a missing or unreadable JSON report, and a report that scanned no source
file. The tool error names the cause (the last line jscpd or npm printed), so a lane cannot
mistake an unmeasured tree for a clean one.
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


@dataclass(frozen=True, slots=True)
class ToolRun:
    """Exit code and combined console output of one jscpd run."""

    exit_code: int
    output: str = ""


Runner = Callable[[list[str], Path], ToolRun]
"""Runs a command (argument list) in a working directory and returns its outcome."""


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

DATA_COPIES: dict[str, str] = {}
"""Production files that hold copied data, not logic (path -> mandatory reason, feature 011).

A clone touching one of them is a warning that names the reason.
"""


MIRRORS: dict[str, str] = {
    "src/StemStudio.Agent/Policy/ImitationGuard.cs": (
        "agent pre-check mirrors the 009 imitation matching of StemStudio.Domain, "
        "which StemStudio.Agent may not reference"
    ),
    "src/StemStudio.Agent/Policy/ProvenanceChecker.cs": (
        "agent pre-check mirrors the 010 lyrics normalisation of StemStudio.Domain, "
        "which StemStudio.Agent may not reference"
    ),
}
"""Production files that mirror logic of a project they may not reference (path -> mandatory
reason, CTO decision at the 011 integration T080; the referenced original stays authoritative).

A clone touching one of them is a warning that names the reason.
"""


def _mirror_reason(clone: Clone) -> str | None:
    return MIRRORS.get(clone.first) or MIRRORS.get(clone.second)


def _data_copy_reason(clone: Clone) -> str | None:
    return DATA_COPIES.get(clone.first) or DATA_COPIES.get(clone.second)


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


def subprocess_runner(command: list[str], cwd: Path) -> ToolRun:
    """Run ``command`` without a shell and return its exit code and output.

    A command that cannot be started at all (``OSError``) is reported as exit code 127 with the
    error text, like a shell would, instead of raising.
    """
    try:
        completed = subprocess.run(  # noqa: S603
            command, cwd=cwd, check=False, capture_output=True, text=True, errors="replace"
        )
    except OSError as error:
        return ToolRun(127, str(error))
    return ToolRun(completed.returncode, f"{completed.stdout}\n{completed.stderr}")


def failure_reason(output: str) -> str:
    """The last meaningful line jscpd or npm printed (npm notices skipped); empty when none."""
    lines = [line.strip() for line in output.splitlines()]
    meaningful = [line for line in lines if line and not line.startswith("npm notice")]
    return meaningful[-1][:300] if meaningful else ""


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
        *(str(path.resolve()) for path in paths),
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

    A cross-service clone (worker <-> generator production code) and a clone touching a listed
    data copy are warnings with a note.
    """
    return [_clone_finding(clone) for clone in clones]


def _clone_finding(clone: Clone) -> Finding:
    message = f"{clone.lines} duplicated lines, also in {clone.second}:{clone.second_line}"
    note = _warning_note(clone)
    if note:
        message = f"{message} {note}"
    error = clone.in_production and not note
    severity = Severity.ERROR if error else Severity.WARNING
    return Finding("duplication", severity, clone.first, clone.first_line, message, clone.lines)


def _warning_note(clone: Clone) -> str:
    """Why a production clone is only a warning (empty: it is not)."""
    if clone.cross_service:
        return CROSS_SERVICE_NOTE
    reason = _data_copy_reason(clone)
    if reason:
        return f"(data copy: {reason})"
    mirror = _mirror_reason(clone)
    return f"(mirror: {mirror})" if mirror else ""


def run(root: Path, paths: Sequence[Path], runner: Runner = subprocess_runner) -> DuplicationResult:
    """Run jscpd over ``paths`` (default roots when empty) and evaluate the report.

    Relative ``paths`` are resolved against the current directory before jscpd runs in
    ``root``, the same way ``sig_check`` reads them.
    """
    npx = shutil.which("npx")
    if npx is None:
        return DuplicationResult(tool_error="npx not found: install Node.js 22 to run jscpd")
    with tempfile.TemporaryDirectory(prefix="jscpd-") as temp:
        output_dir = Path(temp)
        command = build_command(npx, output_dir, paths or default_paths(root))
        outcome = runner(command, root)
        report = _read_report(outcome, output_dir / "jscpd-report.json")
    if isinstance(report, str):
        return DuplicationResult(tool_error=report)
    clones, percentage = parse_report(report, root)
    return DuplicationResult(clones, evaluate(clones), percentage)


def _read_report(outcome: ToolRun, report_file: Path) -> dict[str, Any] | str:
    """The parsed jscpd report, or the tool error that says why there is no usable one."""
    reason = failure_reason(outcome.output)
    suffix = f": {reason}" if reason else ""
    if outcome.exit_code != 0 or not report_file.is_file():
        return f"jscpd failed (exit code {outcome.exit_code}){suffix}"
    try:
        report: dict[str, Any] = json.loads(report_file.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        return f"jscpd report unreadable: {error}"
    if not report.get("statistics", {}).get("total", {}).get("sources"):
        return "jscpd scanned no source file: check the paths and tools/quality/.jscpd.json"
    return report


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
