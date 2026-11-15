"""Choice of one LRCLIB record (data-model.md 2.3, ADR-015 D1).

Records within +-2 s of our duration are ranked word sync > line sync > plain text >
instrumental; ties go to the record whose album equals ours, then to the lowest id. Without such a
record, the closest record with plain text within +-15 s is used as text only (to be aligned).
"""

from dataclasses import dataclass
from typing import Final, Literal

from stemstudio_worker.lyrics.lrclib import LrclibRecord
from stemstudio_worker.ports import LyricsQuery

MATCH_SECONDS: Final = 2.0
"""Largest duration difference of a timed match."""

PLAIN_MATCH_SECONDS: Final = 15.0
"""Largest duration difference of a plain-text fallback."""

RecordKind = Literal["word", "line", "plain", "instrumental"]
"""What a record offers (best first)."""

_RANK: Final[dict[RecordKind, int]] = {"word": 0, "line": 1, "plain": 2, "instrumental": 3}


@dataclass(frozen=True, slots=True)
class Choice:
    """The chosen record and how it is used.

    Attributes:
        record: The LRCLIB record.
        kind: ``word``/``line`` (timed), ``plain`` (text only) or ``instrumental``.
    """

    record: LrclibRecord
    kind: RecordKind


def kind_of(record: LrclibRecord) -> RecordKind | None:
    """The best thing ``record`` offers, or None when it has neither lyrics nor the flag."""
    offers: tuple[tuple[RecordKind, bool], ...] = (
        ("word", record.has_word_sync and bool(record.lyricsfile)),
        ("line", bool(record.synced_lyrics)),
        ("plain", bool(record.plain_lyrics)),
        ("instrumental", record.instrumental),
    )
    return next((kind for kind, offered in offers if offered), None)


def select_record(records: list[LrclibRecord], query: LyricsQuery) -> Choice | None:
    """Pick the record for ``query`` (rules in the module docstring), or None."""
    return _timed_choice(records, query) or _plain_choice(records, query)


def _timed_choice(records: list[LrclibRecord], query: LyricsQuery) -> Choice | None:
    """The best record within +-2 s."""
    timed = [
        Choice(record, kind)
        for record in records
        if (kind := kind_of(record)) and _delta(record, query) <= MATCH_SECONDS
    ]
    return min(
        timed,
        key=lambda c: (_RANK[c.kind], _other_album(c.record, query), c.record.id),
        default=None,
    )


def _plain_choice(records: list[LrclibRecord], query: LyricsQuery) -> Choice | None:
    """The closest record with plain text within +-15 s, as text only."""
    plain = [r for r in records if r.plain_lyrics and _delta(r, query) <= PLAIN_MATCH_SECONDS]
    best = min(plain, key=lambda r: (_delta(r, query), _other_album(r, query), r.id), default=None)
    return None if best is None else Choice(best, "plain")


def _delta(record: LrclibRecord, query: LyricsQuery) -> float:
    """Absolute duration difference in seconds."""
    return abs(record.duration - query.duration_seconds)


def _other_album(record: LrclibRecord, query: LyricsQuery) -> bool:
    """False when the record's album equals ours (case-insensitive), so it sorts first."""
    mine = (query.album or "").casefold()
    return not mine or (record.album_name or "").casefold() != mine
