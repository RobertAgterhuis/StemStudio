"""Shared types and path rules of the StemStudio quality gate.

Every check returns ``Finding`` objects; ``error`` findings fail the gate, ``warning`` findings
are reported only. Paths in findings are repository-relative POSIX strings so that reports are
identical on Windows and Linux.
"""

import argparse
from collections.abc import Iterable, Iterator, Sequence
from dataclasses import dataclass
from enum import StrEnum
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
"""Repository root (``tools/quality`` lives two levels below it)."""

SOURCE_SUFFIXES = frozenset({".cs", ".py", ".ts", ".tsx", ".razor"})
"""File types measured by the gate (C#, Python, TypeScript, Razor ``@code`` blocks)."""

DEFAULT_ROOTS = ("src", "tests", "worker", "e2e", "tools/quality")
"""Source roots scanned when no paths are given."""

PRODUCTION_ROOTS = ("src/", "worker/src/")
"""Prefixes of production code; duplication there fails the gate."""

EXCLUDED_DIRS = frozenset(
    {
        "bin",
        "obj",
        "node_modules",
        ".venv",
        "venv",
        "Migrations",
        "TestResults",
        "coverage",
        "dist",
        ".git",
        "__pycache__",
        ".mypy_cache",
        ".ruff_cache",
        ".pytest_cache",
        "playwright-report",
        "test-results",
    }
)
"""Directory names that only contain generated, vendored or build output."""

EXCLUDED_SUFFIXES = (".g.cs", ".generated.cs", ".designer.cs", ".d.ts", ".min.js")
"""Generated-file suffixes (compared case-insensitively)."""

EXCLUDED_PATHS = (
    "src/StemStudio.Web/wwwroot/js/audio-engine.js",
    "tools/quality/tests/samples/",
    "worker/src/stemstudio_worker/detail/vendor/",
)
"""Repository-relative paths (files or prefixes) never measured.

``detail/vendor/`` holds vendored third-party MIT inference code (MSST), excluded from SIG like
``Migrations/`` (feature 002 plan.md Complexity Tracking, accepted by the CTO).
"""

OWNERS = (
    ("src/StemStudio.Contracts/", "architect"),
    ("src/StemStudio.Domain/", "backend"),
    ("src/StemStudio.Persistence/", "backend"),
    ("src/StemStudio.Api/", "backend"),
    ("tests/StemStudio.Domain.Tests/", "backend"),
    ("tests/StemStudio.Persistence.Tests/", "backend"),
    ("tests/StemStudio.Api.Tests/", "backend"),
    ("src/StemStudio.Web/", "frontend"),
    ("tests/StemStudio.Web.Tests/", "frontend"),
    ("src/audio-engine/src/types.ts", "architect"),
    ("src/audio-engine/", "audio-engine"),
    ("worker/src/stemstudio_worker/ingest/", "audio-ingest"),
    ("worker/tests/ingest/", "audio-ingest"),
    ("worker/src/stemstudio_worker/analysis/", "beat-analysis"),
    ("worker/tests/analysis/", "beat-analysis"),
    ("worker/src/stemstudio_worker/separation/", "stem-separation"),
    ("worker/src/stemstudio_worker/detail/", "stem-separation"),
    ("worker/src/stemstudio_worker/devices/", "stem-separation"),
    ("worker/src/stemstudio_worker/jobs/", "stem-separation"),
    ("worker/src/stemstudio_worker/api/", "stem-separation"),
    ("worker/src/stemstudio_worker/main.py", "stem-separation"),
    ("worker/tests/separation/", "stem-separation"),
    ("worker/tests/detail/", "stem-separation"),
    ("worker/tests/devices/", "stem-separation"),
    ("worker/tests/jobs/", "stem-separation"),
    ("worker/tests/api/", "stem-separation"),
    ("e2e/", "qa"),
    ("tests/fixtures/", "qa"),
    ("tools/quality/", "quality-gate"),
)
"""Path prefix -> owning agent (plan.md ownership map); first match wins."""

DEFAULT_OWNER = "architect"
"""Owner of everything not listed (skeleton files, ports, schemas, config)."""


class Severity(StrEnum):
    """Finding severity: errors fail the gate, warnings are reported."""

    ERROR = "error"
    WARNING = "warning"


