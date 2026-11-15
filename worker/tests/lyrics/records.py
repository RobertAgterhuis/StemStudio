"""LRCLIB response shapes of research F-01/F-02 with self-written text (no third-party lyrics).

The ids, durations and flags follow the recorded records; every text field is replaced by the
fictional "Fake line one / La la la" song.
"""

import json
from collections.abc import Mapping
from dataclasses import dataclass, field
from typing import Any

from stemstudio_worker.metadata.http import OnlineResponse

PLAIN = "Fake line one\nLa la la"
SYNCED = "[ti:Fake Song]\n[00:10.95]Fake line one\n[00:14.00]La la la\n"
LYRICSFILE = """version: '1.0'
metadata: {title: Fake Song, duration_ms: 262000, instrumental: false, language: en}
lines:
  - text: Fake line one
    start_ms: 10950
    end_ms: 13200
    words:
      - {text: Fake, start_ms: 10950}
      - {text: line, start_ms: 11400}
      - {text: one, start_ms: 11900}
  - text: La la la
    start_ms: 14000
    end_ms: 16000
    words:
      - {text: La, start_ms: 14000}
      - {text: la, start_ms: 14500}
      - {text: la, start_ms: 15000}
"""
WORDS = ("Fake line one", "La la la")
"""Lines of the fixture song: none of them may appear in log output (FR-010)."""


def record(record_id: int, duration: float, **changes: Any) -> dict[str, Any]:
    """One LRCLIB record (camelCase JSON) with lyrics in all three formats unless changed."""
    body: dict[str, Any] = {
        "id": record_id,
        "trackName": "Fake Song",
        "artistName": "The Fixture Band",
        "albumName": "Synthetic Sessions",
        "duration": duration,
        "instrumental": False,
        "plainLyrics": PLAIN,
        "syncedLyrics": SYNCED,
        "hasWordSync": True,
        "lyricsfile": LYRICSFILE,
    }
    body.update(changes)
    return body


def line_record(record_id: int, duration: float, **changes: Any) -> dict[str, Any]:
    """A record with LRC line timing only."""
    return record(record_id, duration, hasWordSync=False, lyricsfile=None, **changes)


def plain_record(record_id: int, duration: float, **changes: Any) -> dict[str, Any]:
    """A record with plain text only."""
    return line_record(record_id, duration, syncedLyrics=None, **changes)


def instrumental_record(record_id: int, duration: float) -> dict[str, Any]:
    """An instrumental record (no lyrics; F-01 "Monkeys Spinning Monkeys")."""
    return plain_record(record_id, duration, instrumental=True, plainLyrics=None)


# F-02 search for the 262 s song: 13 records at 262.0 (12 with word sync), 7 live at 285.0.
SEARCH_262 = [
    *(record(546388 + i, 262.0, albumName=f"Album {i}") for i in range(12)),
    line_record(900001, 262.0, albumName="Single"),
    *(line_record(3780737 + i, 285.0, albumName="Live") for i in range(7)),
]
SEARCH_INSTRUMENTAL = [
    *(instrumental_record(3879894 + i, 125.0 + i) for i in range(6)),
    instrumental_record(4000000, 173.0),
]


def answer(body: object, url: str = "https://lrclib.net/api/get") -> OnlineResponse:
    """A 200 JSON response of the 005 online client."""
    return OnlineResponse(url, json.dumps(body).encode(), "application/json")


Answer = OnlineResponse | None | Exception


@dataclass
class FakeGetter:
    """Stands in for the 005 ``OnlineClient``: records calls and replays scripted answers."""

    answers: list[Answer] = field(default_factory=list)
    calls: list[tuple[str, dict[str, str]]] = field(default_factory=list)

    def get(self, url: str, params: Mapping[str, str] | None = None) -> OnlineResponse | None:
        self.calls.append((url, dict(params or {})))
        reply = self.answers.pop(0) if self.answers else None
        if isinstance(reply, Exception):
            raise reply
        return reply


@dataclass
class FakeClock:
    """Monotonic clock that advances only when the fake sleeper sleeps."""

    now: float = 50.0
    sleeps: list[float] = field(default_factory=list)

    def __call__(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds
