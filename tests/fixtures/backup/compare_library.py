"""Row dump and file-hash comparison of two StemStudio libraries (qa, feature 013, T012, SC-001).

A library is read from a backup archive (``*.zip``: ``db/stemstudio.db`` and ``files/**``) or from
a copy of the API data directory (``stemstudio.db`` and the allow-listed folders ``tracks``,
``assets``, ``generations``, ``voices``, ``uploads``, e.g. ``docker compose cp api:/data ./x``)::

    python -I tests/fixtures/backup/compare_library.py dump <archive.zip|data_dir> -o before.json
    python -I tests/fixtures/backup/compare_library.py compare before.json after.json

The dump holds every row of every table (sorted by all columns, so the order of the file does
not matter) and the SHA-256 of every library file. ``compare`` exits 0 when both are equal and 1
otherwise, printing the differences. Job-state columns of rows that were not terminal in the
first dump are ignored (SC-001: a restore marks them failed "Interrupted by restore"); more
columns can be ignored with ``--ignore Table.Column``. Only the standard library is used.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sqlite3
import sys
import tempfile
import zipfile
from collections.abc import Iterable, Sequence
from pathlib import Path
from typing import Any

DUMP_VERSION = 1
LIBRARY_FOLDERS = ("tracks", "assets", "generations", "voices", "uploads")
TERMINAL = frozenset({"Completed", "Ready", "Failed", "Cancelled"})
JOB_COLUMNS = (
    "Status",
    "Stage",
    "Progress",
    "Error",
    "WorkerJobId",
    "GeneratorJobId",
    "UpdatedAt",
    "CompletedAt",
)
MAX_REPORTED = 50

Row = dict[str, Any]
Dump = dict[str, Any]


def _cell(value: object) -> object:
    return value.hex() if isinstance(value, bytes) else value


def table_rows(connection: sqlite3.Connection, table: str) -> list[Row]:
    """Every row of ``table`` as a column -> value mapping, in a stable order."""
    cursor = connection.execute(f'SELECT * FROM "{table}"')  # noqa: S608 - name from sqlite_schema
    names = [column[0] for column in cursor.description]
    rows = [
        {name: _cell(value) for name, value in zip(names, row, strict=True)}
        for row in cursor.fetchall()
    ]
    return sorted(rows, key=lambda row: json.dumps(row, sort_keys=True, default=str))


def database_rows(database: Path) -> dict[str, list[Row]]:
    """All user tables of the SQLite file at ``database`` (opened read-only)."""
    connection = sqlite3.connect(f"{database.as_uri()}?mode=ro", uri=True)
    try:
        tables = [
            name
            for (name,) in connection.execute(
                "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' "
                "ORDER BY name"
            )
        ]
        return {table: table_rows(connection, table) for table in tables}
    finally:
        connection.close()


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def archive_dump(archive: Path) -> Dump:
    """Rows and file hashes of a backup archive."""
    with zipfile.ZipFile(archive) as source, tempfile.TemporaryDirectory() as folder:
        database = Path(folder) / "stemstudio.db"
        database.write_bytes(source.read("db/stemstudio.db"))
        files = {
            info.filename.removeprefix("files/"): _sha256(source.read(info))
            for info in source.infolist()
            if info.filename.startswith("files/") and not info.is_dir()
        }
        return {
            "version": DUMP_VERSION,
            "tables": database_rows(database),
            "files": dict(sorted(files.items())),
        }


def _library_files(data_dir: Path) -> Iterable[Path]:
    for folder in LIBRARY_FOLDERS:
        yield from (path for path in (data_dir / folder).rglob("*") if path.is_file())


def directory_dump(data_dir: Path) -> Dump:
    """Rows and file hashes of a copy of the API data directory."""
    files = {
        path.relative_to(data_dir).as_posix(): _sha256(path.read_bytes())
        for path in _library_files(data_dir)
    }
    return {
        "version": DUMP_VERSION,
        "tables": database_rows(data_dir / "stemstudio.db"),
        "files": dict(sorted(files.items())),
    }


def dump(source: Path) -> Dump:
    """Dump of an archive (``*.zip``) or a data directory."""
    return archive_dump(source) if source.suffix == ".zip" else directory_dump(source)


def _without(row: Row, columns: Iterable[str]) -> Row:
    skipped = set(columns)
    return {name: value for name, value in row.items() if name not in skipped}


def _ignored(table: str, row: Row, ignore: frozenset[str]) -> list[str]:
    explicit = [column for column in row if f"{table}.{column}" in ignore]
    status = row.get("Status")
    job = list(JOB_COLUMNS) if isinstance(status, str) and status not in TERMINAL else []
    return explicit + job


def _key(table: str, row: Row) -> str:
    return f"{table}:{row.get('Id', row.get('MigrationId', json.dumps(row, default=str)))}"


def _compare_table(
    table: str, rows: tuple[list[Row], list[Row]], ignore: frozenset[str]
) -> list[str]:
    before = {_key(table, row): row for row in rows[0]}
    after = {_key(table, row): row for row in rows[1]}
    problems = [f"row missing after: {key}" for key in before if key not in after]
    problems += [f"row added after: {key}" for key in after if key not in before]
    for key in before.keys() & after.keys():
        skipped = _ignored(table, before[key], ignore)
        if _without(before[key], skipped) != _without(after[key], skipped):
            problems.append(f"row differs: {key}")
    return problems


def compare_dumps(before: Dump, after: Dump, ignore: Iterable[str] = ()) -> list[str]:
    """Every difference between two dumps (empty when the libraries are identical)."""
    skip = frozenset(ignore)
    tables = sorted(set(before["tables"]) | set(after["tables"]))
    problems = [
        problem
        for table in tables
        for problem in _compare_table(
            table, (before["tables"].get(table, []), after["tables"].get(table, [])), skip
        )
    ]
    files = sorted(set(before["files"]) | set(after["files"]))
    problems += [
        f"file differs or missing: {path}"
        for path in files
        if before["files"].get(path) != after["files"].get(path)
    ]
    return problems


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    dump_command = commands.add_parser("dump", help="write the dump of an archive or data dir")
    dump_command.add_argument("source", type=Path)
    dump_command.add_argument("-o", "--output", type=Path, required=True)
    compare_command = commands.add_parser("compare", help="compare two dumps")
    compare_command.add_argument("before", type=Path)
    compare_command.add_argument("after", type=Path)
    compare_command.add_argument("--ignore", action="append", default=[], metavar="TABLE.COLUMN")
    return parser


def _read(path: Path) -> Dump:
    result: Dump = json.loads(path.read_text(encoding="utf-8"))
    return result


def _run_compare(arguments: argparse.Namespace) -> int:
    problems = compare_dumps(_read(arguments.before), _read(arguments.after), arguments.ignore)
    for problem in problems[:MAX_REPORTED]:
        sys.stdout.write(problem + "\n")
    sys.stdout.write(f"{len(problems)} difference(s)\n")
    return 0 if not problems else 1


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entry point."""
    arguments = _parser().parse_args(argv)
    if arguments.command == "dump":
        arguments.output.write_text(
            json.dumps(dump(arguments.source), indent=1) + "\n", encoding="utf-8"
        )
        return 0
    return _run_compare(arguments)


if __name__ == "__main__":
    raise SystemExit(main())
