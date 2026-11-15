"""Checks of the hand-maintained 009/010 fixtures: own lyrics, ethics cases, fake modes."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

import pytest

ROOT = Path(__file__).resolve().parents[1]
LYRICS = ROOT / "lyrics"
GENERATIVE = ROOT / "generative"
STAMP = re.compile(r"^(\[\d{2}:\d{2}(\.\d{1,3})?\])+")
MARKER_LINE = re.compile(r"^\s*\[[^\]]*\]\s*$")
FAKE_MODES = {"ready", "no-gpu", "oom", "oom-twice", "busy", "slow", "unvoiced-sample"}
OWN_LYRICS = ("sample-en.txt", "sample-nl.txt", "sample.lrc")


def _json(path: Path) -> dict[str, Any]:
    data: dict[str, Any] = json.loads(path.read_text(encoding="utf-8"))
    return data


def _words(text: str) -> int:
    lines = [line for line in text.split("\n") if not MARKER_LINE.match(line)]
    return len(" ".join(lines).split())


def _strip_stamps(lrc: str) -> str:
    return "\n".join(STAMP.sub("", line).rstrip() for line in lrc.split("\n")).strip()


@pytest.fixture(scope="module")
def expected() -> dict[str, Any]:
    return _json(LYRICS / "own-lyrics-expected.json")


@pytest.fixture(scope="module")
def ethics() -> dict[str, Any]:
    return _json(GENERATIVE / "ethics-cases.json")


def test_english_sample_has_the_recorded_words_and_density(expected: dict[str, Any]) -> None:
    facts = expected["sample-en.txt"]
    text = (LYRICS / "sample-en.txt").read_text(encoding="utf-8")
    assert _words(text) == facts["words"]
    assert facts["wordsPerSecondAt120s"] == pytest.approx(facts["words"] / 120, abs=1e-3)
    assert facts["densityWarningBelowSeconds"] == pytest.approx(facts["words"] / 4.0)


def test_english_sample_repeats_its_chorus_block(expected: dict[str, Any]) -> None:
    blocks = (LYRICS / "sample-en.txt").read_text(encoding="utf-8").strip().split("\n\n")
    facts = expected["sample-en.txt"]
    assert len(blocks) == facts["blocks"] == len(facts["proposedMarkers"])
    first, second = (blocks[i - 1] for i in facts["repeatedBlocks"])
    assert first == second
    assert len(set(blocks)) == facts["blocks"] - 1


def test_dutch_sample_has_its_markers(expected: dict[str, Any]) -> None:
    text = (LYRICS / "sample-nl.txt").read_text(encoding="utf-8")
    markers = [line.strip("[] ") for line in text.split("\n") if MARKER_LINE.match(line)]
    assert markers == expected["sample-nl.txt"]["markers"]


def test_lrc_sample_normalises_to_the_expected_text(expected: dict[str, Any]) -> None:
    lrc = (LYRICS / "sample.lrc").read_text(encoding="utf-8")
    assert _strip_stamps(lrc) == expected["sample.lrc"]["normalisedText"]


@pytest.mark.parametrize("name", OWN_LYRICS)
def test_own_lyrics_fit_the_limit_and_use_lf(name: str, expected: dict[str, Any]) -> None:
    raw = (LYRICS / name).read_bytes()
    assert b"\r" not in raw
    assert len(raw.decode("utf-8")) <= expected["maxCharacters"]


def test_spec_imitation_cases_quote_the_spec_phrases(ethics: dict[str, Any]) -> None:
    spec = [c["text"].lower() for c in ethics["imitationRefused"] if c["source"] == "spec"]
    for phrase in ("in the style of", "sounds like", "voice of"):
        assert any(phrase in text for text in spec), phrase


def test_every_refused_case_names_a_guarded_field(ethics: dict[str, Any]) -> None:
    cases = ethics["imitationRefused"] + ethics["songTextsRefused"]
    fields = {"name", "description", "subGenre", "freeCaption", "delivery"}
    assert all(c["field"] in fields and c["source"] in {"spec", "qa-proposal"} for c in cases)


def test_accepted_genre_names_contain_no_forbidden_phrase(ethics: dict[str, Any]) -> None:
    for name in ethics["genreNamesAccepted"]:
        assert not any(phrase in name.lower() for phrase in ethics["presetForbiddenPhrases"])


def test_library_artist_case_uses_the_artwork_fixture_tag(ethics: dict[str, Any]) -> None:
    artist = ethics["libraryArtist"]
    tags = _json(ROOT / "artwork-manifest.json")["fixtures"][artist["fixture"]]["tags"]
    assert tags["artist"] == artist["artist"]
    assert artist["artist"] in artist["refusedDescription"]


def test_consent_text_matches_the_010_contract_example(ethics: dict[str, Any]) -> None:
    contract = ROOT.parents[1] / "specs" / "010-lyrics-to-song" / "contracts"
    text = (contract / "backend-api.openapi.yaml").read_text(encoding="utf-8")
    assert ethics["consent"]["textEn"] in text
    assert ethics["consent"]["textNl"] in text
    assert ethics["consent"]["statementVersion"] == "consent.v1"


def test_fake_generator_table_covers_every_mode() -> None:
    table = _json(GENERATIVE / "fake-generator.json")
    assert set(table["modes"]) == FAKE_MODES
    pattern = table["modes"]["busy"]["restyle"]["messagePattern"]
    assert re.match(pattern, "GPU busy: needs 13.6 GB, 4.2 GB free")
