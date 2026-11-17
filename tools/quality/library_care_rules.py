"""Library care rules (feature 013, tasks.md T009 (a) and (b); plan.md dependency direction).

Production files below ``src/`` (C# and Razor; comments and string literals are ignored)::

    (a) In src/StemStudio.Api, files outside LibraryCare/ (and outside the composition root
        ApiComposition.cs) reference only StemStudio.Api.LibraryCare.Gate and names below it.
        Fully qualified (StemStudio.Api.LibraryCare.Backup), partially qualified
        (Api.LibraryCare.Restore) and namespace-relative names (LibraryCare.Storage, which every
        StemStudio.Api.* file resolves to the API folder) count; StemStudio.Contracts.LibraryCare
        and identifiers such as LibraryCareLimits do not.
    (b) No file calls the bulk ZIP helpers that extract or pack whole trees without per-entry
        checks: ExtractToDirectory, ExtractToFile, CreateFromDirectory (and their Async
        variants) as member calls, and no ``using static`` of ZipFile / ZipFileExtensions.
        Archives are read and written entry by entry (ArchiveReader/ArchiveWriter, research R-06).

The worker rule of T009 (c) (``storage`` may import ``jobs`` but not ``separation``, ``detail``
or ``api``) is part of ``dependency_check.PYTHON_ALLOWED``.

Usage::

    python tools/quality/library_care_rules.py [--root DIR]

Exit code 0 = clean, 1 = a rule is broken.
"""

import re
import sys
from collections.abc import Iterator, Sequence
from pathlib import Path

from common import Finding, Severity, build_parser, report_findings
from secret_rules import Source, production_sources

CHECK = "library-care"
"""Check name of every finding of this module."""

API_DIR = "src/StemStudio.Api/"
LIBRARY_CARE_DIR = "src/StemStudio.Api/LibraryCare/"
COMPOSITION_ROOT = "src/StemStudio.Api/ApiComposition.cs"
GATE = "Gate"
"""The only ``StemStudio.Api.LibraryCare`` sub-namespace other API folders may reference."""

BANNED_ZIP_APIS = ("ExtractToDirectory", "ExtractToFile", "CreateFromDirectory")
"""Bulk ``System.IO.Compression`` helpers (each also with the ``Async`` suffix)."""

_TAIL = r"(?:\.(?P<sub>[A-Za-z_]\w*)(?:\.[A-Za-z_]\w*)*)"
_QUALIFIED = re.compile(
    rf"(?<![\w.:])(?:global::)?(?P<name>(?:StemStudio\.)?Api\.LibraryCare\b{_TAIL}?)"
)
_RELATIVE = re.compile(rf"(?<![\w.:])(?P<name>LibraryCare{_TAIL})")
_ZIP_CALL = re.compile(rf"\.\s*(?P<api>(?:{'|'.join(BANNED_ZIP_APIS)})(?:Async)?)\s*\(")
_ZIP_STATIC = re.compile(
    r"^[ \t]*(?:global[ \t]+)?using[ \t]+static[ \t]+(?:global::)?System\.IO\.Compression\."
    r"(?P<api>ZipFile(?:Extensions)?)[ \t]*;",
    re.MULTILINE,
)
_ZIP_MESSAGE = (
    "banned API {api}: read and write archive entries one by one (ArchiveReader, research R-06)"
)
_LAYER_MESSAGE = (
    "forbidden dependency on {name}: outside Api/LibraryCare only "
    "StemStudio.Api.LibraryCare.Gate may be referenced"
)


def run(root: Path) -> list[Finding]:
    """Check every production C#/Razor file below ``root / "src"``."""
    findings: list[Finding] = []
    for source in production_sources(root):
        findings.extend(check_zip_apis(source))
        if in_layer_scope(source.path):
            findings.extend(check_layering(source))
    return findings


def in_layer_scope(path: str) -> bool:
    """An API file outside ``LibraryCare/`` and the composition root."""
    inside = path.startswith(LIBRARY_CARE_DIR) or path == COMPOSITION_ROOT
    return path.startswith(API_DIR) and not inside


# ------------------------------------------------------------------------- (a) layering


def check_layering(source: Source) -> list[Finding]:
    """References to ``StemStudio.Api.LibraryCare`` other than ``Gate``, in source order."""
    hits = sorted(_layer_hits(source.code))
    return [_finding(source, offset, _LAYER_MESSAGE.format(name=name)) for offset, name in hits]


def _layer_hits(code: str) -> Iterator[tuple[int, str]]:
    for pattern in (_QUALIFIED, _RELATIVE):
        for match in pattern.finditer(code):
            if match.group("sub") != GATE:
                yield match.start("name"), match.group("name")


# ------------------------------------------------------------------------- (b) banned ZIP APIs


def check_zip_apis(source: Source) -> list[Finding]:
    """Bulk ZIP helper calls and ``using static`` of the ZIP helper classes, in source order."""
    calls = [(m.start(), m.group("api")) for m in _ZIP_CALL.finditer(source.code)]
    statics = [
        (m.start(), f"using static {m.group('api')}") for m in _ZIP_STATIC.finditer(source.code)
    ]
    hits = sorted(calls + statics)
    return [_finding(source, offset, _ZIP_MESSAGE.format(api=api)) for offset, api in hits]


def _finding(source: Source, offset: int, message: str) -> Finding:
    return Finding(CHECK, Severity.ERROR, source.path, source.line(offset), message)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    root: Path = build_parser(__doc__, with_paths=False).parse_args(argv).root
    return report_findings("library_care_rules", run(root))


if __name__ == "__main__":
    sys.exit(main())
