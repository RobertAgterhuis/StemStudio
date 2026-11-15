"""MusicBrainz WS/2 client: recording search (ISRC first) and artist facts (research F-01/F-02).

An ISRC lookup names the recordings only; each is then looked up with its releases and release
groups (``/isrc`` refuses ``inc=release-groups`` with HTTP 400).

Only the song title, artist, duration and ISRC are sent (FR-010). Search queries quote the
(Lucene-escaped) names and restrict the duration to +-5 s when it is known. Responses are
validated before use; a malformed answer is an ``OnlineRequestError`` like a failed request.
MusicBrainz core data is CC0.
"""

import re
from typing import Annotated, Final

from pydantic import BaseModel, ConfigDict, Field

from stemstudio_worker.metadata.http import (
    LenientModel,
    OnlineClient,
    OnlineResponse,
    parse_answer,
)
from stemstudio_worker.metadata.matcher import (
    DURATION_WINDOW_SECONDS,
    Candidate,
    Credit,
    RecordingFound,
    Release,
    select,
    year_of,
)
from stemstudio_worker.metadata.titles import clean_title
from stemstudio_worker.ports import MetadataQuery
from stemstudio_worker.schemas import ArtistType, Mbid, ReleaseGroupInfo

MB_ROOT: Final = "https://musicbrainz.org/ws/2"
"""Root of the MusicBrainz web service."""

SEARCH_LIMIT: Final = 25
"""Search results requested per query."""

ISRC_INCLUDES: Final = "artist-credits+releases"
"""``inc`` of the ISRC lookup (``/isrc/{isrc}``).

MusicBrainz refuses ``release-groups`` here (HTTP 400) and lists the recordings without their
releases, so every recording is looked up afterwards with ``RECORDING_INCLUDES``.
"""

RECORDING_INCLUDES: Final = "artist-credits+releases+release-groups"
"""``inc`` of the recording lookup (``/recording/{mbid}``): credits, releases and their groups."""

ISRC_RECORDING_LIMIT: Final = 5
"""Recordings of one ISRC that are looked up (one request each, 1 request/s)."""

_LUCENE_SPECIAL: Final = re.compile(r'([+\-&|!(){}\[\]^"~*?:\\/])')
_ARTIST_TYPES: Final[dict[str, ArtistType]] = {
    "group": "group",
    "person": "person",
    "orchestra": "orchestra",
    "choir": "choir",
    "character": "character",
    "other": "other",
}
_COUNTRY: Final = re.compile(r"[A-Z]{2}")
_WIKIDATA_URL: Final = re.compile(r"https://www\.wikidata\.org/wiki/(Q[0-9]+)")


class _ArtistRef(LenientModel):
    id: Mbid | None = None


class _Credit(LenientModel):
    name: str = ""
    joinphrase: str = ""
    artist: _ArtistRef | None = None


class _ReleaseGroup(LenientModel):
    id: Mbid
    title: Annotated[str, Field(min_length=1)]
    primary_type: str | None = Field(default=None, alias="primary-type")
    secondary_types: list[str] = Field(default_factory=list, alias="secondary-types")


class _Release(LenientModel):
    status: str | None = None
    date: str | None = None
    release_group: _ReleaseGroup | None = Field(default=None, alias="release-group")


class _Recording(LenientModel):
    id: Mbid
    title: Annotated[str, Field(min_length=1)]
    length: Annotated[int, Field(gt=0)] | None = None
    score: int = 0
    artist_credit: list[_Credit] = Field(default_factory=list, alias="artist-credit")
    releases: list[_Release] = Field(default_factory=list)


class _Search(LenientModel):
    recordings: list[_Recording] = Field(default_factory=list)


class _ReleaseGroupDetail(LenientModel):
    id: Mbid
    title: Annotated[str, Field(min_length=1)]
    primary_type: Annotated[str, Field(max_length=50)] | None = Field(
        default=None, alias="primary-type"
    )
    first_release_date: str | None = Field(default=None, alias="first-release-date")


