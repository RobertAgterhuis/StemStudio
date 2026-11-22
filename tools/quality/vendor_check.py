"""Licence records of vendored third-party code (features 002, 008 and 016).

The vendor folders are excluded from the unit metrics, duplication and the dependency graph
(``common.EXCLUDED_PATHS``), so this check makes sure each vendored package still carries
its licence: every directory directly below a vendor root must contain a ``LICENSE`` file
(``LICENCE`` and ``COPYING`` are accepted, any extension, any case) and a ``NOTICE`` file that
records origin, version and modifications.

Usage::

    python tools/quality/vendor_check.py [--root DIR]

Exit code 0 = clean, 1 = a vendored package lacks a licence record.
"""

import sys
from collections.abc import Iterator, Sequence
from pathlib import Path

from common import EXCLUDED_DIRS, Finding, Severity, build_parser, relative, report_findings

VENDOR_ROOTS = (
    "worker/src/stemstudio_worker/detail/vendor",
    "worker/src/stemstudio_worker/transcription/vendor",
    "worker/src/stemstudio_worker/harmony/vendor",
)
"""Repository-relative folders whose sub-directories are vendored packages."""

REQUIRED_RECORDS = (
    ("LICENSE", frozenset({"LICENSE", "LICENCE", "COPYING"})),
    ("NOTICE", frozenset({"NOTICE"})),
)
"""Record name -> accepted file stems (upper case, before the first dot)."""

_PACKAGE_PREFIX = "worker/src/stemstudio_worker/"


def run(root: Path) -> list[Finding]:
    """Return an error per missing licence record of every vendored package below ``root``."""
    return [finding for package in _packages(root) for finding in _package_findings(package, root)]


def _packages(root: Path) -> Iterator[Path]:
    for vendor_root in VENDOR_ROOTS:
        yield from _sub_packages(root / vendor_root)


def _sub_packages(folder: Path) -> list[Path]:
    if not folder.is_dir():
        return []
    return sorted(p for p in folder.iterdir() if p.is_dir() and p.name not in EXCLUDED_DIRS)


def _package_findings(package: Path, root: Path) -> Iterator[Finding]:
    stems = {p.name.split(".")[0].upper() for p in package.iterdir() if p.is_file()}
    rel_path = relative(package, root)
    name = rel_path.removeprefix(_PACKAGE_PREFIX)
    for record, accepted in REQUIRED_RECORDS:
        if not stems & accepted:
            message = f"vendored package {name} has no {record} file"
            yield Finding("vendor", Severity.ERROR, rel_path, 0, message)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    return report_findings("vendor_check", run(args.root))


if __name__ == "__main__":
    sys.exit(main())
