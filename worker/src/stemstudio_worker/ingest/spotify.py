"""Spotify track metadata and YouTube matching through spotDL.

spotDL is used for two things only: reading a track's public metadata (title, artist, duration)
and searching YouTube (Music) for a matching public video. Spotify audio is never requested, so
no DRM-protected content is accessed (constitution IV). spotDL is imported lazily so that the
worker starts, and unit tests run, without touching it.
"""

import logging
import threading
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any, Final, Protocol

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

MAX_TEXT_LENGTH: Final = 500
"""Longest title/artist kept (``SourceMetadata`` limit)."""

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
        handle: Library object the matcher may reuse (spotDL ``Song``); excluded from equality.
    """

    track_id: str
    title: str
    artist: str | None
    duration_seconds: float
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
                ``name``, ``artist`` and ``duration``; defaults to ``load_spotdl_song``.
        """
        self._load = song_loader or load_spotdl_song

    def track(self, track_id: str) -> SpotifyTrack:
        """Look up ``track_id``; library errors are logged and replaced by a readable error."""
        try:
            song = self._load(canonical_spotify_url(track_id))
        except Exception as exc:
            _LOG.warning("spotDL metadata lookup failed: %s", type(exc).__name__)
            raise SourceUnavailableError(METADATA_FAILED_MESSAGE) from None
        title = _clean(getattr(song, "name", None))
        if title is None:
            raise SourceUnavailableError(METADATA_FAILED_MESSAGE)
        artist = _clean(getattr(song, "artist", None))
        duration = float(getattr(song, "duration", 0) or 0)
        return SpotifyTrack(track_id, title, artist, duration, handle=song)


class SpotdlYouTubeMatcher:
    """``YouTubeMatcher`` backed by spotDL's YouTube Music / YouTube search providers."""

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
    """Search YouTube Music, then YouTube, for ``song`` and return the first match URL.

    Args:
        song: A spotDL ``Song``.

    Returns:
        A YouTube video URL, or None when neither provider found a match.
    """
    from spotdl.providers.audio import YouTube, YouTubeMusic  # noqa: PLC0415 - lazy import

    for provider_type in (YouTubeMusic, YouTube):
        url: str | None = provider_type().search(song)
        if url:
            return url
    return None


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


def _clean(value: object) -> str | None:
    """Strip ``value`` and cut it to ``MAX_TEXT_LENGTH``; None when empty or not a string."""
    if not isinstance(value, str) or not value.strip():
        return None
    return value.strip()[:MAX_TEXT_LENGTH]
