"""A small, fully known StemStudio library packed as a backup archive 1.0.0 (qa, feature 013, T012).

The database is created from the committed SQL scripts in ``schema/`` (output of
``dotnet ef migrations script`` up to a migration, see ``schema/README.md``) and filled with raw
SQL inserts in the stored JSON shapes of the EF Core model, as the persistence tests do for legacy
rows. The files follow the layout allow-list of data-model 3.3 exactly, and the manifest follows
``specs/013-library-care/contracts/backup-manifest.schema.json``.

Library content (every instrument family is treated alike: six stems of equal level):

* one completed track (``htdemucs_6s``) with six 0.1 s stems and one mix of all six;
* one exported audio asset of that track (schema 008 and later);
* one voice profile with a complete ``consent.v1`` record and its sample (schema 010 and later);
* one lyrics sheet (schema 010 and later).
"""

from __future__ import annotations

import hashlib
import io
import json
import re
import sqlite3
import tempfile
import uuid
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]

from backup_zip import DEFLATED, STORED, RawEntry, build_zip

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[2]
SCHEMA_DIR = HERE / "schema"
CONSENT_TEXT = REPO_ROOT / "src" / "StemStudio.Domain" / "Voices" / "consent.v1.en.txt"

TRACK_ID = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f"
MIX_ID = "5d1e2f3a-4b5c-4d6e-8f70-8192a3b4c5d6"
ASSET_ID = "3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e"
PROFILE_ID = "7d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09"
LYRICS_ID = "9e8d7c6b-5a49-4382-a1b0-c9d8e7f6a5b4"
STEMS = ("vocals", "drums", "bass", "guitar", "piano", "other")
STEM_HZ = {
    "vocals": 440.0,
    "drums": 110.0,
    "bass": 55.0,
    "guitar": 330.0,
    "piano": 262.0,
    "other": 660.0,
}
SAMPLE_RATE = 44_100
CLIP_FRAMES = SAMPLE_RATE // 10
CREATED_AT = "2026-11-16T14:22:33Z"
ROW_TIME = "2026-11-16 12:00:00"
CONSENTED_AT = "2026-11-15T21:03:00Z"
NAMESPACE = uuid.UUID("2b6f0c1e-0d13-4c3a-9a51-013000000000")
CATEGORY_ORDER = (
    "database",
    "stems",
    "artwork",
    "pendingUploads",
    "detailStems",
    "exports",
    "scores",
    "generations",
    "recordings",
    "voiceProfiles",
)
STORED_SUFFIXES = (".flac", ".wav", ".mp3", ".jpg", ".png", ".pdf", ".webm")
MIGRATION_ID = re.compile(r"VALUES \('([0-9]{14}_[A-Za-z0-9]+)', '")
DB_ENTRY = "db/stemstudio.db"

Row = tuple[str, tuple[Any, ...]]
"""One parameterised insert."""


@dataclass(frozen=True)
class Library:
    """What goes into the fixture library.

    Attributes:
        schema: Name of the SQL script in ``schema/`` (``008``, ``010`` or ``head``).
        sample_path: Stored ``VoiceProfiles.SamplePath``; ``None`` writes the generated one.
        consent_sha256: Stored consent statement hash; ``None`` writes the real one.
        extra_migrations: Migration ids this StemStudio does not know (a newer archive).
    """

    schema: str = "head"
    sample_path: str | None = None
    consent_sha256: str | None = None
    extra_migrations: tuple[str, ...] = ()

    @property
    def has_voices(self) -> bool:
        """Voice profiles and lyrics sheets exist from the 010 schema on."""
        return self.schema != "008"


@dataclass(frozen=True)
class LibraryFile:
    """One archived file.

    Attributes:
        path: Path below the data directory (``stemstudio.db`` for the database).
        category: Backup category.
        data: File content.
        entity: ``(type, id)`` of the owning entity, if any.
    """

    path: str
    category: str
    data: bytes
    entity: tuple[str, str] | None = None

    @property
    def entry_name(self) -> str:
        """Archive entry name."""
        return DB_ENTRY if self.category == "database" else f"files/{self.path}"


@dataclass
class ArchiveParts:
    """An archive before it is written; hostile vectors edit these fields.

    Attributes:
        entries: Database and file entries in archive order.
        manifest: The manifest object.
        readme: ``README.txt`` content.
        manifest_size: Declared uncompressed size of ``manifest.json``; ``None`` = real size.
        declared_count: Entry count of the end records; ``None`` = real count.
    """

    entries: list[RawEntry]
    manifest: dict[str, Any]
    readme: bytes = b""
    manifest_size: int | None = None
    declared_count: int | None = None

    def build(self) -> bytes:
        """Writes the archive: entries, ``README.txt``, then ``manifest.json`` last."""
        manifest = RawEntry(
            b"manifest.json", manifest_bytes(self.manifest), DEFLATED, self.manifest_size
        )
        readme = RawEntry(b"README.txt", self.readme, DEFLATED)
        return build_zip([*self.entries, readme, manifest], self.declared_count)


