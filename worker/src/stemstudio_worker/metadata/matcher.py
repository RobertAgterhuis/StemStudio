"""Choosing the recording among MusicBrainz candidates: precision over recall (ADR-014 D4).

A search candidate qualifies when its normalised title equals the (cleaned, normalised) query
title, its artist credit (or first credited artist) is at least 90 % similar to the query
artist (rapidfuzz ratio over normalised names) and, when the duration is known, its length is
within +-5 s. ISRC hits are exact and skip these checks. Among the remaining candidates the
closest duration wins; ties prefer the candidate whose best release is an official studio album
with the earliest date. No candidate means "not found": nothing is guessed.
"""

import math
from collections.abc import Iterable
from dataclasses import dataclass
from typing import Final, Literal

from rapidfuzz import fuzz

from stemstudio_worker.metadata.titles import clean_title, normalize_for_compare
from stemstudio_worker.ports import MetadataQuery
from stemstudio_worker.schemas import RecordingMatch, ReleaseGroupInfo

ARTIST_SIMILARITY: Final = 0.9
"""Lowest accepted artist similarity (0..1)."""

DURATION_WINDOW_SECONDS: Final = 5.0
"""Largest accepted difference between the audio duration and the recording length."""

MatchedBy = Literal["isrc", "search"]
"""How the recording was found."""

_NO_DATE: Final = "9999"
_MIN_YEAR: Final = 1000
_MAX_YEAR: Final = 2999
_MAX_TITLE: Final = 500


@dataclass(frozen=True, slots=True)
class Credit:
    """One entry of a recording's artist credit.

    Attributes:
        name: Credited name.
        joinphrase: Text joining it to the next entry (``" & "``, ``" feat. "``).
        artist_mbid: MusicBrainz id of the artist, or None.
    """

    name: str
    joinphrase: str
    artist_mbid: str | None


@dataclass(frozen=True, slots=True)
class Release:
    """A release of a recording, reduced to what the release group choice needs.

    Attributes:
        status: ``Official``, ``Promotion``, ``Bootleg``, ... or None.
        date: Release date ``YYYY[-MM[-DD]]`` or None.
        group_mbid: Release group id.
        group_title: Release group title.
        primary_type: ``Album``, ``Single``, ``EP``, ... or None.
        secondary_types: ``Live``, ``Compilation``, ... (empty for studio albums).
    """

    status: str | None
    date: str | None
    group_mbid: str
    group_title: str
    primary_type: str | None
    secondary_types: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class Candidate:
    """A recording returned by a MusicBrainz search.

    Attributes:
        mbid: Recording id.
        title: Recording title.
        length_seconds: Length, or None when unknown.
        score: Search score 0..100.
        credits: Artist credit entries, in order.
        releases: Releases the recording appears on.
    """

    mbid: str
    title: str
    length_seconds: float | None
    score: int
    credits: tuple[Credit, ...]
    releases: tuple[Release, ...]

    @property
    def credit_name(self) -> str:
        """The full artist credit, e.g. ``"Queen & David Bowie"``."""
        return "".join(credit.name + credit.joinphrase for credit in self.credits)


@dataclass(frozen=True, slots=True)
class RecordingFound:
    """The chosen recording.

    Attributes:
        recording: The contract record of the recording.
        release_group: Its preferred release group (album), or None.
        artist_mbid: Id of the first credited artist, or None.
    """

    recording: RecordingMatch
    release_group: ReleaseGroupInfo | None
    artist_mbid: str | None


def qualifies(candidate: Candidate, title: str, artist: str, duration: float | None) -> bool:
    """Check a search candidate against the query (equal title, similar artist, duration).

    Args:
        candidate: The search result.
        title: Cleaned query title.
        artist: Query artist.
        duration: Audio duration in seconds, or None when unknown.

    Returns:
        True when the candidate may be used.
    """
    same_title = normalize_for_compare(candidate.title) == normalize_for_compare(title)
    return same_title and _similar_artist(candidate, artist) and _in_window(candidate, duration)


