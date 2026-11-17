"""Reference model of the restore validation order (data-model 3.4) for the fixture tests.

It is not product code: it exists so the qa tests can prove that every archive of the corpus
fails exactly at the step and with the code its catalogue promises, and that every earlier step
passes. The backend's ``ArchiveValidator``/``StagingExtractor``/``StagedDatabase`` implement the
same rules in C# (tasks.md T042-T045); when the two disagree, the contract decides.

``validate(data)`` returns an ``Outcome``: ``restored`` (the archive would restore), ``migrated``
(restores after a migration), ``profile-dropped`` (restores without an invalid voice profile) or
a problem code of backup-archive 1.0.0 section 5 with the failing step.
"""

from __future__ import annotations

import json
import re
import sqlite3
import tempfile
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import jsonschema

from backup_library import REPO_ROOT, consent_statement_sha256, migrations, sha256
from backup_zip import S_IFLNK, CentralRecord, ZipFormatError, read_central_directory, read_payload

SCHEMA = REPO_ROOT / "specs" / "013-library-care" / "contracts" / "backup-manifest.schema.json"
MAX_ENTRIES = 2_000_000
MAX_MANIFEST = 256 * 1024 * 1024
SPACE_MARGIN = 256 * 1024 * 1024
MAX_NAME = 260
FILE_TYPE_MASK = 0o170000
FIRST_PRINTABLE = 0x20
DELETE = 0x7F

_GUID = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"
_STEM = "(vocals|drums|bass|guitar|piano|other)"
_PARENT = "(vocals|drums|guitar|piano|other)"
_DETAIL = (
    "(kick|snare|toms|hihat|cymbals|lead|backing|strings|brass|woodwinds|keys|synth|"
    "acoustic-guitar|electric-guitar|percussion|rest)"
)
_NAME = r"(?!\.\.?(/|$))(?![^/]*\.\.)[A-Za-z0-9][A-Za-z0-9._-]{0,99}"
LAYOUT = tuple(
    re.compile(f"^{pattern}$")
    for pattern in (
        rf"tracks/{_GUID}/stems/{_STEM}\.flac",
        rf"tracks/{_GUID}/artwork/(auto|user)/(cover|thumb)\.jpg",
        rf"tracks/{_GUID}/detail/{_PARENT}/{_DETAIL}\.flac",
        r"uploads/[0-9a-f]{64}\.(mp3|wav|flac)",
        rf"assets/{_GUID}/{_NAME}",
        rf"generations/{_GUID}/{_NAME}(/{_NAME}){{0,2}}",
        rf"voices/{_GUID}/{_NAME}",
    )
)
"""Layout allow-list of data-model 3.3 (relative to the data directory)."""


class ArchiveProblemError(Exception):
    """A validation step failed.

    Attributes:
        code: Problem code (``archive-unsafe``, ...).
        step: Step of data-model 3.4.
    """

    def __init__(self, code: str, step: int, detail: str) -> None:
        """Creates the problem."""
        super().__init__(f"{code} (step {step}): {detail}")
        self.code = code
        self.step = step


@dataclass(frozen=True)
class Outcome:
    """Result of ``validate``.

    Attributes:
        outcome: ``restored``, ``migrated``, ``profile-dropped`` or a problem code.
        step: Failing step (9 for a dropped profile, 0 when the archive would restore).
    """

    outcome: str
    step: int = 0


@dataclass
class _Archive:
    data: bytes
    records: list[CentralRecord] = field(default_factory=list)
    manifest: dict[str, Any] = field(default_factory=dict)
    files: dict[str, bytes] = field(default_factory=dict)


def is_allowed(path: str) -> bool:
    """True when ``path`` (below the data directory) matches the layout allow-list."""
    return any(pattern.match(path) for pattern in LAYOUT)


def _fail(code: str, step: int, detail: str) -> ArchiveProblemError:
    return ArchiveProblemError(code, step, detail)


