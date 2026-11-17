import json
import sqlite3
import zipfile
from pathlib import Path

import pytest

import compare_library as compare
from make_hostile_archives import ARCHIVE_DIR

BASE = ARCHIVE_DIR / "valid-base.zip"


def _data_dir(tmp_path: Path) -> Path:
    """Unpacks the control archive into a data-directory layout."""
    root = tmp_path / "data"
    with zipfile.ZipFile(BASE) as archive:
        for info in archive.infolist():
            target = root / (
                "stemstudio.db"
                if info.filename == "db/stemstudio.db"
                else info.filename.removeprefix("files/")
            )
            if info.filename.startswith(("files/", "db/")):
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(info))
    return root


def test_archive_and_data_directory_of_the_same_library_are_equal(tmp_path: Path) -> None:
    assert compare.compare_dumps(compare.dump(BASE), compare.dump(_data_dir(tmp_path))) == []


def test_dump_lists_tables_rows_and_file_hashes() -> None:
    result = compare.dump(BASE)
    assert len(result["tables"]["Tracks"]) == 1
    assert "__EFMigrationsHistory" in result["tables"]
    assert all(len(sha) == 64 for sha in result["files"].values())
    assert any(path.startswith("voices/") for path in result["files"])


def _change(root: Path, sql: str) -> None:
    connection = sqlite3.connect(root / "stemstudio.db")
    try:
        connection.execute(sql)
        connection.commit()
    finally:
        connection.close()


def test_changed_row_file_and_missing_row_are_reported(tmp_path: Path) -> None:
    root = _data_dir(tmp_path)
    _change(root, 'UPDATE "Mixes" SET "Name" = \'renamed\'')
    _change(root, 'DELETE FROM "LyricsSheets"')
    stem = next((root / "tracks").rglob("vocals.flac"))
    stem.write_bytes(b"changed")
    problems = compare.compare_dumps(compare.dump(BASE), compare.dump(root))
    assert any(p.startswith("row differs: Mixes:") for p in problems)
    assert any(p.startswith("row missing after: LyricsSheets:") for p in problems)
    assert any(p.startswith("file differs or missing: tracks/") for p in problems)


def test_added_rows_are_reported(tmp_path: Path) -> None:
    root = _data_dir(tmp_path)
    _change(root, "INSERT INTO \"LyricsSheets\" VALUES ('X', 't', 'en', 'x', 'a', 'b')")
    problems = compare.compare_dumps(compare.dump(BASE), compare.dump(root))
    assert problems == ["row added after: LyricsSheets:X"]


def test_job_state_of_non_terminal_rows_and_explicit_columns_are_ignored(tmp_path: Path) -> None:
    root = _data_dir(tmp_path)
    _change(root, 'UPDATE "Tracks" SET "Status" = \'Separating\', "Progress" = 40')
    before = compare.dump(root)
    _change(root, 'UPDATE "Tracks" SET "Status" = \'Failed\', "Error" = \'Interrupted by restore\'')
    _change(root, 'UPDATE "Mixes" SET "UpdatedAt" = \'later\'')
    after = compare.dump(root)
    assert compare.compare_dumps(before, after) == [
        "row differs: Mixes:5D1E2F3A-4B5C-4D6E-8F70-8192A3B4C5D6"
    ]
    assert compare.compare_dumps(before, after, ["Mixes.UpdatedAt"]) == []


def test_cli_dump_and_compare(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    first, second = tmp_path / "a.json", tmp_path / "b.json"
    assert compare.main(["dump", str(BASE), "-o", str(first)]) == 0
    assert compare.main(["dump", str(_data_dir(tmp_path)), "-o", str(second)]) == 0
    assert compare.main(["compare", str(first), str(second)]) == 0
    changed = json.loads(second.read_text(encoding="utf-8"))
    changed["files"]["extra/file"] = "0" * 64
    second.write_text(json.dumps(changed), encoding="utf-8")
    assert compare.main(["compare", str(first), str(second)]) == 1
    assert "1 difference(s)" in capsys.readouterr().out
