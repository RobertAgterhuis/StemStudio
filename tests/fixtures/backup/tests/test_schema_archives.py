import hashlib
import json
from pathlib import Path
from typing import Any

import pytest

import make_schema_archives as schemas
from backup_library import Library, migrations
from reference_validator import validate

CATALOGUE: dict[str, Any] = json.loads(schemas.CATALOGUE.read_text(encoding="utf-8"))
ITEMS = CATALOGUE["archives"]


@pytest.mark.parametrize("item", ITEMS, ids=[item["file"] for item in ITEMS])
def test_committed_archive_has_its_outcome_and_hash(item: dict[str, Any]) -> None:
    data = (schemas.ARCHIVE_DIR / item["file"]).read_bytes()
    assert validate(data).outcome == item["outcome"]
    assert hashlib.sha256(data).hexdigest() == item["sha256"]


def test_states_cover_every_backup_row_state_but_restorable() -> None:
    # restorable is valid-base.zip of the hostile corpus (the positive control).
    assert {item["state"] for item in ITEMS} == {"migration", "incompatible", "unrecognised"}


def test_older_schemas_stop_at_the_008_and_010_migrations() -> None:
    assert migrations("008")[-1] == "20261115171827_AddScoreFacts"
    assert migrations("010")[-1] == "20261115200748_AddLyricsAndVoices"
    head = migrations("head")
    assert head[: len(migrations("010"))] == migrations("010")


def test_008_library_has_no_voice_data() -> None:
    assert not Library(schema="008").has_voices
    assert Library(schema="010").has_voices


def test_generation_is_deterministic(tmp_path: Path) -> None:
    assert schemas.write_archives(tmp_path / "a") == schemas.write_archives(tmp_path / "b")


def test_cli_writes_into_a_given_folder(tmp_path: Path) -> None:
    before = schemas.CATALOGUE.read_text(encoding="utf-8")
    assert schemas.main([str(tmp_path)]) == 0
    assert schemas.CATALOGUE.read_text(encoding="utf-8") == before
    assert sorted(p.name for p in tmp_path.iterdir()) == sorted(i["file"] for i in ITEMS)