def _manifest_record(archive: _Archive) -> CentralRecord:
    found = [record for record in archive.records if record.name == b"manifest.json"]
    if not found:
        raise _fail("archive-invalid", 2, "no manifest.json")
    if found[-1].size > MAX_MANIFEST:
        raise _fail("archive-invalid", 2, "manifest.json larger than 256 MiB")
    return found[-1]


def _check_format(manifest: dict[str, Any]) -> None:
    if manifest.get("format") != "stemstudio-backup":
        raise _fail("archive-invalid", 2, "format is not stemstudio-backup")
    if not str(manifest.get("formatVersion", "")).startswith("1."):
        raise _fail("archive-incompatible", 2, "another format major version")


def _read_manifest(archive: _Archive) -> dict[str, Any]:
    record = _manifest_record(archive)
    try:
        manifest: dict[str, Any] = json.loads(read_payload(archive.data, record, record.size + 1))
    except ValueError as error:
        raise _fail("archive-invalid", 2, "manifest is not JSON") from error
    return manifest


def _check_schema(manifest: dict[str, Any]) -> None:
    validator = jsonschema.Draft202012Validator(json.loads(SCHEMA.read_text(encoding="utf-8")))
    error = next(iter(validator.iter_errors(manifest)), None)
    if error is not None:
        raise _fail("archive-invalid", 2, f"manifest fails the schema: {error.message}")


def step2_open(archive: _Archive) -> None:
    """Central directory, entry limit, manifest size, format and JSON Schema."""
    try:
        archive.records = read_central_directory(archive.data)
    except ZipFormatError as error:
        raise _fail("archive-invalid", 2, str(error)) from error
    if len(archive.records) > MAX_ENTRIES:
        raise _fail("archive-invalid", 2, "more than 2,000,000 entries")
    archive.manifest = _read_manifest(archive)
    _check_format(archive.manifest)
    _check_schema(archive.manifest)


def _name_problem(name: bytes) -> str | None:
    text = name.decode("utf-8", errors="replace")
    checks = (
        (len(text) > MAX_NAME, "longer than 260 characters"),
        (any(ord(char) < FIRST_PRINTABLE or ord(char) == DELETE for char in text), "control"),
        ("\\" in text or ":" in text, "backslash or drive letter"),
        (text.startswith("/") or "//" in text, "absolute or empty segment"),
        (any(part in {".", ".."} for part in text.split("/")), "dot segment"),
    )
    return next((reason for failed, reason in checks if failed), None)


def _entry_allowed(name: str) -> bool:
    if name in {"db/stemstudio.db", "manifest.json", "README.txt"}:
        return True
    return name.startswith("files/") and is_allowed(name.removeprefix("files/"))


def _check_record(
    record: CentralRecord, listed: dict[str, dict[str, Any]], seen: set[bytes]
) -> None:
    problem = _name_problem(record.name)
    if problem is not None:
        raise _fail("archive-unsafe", 3, f"entry name: {problem}")
    if record.name in seen:
        raise _fail("archive-unsafe", 3, "duplicate entry name")
    seen.add(record.name)
    if record.unix_mode & FILE_TYPE_MASK == S_IFLNK:
        raise _fail("archive-unsafe", 3, "link entry")
    _check_listing(record, listed)


def _check_listing(record: CentralRecord, listed: dict[str, dict[str, Any]]) -> None:
    name = record.name.decode()
    if not _entry_allowed(name) or (name not in listed and name not in _PACKAGING):
        raise _fail("archive-unsafe", 3, f"entry not allowed or not listed: {name}")
    if name in listed and listed[name]["sizeBytes"] != record.size:
        raise _fail("archive-corrupt", 3, f"size differs from the manifest: {name}")


_PACKAGING = frozenset({"manifest.json", "README.txt"})


def step3_entries(archive: _Archive) -> None:
    """Name rules, duplicates, links, listing and sizes; every listed entry present."""
    listed = {entry["path"]: entry for entry in archive.manifest["entries"]}
    seen: set[bytes] = set()
    for record in archive.records:
        _check_record(record, listed, seen)
    missing = sorted(set(listed) - {record.name.decode() for record in archive.records})
    if missing:
        raise _fail("archive-corrupt", 3, f"listed entry missing: {missing[0]}")


