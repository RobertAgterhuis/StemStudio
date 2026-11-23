"""Tests of the chord spelling vector generator (016 Phase 0 T004); run with pytest from the repo root."""

import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_SPEC = importlib.util.spec_from_file_location("gen", HERE / "generate_chord_spelling_vectors.py")
assert _SPEC is not None and _SPEC.loader is not None
gen = importlib.util.module_from_spec(_SPEC)
sys.modules["gen"] = gen
_SPEC.loader.exec_module(gen)


def test_file_is_fresh_and_deterministic() -> None:
    assert gen.build() == gen.build() == (HERE / "chord-spelling-vectors.json").read_text(encoding="utf-8")


def test_sizes_match_data_model() -> None:
    data = json.loads(gen.build())
    assert data["version"] == "1.0.0"
    assert len(data["cases"]) >= 200
    assert len(data["capo"]) >= 12 * 14 * 13
    assert len({f["name"] for f in data["capoFixtures"]}) == 4


def test_hand_checked_symbols() -> None:
    c_major = (0, "major")
    assert gen.symbol({"root": 9, "quality": "min7", "bass": 7}, c_major, 0, 0) == "Am7/G"
    assert gen.symbol({"root": 1, "quality": "maj", "bass": None}, (1, "major"), 0, 0) == "Db"
    assert gen.symbol({"root": 6, "quality": "maj", "bass": None}, (6, "major"), 0, 0) == "Gb"
    assert gen.symbol({"root": 3, "quality": "maj", "bass": None}, (3, "major"), 0, 3) == "C"
    assert gen.symbol({"root": None, "quality": None, "bass": None}, c_major, 5, 0) == "N.C."
    assert gen.symbol({"root": 10, "quality": "maj", "bass": None}, c_major, 0, 0) == "A#"


def test_hand_checked_keys() -> None:
    assert gen.key_display(0, "major", 2, 0.9) == "D major (original C major, +2)"
    assert gen.key_display(9, "minor", 0, 0.3) == "A minor (uncertain)"
    assert gen.key_name((0 + gen.WRITTEN["alto-sax-eb"]) % 12, "major") == "A major"
    assert gen.key_name(3, "minor") == "Eb minor"


def test_capo_three_after_transpose_three_keeps_the_shapes() -> None:
    advice = gen.advise([(0, "maj", 1000), (7, "maj", 1000), (9, "min", 1000), (5, "maj", 1000)], (0, "major"), 3)
    assert advice["shortcut"] == {"capo": 3, "reason": "original-shapes"}
    # F is a barre shape, so capo 1 (D shapes, Bm barre) ties at 0.75 and wins by the lower fret (R-14 tie rule).
    assert [(s["capo"], s["shapeKey"], s["openShare"]) for s in advice["suggestions"][:2]] == [(1, "D major", 0.75), (3, "C major", 0.75)]
