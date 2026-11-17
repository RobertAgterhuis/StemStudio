import hashlib
import json
import sqlite3
from pathlib import Path
from typing import Any

import pytest

import make_hostile_archives as hostile
from backup_library import CONSENT_TEXT, PROFILE_ID, consent_statement_sha256
from backup_zip import read_central_directory, read_payload
from reference_validator import Outcome, validate

CATALOGUE: dict[str, Any] = json.loads(hostile.CATALOGUE.read_text(encoding="utf-8"))
ITEMS = CATALOGUE["archives"]
READABLE = [item for item in ITEMS if item["file"] != "h17b-entry-count.zip"]
"""Archives whose central directory agrees with the end record."""


def _committed(name: str) -> bytes:
    return (hostile.ARCHIVE_DIR / name).read_bytes()


def _manifest(data: bytes) -> dict[str, Any]:
    record = next(r for r in read_central_directory(data) if r.name == b"manifest.json")
    result: dict[str, Any] = json.loads(read_payload(data, record, 1 << 20))
    return result


@pytest.mark.parametrize("item", ITEMS, ids=[item["file"] for item in ITEMS])
def test_committed_archive_fails_at_its_step_with_its_code(item: dict[str, Any]) -> None:
    assert validate(_committed(item["file"])) == Outcome(item["outcome"], item["step"])


@pytest.mark.parametrize("item", ITEMS, ids=[item["file"] for item in ITEMS])
def test_catalogue_hash_matches_the_committed_file(item: dict[str, Any]) -> None:
    data = _committed(item["file"])
    assert (len(data), hashlib.sha256(data).hexdigest()) == (item["bytes"], item["sha256"])


def test_corpus_covers_every_research_vector() -> None:
    vectors = {item["vector"] for item in ITEMS}
    assert vectors == {"control"} | {f"H{n}" for n in range(1, 18)}
    assert len(ITEMS) >= 15  # SC-004: at least 14 vectors, plus the control


def test_catalogue_equals_the_generator_definition() -> None:
    expected = [(v.file, v.vector, v.outcome, v.step) for v in hostile.vectors()]
    assert [(i["file"], i["vector"], i["outcome"], i["step"]) for i in ITEMS] == expected


def test_regeneration_is_deterministic_and_matches_the_committed_layout(tmp_path: Path) -> None:
    first = hostile.write_corpus(tmp_path / "a")
    second = hostile.write_corpus(tmp_path / "b")
    assert first == second
    for item in READABLE:
        fresh = (tmp_path / "a" / item["file"]).read_bytes()
        names = [record.name for record in read_central_directory(fresh)]
        assert names == [record.name for record in read_central_directory(_committed(item["file"]))]
        assert validate(fresh).outcome == item["outcome"]


def test_every_archive_ends_with_the_manifest_and_has_a_readme() -> None:
    for item in READABLE:
        names = [record.name for record in read_central_directory(_committed(item["file"]))]
        assert names[-1] == b"manifest.json"
        assert b"README.txt" in names


def test_control_database_is_a_stemstudio_library_in_delete_journal_mode(tmp_path: Path) -> None:
    data = _committed("valid-base.zip")
    record = next(r for r in read_central_directory(data) if r.name == b"db/stemstudio.db")
    path = tmp_path / "db.sqlite"
    path.write_bytes(read_payload(data, record, record.size))
    connection = sqlite3.connect(path)
    try:
        assert connection.execute("PRAGMA journal_mode").fetchone() == ("delete",)
        assert connection.execute('SELECT COUNT(*) FROM "Tracks"').fetchone() == (1,)
        consent = json.loads(
            connection.execute('SELECT "Consent" FROM "VoiceProfiles"').fetchone()[0]
        )
    finally:
        connection.close()
    assert consent["StatementSha256"] == consent_statement_sha256()


def test_control_manifest_repeats_the_consent_proof() -> None:
    manifest = _manifest(_committed("valid-base.zip"))
    proof = manifest["voiceProfiles"][0]
    assert proof["id"] == PROFILE_ID
    assert proof["consent"]["statementSha256"] == consent_statement_sha256()
    assert proof["samplePath"] == f"files/voices/{PROFILE_ID}/sample.flac"
    assert manifest["contents"]["excluded"] == [{"category": "recordings", "reason": "empty"}]


def test_consent_hash_is_taken_from_the_embedded_statement() -> None:
    text = CONSENT_TEXT.read_text(encoding="utf-8-sig").strip()
    assert consent_statement_sha256() == hashlib.sha256(text.encode()).hexdigest()


def test_consent_vector_keeps_database_and_manifest_in_agreement() -> None:
    manifest = _manifest(_committed("h15-consent-hash.zip"))
    assert manifest["voiceProfiles"][0]["consent"]["statementSha256"] == "0" * 64


def test_cli_writes_the_catalogue_only_for_the_default_folder(tmp_path: Path) -> None:
    before = hostile.CATALOGUE.read_text(encoding="utf-8")
    assert hostile.main([str(tmp_path)]) == 0
    assert hostile.CATALOGUE.read_text(encoding="utf-8") == before
    assert len(list(tmp_path.glob("*.zip"))) == len(ITEMS)
