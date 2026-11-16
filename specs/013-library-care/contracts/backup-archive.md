# Contract: StemStudio backup archive (format 1.0.0)

Binding for the **backend** agent (writer and reader) and the **qa** agent (fixtures, hostile
corpus). Manifest schema: [backup-manifest.schema.json](backup-manifest.schema.json) (JSON Schema
2020-12). Rationale: [research.md](../research.md) R-04, R-06; [ADR-022](../../../docs/decisions/ADR-022-library-backup-and-storage-care.md).

Versioning: **MAJOR** = a reader of the previous major cannot restore it (layout or semantics
change); **MINOR** = additive (new optional manifest members, new categories that older readers may
ignore because the database scrub keeps the archive self-consistent); **PATCH** = wording.
Readers accept `formatVersion` 1.x.y and ignore unknown manifest members.

## 1. Container

- One ZIP file (PKWARE APPNOTE 6.3.x), Zip64 records where sizes, offsets or the entry count need
  them (written automatically by `System.IO.Compression.ZipArchive` on a seekable stream).
- File name written by StemStudio: `stemstudio-backup-<yyyyMMdd-HHmmss>.zip` (UTC time of the
  snapshot; on a clash `-2`, `-3`, ...). Readers recognise an archive by its manifest, not its
  name; any name matching `^[A-Za-z0-9][A-Za-z0-9._-]{0,119}\.zip$` is accepted.
- Methods: `0` (store) for `.flac`, `.wav`, `.mp3`, `.jpg`, `.png`, `.pdf`, `.webm`; `8` (deflate)
  for everything else. No encryption, no multi-disk, no link entries, no comments required.
- Entry names: UTF-8 (general-purpose flag bit 11), `/` separators, relative, unique (ordinal).
- While writing, the file is `<name>.zip.partial` in the same folder; it is renamed when complete
  and checked (FR-005, FR-008).

## 2. Entries

| Entry | Required | Content |
|---|---|---|
| `db/stemstudio.db` | yes | SQLite database snapshot (online backup API), scrubbed for excluded categories, `PRAGMA integrity_check` = ok, journal mode `DELETE` (no `-wal`/`-shm` in the archive) |
| `files/<data-relative path>` | per category | library files; the path after `files/` is exactly the path below the API data directory and matches the layout allow-list ([data-model.md](../data-model.md) 3.3) |
| `README.txt` | yes | human-readable explanation (what this is, how to restore, "contains voice samples; not encrypted") |
| `manifest.json` | yes, **last entry** | section 3 |

No other entries are allowed. Directory records (`.../`) may exist and are ignored.

## 3. Manifest

UTF-8 JSON without BOM, camelCase, validated by the schema. Members (see the schema for limits):

| Member | Meaning |
|---|---|
| `format` | `"stemstudio-backup"` |
| `formatVersion` | `"1.0.0"` |
| `backupId` | random UUID of this backup |
| `createdAt` | UTC time of the database snapshot |
| `app` | `{ name: "StemStudio", version, backendApi: "1.12.0" }` |
| `schema` | `{ provider: "sqlite", lastMigration, migrations[] }` from `__EFMigrationsHistory` |
| `contents` | `{ included: category[], excluded: [{ category, reason: option\|empty }] }` |
| `counts` | rows per entity type in the archived database (after the scrub) |
| `totals` | `{ entries, bytes }` of `entries[]` (uncompressed) |
| `hashAlgorithm` | `"sha-256"` |
| `encryption` | `"none"` |
| `entries[]` | `{ path, category, sizeBytes, sha256, entity?: { type, id } }` for every entry except `manifest.json` and `README.txt`, in archive order |
| `voiceProfiles[]` | consent proof per profile: `{ id, displayName, consent: { statementVersion, statementSha256, personName, consentedAt }, samplePath, sampleSha256 }` |
| `warnings[]` | `{ code, message, entity? }`: `missing-file`, `changed-after-snapshot`, `voice-profile-without-consent`, `interrupted-entity` |

