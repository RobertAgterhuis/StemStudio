# Migration scripts for the backup fixtures (qa, feature 013, T012)

`schema-<name>.sql` is the output of `dotnet ef migrations script` from the empty database up to a
migration of `src/StemStudio.Persistence/Migrations`. `backup_library.py` runs a script with
SQLite and inserts the fixture rows, so the fixture generators need Python only.

| File | Up to migration | Feature schema |
|---|---|---|
| `schema-008.sql` | `20261115171827_AddScoreFacts` | 008 sheet music (SC-009) |
| `schema-010.sql` | `20261115200748_AddLyricsAndVoices` | 010 lyrics to song (SC-009) |
| `schema-head.sql` | `20261116211814_AddMixPractice` | the current schema (012) |

Regenerate (repository root; `dotnet tool restore` once) when a migration is added, then rerun both
generators and commit the archives and catalogues:

```bash
dotnet build src/StemStudio.Persistence
for m in 20261115171827_AddScoreFacts:008 20261115200748_AddLyricsAndVoices:010 <last migration>:head; do
  dotnet ef migrations script 0 "${m%%:*}" -p src/StemStudio.Persistence -s src/StemStudio.Persistence \
    --no-build -o "tests/fixtures/backup/schema/schema-${m##*:}.sql"
done
uv run --project worker python tests/fixtures/backup/make_hostile_archives.py
uv run --project worker python tests/fixtures/backup/make_schema_archives.py
```

A new migration that is not yet in `schema-head.sql` only changes the corpus' verdict from
"restorable" to "migration" for the head archives; the hostile vectors fail before the database
steps (except H13-H15, which stay valid because migration happens after the integrity check).