class _Url(LenientModel):
    resource: str


class _Relation(LenientModel):
    type: str = ""
    url: _Url | None = None


class _LifeSpan(LenientModel):
    begin: str | None = None
    end: str | None = None


class _Artist(LenientModel):
    id: Mbid
    name: Annotated[str, Field(min_length=1)]
    type: str | None = None
    country: str | None = None
    life_span: _LifeSpan = Field(default=_LifeSpan(), alias="life-span")
    relations: list[_Relation] = Field(default_factory=list)


class ArtistFacts(BaseModel):
    """Artist facts from MusicBrainz (``/artist/{mbid}?inc=url-rels``).

    Attributes:
        mbid: Artist id.
        name: Artist name.
        type: Contract artist type, or None.
        country: ISO 3166-1 alpha-2 code, or None.
        begin_year: Year the artist started (life-span begin), or None.
        end_year: Year the artist ended, or None while active.
        website: The https ``official homepage`` relation, or None.
        wikidata_id: The ``Q`` id of the ``wikidata`` relation, or None.
    """

    model_config = ConfigDict(frozen=True)

    mbid: str
    name: str
    type: ArtistType | None = None
    country: str | None = None
    begin_year: int | None = None
    end_year: int | None = None
    website: str | None = None
    wikidata_id: str | None = None


def escape_lucene(text: str) -> str:
    """Backslash-escape the Lucene query syntax characters of ``text``."""
    return _LUCENE_SPECIAL.sub(r"\\\1", text)


def search_query(title: str, artist: str, duration_seconds: float | None) -> str:
    """The recording search query: quoted title and artist, +-5 s duration window when known.

    Args:
        title: Cleaned title.
        artist: Artist name.
        duration_seconds: Audio duration, or None.

    Returns:
        e.g. ``recording:"Hero" AND artist:"Metallica" AND dur:[257000 TO 267000]``.
    """
    query = f'recording:"{escape_lucene(title)}" AND artist:"{escape_lucene(artist)}"'
    if duration_seconds is None:
        return query
    centre = round(duration_seconds * 1000)
    window = round(DURATION_WINDOW_SECONDS * 1000)
    return f"{query} AND dur:[{max(centre - window, 0)} TO {centre + window}]"