Categories: `database`, `stems`, `artwork`, `pendingUploads` (always), `detailStems`, `exports`,
`scores`, `generations`, `recordings`, `voiceProfiles` (optional). `recordings` = play-along takes of
feature 015 (assets with provenance origin `recording`, originals); it is defined in 1.0.0 so 015
needs no format change, and is `excluded` with reason `empty` until recordings exist. Latency
calibrations of 015 are database rows (`database`). Every category absent from `included` is in
`excluded`.

Example (shortened):

```json
{
  "format": "stemstudio-backup",
  "formatVersion": "1.0.0",
  "backupId": "5b0f6a3e-2c1d-4e8f-9a7b-1c2d3e4f5a6b",
  "createdAt": "2026-11-16T14:22:33Z",
  "app": { "name": "StemStudio", "version": "1.12.0+5ae61cf", "backendApi": "1.12.0" },
  "schema": { "provider": "sqlite", "lastMigration": "20261115200748_AddLyricsAndVoices",
              "migrations": ["20261114215330_Initial", "...", "20261115200748_AddLyricsAndVoices"] },
  "contents": { "included": ["database", "stems", "artwork", "pendingUploads", "exports", "scores", "generations", "voiceProfiles"],
                "excluded": [ { "category": "detailStems", "reason": "option" } ] },
  "counts": { "tracks": 3, "mixes": 2, "assets": 2, "generations": 1, "styles": 1, "lyricsSheets": 1, "voiceProfiles": 1, "artists": 2 },
  "totals": { "entries": 25, "bytes": 412339200 },
  "hashAlgorithm": "sha-256",
  "encryption": "none",
  "entries": [
    { "path": "db/stemstudio.db", "category": "database", "sizeBytes": 1310720, "sha256": "9f2c...e1" },
    { "path": "files/tracks/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/stems/vocals.flac", "category": "stems",
      "sizeBytes": 28311552, "sha256": "4a1d...07", "entity": { "type": "track", "id": "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f" } }
  ],
  "voiceProfiles": [
    { "id": "7d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09", "displayName": "My voice",
      "consent": { "statementVersion": "consent.v1", "statementSha256": "c0ff...ee", "personName": "Alex", "consentedAt": "2026-11-15T21:03:00Z" },
      "samplePath": "files/voices/7d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09/sample.flac", "sampleSha256": "b16b...00" }
  ],
  "warnings": []
}
```

## 4. Writer rules

1. Snapshot the database (online backup API), scrub excluded categories and incomplete consent
   records, set `journal_mode=DELETE`, run `PRAGMA integrity_check`.
2. Write `db/stemstudio.db`, then files in category order (database, stems, artwork,
   pendingUploads, detailStems, exports, scores, generations, recordings, voiceProfiles), each hashed while
   copied; a file that changes size while copied is re-read once, then recorded with a
   `changed-after-snapshot` warning if it still differs.
3. Write `README.txt`, then `manifest.json`; close; reopen; compare central directory with the
   manifest (names, sizes, count); rename `.partial` -> `.zip`.
4. Never write: anything outside the allow-list, model caches, worker or generator data,
   `library-care/`, the backups folder, configuration, environment values, keys or tokens.

## 5. Reader rules

The validation order of [data-model.md](../data-model.md) 3.4 is binding. Problem codes (backend
API): `archive-invalid` (not a ZIP, no or invalid manifest, limits), `archive-incompatible` (format
major, newer migrations), `archive-unsafe` (name, link, duplicate, unlisted entry, unsafe stored
path), `archive-corrupt` (hash, size, missing entry, integrity check), `insufficient-disk-space`.
A voice profile whose consent proof is not valid is dropped with a report entry; it never fails
the whole restore.

## 6. Compatibility promise

- Every StemStudio from 013 on restores every 1.x archive whose migrations it knows.
- Embedded consent statement texts are never removed (ADR-022 D7), so old consent proofs stay
  verifiable.
- A future change of the data layout (a new path pattern) is a MINOR version when old readers can
  ignore it safely, otherwise MAJOR.
