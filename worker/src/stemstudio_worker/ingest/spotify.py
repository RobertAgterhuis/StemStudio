"""Spotify track metadata (spotDL) and YouTube matching (flat yt-dlp search).

spotDL is used for one thing only: reading a track's public metadata (title, artist, duration).
A matching public YouTube video is then found with a flat yt-dlp search. Spotify audio is never
requested, so no DRM-protected content is accessed (constitution IV). Both libraries are
imported lazily so that the worker starts, and unit tests run, without touching them.
"""

import logging
import re
import threading
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any, Final, Protocol

from stemstudio_worker.ingest.text import clean_text
from stemstudio_worker.ingest.urls import canonical_spotify_url
from stemstudio_worker.ports import SourceUnavailableError

NO_MATCH_MESSAGE: Final = "No matching YouTube audio was found for this Spotify track."
"""Fixed job error text when a Spotify track has no YouTube match (data-model.md)."""

METADATA_FAILED_MESSAGE: Final = (
    "The Spotify track details could not be retrieved; try again later."
)
"""Job error text when spotDL cannot read the track metadata."""

SEARCH_FAILED_MESSAGE: Final = "The YouTube search for this Spotify track failed; try again later."
"""Job error text when the YouTube search itself fails (network, provider error)."""

SEARCH_CANDIDATES: Final = 5
"""Number of YouTube search results considered for a match."""

DURATION_TOLERANCE_SECONDS: Final = 10.0
DURATION_TOLERANCE_RATIO: Final = 0.1
"""A candidate matches when its duration is within max(10 s, 10 %) of the Spotify duration."""

_FLAT_SEARCH_OPTIONS: Final[dict[str, Any]] = {
    "quiet": True,
    "no_warnings": True,
    "skip_download": True,
    "extract_flat": "in_playlist",
}

_ISRC: Final = re.compile(r"[A-Z]{2}[A-Z0-9]{3}[0-9]{7}")
_LOG = logging.getLogger(__name__)
_CLIENT_LOCK = threading.Lock()
_client_ready = False


@dataclass(frozen=True, slots=True)
class SpotifyTrack:
    """Public metadata of one Spotify track.

    Attributes:
        track_id: 22-character Spotify track id.
        title: Track title (1..500 chars).
        artist: Primary artist, or None when unknown.
        duration_seconds: Duration reported by Spotify.
        album: Album name, or None (feature 005).
        isrc: ISRC in canonical form (12 characters, no hyphens), or None (feature 005).
        handle: Library object the matcher may reuse (spotDL ``Song``); excluded from equality.
    """

    track_id: str
    title: str
    artist: str | None
    duration_seconds: float
    album: str | None = None
    isrc: str | None = None
    handle: object | None = field(default=None, compare=False, repr=False)


class SpotifyMetadataClient(Protocol):
    """Looks up public Spotify track metadata."""

    def track(self, track_id: str) -> SpotifyTrack:
        """Return the metadata of ``track_id``.

        Raises:
            SourceUnavailableError: The metadata could not be retrieved.
        """
        ...


class YouTubeMatcher(Protocol):
    """Finds a public YouTube video matching a Spotify track."""

    def match(self, track: SpotifyTrack) -> str | None:
        """Return the URL of the best matching YouTube video, or None when there is none.

        Raises:
            SourceUnavailableError: The search itself failed.
        """
        ...


class SpotdlMetadataClient:
    """``SpotifyMetadataClient`` backed by spotDL's ``Song.from_url`` (metadata only)."""

    def __init__(self, song_loader: Callable[[str], Any] | None = None) -> None:
        """Create the client.

        Args:
            song_loader: Maps a canonical track URL to a spotDL ``Song``-like object with
                ``name``, ``artist``, ``duration`` and optionally ``album_name`` and ``isrc``;
                defaults to ``load_spotdl_song``.
        """
        self._load = song_loader or load_spotdl_song

    def track(self, track_id: str) -> SpotifyTrack:
        """Look up ``track_id``; library errors are logged and replaced by a readable error."""
        try:
            song = self._load(canonical_spotify_url(track_id))
        except Exception as exc:
            _LOG.warning("spotDL metadata lookup failed: %s", type(exc).__name__)
            raise SourceUnavailableError(METADATA_FAILED_MESSAGE) from None
        return _track_from_song(track_id, song)