def step4_compatibility(archive: _Archive, known: Sequence[str]) -> None:
    """Every archive migration must be known to this StemStudio."""
    unknown = [m for m in archive.manifest["schema"]["migrations"] if m not in set(known)]
    if unknown:
        raise _fail("archive-incompatible", 4, f"unknown migrations: {unknown}")


def step5_space(archive: _Archive, free_bytes: int) -> None:
    """``totals.bytes`` + 256 MiB must fit into the library volume."""
    if archive.manifest["totals"]["bytes"] + SPACE_MARGIN > free_bytes:
        raise _fail("insufficient-disk-space", 5, "not enough free space")


def step6_extract(archive: _Archive) -> None:
    """Bounded extraction with the size and SHA-256 of every listed entry."""
    by_name = {record.name.decode(): record for record in archive.records}
    for entry in archive.manifest["entries"]:
        data = read_payload(archive.data, by_name[entry["path"]], entry["sizeBytes"] + 1)
        if len(data) != entry["sizeBytes"] or sha256(data) != entry["sha256"]:
            raise _fail("archive-corrupt", 6, f"size or hash differs: {entry['path']}")
        archive.files[entry["path"]] = data


def _query(database: bytes, sql: str) -> list[tuple[Any, ...]]:
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "staged.db"
        path.write_bytes(database)
        connection = sqlite3.connect(f"{path.as_uri()}?mode=ro", uri=True)
        try:
            return connection.execute(sql).fetchall()
        except sqlite3.DatabaseError as error:
            raise _fail("archive-corrupt", 7, f"database: {error}") from error
        finally:
            connection.close()


def step7_database(archive: _Archive) -> bool:
    """Integrity check and migration history; returns True when a migration is needed."""
    database = archive.files["db/stemstudio.db"]
    if _query(database, "PRAGMA integrity_check") != [("ok",)]:
        raise _fail("archive-corrupt", 7, "PRAGMA integrity_check failed")
    history = [
        row[0]
        for row in _query(
            database, 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"'
        )
    ]
    if history != archive.manifest["schema"]["migrations"]:
        raise _fail("archive-corrupt", 7, "migration history differs from the manifest")
    return history != migrations("head")


def _has_table(database: bytes, table: str) -> bool:
    return bool(_query(database, f"SELECT 1 FROM sqlite_schema WHERE name = '{table}'"))  # noqa: S608


def step8_stored_paths(archive: _Archive) -> None:
    """Stored relative paths match the layout and their files were extracted."""
    database = archive.files["db/stemstudio.db"]
    if not _has_table(database, "VoiceProfiles"):
        return
    for (path,) in _query(database, 'SELECT "SamplePath" FROM "VoiceProfiles"'):
        if not is_allowed(path) or f"files/{path}" not in archive.files:
            raise _fail("archive-unsafe", 8, f"stored path outside the layout: {path}")


def step9_consent(archive: _Archive) -> bool:
    """Returns True when every voice profile has a valid consent proof."""
    known = {"consent.v1": consent_statement_sha256()}
    proofs = archive.manifest["voiceProfiles"]
    return all(
        known.get(p["consent"]["statementVersion"]) == p["consent"]["statementSha256"]
        for p in proofs
    )


Step = Callable[[_Archive], None]


def validate(data: bytes, free_bytes: int = 1 << 40) -> Outcome:
    """Runs steps 2-9 of data-model 3.4 on the archive bytes (step 1 is the file name)."""
    archive = _Archive(data)
    steps: list[Step] = [
        step2_open,
        step3_entries,
        lambda a: step4_compatibility(a, migrations("head")),
        lambda a: step5_space(a, free_bytes),
        step6_extract,
    ]
    try:
        for step in steps:
            step(archive)
        migrated = step7_database(archive)
        step8_stored_paths(archive)
    except ArchiveProblemError as problem:
        return Outcome(problem.code, problem.step)
    if not step9_consent(archive):
        return Outcome("profile-dropped", 9)
    return Outcome("migrated" if migrated else "restored")
