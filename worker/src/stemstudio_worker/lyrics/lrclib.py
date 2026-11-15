"""LRCLIB HTTP client (research F-01/F-02, R-02) on top of the 005 online client.

Requests go through the shared 005 ``OnlineClient`` (allow-list, User-Agent, ``Retry-After``,
body cap); this client adds the LRCLIB parameters, at least 500 ms between its own requests and
lenient answer parsing: a 404, a non-JSON body or an unexpected shape all mean "not found".
Network failures and services still busy after the retries raise ``LyricsUnavailableError``.
"""

import json
from collections.abc import Mapping
from typing import Final, Protocol

from pydantic import ConfigDict, Field, ValidationError

from stemstudio_worker.metadata.http import LenientModel, OnlineRequestError, OnlineResponse
from stemstudio_worker.metadata.ratelimit import RateLimiter
from stemstudio_worker.ports import LyricsQuery, LyricsUnavailableError

LRCLIB_HOST: Final = "lrclib.net"
"""Host of the LRCLIB API."""

LRCLIB_GET_URL: Final = f"https://{LRCLIB_HOST}/api/get"
"""Exact match by names and duration (+-2 s on LRCLIB's side)."""

LRCLIB_SEARCH_URL: Final = f"https://{LRCLIB_HOST}/api/search"
"""Search by names (at most 20 records)."""

MIN_REQUEST_GAP_SECONDS: Final = 0.5
"""Least time between two LRCLIB requests (LRCLIB asks for 200-500 ms)."""


class Getter(Protocol):
    """What the client needs from the 005 ``OnlineClient``."""

    def get(self, url: str, params: Mapping[str, str] | None = None) -> OnlineResponse | None:
        """GET an allow-listed URL; None for 404."""
        ...


class LrclibRecord(LenientModel):
    """One LRCLIB record (only the fields the selection and the parsers use)."""

    model_config = ConfigDict(extra="ignore", frozen=True, populate_by_name=True)

    id: int
    album_name: str | None = Field(default=None, alias="albumName")
    duration: float = 0.0
    instrumental: bool = False
    plain_lyrics: str | None = Field(default=None, alias="plainLyrics")
    synced_lyrics: str | None = Field(default=None, alias="syncedLyrics")
    has_word_sync: bool = Field(default=False, alias="hasWordSync")
    lyricsfile: str | None = None


class LrclibClient:
    """Sends the LRCLIB ``get`` and ``search`` requests of one lookup."""

    def __init__(self, getter: Getter, limiter: RateLimiter | None = None) -> None:
        """Create the client.

        Args:
            getter: The process-wide 005 ``OnlineClient`` (a fake in tests).
            limiter: Spacing of LRCLIB requests; default ``MIN_REQUEST_GAP_SECONDS``.
        """
        self._getter = getter
        self._limiter = limiter or RateLimiter(interval=MIN_REQUEST_GAP_SECONDS)

    def get(self, query: LyricsQuery, with_album: bool) -> LrclibRecord | None:
        """``/api/get`` with title, artist, album (when ``with_album``) and the rounded duration.

        Raises:
            LyricsUnavailableError: LRCLIB could not be reached.
        """
        params = _names(query) | {"duration": str(round(query.duration_seconds))}
        if with_album and query.album:
            params["album_name"] = query.album
        return _record(self._fetch(LRCLIB_GET_URL, params))

    def search(self, query: LyricsQuery) -> list[LrclibRecord]:
        """``/api/search`` with title and artist; unreadable records are skipped.

        Raises:
            LyricsUnavailableError: LRCLIB could not be reached.
        """
        found = self._fetch(LRCLIB_SEARCH_URL, _names(query))
        items = found if isinstance(found, list) else []
        return [record for record in map(_record, items) if record is not None]

    def _fetch(self, url: str, params: dict[str, str]) -> object:
        """GET ``url`` after the pacing gap; the decoded JSON, or None when absent/not JSON."""
        self._limiter.acquire(LRCLIB_HOST)
        try:
            response = self._getter.get(url, params)
        except OnlineRequestError:
            raise LyricsUnavailableError from None
        return None if response is None else _json(response)


def _names(query: LyricsQuery) -> dict[str, str]:
    """``track_name`` and, when known, ``artist_name``."""
    names = {"track_name": query.title}
    if query.artist:
        names["artist_name"] = query.artist
    return names


def _json(response: OnlineResponse) -> object:
    """The decoded body, or None when it is not JSON."""
    try:
        return response.json()
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None


def _record(value: object) -> LrclibRecord | None:
    """Validate one record; None when it is not a readable record."""
    try:
        return LrclibRecord.model_validate(value)
    except ValidationError:
        return None