class MusicBrainz:
    """Finds recordings and reads artist facts through the allow-listed client."""

    def __init__(self, client: OnlineClient) -> None:
        """Create the client wrapper.

        Args:
            client: The process-wide online client (rate-limited, retrying).
        """
        self._client = client

    def find(self, query: MetadataQuery) -> RecordingFound | None:
        """Look ``query`` up: ISRC lookup first (404 = unknown), then the title/artist search.

        Args:
            query: The metadata job query.

        Returns:
            The chosen recording, or None when nothing qualifies (or nothing can be searched
            because neither ISRC nor artist is known).

        Raises:
            OnlineRequestError: A request failed after retries or an answer was malformed.
        """
        if query.isrc is not None:
            found = self._isrc(query)
            if found is not None:
                return found
        if query.artist is None:
            return None
        text = search_query(clean_title(query.title), query.artist, query.duration_seconds)
        params = {"query": text, "fmt": "json", "limit": str(SEARCH_LIMIT)}
        return select(self._candidates(f"{MB_ROOT}/recording", params), query, "search")

    def release_group(self, mbid: str) -> ReleaseGroupInfo | None:
        """Read release group ``mbid`` (title, primary type, first release year).

        Raises:
            OnlineRequestError: The request failed after retries or the answer was malformed.
        """
        response = self._client.get(f"{MB_ROOT}/release-group/{mbid}", {"fmt": "json"})
        if response is None:
            return None
        raw = _parse(response, _ReleaseGroupDetail)
        return ReleaseGroupInfo(
            mbid=raw.id,
            title=raw.title[:500],
            primary_type=raw.primary_type,
            first_release_year=year_of(raw.first_release_date),
        )

    def artist(self, mbid: str) -> ArtistFacts | None:
        """Read the facts of artist ``mbid`` (None when MusicBrainz does not know it).

        Raises:
            OnlineRequestError: The request failed after retries or the answer was malformed.
        """
        response = self._client.get(f"{MB_ROOT}/artist/{mbid}", {"inc": "url-rels", "fmt": "json"})
        return None if response is None else _artist_facts(_parse(response, _Artist))

    def _isrc(self, query: MetadataQuery) -> RecordingFound | None:
        """The recordings registered under the query's ISRC (exact; no title/artist checks).

        The ISRC answer only names the recordings; each (up to ``ISRC_RECORDING_LIMIT``) is
        then looked up for its releases and release groups. Unknown recordings are skipped.
        """
        params = {"inc": ISRC_INCLUDES, "fmt": "json"}
        listed = self._recordings(f"{MB_ROOT}/isrc/{query.isrc}", params)
        candidates = [self._lookup_recording(raw.id) for raw in listed[:ISRC_RECORDING_LIMIT]]
        return select([c for c in candidates if c is not None], query, "isrc")

    def _lookup_recording(self, mbid: str) -> Candidate | None:
        """Recording ``mbid`` with its releases and release groups (None when unknown)."""
        params = {"inc": RECORDING_INCLUDES, "fmt": "json"}
        response = self._client.get(f"{MB_ROOT}/recording/{mbid}", params)
        return None if response is None else _candidate(_parse(response, _Recording))

    def _recordings(self, url: str, params: dict[str, str]) -> list[_Recording]:
        """The recordings of one lookup or search (none for a 404 answer)."""
        response = self._client.get(url, params)
        return [] if response is None else _parse(response, _Search).recordings

    def _candidates(self, url: str, params: dict[str, str]) -> list[Candidate]:
        """The matcher candidates of one search (none for a 404 answer)."""
        return [_candidate(raw) for raw in self._recordings(url, params)]


def _parse[M: BaseModel](response: OnlineResponse, model: type[M]) -> M:
    """Validate a MusicBrainz JSON answer."""
    return parse_answer(response, model, "MusicBrainz")


def _candidate(raw: _Recording) -> Candidate:
    """A matcher candidate from a parsed search result."""
    releases = tuple(_release(release) for release in raw.releases if release.release_group)
    length = raw.length / 1000 if raw.length is not None else None
    return Candidate(raw.id, raw.title, length, raw.score, _credits(raw), releases)


def _credits(raw: _Recording) -> tuple[Credit, ...]:
    """The artist credit entries of a parsed search result."""
    return tuple(
        Credit(entry.name, entry.joinphrase, entry.artist.id if entry.artist else None)
        for entry in raw.artist_credit
    )


def _release(raw: _Release) -> Release:
    """A matcher release from a parsed release that has a release group."""
    group = raw.release_group
    assert group is not None  # noqa: S101 - filtered by the caller
    return Release(
        raw.status,
        raw.date,
        group.id,
        group.title,
        group.primary_type,
        tuple(group.secondary_types),
    )


def _artist_facts(raw: _Artist) -> ArtistFacts:
    """Map a parsed artist to the facts StemStudio keeps."""
    return ArtistFacts(
        mbid=raw.id,
        name=raw.name,
        type=_ARTIST_TYPES.get((raw.type or "").lower()),
        country=raw.country if raw.country and _COUNTRY.fullmatch(raw.country) else None,
        begin_year=year_of(raw.life_span.begin),
        end_year=year_of(raw.life_span.end),
        website=_relation(raw, "official homepage", re.compile(r"(https://\S+)")),
        wikidata_id=_relation(raw, "wikidata", _WIKIDATA_URL),
    )


def _relation(raw: _Artist, kind: str, pattern: re.Pattern[str]) -> str | None:
    """The first ``kind`` URL relation matching ``pattern`` (its first group), or None."""
    for relation in raw.relations:
        match = pattern.fullmatch(relation.url.resource) if relation.url else None
        if relation.type == kind and match:
            return match.group(1)
    return None
