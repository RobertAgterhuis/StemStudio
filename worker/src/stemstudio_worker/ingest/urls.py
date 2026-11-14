"""Validation of user-supplied YouTube/Spotify URLs and derivation of source keys.

Only allow-listed hosts are accepted; playlists, albums, artists, episodes and shows are rejected
with an actionable message. The accepted and rejected forms are fixed by
``specs/001-stemstudio-mvp/contracts/source-url-vectors.json``. Parsing never touches the network.
"""

import re
from dataclasses import dataclass
from typing import Final
from urllib.parse import SplitResult, parse_qs, urlsplit

from stemstudio_worker.ports import InvalidSourceError
from stemstudio_worker.schemas import SourceType

MAX_URL_LENGTH: Final = 2048
"""Longest URL accepted (same limit as ``CreateJobRequest.url``)."""

YOUTUBE_HOSTS: Final = frozenset(
    {"youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com"}
)
"""Hosts serving ``/watch?v=<id>`` and ``/shorts/<id>``."""

YOUTU_BE_HOST: Final = "youtu.be"
"""Short-link host serving ``/<id>``."""

SPOTIFY_HOST: Final = "open.spotify.com"
"""Host serving ``/track/<id>`` and ``/intl-<xx>/track/<id>``."""

_YOUTUBE_ID: Final = re.compile(r"[A-Za-z0-9_-]{11}")
_SPOTIFY_ID: Final = re.compile(r"[A-Za-z0-9]{22}")
_SPOTIFY_LOCALE: Final = re.compile(r"intl-[A-Za-z]{2}(?:[-_][A-Za-z]{2})?")

_MSG_EMPTY: Final = "Enter a YouTube video URL or a Spotify track URL."
_MSG_MALFORMED: Final = "This is not a valid URL; paste the full link starting with https://."
_MSG_SCHEME: Final = "Only http and https links are supported."
_MSG_HOST: Final = "Only YouTube and Spotify links are supported."
_MSG_PLAYLIST: Final = "Playlists are not supported; paste the link of a single video or track."
_MSG_YOUTUBE_ID: Final = "The YouTube link does not contain a valid video id."
_MSG_YOUTUBE_PATH: Final = "Only YouTube video links (watch, shorts or youtu.be) are supported."
_MSG_SPOTIFY_ID: Final = "The Spotify link does not contain a valid track id."
_MSG_SPOTIFY_PATH: Final = "Only Spotify track links (open.spotify.com/track/...) are supported."
_SPOTIFY_KIND_MESSAGES: Final = {
    "album": "Spotify albums are not supported; paste the link of a single track.",
    "playlist": _MSG_PLAYLIST,
    "artist": "Spotify artists are not supported; paste the link of a single track.",
    "episode": "Spotify podcast episodes are not supported; paste the link of a music track.",
    "show": "Spotify shows are not supported; paste the link of a music track.",
}


@dataclass(frozen=True, slots=True)
class ParsedSourceUrl:
    """A validated source URL.

    Attributes:
        source_type: ``youtube`` or ``spotify``.
        item_id: YouTube video id (11 chars) or Spotify track id (22 chars).
        canonical_url: ``https://www.youtube.com/watch?v=<id>`` or
            ``https://open.spotify.com/track/<id>``.
    """

    source_type: SourceType
    item_id: str
    canonical_url: str

    @property
    def source_key(self) -> str:
        """Cache identity ``<source_type>:<item_id>``, e.g. ``youtube:dQw4w9WgXcQ``."""
        return f"{self.source_type.value}:{self.item_id}"


def canonical_youtube_url(video_id: str) -> str:
    """Return ``https://www.youtube.com/watch?v=<video_id>``."""
    return f"https://www.youtube.com/watch?v={video_id}"


def canonical_spotify_url(track_id: str) -> str:
    """Return ``https://open.spotify.com/track/<track_id>``."""
    return f"https://open.spotify.com/track/{track_id}"


