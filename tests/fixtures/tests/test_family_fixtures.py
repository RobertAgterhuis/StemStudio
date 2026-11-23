"""Tests of the per-family handout fixtures (020 T028, SC-006): the seeding plan and the setlist songs.

The plan in ``tests/fixtures/families/manifest.json`` drives the E2E seeding helper
(``e2e/support/handout-seed.ts``). Every fake it relies on is checked against the contract texts
(LESSONS 016): the defaults per family against data-model 6.2, the fake analyzer ratings against
data-model 2.6, the families against ``HandoutFamily`` and the score profiles against 016.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

import numpy as np
import pytest
import soundfile as sf  # type: ignore[import-untyped]

FOLDER = Path(__file__).resolve().parents[1] / "families"
REPO = Path(__file__).resolve().parents[3]
SPEC = REPO / "specs" / "020-insights-handouts"
DATA_MODEL = (SPEC / "data-model.md").read_text(encoding="utf-8")
DELTA = (SPEC / "contracts" / "backend-api.delta.openapi.yaml").read_text(encoding="utf-8")
SEVEN = ["overview", "vocals", "guitar", "bass", "keys", "drums", "melodic"]


def _load() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "generate_family_fixtures", FOLDER / "generate_family_fixtures.py"
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


GENERATOR = _load()


@pytest.fixture(scope="module")
def generated(tmp_path_factory: pytest.TempPathFactory) -> tuple[Path, dict[str, Any]]:
    out = tmp_path_factory.mktemp("families")
    return out, GENERATOR.generate(out, out / "manifest.json")


def _enum(name: str) -> list[str]:
    match = re.search(rf"^    {name}:\n      type: string\n      enum: \[([^\]]+)\]", DELTA, re.M)
    assert match, name
    return [item.strip() for item in match.group(1).split(",")]


def _defaults_table() -> dict[str, list[str]]:
    """data-model 6.2: the ingredient codes marked D per family (column order of the header)."""
    lines = DATA_MODEL.split("### 6.2", 1)[1].split("\n\n", 2)[1].splitlines()
    families = [cell.strip() for cell in lines[0].strip("|").split("|")[1:]]
    defaults: dict[str, list[str]] = {family: [] for family in families}
    for row in lines[2:]:
        cells = [cell.strip() for cell in row.strip("|").split("|")]
        code = cells[0].split("`")[1]
        for family, cell in zip(families, cells[1:], strict=True):
            if cell == "D":
                defaults[family].append(code)
    return defaults


# ------------------------------------------------------------------ the plan


def test_the_committed_manifest_is_the_generated_one(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    committed = json.loads((FOLDER / "manifest.json").read_text(encoding="utf-8"))
    assert committed == generated[1]


def test_every_family_of_the_contract_has_a_plan(generated: tuple[Path, dict[str, Any]]) -> None:
    families = generated[1]["families"]
    assert list(families) == SEVEN == _enum("HandoutFamily")
    assert [plan["label"] for plan in families.values()] == [
        "Band overview", "Vocals", "Guitar", "Bass", "Keys", "Drums", "Melodic instruments",
    ]  # fmt: skip


def test_expected_ingredients_are_the_data_model_defaults(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    """The fixture song has every ingredient, so each family gets exactly its D column."""
    table = _defaults_table()
    for family, plan in generated[1]["families"].items():
        assert plan["expectedIncluded"] == table[family], family
        assert set(plan["expectedIncluded"]) <= set(_enum("IngredientCode"))
        assert {"facts", "form-chart", "practice-notes"} <= set(plan["expectedIncluded"])


def test_every_family_but_overview_gets_its_own_notation(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    families = generated[1]["families"]
    assert families["overview"]["scores"] == []
    profiles = {
        "vocals",
        "bass",
        "guitar",
        "keys",
        "piano",
        "drums",
        "strings",
        "brass",
        "woodwinds",
    }
    for family in SEVEN[1:]:
        scores = families[family]["scores"]
        assert scores, family
        assert all(score["profile"] in profiles for score in scores), family


def test_guitar_and_bass_get_a_score_and_a_tablature(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    families = generated[1]["families"]
    for family, tuning in (("guitar", "guitar-standard"), ("bass", "bass-4-standard")):
        scores = families[family]["scores"]
        assert [score.get("tablature") for score in scores] == [None, {"tuning": tuning}]
        assert {score["channel"] for score in scores} == {family}


def test_melodic_is_a_b_flat_part_of_a_fake_detail_stem(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    """data-model 6.1: B-flat picks a part ``*-bb``; the fake split of ``other`` has woodwinds."""
    melodic = generated[1]["families"]["melodic"]
    assert melodic["transposition"] == "b-flat"
    assert melodic["needsDetail"] is True
    (score,) = melodic["scores"]
    assert score["channel"] == "other.woodwinds"
    assert score["part"]["instrument"].endswith("-bb")
    assert melodic["transpositionNote"] == "Part in B-flat (sounds a major second lower)"
    assert "Part in B-flat (sounds a major second lower)" in (SPEC / "spec.md").read_text("utf-8")


def test_only_melodic_needs_the_detail_split(generated: tuple[Path, dict[str, Any]]) -> None:
    needs = [f for f, plan in generated[1]["families"].items() if plan["needsDetail"]]
    assert needs == ["melodic"]


def test_every_family_has_its_own_practice_note(generated: tuple[Path, dict[str, Any]]) -> None:
    notes = [plan["practiceNote"] for plan in generated[1]["families"].values()]
    assert len(set(notes)) == len(SEVEN)
    assert all(0 < len(note) <= 4000 and note.isascii() for note in notes)


def test_the_fake_quality_ratings_are_the_data_model_ones(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    """data-model 2.6 (the fake analyzer of the E2E stack), stem by stem."""
    fake = generated[1]["fakeQuality"]
    section = DATA_MODEL.split("### 2.6", 1)[1].split("###", 1)[0]
    flat = " ".join(section.split())
    for channel in ("vocals", "drums", "bass", "guitar", "piano", "other"):
        rating = fake["stems"][channel]["rating"]
        assert f"`{channel}` {rating}" in flat, channel
    assert fake["stems"]["vocals"]["bleedSource"] == "drums"
    assert fake["stems"]["other"]["bleedSource"] == "guitar"
    assert "flicker" in fake["stems"]["guitar"]["artefactKinds"]
    assert fake["detail"] == {"rating": "good", "rest": "fair", "restKind": "rest-share"}
    assert f'`analyzerVersion = "{fake["analyzerVersion"]}"`' in flat
    assert set(fake["stems"][c]["rating"] for c in fake["stems"]) <= set(_enum("QualityRating"))


# ------------------------------------------------------------------ the setlist and its songs


def test_the_setlist_has_three_distinct_songs(generated: tuple[Path, dict[str, Any]]) -> None:
    setlist = generated[1]["setlist"]
    entries = setlist["entries"]
    assert len(entries) == 3
    assert entries[0]["file"] == generated[1]["song"]["file"]
    assert len({entry["file"] for entry in entries}) == 3
    assert [entry["note"] for entry in entries] == ["Open the set", "Capo 2", ""]
    assert setlist["note"]


def test_the_song_is_the_014_practice_song_with_known_sections(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    song = generated[1]["song"]
    practice = json.loads((FOLDER.parent / "practice-flow" / "manifest.json").read_text("utf-8"))
    assert (song["folder"], song["file"]) == ("practice-flow", practice["song"]["file"])
    assert song["sections"] == [s["label"] for s in practice["song"]["sections"]]
    assert (song["bpm"], song["beatsPerBar"]) == (120, 4)


@pytest.mark.parametrize(("index", "bpm", "beats"), [(1, 100, 4), (2, 90, 3)])
def test_setlist_songs_have_their_tempo_and_length(
    generated: tuple[Path, dict[str, Any]], index: int, bpm: int, beats: int
) -> None:
    out, manifest = generated
    entry = manifest["setlist"]["entries"][index]
    assert (entry["bpm"], entry["beatsPerBar"], entry["folder"]) == (bpm, beats, "families")
    audio, rate = sf.read(out / entry["file"], always_2d=True)
    assert rate == manifest["sampleRate"] == 44_100
    assert audio.shape == (round(entry["seconds"] * rate), 2)
    assert 0.5 < float(np.max(np.abs(audio))) <= 0.71
    assert hashlib.sha256((out / entry["file"]).read_bytes()).hexdigest() == entry["sha256"]


def test_setlist_song_clicks_fall_on_the_beat(generated: tuple[Path, dict[str, Any]]) -> None:
    """The accent of every bar starts at its downbeat (onset within 5 ms)."""
    out, manifest = generated
    entry = manifest["setlist"]["entries"][2]
    audio, rate = sf.read(out / entry["file"], always_2d=True)
    mono = audio[:, 0]
    bar = 60 / entry["bpm"] * entry["beatsPerBar"]
    for k in range(3):
        start = round((entry["firstDownbeatSeconds"] + k * bar) * rate)
        window = np.abs(mono[start - 441 : start + 441])
        assert int(np.argmax(window > 0.2)) <= 441 + 221


def test_reruns_are_byte_identical(generated: tuple[Path, dict[str, Any]], tmp_path: Path) -> None:
    out, manifest = generated
    again = GENERATOR.generate(tmp_path, tmp_path / "manifest.json")
    assert again == manifest
    for entry in manifest["setlist"]["entries"][1:]:
        assert (tmp_path / entry["file"]).read_bytes() == (out / entry["file"]).read_bytes()


def test_main_writes_next_to_the_script_by_default_and_checks(tmp_path: Path) -> None:
    assert GENERATOR.main(["--out-dir", str(tmp_path), "--manifest", str(tmp_path / "m.json")]) == 0
    assert (tmp_path / "m.json").is_file()
    assert GENERATOR.main(["--check", "--manifest", str(FOLDER / "manifest.json")]) == 0
    (tmp_path / "stale.json").write_text("{}\n", encoding="utf-8")
    assert GENERATOR.main(["--check", "--manifest", str(tmp_path / "stale.json")]) == 1