def select(
    candidates: Iterable[Candidate], query: MetadataQuery, matched_by: MatchedBy
) -> RecordingFound | None:
    """Pick the recording for ``query`` among ``candidates``.

    Args:
        candidates: Search (or ISRC) results.
        query: What was looked up.
        matched_by: ``isrc`` (exact hits, no checks) or ``search``.

    Returns:
        The chosen recording, or None when no candidate qualifies.
    """
    pool = [c for c in candidates if matched_by == "isrc" or _qualifies_for(c, query)]
    if not pool:
        return None
    best = min(pool, key=lambda candidate: _preference(candidate, query.duration_seconds))
    return RecordingFound(_recording(best, matched_by), best_release_group(best), _artist(best))


def best_release_group(candidate: Candidate) -> ReleaseGroupInfo | None:
    """The release group of the candidate's preferred release (official studio album, earliest)."""
    if not candidate.releases:
        return None
    release = min(candidate.releases, key=_release_key)
    return ReleaseGroupInfo(
        mbid=release.group_mbid,
        title=release.group_title[:_MAX_TITLE],
        primary_type=release.primary_type,
        first_release_year=year_of(release.date),
    )


def _qualifies_for(candidate: Candidate, query: MetadataQuery) -> bool:
    """``qualifies`` with the query's cleaned title; a query without artist never qualifies."""
    artist = query.artist
    return artist is not None and qualifies(
        candidate, clean_title(query.title), artist, query.duration_seconds
    )


def _similar_artist(candidate: Candidate, artist: str) -> bool:
    """True when the full credit or the first credited name is >= 90 % similar to ``artist``."""
    if not candidate.credits:
        return False
    wanted = normalize_for_compare(artist)
    names = (candidate.credit_name, candidate.credits[0].name)
    return (
        max(fuzz.ratio(wanted, normalize_for_compare(name)) for name in names)
        >= ARTIST_SIMILARITY * 100
    )


def _in_window(candidate: Candidate, duration: float | None) -> bool:
    """True when the duration is unknown or the length is known and within +-5 s."""
    if duration is None:
        return True
    length = candidate.length_seconds
    return length is not None and abs(length - duration) <= DURATION_WINDOW_SECONDS


def _preference(candidate: Candidate, duration: float | None) -> tuple[float, tuple[object, ...]]:
    """Sort key: duration distance first, then the quality of the best release."""
    return _distance(candidate.length_seconds, duration), _best_release_key(candidate)


def _distance(length: float | None, duration: float | None) -> float:
    """Distance to the audio duration; 0 when the duration is unknown, inf without a length."""
    if duration is None:
        return 0.0
    return math.inf if length is None else abs(length - duration)


def _best_release_key(candidate: Candidate) -> tuple[object, ...]:
    """The release key of the candidate's best release; candidates without releases rank last."""
    keys = [_release_key(release) for release in candidate.releases]
    return min(keys) if keys else (True, True, True, _NO_DATE)


def _release_key(release: Release) -> tuple[object, ...]:
    """Official before others, albums before others, studio before live/compilation, then date."""
    return (
        release.status != "Official",
        release.primary_type != "Album",
        bool(release.secondary_types),
        release.date or _NO_DATE,
    )


def year_of(date: str | None) -> int | None:
    """The year of ``YYYY[-MM[-DD]]`` when it is in 1000..2999."""
    head = (date or "")[:4]
    if len(head) != 4 or not head.isdigit():  # noqa: PLR2004 - four year digits
        return None
    year = int(head)
    return year if _MIN_YEAR <= year <= _MAX_YEAR else None


def _recording(candidate: Candidate, matched_by: MatchedBy) -> RecordingMatch:
    """The contract record of a candidate."""
    return RecordingMatch(
        mbid=candidate.mbid,
        title=candidate.title[:_MAX_TITLE],
        length_seconds=candidate.length_seconds,
        score=min(max(candidate.score, 0), 100),
        matched_by=matched_by,
    )


def _artist(candidate: Candidate) -> str | None:
    """Id of the first credited artist."""
    return candidate.credits[0].artist_mbid if candidate.credits else None
