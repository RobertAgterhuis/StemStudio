"""Checks of the hand-written lyrics fixtures in ``tests/fixtures/lyrics/`` (006:T012).

The LRC files are parsed by an independent reference reader of the rules in
``lrc/expected.json``; the LRCLIB record sets are run through a reference of the selection rule
(data-model 2.3); the fake-provider table is checked against data-model 2.1 invariants. No
third-party lyrics: every lyric text is one of the self-written placeholders.
"""

from __future__ import annotations

import itertools
import json
import re
from pathlib import Path
from typing import Any

import pytest
import yaml

LYRICS = Path(__file__).resolve().parents[1] / "lyrics"
STAMP = re.compile(r"\[(\d{2}):(\d{2})(?:\.(\d{1,3}))?\]")
TAG = re.compile(r"^\[(ti|ar|al|length|by|re|offset):(.*)\]$")
PLACEHOLDER = "Fake line one\nLa la la\nSecond fake line for the ball\nLast fake line"
MAX_WORD_MS = 3000


def _json(name: str) -> Any:
    return json.loads((LYRICS / name).read_text(encoding="utf-8"))


def _stamp_ms(match: re.Match[str]) -> int:
    fraction = (match.group(3) or "0").ljust(3, "0")
    return (int(match.group(1)) * 60 + int(match.group(2))) * 1000 + int(fraction)


def _offset(lines: list[str]) -> int:
    tags = [TAG.match(line) for line in lines]
    values = [int(t.group(2)) for t in tags if t is not None and t.group(1) == "offset"]
    return values[0] if values else 0


def read_lrc(text: str) -> tuple[list[dict[str, Any]], list[int]]:
    """Reference LRC reader: (lines sorted by start, break times)."""
    raw = text.splitlines()
    offset = _offset(raw)
    lines: list[dict[str, Any]] = []
    breaks: list[int] = []
    for line in raw:
        stamps = list(STAMP.finditer(line))
        content = STAMP.sub("", line).strip()
        starts = [_stamp_ms(stamp) - offset for stamp in stamps]
        lines += [{"startMs": start, "text": content} for start in starts if content]
        breaks += [start for start in starts if not content]
    return sorted(lines, key=lambda entry: entry["startMs"]), breaks


@pytest.mark.parametrize("name", sorted(_json("lrc/expected.json")["files"]))
def test_lrc_files_read_as_expected(name: str) -> None:
    expected = _json("lrc/expected.json")["files"][name]
    lines, breaks = read_lrc((LYRICS / "lrc" / name).read_text(encoding="utf-8"))
    assert (lines, breaks) == (expected["lines"], expected["breaksMs"])
    assert bool(lines) == expected["usable"]


def test_edge_cases_cover_the_t012_list() -> None:
    text = (LYRICS / "lrc" / "edge-cases.lrc").read_text(encoding="utf-8")
    assert "[offset:+250]" in text
    assert re.search(r"\]\[\d{2}:", text)
    assert re.search(r"[぀-ヿ一-鿿]", text)
    assert re.search(r"[֐-׿]", text)
    assert "\n\n" in text


def _ends(line: dict[str, Any]) -> list[int]:
    starts = [w["start_ms"] for w in line["words"]]
    return [*starts[1:], min(line["end_ms"], starts[-1] + MAX_WORD_MS)]


def test_lyricsfile_word_synced_has_the_f03_shape() -> None:
    doc = yaml.safe_load((LYRICS / "lyricsfile" / "word-synced.yaml").read_text(encoding="utf-8"))
    assert doc["version"] == "1.0"
    assert set(doc["metadata"]) == {
        "title",
        "artist",
        "album",
        "duration_ms",
        "instrumental",
        "language",
    }
    assert all(set(w) == {"text", "start_ms"} for line in doc["lines"] for w in line["words"])
    assert _ends(doc["lines"][2])[-1] == 19400 + MAX_WORD_MS


def test_lyricsfile_instrumental_has_no_lines() -> None:
    doc = yaml.safe_load((LYRICS / "lyricsfile" / "instrumental.yaml").read_text(encoding="utf-8"))
    assert (doc["metadata"]["instrumental"], doc["lines"]) == (True, [])


def test_lyricsfile_with_a_python_tag_is_refused_by_safe_load() -> None:
    with pytest.raises(yaml.constructor.ConstructorError):
        yaml.safe_load((LYRICS / "lyricsfile" / "unsafe-tag.yaml").read_text(encoding="utf-8"))


