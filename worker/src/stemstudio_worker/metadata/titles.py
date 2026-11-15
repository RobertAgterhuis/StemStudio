"""Title cleaning for lookups and the normalisation used to compare names (research R-04).

YouTube names are looked up in this order: yt-dlp ``track`` + ``artist`` (YouTube Music
metadata), then ``"<artist> - <title>"`` split on the first dash, then the raw title with the
uploader as artist (minus `` - Topic``, ``VEVO``, ``Official``). Suffixes such as "(Official
Music Video)", "[HD]" or "Remastered 2021" and "feat." parts are removed; "Live" is kept because
live versions are separate recordings in MusicBrainz.
"""

import re
import unicodedata
from collections.abc import Mapping
from typing import Any, Final

MAX_NAME_LENGTH: Final = 500
"""Longest lookup title or artist (``SourceMetadata`` limit)."""

_NOISE: Final = (
    r"official\s+(?:music\s+|lyrics?\s+)?video|official\s+audio|lyrics?\s+video|lyrics"
    r"|audio|hd|hq|4k|(?:\d{4}\s+)?remaster(?:ed)?(?:\s+\d{4})?"
)
_BRACKETED: Final = re.compile(rf"\s*[\(\[]\s*(?:{_NOISE})\s*[\)\]]", re.IGNORECASE)
_DASHED: Final = re.compile(rf"\s+[-\u2013]\s+(?:{_NOISE})\s*$", re.IGNORECASE)
_FEAT_BRACKETED: Final = re.compile(r"\s*[\(\[]\s*(?:feat|ft|featuring)\b[^\)\]]*[\)\]]", re.I)
_FEAT_TAIL: Final = re.compile(r"\s+(?:feat\.?|ft\.?|featuring)\s.*$", re.IGNORECASE)
_SEPARATOR: Final = re.compile(r"\s+[-\u2013]\s+")
_ARTIST_SUFFIX: Final = re.compile(r"(?:\s*-\s*Topic|\s*VEVO|\s+Official)$")


def clean_title(title: str) -> str:
    """Remove video/lyrics/quality/remaster suffixes and "feat." parts from a title.

    Args:
        title: A song or video title.

    Returns:
        The cleaned title with collapsed whitespace; the (collapsed) input when cleaning would
        leave nothing.
    """
    original = " ".join(title.split())
    cleaned = _FEAT_BRACKETED.sub("", _BRACKETED.sub("", original))
    cleaned = _FEAT_TAIL.sub("", _DASHED.sub("", cleaned))
    return " ".join(cleaned.split()) or original


def clean_artist(artist: str) -> str:
    """Remove channel suffixes (`` - Topic``, ``VEVO``, `` Official``) from an artist name."""
    original = " ".join(artist.split())
    return _ARTIST_SUFFIX.sub("", original).strip() or original


def youtube_lookup_names(info: Mapping[str, Any]) -> tuple[str | None, str | None]:
    """Derive the lookup title and artist of a YouTube video from its yt-dlp info.

    Args:
        info: The yt-dlp info dict (``track``, ``artist``, ``title``, ``uploader``, ``channel``).

    Returns:
        ``(title, artist)``, each at most 500 characters or None when unknown.
    """
    track, artist = _text(info.get("track")), _text(info.get("artist"))
    if track and artist:
        return clean_title(track), clean_artist(artist)
    title = _text(info.get("title"))
    return (None, None) if title is None else _names_from_title(title, info)


def _names_from_title(title: str, info: Mapping[str, Any]) -> tuple[str, str | None]:
    """``"<artist> - <title>"`` when the title has that form, else the uploader as artist."""
    split = _split_artist(title)
    if split is not None:
        return clean_title(split[1]), clean_artist(split[0])
    uploader = _text(info.get("uploader")) or _text(info.get("channel"))
    return clean_title(title), clean_artist(uploader) if uploader else None


def normalize_for_compare(text: str) -> str:
    """Normalise a name for equality checks: NFKC, casefold, ``&`` -> and, no punctuation.

    Args:
        text: A title or artist name.

    Returns:
        The comparison key with single spaces.
    """
    folded = unicodedata.normalize("NFKC", text).casefold().replace("&", " and ")
    kept = "".join(char for char in folded if not unicodedata.category(char).startswith("P"))
    return " ".join(kept.split())


def _split_artist(title: str) -> tuple[str, str] | None:
    """Split ``"<artist> - <title>"`` on the first dash; None when a side is empty."""
    parts = _SEPARATOR.split(title, maxsplit=1)
    if len(parts) != 2 or not parts[0].strip() or not parts[1].strip():  # noqa: PLR2004
        return None
    return parts[0].strip(), parts[1].strip()


def _text(value: object) -> str | None:
    """A stripped string of at most 500 characters, or None for blanks and non-strings."""
    if not isinstance(value, str) or not value.strip():
        return None
    return value.strip()[:MAX_NAME_LENGTH]
