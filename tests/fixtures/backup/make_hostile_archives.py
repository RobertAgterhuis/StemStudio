"""Hostile and damaged backup archives H1-H17 of research R-06 (qa, feature 013, T012, SC-004).

Run from the repository root with the worker environment (numpy, soundfile)::

    uv run --project worker python tests/fixtures/backup/make_hostile_archives.py [out_dir]

``out_dir`` defaults to ``tests/fixtures/backup/archives`` (committed, so the .NET tests need no
Python). Every archive is the valid fixture library of ``backup_library`` with exactly one
defect, so the first check of data-model 3.4 that fails is the one the vector targets. The
catalogue ``tests/fixtures/backup/hostile-manifest.json`` (written next to this script) lists per
file the vector, the expected outcome and the validation step; ``valid-base.zip`` is the positive
control (restores cleanly). Reruns are identical for the same zlib, SQLite and libFLAC.
"""

from __future__ import annotations

import argparse
import json
import sqlite3
import sys
import tempfile
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any

from backup_library import (
    HERE,
    PROFILE_ID,
    TRACK_ID,
    ArchiveParts,
    ArchiveRecipe,
    FileEdit,
    Library,
    LibraryFile,
    package,
    replace_file,
    sha256,
)
from backup_zip import DEFLATED, S_IFLNK, STORED, RawEntry

ARCHIVE_DIR = HERE / "archives"
CATALOGUE = HERE / "hostile-manifest.json"
CATALOGUE_VERSION = 1
STEM_PATH = f"tracks/{TRACK_ID}/stems"
VOCALS = f"files/{STEM_PATH}/vocals.flac"
BOMB_BYTES = 4 * 1024 * 1024
HUGE_BYTES = 1 << 50
MANIFEST_LIMIT = 256 * 1024 * 1024
ENTRY_LIMIT = 2_000_000

ArchiveEdit = Callable[[ArchiveParts], None]
"""Changes the packed archive in place (entries, manifest, declared sizes)."""


@dataclass(frozen=True)
class Vector:
    """One hostile archive.

    Attributes:
        vector: Research R-06 id (``H1`` .. ``H17``) or ``control``.
        file: Archive file name.
        outcome: Expected problem code, or ``restored`` / ``profile-dropped``.
        step: Step of data-model 3.4 that must catch it (0 for the control).
        summary: What is wrong.
        recipe: Library and file edits.
        edit: Edit of the packed archive.
    """

    vector: str
    file: str
    outcome: str
    step: int
    summary: str
    recipe: ArchiveRecipe = field(default_factory=lambda: ArchiveRecipe("unset.zip"))
    edit: ArchiveEdit | None = None

    def parts(self) -> ArchiveParts:
        """The packed archive with this vector's defect."""
        parts = package(replace(self.recipe, name=self.file))
        if self.edit is not None:
            self.edit(parts)
        return parts


def _add(name: bytes, data: bytes = b"#!/bin/sh\necho hostile\n") -> ArchiveEdit:
    """Adds an entry that the manifest does not list (a name-rule or unlisted-entry vector)."""

    def edit(parts: ArchiveParts) -> None:
        parts.entries.append(RawEntry(name, data, DEFLATED))

    return edit


def _entry_index(parts: ArchiveParts, name: str) -> int:
    return next(i for i, entry in enumerate(parts.entries) if entry.name == name.encode())


def _manifest_entry(parts: ArchiveParts, name: str) -> dict[str, Any]:
    entries: list[dict[str, Any]] = parts.manifest["entries"]
    return next(entry for entry in entries if entry["path"] == name)


def _symlink(parts: ArchiveParts) -> None:
    """H5: the vocals stem is a link; its manifest size and hash match the link target text."""
    target = b"../../../../etc/passwd"
    parts.entries[_entry_index(parts, VOCALS)] = RawEntry(
        VOCALS.encode(), target, STORED, unix_mode=S_IFLNK | 0o777
    )
    _manifest_entry(parts, VOCALS).update(sizeBytes=len(target), sha256=sha256(target))
    _fix_totals(parts)


