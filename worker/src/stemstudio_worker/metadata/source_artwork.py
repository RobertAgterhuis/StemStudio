"""Pictures and lookup names offered by the import source (FR-001, research R-02).

* YouTube: the best ``info["thumbnails"]`` entry on ``i.ytimg.com`` (``maxresdefault`` >
  ``sddefault`` > ``hqdefault`` > the widest other entry of at least 300 px); no second yt-dlp
  call. Lookup names are cleaned (``titles.youtube_lookup_names``).
* Spotify: spotDL ``Song.cover_url`` on ``i.scdn.co``.
* Upload: the embedded front cover (else any embedded picture) read with tinytag, plus the tag
  title, artist and album for the lookup. WAV files carry no tags.

``SourceDetailsReader`` bundles these for the ingest adapters, which receive it from the
composition root (``ingest`` must not import ``metadata``) and treat it as best effort.
"""

import dataclasses
import logging
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Final
from urllib.parse import urlsplit

from tinytag import TinyTag

from stemstudio_worker.metadata.titles import clean_title, youtube_lookup_names
from stemstudio_worker.ports import ArtworkCandidate, FetchedAudio, SourceInfo

YOUTUBE_IMAGE_HOST: Final = "i.ytimg.com"
"""Host of YouTube thumbnails."""

SPOTIFY_IMAGE_HOST: Final = "i.scdn.co"
"""Host of Spotify album images."""

MIN_THUMBNAIL_WIDTH: Final = 300
"""Narrowest thumbnail of an unknown name that is accepted."""

MAX_TAG_LENGTH: Final = 500
"""Longest tag value kept."""

_NAME_RANK: Final = {"maxresdefault": 3, "sddefault": 2, "hqdefault": 1}
_LOG = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class EmbeddedTags:
    """What an uploaded file's tags offer.

    Attributes:
        artwork: The embedded picture, or None.
        title: Tag title, or None.
        artist: Tag artist, or None.
        album: Tag album, or None.
    """

    artwork: ArtworkCandidate | None = None
    title: str | None = None
    artist: str | None = None
    album: str | None = None


def youtube_thumbnail(info: Mapping[str, Any]) -> ArtworkCandidate | None:
    """Choose the YouTube thumbnail to use as source artwork.

    Args:
        info: The yt-dlp info dict of the video.

    Returns:
        A ``youtube`` URL candidate, or None when no thumbnail qualifies.
    """
    ranked = [item for item in map(_ranked, _thumbnail_entries(info)) if item is not None]
    if not ranked:
        return None
    return ArtworkCandidate(source="youtube", url=max(ranked)[1])


def _ranked(entry: Mapping[str, Any]) -> tuple[tuple[int, int], str] | None:
    """A qualifying thumbnail with its rank (name preference, width), or None."""
    url = entry.get("url")
    if not isinstance(url, str) or not _is_https_on(url, YOUTUBE_IMAGE_HOST):
        return None
    rank = _rank(url, entry.get("width"))
    return (rank, url) if rank[0] or rank[1] >= MIN_THUMBNAIL_WIDTH else None


def _rank(url: str, width: object) -> tuple[int, int]:
    """Rank of a thumbnail: (name preference, width in pixels; 0 when unknown)."""
    name_rank = _NAME_RANK.get(PurePosixPath(urlsplit(url).path).stem, 0)
    return name_rank, width if isinstance(width, int) else 0


def _thumbnail_entries(info: Mapping[str, Any]) -> list[Mapping[str, Any]]:
    """The ``thumbnails`` entries that are mappings, plus the single ``thumbnail`` URL."""
    listed = info.get("thumbnails")
    entries = (
        [entry for entry in listed if isinstance(entry, Mapping)]
        if isinstance(listed, list)
        else []
    )
    return [*entries, {"url": info.get("thumbnail")}]


def spotify_cover(song: object) -> ArtworkCandidate | None:
    """The Spotify album image of a spotDL ``Song`` (``cover_url``), or None."""
    url = getattr(song, "cover_url", None)
    if not isinstance(url, str) or not _is_https_on(url, SPOTIFY_IMAGE_HOST):
        return None
    return ArtworkCandidate(source="spotify", url=url)


def read_embedded(path: Path) -> EmbeddedTags:
    """Read the embedded picture and tags of an uploaded file with tinytag.

    Args:
        path: The stored upload (MP3, FLAC or WAV).

    Returns:
        The tags; empty when the file has none or cannot be parsed (never raises).
    """
    try:
        tag = TinyTag.get(path, image=True)
    except Exception as exc:  # malformed uploads never fail the import
        _LOG.info("Upload tags could not be read: %s", type(exc).__name__)
        return EmbeddedTags()
    image = tag.images.front_cover or tag.images.any
    artwork = ArtworkCandidate(source="embedded", data=image.data) if image and image.data else None
    return EmbeddedTags(artwork, _text(tag.title), _text(tag.artist), _text(tag.album))


class SourceDetailsReader:
    """Adds source artwork and lookup names to what the ingest adapters produce.

    Structurally implements ``ingest.details.SourceDetails``; it is passed to
    ``ingest.create_ingest_adapters`` by the composition root.
    """

    def youtube(self, fetched: FetchedAudio, info: dict[str, Any]) -> FetchedAudio:
        """Add the thumbnail and the cleaned lookup names to a YouTube download."""
        title, artist = youtube_lookup_names(info)
        thumbnail = youtube_thumbnail(info)
        return dataclasses.replace(
            fetched, artwork=thumbnail, lookup_title=title, lookup_artist=artist
        )

    def spotify(self, source: SourceInfo, song: object) -> SourceInfo:
        """Add the album image and the lookup names to a resolved Spotify track."""
        return dataclasses.replace(
            source,
            artwork=spotify_cover(song),
            lookup_title=clean_title(source.title),
            lookup_artist=source.artist,
        )

    def upload(self, source: SourceInfo, path: Path) -> SourceInfo:
        """Add the embedded picture, tag names and album to an accepted upload."""
        tags = read_embedded(path)
        return dataclasses.replace(
            source,
            artwork=tags.artwork,
            lookup_title=tags.title or source.title,
            lookup_artist=tags.artist,
            album=tags.album,
        )


def _is_https_on(url: str, host: str) -> bool:
    """True for an https URL on exactly ``host``."""
    parts = urlsplit(url)
    return parts.scheme == "https" and parts.hostname == host


def _text(value: object) -> str | None:
    """A stripped tag value of at most 500 characters, or None."""
    if not isinstance(value, str) or not value.strip():
        return None
    return value.strip()[:MAX_TAG_LENGTH]
