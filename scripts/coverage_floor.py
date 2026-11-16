"""Asserts that a component's Cobertura report exists and meets the line AND branch floor.

The coverlet threshold (Directory.Build.props) already fails `dotnet test` below 90 %; this check
also fails when the report is missing, so a component cannot drop out of the gate unnoticed
(feature 011: StemStudio.Agent).

Usage: python scripts/coverage_floor.py <coverage.cobertura.xml> [--min <percent>]   (default 90)
Exit codes: 0 met, 1 below the floor or report missing, 2 usage. Stdlib only; the input is the
report the same job just produced (trusted).
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def rates(report: Path) -> tuple[float, float]:
    """Line and branch coverage in percent."""
    root = ET.parse(report).getroot()  # trusted: produced by the same job
    line = float(root.get("line-rate", 0))
    branch = float(root.get("branch-rate", 0))
    return line * 100, branch * 100


def check(report: Path, floor: float) -> int:
    """Prints the result; returns the exit code."""
    if not report.is_file():
        print(f"::error::coverage report missing: {report.as_posix()}")
        return 1
    line, branch = rates(report)
    verdict = "met" if min(line, branch) >= floor else "BELOW"
    print(
        f"{report.as_posix()}: line {line:.2f} %, branch {branch:.2f} % ({verdict} {floor:g} %)"
    )
    return 0 if verdict == "met" else 1


def main(argv: list[str]) -> int:
    """Entry point; see the module docstring."""
    if len(argv) == 1:
        return check(Path(argv[0]), 90.0)
    if len(argv) == 3 and argv[1] == "--min":
        return check(Path(argv[0]), float(argv[2]))
    print(__doc__, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