def manifest_bytes(manifest: dict[str, Any]) -> bytes:
    """UTF-8 JSON without BOM, as the writer emits it."""
    return (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode()


def sha256(data: bytes) -> str:
    """Lower-case hex SHA-256."""
    return hashlib.sha256(data).hexdigest()


def consent_statement_sha256() -> str:
    """SHA-256 of the embedded ``consent.v1`` English text (``ConsentStatement.Sha256``)."""
    return sha256(CONSENT_TEXT.read_text(encoding="utf-8-sig").strip().encode())


def schema_sql(schema: str) -> str:
    """The committed migration script of ``schema`` without its BOM."""
    return (SCHEMA_DIR / f"schema-{schema}.sql").read_text(encoding="utf-8-sig")


def migrations(schema: str) -> list[str]:
    """Migration ids of ``schema`` in order (the rows of ``__EFMigrationsHistory``)."""
    return MIGRATION_ID.findall(schema_sql(schema))


def flac_clip(frequency_hz: float) -> tuple[bytes, bytes]:
    """A 0.1 s stereo 16-bit sine at -20 dBFS: (FLAC file bytes, PCM bytes)."""
    times = np.arange(CLIP_FRAMES) / SAMPLE_RATE
    mono = np.round(3277 * np.sin(2 * np.pi * frequency_hz * times)).astype(np.int16)
    pcm = np.stack([mono, mono], axis=1)
    buffer = io.BytesIO()
    sf.write(buffer, pcm, SAMPLE_RATE, format="FLAC", subtype="PCM_16")
    return buffer.getvalue(), pcm.tobytes()


def stem_files() -> list[LibraryFile]:
    """The six stems of the fixture track."""
    track = ("track", TRACK_ID)
    return [
        LibraryFile(
            f"tracks/{TRACK_ID}/stems/{stem}.flac", "stems", flac_clip(STEM_HZ[stem])[0], track
        )
        for stem in STEMS
    ]


def media_files(library: Library) -> list[LibraryFile]:
    """Stems, the export and (from 010) the voice sample, in category order."""
    export = LibraryFile(
        f"assets/{ASSET_ID}/audio.flac", "exports", flac_clip(523.0)[0], ("asset", ASSET_ID)
    )
    files = [*stem_files(), export]
    if library.has_voices:
        sample = flac_clip(196.0)[0]
        files.append(
            LibraryFile(
                f"voices/{PROFILE_ID}/sample.flac",
                "voiceProfiles",
                sample,
                ("voiceProfile", PROFILE_ID),
            )
        )
    return files


def _json(value: object) -> str:
    return json.dumps(value, separators=(",", ":"))


def track_rows(stems: Sequence[LibraryFile]) -> list[Row]:
    """The completed track and its mix of all six stems at equal level."""
    stored = [
        {"Name": STEMS[index], "SizeBytes": len(file.data)} for index, file in enumerate(stems)
    ]
    channels = [
        {"Mute": False, "Pan": 0.0, "Solo": False, "Stem": stem, "Volume": 1.0} for stem in STEMS
    ]
    metronome = {
        "BeatsPerBar": 4,
        "BpmOverride": None,
        "Enabled": False,
        "OffsetMs": 0,
        "Volume": 0.7,
    }
    return [
        (
            'INSERT INTO "Tracks" ("Id", "SourceType", "SourceKey", "SourceUrl", "Title", '
            '"Status", "Stage", "Progress", "WorkerJobId", "DurationSeconds", "Model", '
            '"CreatedAt", "UpdatedAt", "Stems", "Metadata") '
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (
                TRACK_ID.upper(),
                "Youtube",
                "youtube:Fx013Backup",
                "https://youtu.be/Fx013Backup",
                "Library care fixture",
                "Completed",
                "Completed",
                100,
                "13b0c0de0000400080000000000013ff",
                0.1,
                "htdemucs_6s",
                ROW_TIME,
                ROW_TIME,
                _json(stored),
                '{"Status":"None","CorrectedByUser":false}',
            ),
        ),
        (
            'INSERT INTO "Mixes" ("Id", "TrackId", "Name", "NormalizedName", "MasterVolume", '
            '"CreatedAt", "UpdatedAt", "Channels", "Loop", "Metronome") '
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (
                MIX_ID.upper(),
                TRACK_ID.upper(),
                "All six",
                "ALL SIX",
                1.0,
                ROW_TIME,
                ROW_TIME,
                _json(channels),
                None,
                _json(metronome),
            ),
        ),
    ]


def asset_rows(export: LibraryFile) -> list[Row]:
    """The exported mix of the fixture track (an ``Export`` asset, origin is not ``recording``)."""
    files = [
        {
            "Role": "audio",
            "FileName": "audio.flac",
            "ContentType": "audio/flac",
            "SizeBytes": len(export.data),
            "Sha256": sha256(export.data),
        }
    ]
    audio = {
        "Format": "Flac",
        "BitDepth": 16,
        "Mp3BitrateKbps": None,
        "DurationSeconds": 0.1,
        "PeakDbfs": -20.0,
        "AppliedGainDb": 0.0,
    }
    recipe = {
        "TrackId": TRACK_ID,
        "Mode": "Raw",
        "Channels": list(STEMS),
        "Mix": None,
        "Range": None,
        "Format": {"Format": "Flac", "BitDepth": 16, "Mp3BitrateKbps": None},
    }
    provenance: dict[str, object] = {
        "Origin": "Export",
        "TrackId": TRACK_ID,
        "TrackTitle": "Library care fixture",
        "TrackArtist": None,
        "SourceAssetIds": [],
        "Contents": "all six stems, raw",
        "Recipe": recipe,
    }
    return [
        (
            'INSERT INTO "Assets" ("Id", "Kind", "Name", "Status", "Progress", "Audio", '
            '"Provenance", "TrackId", "SizeBytes", "CreatedAt", "UpdatedAt", "Files") '
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (
                ASSET_ID.upper(),
                "Audio",
                "Library care fixture - all six",
                "Ready",
                100,
                _json(audio),
                _json(provenance),
                TRACK_ID.upper(),
                len(export.data),
                ROW_TIME,
                ROW_TIME,
                _json(files),
            ),
        )
    ]


def consent_record(library: Library) -> dict[str, str]:
    """The stored consent record of the fixture profile (010 ``VoiceConsent`` JSON)."""
    statement = library.consent_sha256 or consent_statement_sha256()
    return {
        "StatementVersion": "consent.v1",
        "StatementSha256": statement,
        "PersonName": "Alex Example",
        "ConsentedAt": CONSENTED_AT,
    }


def voice_rows(library: Library, sample: LibraryFile) -> list[Row]:
    """The voice profile with its consent record, and one lyrics sheet."""
    pcm = flac_clip(196.0)[1]
    sample_path = library.sample_path or sample.path
    return [
        (
            'INSERT INTO "VoiceProfiles" ("Id", "DisplayName", "NormalizedName", "Consent", '
            '"SampleSource", "SamplePath", "SamplePcmSha256", "SampleFileSha256", '
            '"DurationSeconds", "VoicedRatio", "CreatedAt") '
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (
                PROFILE_ID.upper(),
                "My voice",
                "MY VOICE",
                _json(consent_record(library)),
                "Uploaded",
                sample_path,
                sha256(pcm),
                sha256(sample.data),
                0.1,
                0.9,
                ROW_TIME,
            ),
        ),
        (
            'INSERT INTO "LyricsSheets" ("Id", "Title", "Language", "Text", "CreatedAt", '
            '"UpdatedAt") VALUES (?, ?, ?, ?, ?, ?)',
            (
                LYRICS_ID.upper(),
                "Fixture lyrics",
                "en",
                "Own words, line one\nline two",
                ROW_TIME,
                ROW_TIME,
            ),
        ),
    ]


def library_rows(library: Library, files: Sequence[LibraryFile]) -> list[Row]:
    """Every insert of the fixture library for the files ``media_files`` returned."""
    rows = [*track_rows(files[: len(STEMS)]), *asset_rows(files[len(STEMS)])]
    if library.has_voices:
        rows += voice_rows(library, files[len(STEMS) + 1])
    rows += [
        (
            'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES (?, ?)',
            (migration, "99.0.0"),
        )
        for migration in library.extra_migrations
    ]
    return rows


def sqlite_file(script: str, rows: Sequence[Row]) -> bytes:
    """Runs ``script`` and ``rows`` on a new database; returns the file (journal mode DELETE)."""
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "stemstudio.db"
        connection = sqlite3.connect(path)
        try:
            connection.executescript(script)
            for sql, parameters in rows:
                connection.execute(sql, parameters)
            connection.commit()
            connection.execute("PRAGMA journal_mode=DELETE")
            connection.execute("VACUUM")
        finally:
            connection.close()
        return path.read_bytes()


def library_files(library: Library) -> list[LibraryFile]:
    """The database followed by the media files, in archive order."""
    media = media_files(library)
    database = sqlite_file(schema_sql(library.schema), library_rows(library, media))
    return [LibraryFile("stemstudio.db", "database", database), *media]


def entry_method(name: str) -> int:
    """Store media, deflate everything else (backup-archive 1.0.0 section 1)."""
    return STORED if name.endswith(STORED_SUFFIXES) else DEFLATED


def manifest_entries(files: Sequence[LibraryFile]) -> list[dict[str, Any]]:
    """``entries[]`` of the manifest for ``files``."""
    entries: list[dict[str, Any]] = []
    for file in files:
        entry: dict[str, Any] = {
            "path": file.entry_name,
            "category": file.category,
            "sizeBytes": len(file.data),
            "sha256": sha256(file.data),
        }
        if file.entity is not None:
            entry["entity"] = {"type": file.entity[0], "id": file.entity[1]}
        entries.append(entry)
    return entries


def voice_proofs(library: Library, files: Sequence[LibraryFile]) -> list[dict[str, Any]]:
    """``voiceProfiles[]`` of the manifest (the consent proof repeated from the database)."""
    samples = [file for file in files if file.category == "voiceProfiles"]
    record = consent_record(library)
    return [
        {
            "id": PROFILE_ID,
            "displayName": "My voice",
            "consent": {
                "statementVersion": record["StatementVersion"],
                "statementSha256": record["StatementSha256"],
                "personName": record["PersonName"],
                "consentedAt": CONSENTED_AT,
            },
            "samplePath": sample.entry_name,
            "sampleSha256": sha256(sample.data),
        }
        for sample in samples
    ]


def library_counts(library: Library) -> dict[str, int]:
    """``counts`` of the manifest."""
    voices = 1 if library.has_voices else 0
    return {
        "tracks": 1,
        "mixes": 1,
        "assets": 1,
        "generations": 0,
        "styles": 0,
        "lyricsSheets": voices,
        "voiceProfiles": voices,
        "artists": 0,
    }


def build_manifest(name: str, library: Library, files: Sequence[LibraryFile]) -> dict[str, Any]:
    """The manifest of an archive called ``name`` holding ``files``."""
    entries = manifest_entries(files)
    history = [*migrations(library.schema), *library.extra_migrations]
    return {
        "format": "stemstudio-backup",
        "formatVersion": "1.0.0",
        "backupId": str(uuid.uuid5(NAMESPACE, name)),
        "createdAt": CREATED_AT,
        "app": {"name": "StemStudio", "version": "1.12.0+fixture", "backendApi": "1.12.0"},
        "schema": {"provider": "sqlite", "lastMigration": history[-1], "migrations": history},
        "contents": {
            "included": [c for c in CATEGORY_ORDER if c != "recordings"],
            "excluded": [{"category": "recordings", "reason": "empty"}],
        },
        "counts": library_counts(library),
        "totals": {"entries": len(entries), "bytes": sum(e["sizeBytes"] for e in entries)},
        "hashAlgorithm": "sha-256",
        "encryption": "none",
        "entries": entries,
        "voiceProfiles": voice_proofs(library, files),
        "warnings": [],
    }


README = (
    b"StemStudio library backup (format stemstudio-backup 1.0.0).\n\n"
    b"This is a test fixture of the StemStudio repository (tests/fixtures/backup).\n"
    b"To restore an archive, copy it into the backups folder and open Library care -> Backups.\n"
    b"It contains voice samples and is not encrypted; keep it on a protected drive.\n"
)

FileEdit = Callable[[list[LibraryFile]], list[LibraryFile]]
"""Changes the files before the manifest is computed."""


@dataclass(frozen=True)
class ArchiveRecipe:
    """How to make one fixture archive.

    Attributes:
        name: Archive file name.
        library: Library content.
        edit_files: Optional change of the files before the manifest is computed.
    """

    name: str
    library: Library = field(default_factory=Library)
    edit_files: FileEdit | None = None


def package(recipe: ArchiveRecipe) -> ArchiveParts:
    """Materialises the library of ``recipe`` and computes its manifest."""
    files = library_files(recipe.library)
    if recipe.edit_files is not None:
        files = recipe.edit_files(files)
    entries = [
        RawEntry(file.entry_name.encode(), file.data, entry_method(file.entry_name))
        for file in files
    ]
    return ArchiveParts(entries, build_manifest(recipe.name, recipe.library, files), README)


def replace_file(files: list[LibraryFile], path: str, data: bytes) -> list[LibraryFile]:
    """Returns ``files`` with the content of ``path`` replaced."""
    return [replace(file, data=data) if file.path == path else file for file in files]
