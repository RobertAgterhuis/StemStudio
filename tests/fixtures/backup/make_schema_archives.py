"""Archives at older and newer schemas and unrecognised files (qa, feature 013, T012, SC-009).

Run from the repository root with the worker environment::

    uv run --project worker python tests/fixtures/backup/make_schema_archives.py [out_dir]

Writes into ``tests/fixtures/backup/archives`` by default and describes each file in
``tests/fixtures/backup/schema-manifest.json`` with the compatibility verdict the Backups list
must show (``backup-row-<fileName>`` ``data-state``, ui-test-ids 1.12.0) and the restore outcome:

* ``schema-008.zip``, ``schema-010.zip``: valid archives whose database stops at the 008
  (``AddScoreFacts``) and 010 (``AddLyricsAndVoices``) migrations -> ``migration``, restore
  migrates in staging (research R-08);
* ``schema-newer.zip``: one migration this StemStudio does not know -> ``incompatible``;
* ``format-2.zip``: ``formatVersion`` 2.0.0 -> ``incompatible`` (CTO decision 2026-11-17: the
  reader reads ``format``/``formatVersion`` before the full schema check);
* ``not-a-backup.zip`` (a ZIP without manifest) and ``not-a-zip.zip`` -> ``unrecognised``.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from backup_library import HERE, ArchiveParts, ArchiveRecipe, Library, package, sha256
from backup_zip import DEFLATED, RawEntry, build_zip

ARCHIVE_DIR = HERE / "archives"
CATALOGUE = HERE / "schema-manifest.json"
CATALOGUE_VERSION = 1
NEWER_MIGRATION = "20991231235959_FromANewerStemStudio"


@dataclass(frozen=True)
class StateArchive:
    """One archive with a known compatibility verdict.

    Attributes:
        file: Archive file name.
        state: Expected ``data-state`` of its row.
        outcome: Expected restore outcome (``restored``, ``migrated`` or a problem code).
        summary: What it is.
        make: Produces the archive bytes.
    """

    file: str
    state: str
    outcome: str
    summary: str
    make: Callable[[str], bytes]


def _library(library: Library) -> Callable[[str], bytes]:
    return lambda name: package(ArchiveRecipe(name, library)).build()


def _format_two(name: str) -> bytes:
    parts: ArchiveParts = package(ArchiveRecipe(name))
    parts.manifest["formatVersion"] = "2.0.0"
    return parts.build()


def _no_manifest(_name: str) -> bytes:
    return build_zip([RawEntry(b"notes/readme.txt", b"Holiday photos, not a backup.\n", DEFLATED)])


def _not_a_zip(_name: str) -> bytes:
    return b"This file has a .zip name but is plain text.\n" * 4


def state_archives() -> list[StateArchive]:
    """Every archive of this generator."""
    return [
        StateArchive(
            "schema-008.zip",
            "migration",
            "migrated",
            "database at 20261115171827_AddScoreFacts (008): track, mix, export",
            _library(Library(schema="008")),
        ),
        StateArchive(
            "schema-010.zip",
            "migration",
            "migrated",
            "database at 20261115200748_AddLyricsAndVoices (010): track, mix, export, "
            "voice profile with consent, lyrics sheet",
            _library(Library(schema="010")),
        ),
        StateArchive(
            "schema-newer.zip",
            "incompatible",
            "archive-incompatible",
            f"migration history ends with the unknown {NEWER_MIGRATION}",
            _library(Library(extra_migrations=(NEWER_MIGRATION,))),
        ),
        StateArchive(
            "format-2.zip",
            "incompatible",
            "archive-incompatible",
            "manifest formatVersion 2.0.0 (another format major)",
            _format_two,
        ),
        StateArchive(
            "not-a-backup.zip",
            "unrecognised",
            "archive-invalid",
            "a ZIP without manifest.json",
            _no_manifest,
        ),
        StateArchive(
            "not-a-zip.zip", "unrecognised", "archive-invalid", "plain text named .zip", _not_a_zip
        ),
    ]


def catalogue(items: Sequence[StateArchive], archives: dict[str, bytes]) -> dict[str, Any]:
    """The committed description of the archives."""
    return {
        "version": CATALOGUE_VERSION,
        "generator": "tests/fixtures/backup/make_schema_archives.py",
        "contract": "backup-archive 1.0.0, ui-test-ids 1.12.0 backup-row-<fileName> data-state",
        "archives": [
            {
                "file": item.file,
                "state": item.state,
                "outcome": item.outcome,
                "summary": item.summary,
                "bytes": len(archives[item.file]),
                "sha256": sha256(archives[item.file]),
            }
            for item in items
        ],
    }


def write_archives(out_dir: Path) -> dict[str, Any]:
    """Writes every archive into ``out_dir``; returns the catalogue."""
    out_dir.mkdir(parents=True, exist_ok=True)
    items = state_archives()
    archives = {item.file: item.make(item.file) for item in items}
    for name, data in archives.items():
        (out_dir / name).write_bytes(data)
    return catalogue(items, archives)


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entry point; writes the catalogue only for the default output folder."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("out_dir", nargs="?", type=Path, default=ARCHIVE_DIR)
    out_dir: Path = parser.parse_args(argv).out_dir
    result = write_archives(out_dir)
    if out_dir.resolve() == ARCHIVE_DIR.resolve():
        CATALOGUE.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    sys.stdout.write(f"{len(result['archives'])} archives -> {out_dir}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
