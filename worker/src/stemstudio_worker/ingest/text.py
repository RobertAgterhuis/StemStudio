"""Cleaning of metadata texts reported by spotDL and yt-dlp."""

from typing import Final

MAX_TEXT_LENGTH: Final = 500
"""Longest title/artist kept (``SourceMetadata`` limit)."""


def clean_text(value: object) -> str | None:
    """Strip ``value`` and cut it to ``MAX_TEXT_LENGTH``.

    Args:
        value: A library-reported value of any type.

    Returns:
        The cleaned text, or None when ``value`` is not a string or is blank.
    """
    if not isinstance(value, str) or not value.strip():
        return None
    return value.strip()[:MAX_TEXT_LENGTH]
