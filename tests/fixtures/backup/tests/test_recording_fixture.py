import json
from pathlib import Path

from backup_library import HERE
from reference_validator import is_allowed

FIXTURE = json.loads((HERE / "recording-asset.json").read_text(encoding="utf-8"))


def test_recording_asset_is_a_hidden_audio_asset_of_origin_recording() -> None:
    row = FIXTURE["row"]
    provenance = json.loads(row["Provenance"])
    assert (row["Kind"], provenance["Origin"]) == ("Audio", "Recording")
    assert provenance["TrackId"].upper() == row["TrackId"]


def test_recording_file_matches_the_layout_and_the_row() -> None:
    row, file = FIXTURE["row"], FIXTURE["file"]
    stored = json.loads(row["Files"])[0]
    assert is_allowed(file["path"])
    assert Path(file["path"]).name == stored["FileName"]
    assert file["path"].split("/")[1] == row["Id"]
    assert stored["SizeBytes"] == row["SizeBytes"] == file["sizeBytes"]


def test_recording_is_never_an_export_nor_part_of_a_cleanup() -> None:
    expect = FIXTURE["expect"]
    assert expect["backupCategory"] == expect["storageCategory"] == "recordings"
    assert expect["neverCountedAs"] == ["exports"]
    assert expect["neverInCleanupPlan"] is True