def parse_source_url(url: str) -> ParsedSourceUrl:
    """Validate a YouTube video URL or a Spotify track URL and derive its identity.

    Args:
        url: URL as entered by the user; surrounding whitespace is ignored.

    Returns:
        The source type, item id and canonical URL.

    Raises:
        InvalidSourceError: Empty, malformed, non-http(s), host not allow-listed, playlist,
            album/artist/episode/show, unsupported path or missing/invalid id.
    """
    parts = _split(url.strip())
    host = parts.hostname or ""
    if host in YOUTUBE_HOSTS:
        return _youtube(_youtube_site_id(parts))
    if host == YOUTU_BE_HOST:
        return _youtube(_youtu_be_id(parts.path))
    if host == SPOTIFY_HOST:
        return _spotify(parts.path)
    raise InvalidSourceError(_MSG_HOST)


def _split(url: str) -> SplitResult:
    """Split ``url`` and check length, scheme, user info and port."""
    if not url:
        raise InvalidSourceError(_MSG_EMPTY)
    parts = urlsplit(url) if len(url) <= MAX_URL_LENGTH else None
    if parts is None or not parts.scheme:
        raise InvalidSourceError(_MSG_MALFORMED)
    if parts.scheme.lower() not in ("http", "https"):
        raise InvalidSourceError(_MSG_SCHEME)
    if not parts.netloc or "@" in parts.netloc or _has_port(parts):
        raise InvalidSourceError(_MSG_MALFORMED)
    return parts


def _has_port(parts: SplitResult) -> bool:
    """Return True when the URL names an explicit (or unparsable) port."""
    try:
        return parts.port is not None
    except ValueError:
        return True


def _youtube(video_id: str) -> ParsedSourceUrl:
    """Build the parsed YouTube source after validating ``video_id``."""
    if not _YOUTUBE_ID.fullmatch(video_id):
        raise InvalidSourceError(_MSG_YOUTUBE_ID)
    return ParsedSourceUrl(SourceType.YOUTUBE, video_id, canonical_youtube_url(video_id))


def _youtube_site_id(parts: SplitResult) -> str:
    """Extract the video id from a ``youtube.com`` path and query."""
    path = parts.path.rstrip("/")
    query = parse_qs(parts.query)
    if path == "/watch":
        if "v" not in query and "list" in query:
            raise InvalidSourceError(_MSG_PLAYLIST)
        return query.get("v", [""])[0]
    if path == "/playlist":
        raise InvalidSourceError(_MSG_PLAYLIST)
    if path == "/shorts" or path.startswith("/shorts/"):
        return path.removeprefix("/shorts").removeprefix("/")
    raise InvalidSourceError(_MSG_YOUTUBE_PATH)


def _youtu_be_id(path: str) -> str:
    """Extract the video id from a ``youtu.be`` path (exactly one segment)."""
    return path.removeprefix("/")


def _spotify(path: str) -> ParsedSourceUrl:
    """Validate a Spotify path ``[/intl-xx]/track/<id>``."""
    segments = [segment for segment in path.split("/") if segment]
    if segments and _SPOTIFY_LOCALE.fullmatch(segments[0]):
        segments = segments[1:]
    kind = segments[0] if segments else ""
    if kind in _SPOTIFY_KIND_MESSAGES:
        raise InvalidSourceError(_SPOTIFY_KIND_MESSAGES[kind])
    if kind != "track":
        raise InvalidSourceError(_MSG_SPOTIFY_PATH)
    track_id = segments[1] if len(segments) == 2 else ""  # noqa: PLR2004 - ["track", <id>]
    if not _SPOTIFY_ID.fullmatch(track_id):
        raise InvalidSourceError(_MSG_SPOTIFY_ID)
    return ParsedSourceUrl(SourceType.SPOTIFY, track_id, canonical_spotify_url(track_id))