@dataclass(frozen=True, slots=True)
class Finding:
    """One rule breach.

    Attributes:
        check: Check name (``unit-size``, ``complexity``, ``parameters``, ``razor-code``,
            ``exemption``, ``duplication``, ``dependency``).
        severity: ``error`` (hard limit) or ``warning`` (soft limit).
        path: Repository-relative POSIX path, or a project/module name for graph findings.
        line: 1-based line, 0 when not applicable.
        message: Human-readable description incl. measured value and limit.
        excess: Amount above the limit (lines, branches, clone lines); 0 when not measurable.
            Used to rank refactor requests.
    """

    check: str
    severity: Severity
    path: str
    line: int
    message: str
    excess: int = 0

    @property
    def owner(self) -> str:
        """The agent that owns ``path``."""
        return owner_of(self.path)


def owner_of(path: str) -> str:
    """Return the owning agent of a repository-relative path."""
    for prefix, owner in OWNERS:
        if path.startswith(prefix):
            return owner
    return DEFAULT_OWNER


def relative(path: Path, root: Path) -> str:
    """Return ``path`` relative to ``root`` as a POSIX string (absolute when outside root)."""
    resolved = path.resolve()
    try:
        return resolved.relative_to(root.resolve()).as_posix()
    except ValueError:
        return resolved.as_posix()


def is_excluded(rel_path: str) -> bool:
    """Tell whether a repository-relative path is generated, vendored or a deliberate sample."""
    parts = rel_path.split("/")
    if any(part in EXCLUDED_DIRS for part in parts[:-1]):
        return True
    if parts[-1].lower().endswith(EXCLUDED_SUFFIXES):
        return True
    return any(rel_path.startswith(prefix) for prefix in EXCLUDED_PATHS)


TEST_DIRS = frozenset({"tests", "test", "e2e"})
"""Directory names that hold test code (plus any ``*.Tests`` project directory)."""

TEST_FILE_PREFIXES = ("test_", "conftest.py")
TEST_FILE_SUFFIXES = ("Tests.cs", ".test.ts", ".spec.ts")


def is_test(rel_path: str) -> bool:
    """Tell whether a repository-relative path is test code."""
    *dirs, name = rel_path.split("/")
    return _has_test_dir(dirs) or _is_test_file(name)


def _has_test_dir(dirs: Sequence[str]) -> bool:
    return any(part in TEST_DIRS or part.endswith(".Tests") for part in dirs)


def _is_test_file(name: str) -> bool:
    return name.startswith(TEST_FILE_PREFIXES) or name.endswith(TEST_FILE_SUFFIXES)


def is_production(rel_path: str) -> bool:
    """Tell whether a repository-relative path is production code (not test code)."""
    return rel_path.startswith(PRODUCTION_ROOTS) and not is_test(rel_path)


def iter_source_files(root: Path, paths: Iterable[Path]) -> Iterator[Path]:
    """Yield measurable source files below ``paths`` (files or directories), deduplicated."""
    seen: set[Path] = set()
    for candidate in _candidates(paths):
        if candidate not in seen and _is_measurable(candidate, root):
            seen.add(candidate)
            yield candidate


def _candidates(paths: Iterable[Path]) -> Iterator[Path]:
    for base in paths:
        yield from [base] if base.is_file() else sorted(base.rglob("*"))


def _is_measurable(path: Path, root: Path) -> bool:
    if path.suffix.lower() not in SOURCE_SUFFIXES or not path.is_file():
        return False
    return not is_excluded(relative(path, root))


def default_paths(root: Path) -> list[Path]:
    """Return the existing default source roots below ``root``."""
    return [root / name for name in DEFAULT_ROOTS if (root / name).exists()]


def build_parser(doc: str | None, *, with_paths: bool = True) -> argparse.ArgumentParser:
    """Return a CLI parser with the shared ``paths`` and ``--root`` arguments."""
    title = next(iter((doc or "").strip().splitlines()), "")
    parser = argparse.ArgumentParser(description=title)
    if with_paths:
        parser.add_argument("paths", nargs="*", type=Path, help="files or directories")
    parser.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root")
    return parser


def count_errors(findings: Iterable[Finding]) -> int:
    """Return the number of error findings."""
    return sum(1 for finding in findings if finding.severity is Severity.ERROR)


def format_findings(findings: Iterable[Finding]) -> str:
    """Return one ``path:line: severity [check] message`` line per finding."""
    return "\n".join(f"{f.path}:{f.line}: {f.severity} [{f.check}] {f.message}" for f in findings)