def _duplicate(parts: ArchiveParts) -> None:
    """H6: a second central-directory record with the vocals stem's name."""
    original = parts.entries[_entry_index(parts, VOCALS)]
    parts.entries.append(RawEntry(original.name, original.data[::-1], STORED))


def _drop_piano(parts: ArchiveParts) -> None:
    """H8: the manifest lists the piano stem, the archive does not contain it."""
    parts.entries.pop(_entry_index(parts, f"files/{STEM_PATH}/piano.flac"))


def _bomb(parts: ArchiveParts) -> None:
    """H9: headers and manifest declare the real size; the deflate stream inflates to 4 MiB."""
    index = _entry_index(parts, f"files/{STEM_PATH}/other.flac")
    declared = len(parts.entries[index].data)
    parts.entries[index] = RawEntry(
        parts.entries[index].name, bytes(BOMB_BYTES), DEFLATED, declared_size=declared
    )


def _flip_byte(parts: ArchiveParts) -> None:
    """H10: one byte of the drums stem flipped; the manifest keeps the original hash."""
    index = _entry_index(parts, f"files/{STEM_PATH}/drums.flac")
    data = bytearray(parts.entries[index].data)
    data[len(data) // 2] ^= 0xFF
    parts.entries[index] = replace(parts.entries[index], data=bytes(data))


def _unlisted_layout(parts: ArchiveParts) -> None:
    """H11: ``files/tracks/<id>/run.sh`` is listed in the manifest but matches no pattern."""
    script = b"#!/bin/sh\nrm -rf /data\n"
    name = f"files/tracks/{TRACK_ID}/run.sh"
    parts.entries.append(RawEntry(name.encode(), script, DEFLATED))
    parts.manifest["entries"].append(
        {"path": name, "category": "stems", "sizeBytes": len(script), "sha256": sha256(script)}
    )
    _fix_totals(parts)


def _set(member: str, value: object) -> ArchiveEdit:
    def edit(parts: ArchiveParts) -> None:
        parts.manifest[member] = value

    return edit


def _huge_upload(parts: ArchiveParts) -> None:
    """H16: a pending upload whose headers and manifest declare 1 PiB (Zip64; data is tiny)."""
    name = "files/uploads/" + "ab" * 32 + ".wav"
    parts.entries.append(RawEntry(name.encode(), b"RIFF", DEFLATED, declared_size=HUGE_BYTES))
    parts.manifest["entries"].append(
        {
            "path": name,
            "category": "pendingUploads",
            "sizeBytes": HUGE_BYTES,
            "sha256": sha256(b"RIFF"),
        }
    )
    _fix_totals(parts)


def _huge_manifest(parts: ArchiveParts) -> None:
    """H17a: the central directory declares ``manifest.json`` as 256 MiB + 1 byte."""
    parts.manifest_size = MANIFEST_LIMIT + 1


def _entry_count(parts: ArchiveParts) -> None:
    """H17b: the (Zip64) end record declares 2,000,001 entries."""
    parts.declared_count = ENTRY_LIMIT + 1


def _fix_totals(parts: ArchiveParts) -> None:
    entries: list[dict[str, Any]] = parts.manifest["entries"]
    parts.manifest["totals"] = {
        "entries": len(entries),
        "bytes": sum(entry["sizeBytes"] for entry in entries),
    }


def _broken_index(files: list[LibraryFile]) -> list[LibraryFile]:
    """H13a: an index definition rewritten in ``sqlite_schema`` so its entries no longer match."""
    return replace_file(files, "stemstudio.db", corrupt_index(files[0].data))


def _foreign_database(files: list[LibraryFile]) -> list[LibraryFile]:
    """H13b: a valid SQLite file that is not a StemStudio database."""
    return replace_file(files, "stemstudio.db", foreign_database())


def corrupt_index(database: bytes) -> bytes:
    """Returns ``database`` with ``IX_Tracks_UpdatedAt`` redefined over ``Title``.

    The stored index entries still hold ``UpdatedAt`` values, so ``PRAGMA integrity_check``
    reports the index as inconsistent while the file stays readable.
    """
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "broken.db"
        path.write_bytes(database)
        connection = sqlite3.connect(path)
        try:
            connection.execute("PRAGMA writable_schema=ON")
            connection.execute(
                'UPDATE sqlite_schema SET sql = \'CREATE INDEX "IX_Tracks_UpdatedAt" ON '
                '"Tracks" ("Title")\' WHERE name = \'IX_Tracks_UpdatedAt\''
            )
            connection.commit()
        finally:
            connection.close()
        return path.read_bytes()


def foreign_database() -> bytes:
    """A small SQLite database with one unrelated table and no migration history."""
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "foreign.db"
        connection = sqlite3.connect(path)
        try:
            connection.execute("CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Text TEXT NOT NULL)")
            connection.execute("INSERT INTO Notes (Text) VALUES ('not a StemStudio library')")
            connection.commit()
        finally:
            connection.close()
        return path.read_bytes()


def _recipe(library: Library | None = None, edit_files: FileEdit | None = None) -> ArchiveRecipe:
    return ArchiveRecipe("unset.zip", library or Library(), edit_files)


def _name_vectors() -> list[Vector]:
    long_name = f"files/generations/{PROFILE_ID}/" + "a" * 230 + ".json"
    return [
        Vector(
            "H1",
            "h01-traversal.zip",
            "archive-unsafe",
            3,
            "entry files/../../etc/passwd",
            edit=_add(b"files/../../etc/passwd"),
        ),
        Vector(
            "H2",
            "h02a-absolute-path.zip",
            "archive-unsafe",
            3,
            "absolute entry /data/stemstudio.db",
            edit=_add(b"/data/stemstudio.db"),
        ),
        Vector(
            "H2", "h02b-drive-letter.zip", "archive-unsafe", 3, "entry C:\\x", edit=_add(b"C:\\x")
        ),
        Vector(
            "H3",
            "h03-backslash.zip",
            "archive-unsafe",
            3,
            "backslash separators files\\tracks\\<id>\\stems\\vocals.flac",
            edit=_add(VOCALS.replace("/", "\\").encode()),
        ),
        Vector(
            "H4",
            "h04a-control-character.zip",
            "archive-unsafe",
            3,
            "entry name with a control character (U+0001)",
            edit=_add(f"files/{STEM_PATH}/voc\x01als.flac".encode()),
        ),
        Vector(
            "H4",
            "h04b-long-name.zip",
            "archive-unsafe",
            3,
            f"entry name of {len(long_name)} characters (> 260)",
            edit=_add(long_name.encode(), b"{}"),
        ),
        Vector(
            "H5",
            "h05-symlink.zip",
            "archive-unsafe",
            3,
            "vocals stem stored as a Unix symlink (S_IFLNK in the external attributes)",
            edit=_symlink,
        ),
        Vector(
            "H6",
            "h06-duplicate-name.zip",
            "archive-unsafe",
            3,
            "two central-directory records named like the vocals stem",
            edit=_duplicate,
        ),
        Vector(
            "H7",
            "h07-unlisted-entry.zip",
            "archive-unsafe",
            3,
            "valid-layout entry (own cover) not listed in the manifest",
            edit=_add(f"files/tracks/{TRACK_ID}/artwork/user/cover.jpg".encode(), b"\xff\xd8"),
        ),
    ]


def _content_vectors() -> list[Vector]:
    return [
        Vector(
            "H8",
            "h08-missing-entry.zip",
            "archive-corrupt",
            3,
            "manifest lists the piano stem; the archive lacks it",
            edit=_drop_piano,
        ),
        Vector(
            "H9",
            "h09-zip-bomb.zip",
            "archive-corrupt",
            6,
            "other stem declares its real size but inflates to 4 MiB; extraction stops at "
            "declared size + 1 byte",
            edit=_bomb,
        ),
        Vector(
            "H10",
            "h10-hash-mismatch.zip",
            "archive-corrupt",
            6,
            "one byte of the drums stem flipped; manifest keeps the original SHA-256",
            edit=_flip_byte,
        ),
        Vector(
            "H11",
            "h11-layout.zip",
            "archive-unsafe",
            3,
            "listed entry files/tracks/<id>/run.sh matches no layout pattern",
            edit=_unlisted_layout,
        ),
        Vector(
            "H12",
            "h12a-manifest-schema.zip",
            "archive-invalid",
            2,
            "manifest fails the JSON Schema (hashAlgorithm md5)",
            edit=_set("hashAlgorithm", "md5"),
        ),
        Vector(
            "H12",
            "h12b-manifest-format.zip",
            "archive-invalid",
            2,
            "manifest format is not stemstudio-backup",
            edit=_set("format", "other-backup"),
        ),
    ]


def _database_vectors() -> list[Vector]:
    bad_consent = Library(consent_sha256="0" * 64)
    return [
        Vector(
            "H13",
            "h13a-integrity.zip",
            "archive-corrupt",
            7,
            "database fails PRAGMA integrity_check (index redefined over another column)",
            _recipe(edit_files=_broken_index),
        ),
        Vector(
            "H13",
            "h13b-foreign-database.zip",
            "archive-corrupt",
            7,
            "database is valid SQLite but not a StemStudio schema (no migration history)",
            _recipe(edit_files=_foreign_database),
        ),
        Vector(
            "H14",
            "h14-stored-path.zip",
            "archive-unsafe",
            8,
            "VoiceProfiles.SamplePath = '../../x' in the archived database",
            _recipe(Library(sample_path="../../x")),
        ),
        Vector(
            "H15",
            "h15-consent-hash.zip",
            "profile-dropped",
            9,
            "voice profile with a wrong consent statement SHA-256: not restored (skipped "
            "consent-invalid), the restore continues",
            _recipe(bad_consent),
        ),
        Vector(
            "H16",
            "h16-disk-space.zip",
            "insufficient-disk-space",
            5,
            "manifest and Zip64 headers declare a 1 PiB pending upload",
            edit=_huge_upload,
        ),
        Vector(
            "H17",
            "h17a-manifest-too-large.zip",
            "archive-invalid",
            2,
            "central directory declares manifest.json as 256 MiB + 1 byte",
            edit=_huge_manifest,
        ),
        Vector(
            "H17",
            "h17b-entry-count.zip",
            "archive-invalid",
            2,
            "Zip64 end record declares 2,000,001 entries",
            edit=_entry_count,
        ),
    ]


def vectors() -> list[Vector]:
    """Every hostile archive plus the valid control, in R-06 order."""
    control = Vector(
        "control", "valid-base.zip", "restored", 0, "the valid fixture library (positive control)"
    )
    return [control, *_name_vectors(), *_content_vectors(), *_database_vectors()]


def catalogue(items: Sequence[Vector], archives: dict[str, bytes]) -> dict[str, Any]:
    """The committed description of the corpus (what each archive must produce)."""
    return {
        "version": CATALOGUE_VERSION,
        "generator": "tests/fixtures/backup/make_hostile_archives.py",
        "contract": "backup-archive 1.0.0, data-model 3.4, research R-06",
        "archives": [
            {
                "file": v.file,
                "vector": v.vector,
                "outcome": v.outcome,
                "step": v.step,
                "summary": v.summary,
                "bytes": len(archives[v.file]),
                "sha256": sha256(archives[v.file]),
            }
            for v in items
        ],
    }


def write_corpus(out_dir: Path) -> dict[str, Any]:
    """Writes every archive into ``out_dir``; returns the catalogue."""
    out_dir.mkdir(parents=True, exist_ok=True)
    items = vectors()
    archives = {item.file: item.parts().build() for item in items}
    for name, data in archives.items():
        (out_dir / name).write_bytes(data)
    return catalogue(items, archives)


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entry point; writes the catalogue only for the default output folder."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("out_dir", nargs="?", type=Path, default=ARCHIVE_DIR)
    out_dir: Path = parser.parse_args(argv).out_dir
    result = write_corpus(out_dir)
    if out_dir.resolve() == ARCHIVE_DIR.resolve():
        CATALOGUE.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    sys.stdout.write(f"{len(result['archives'])} archives -> {out_dir}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