def _rank(record: dict[str, Any], album: str) -> tuple[bool, bool, bool, bool, int]:
    return (
        not record["hasWordSync"],
        record["syncedLyrics"] is None,
        record["plainLyrics"] is None and not record["instrumental"],
        record["albumName"] != album,
        record["id"],
    )


def select(records: list[dict[str, Any]], duration: float, album: str = "") -> str | int | None:
    """Reference of data-model 2.3 steps 3-4 over one search result."""
    close = [r for r in records if abs(r["duration"] - duration) <= 2]
    if close:
        return int(min(close, key=lambda r: _rank(r, album))["id"])
    plain = [r for r in records if r["plainLyrics"] and abs(r["duration"] - duration) <= 15]
    return f"plain:{min(plain, key=lambda r: r['id'])['id']}" if plain else None


@pytest.mark.parametrize(
    "name", ["search-hero.json", "search-instrumental.json", "search-plain-only.json"]
)
def test_search_sets_select_the_expected_record(name: str) -> None:
    data = _json(f"lrclib/{name}")
    for duration, expected in data["expected"].items():
        assert select(data["body"], float(duration)) == expected, duration


def test_ties_prefer_our_album_then_the_lowest_id() -> None:
    data = _json("lrclib/search-ties.json")
    for key, expected in data["expected"].items():
        duration, album = key.split("|")
        assert select(data["body"], float(duration), album) == expected


def test_search_hero_has_the_f02_record_counts() -> None:
    body = _json("lrclib/search-hero.json")["body"]
    at_262 = [r for r in body if r["duration"] == 262.0]
    assert (len(body), len(at_262), sum(r["hasWordSync"] for r in at_262)) == (20, 13, 12)
    assert not any(r["hasWordSync"] for r in body if r["duration"] == 285.0)


def test_get_by_duration_follows_the_f01_table() -> None:
    responses = _json("lrclib/get-by-duration.json")["responses"]
    assert {d: r["status"] for d, r in responses.items()} == {
        "262": 200, "265": 200, "258": 200, "270": 200, "285": 200, "200": 404,
    }  # fmt: skip
    assert responses["270"]["body"]["hasWordSync"] is False


def test_not_json_body_is_not_json() -> None:
    with pytest.raises(json.JSONDecodeError):
        json.loads((LYRICS / "lrclib" / "get-not-json.txt").read_text(encoding="utf-8"))


def _records() -> list[dict[str, Any]]:
    out: list[dict[str, Any]] = []
    for path in sorted((LYRICS / "lrclib").glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        bodies = data.get("body"), *[r["body"] for r in data.get("responses", {}).values()]
        out += [r for b in bodies for r in (b if isinstance(b, list) else [b]) if r and "id" in r]
    return out


def test_lrclib_records_carry_only_placeholder_lyrics() -> None:
    records = _records()
    assert len(records) > 40
    assert all(r["plainLyrics"] in (None, PLACEHOLDER) for r in records)
    assert all(r["syncedLyrics"] is None or "Fake line one" in r["syncedLyrics"] for r in records)


def _fake_words() -> list[dict[str, Any]]:
    return [w for line in _json("fake-lyrics.json")["lines"] for w in line["words"]]


def test_fake_lyrics_satisfy_the_timed_lyrics_invariants() -> None:
    fake = _json("fake-lyrics.json")
    for line in fake["lines"]:
        words = line["words"]
        assert (words[0]["startMs"], words[-1]["endMs"]) == (line["startMs"], line["endMs"])
        assert " ".join(w["text"] for w in words) == line["text"]
    words = _fake_words()
    assert all(0 < w["endMs"] - w["startMs"] <= MAX_WORD_MS for w in words)
    assert [w["startMs"] for w in words] == sorted(w["startMs"] for w in words)
    assert words[-1]["endMs"] < 20_000


def test_fake_lyrics_follow_the_worker_spread_rule_and_match_the_plain_text() -> None:
    fake = _json("fake-lyrics.json")
    lines = fake["lines"]
    step = fake["spread"]["durationMs"] // (len(lines) + 1)
    assert [line["startMs"] for line in lines] == [step * (i + 1) for i in range(len(lines))]
    gaps = [b["startMs"] - a["endMs"] for a, b in itertools.pairwise(lines)]
    assert max(gaps) <= fake["countdownGapMs"]
    assert fake["text"]["plainText"] == "\n".join(line["text"] for line in lines)
    assert set(fake["modes"]) == {"word", "line", "plain", "instrumental", "not-found", "failed"}