class SpotdlYouTubeMatcher:
    """``YouTubeMatcher`` searching YouTube (flat yt-dlp search) for a spotDL ``Song``."""

    def __init__(self, search: Callable[[Any], str | None] | None = None) -> None:
        """Create the matcher.

        Args:
            search: Maps a spotDL ``Song`` to a YouTube URL or None; defaults to
                ``search_youtube_with_spotdl``.
        """
        self._search = search or search_youtube_with_spotdl

    def match(self, track: SpotifyTrack) -> str | None:
        """Search YouTube for ``track``; returns None when the track has no spotDL handle."""
        if track.handle is None:
            return None
        try:
            return self._search(track.handle)
        except Exception as exc:
            _LOG.warning("spotDL YouTube search failed: %s", type(exc).__name__)
            raise SourceUnavailableError(SEARCH_FAILED_MESSAGE) from None


def _track_from_song(track_id: str, song: Any) -> SpotifyTrack:
    """Map a spotDL ``Song``-like object; a missing title counts as a failed lookup."""
    title = clean_text(getattr(song, "name", None))
    if title is None:
        raise SourceUnavailableError(METADATA_FAILED_MESSAGE)
    return SpotifyTrack(
        track_id,
        title,
        clean_text(getattr(song, "artist", None)),
        float(getattr(song, "duration", 0) or 0),
        album=clean_text(getattr(song, "album_name", None)),
        isrc=canonical_isrc(getattr(song, "isrc", None)),
        handle=song,
    )


def canonical_isrc(value: object) -> str | None:
    """Return ``value`` as a canonical ISRC (upper case, hyphens removed), or None if invalid.

    Args:
        value: The ISRC reported by spotDL (any type).

    Returns:
        e.g. ``"USEVI9692306"``, or None.
    """
    if not isinstance(value, str):
        return None
    code = value.replace("-", "").strip().upper()
    return code if _ISRC.fullmatch(code) else None


def load_spotdl_song(url: str) -> Any:
    """Return spotDL's ``Song`` for a canonical Spotify track URL (metadata only).

    Args:
        url: ``https://open.spotify.com/track/<id>``.

    Returns:
        The spotDL ``Song`` object.
    """
    from spotdl.types.song import Song  # noqa: PLC0415 - lazy: heavy optional import

    _ensure_spotify_client()
    return Song.from_url(url)


def search_youtube_with_spotdl(song: Any) -> str | None:
    """Search YouTube for ``song`` with a flat yt-dlp search and return the best match URL.

    spotDL's own providers run a full extraction (with JavaScript challenge solving) for every
    candidate, which takes minutes; a flat search returns id, title and duration in seconds.

    Args:
        song: A spotDL ``Song`` (``name``, ``artist``, ``duration``).

    Returns:
        A YouTube video URL, or None when no candidate has a plausible duration.
    """
    from yt_dlp import YoutubeDL  # noqa: PLC0415 - lazy: heavy import

    query = f"ytsearch{SEARCH_CANDIDATES}:{song.artist} - {song.name}"
    with YoutubeDL(_FLAT_SEARCH_OPTIONS) as ydl:
        result = ydl.extract_info(query, download=False) or {}
    return pick_match(result.get("entries") or [], float(song.duration or 0))


def pick_match(entries: list[dict[str, Any]], duration_seconds: float) -> str | None:
    """Return the watch URL of the first entry whose duration is close to ``duration_seconds``.

    Args:
        entries: Flat yt-dlp search entries (``id``, ``duration``), best first.
        duration_seconds: Spotify duration; 0 means unknown, so the first entry wins.

    Returns:
        ``https://www.youtube.com/watch?v=<id>``, or None when no entry qualifies.
    """
    tolerance = max(DURATION_TOLERANCE_SECONDS, duration_seconds * DURATION_TOLERANCE_RATIO)
    for entry in entries:
        if entry.get("id") and _plausible(entry, duration_seconds, tolerance):
            return f"https://www.youtube.com/watch?v={entry['id']}"
    return None


def _plausible(entry: dict[str, Any], duration_seconds: float, tolerance: float) -> bool:
    """True when the duration is unknown (0) or the entry is within ``tolerance`` of it."""
    length = float(entry.get("duration") or 0)
    return duration_seconds <= 0 or abs(length - duration_seconds) <= tolerance


def _ensure_spotify_client() -> None:
    """Initialise spotDL's process-wide Spotify client once, with spotDL's public defaults."""
    global _client_ready  # noqa: PLW0603 - spotDL itself keeps a process-wide singleton
    from spotdl.utils.config import DEFAULT_CONFIG  # noqa: PLC0415 - lazy import
    from spotdl.utils.spotify import SpotifyClient, SpotifyError  # noqa: PLC0415

    with _CLIENT_LOCK:
        if _client_ready:
            return
        try:
            SpotifyClient.init(
                client_id=DEFAULT_CONFIG["client_id"],
                client_secret=DEFAULT_CONFIG["client_secret"],
            )
        except SpotifyError:
            _LOG.debug("spotDL Spotify client was already initialised")
        _client_ready = True
