"""User-Agent of every online request, without personal data (FR-010, research R-07).

Format: ``StemStudio/<worker version> ( <contact> )``. The contact is ``STEMSTUDIO_CONTACT_URL``
when it is a safe https URL, else the generic token ``local-practice-tool; contact not
configured``. An invalid value is logged without the value itself (it may be personal data).
"""

import logging
import re
from importlib import metadata
from typing import Final
from urllib.parse import urlsplit

GENERIC_CONTACT: Final = "local-practice-tool; contact not configured"
"""Contact part of the User-Agent when no valid contact URL is configured."""

MAX_CONTACT_URL_LENGTH: Final = 200
"""Longest accepted contact URL."""

FALLBACK_VERSION: Final = "0.0.0"
"""Version used when the worker package metadata is not installed."""

_PACKAGE: Final = "stemstudio-worker"
_UNSAFE: Final = re.compile(r"[\s\x00-\x1f\x7f@]")
_LOG = logging.getLogger(__name__)


def worker_version() -> str:
    """Return the installed worker package version (``FALLBACK_VERSION`` when unknown)."""
    try:
        return metadata.version(_PACKAGE)
    except metadata.PackageNotFoundError:
        return FALLBACK_VERSION


def is_valid_contact_url(value: str) -> bool:
    """Check a contact URL: https, a host, no user-info or ``@``, <= 200 chars, no whitespace.

    Args:
        value: The configured contact URL.

    Returns:
        True when the URL may be sent in the User-Agent.
    """
    if not value or len(value) > MAX_CONTACT_URL_LENGTH or _UNSAFE.search(value):
        return False
    parts = urlsplit(value)
    return parts.scheme == "https" and bool(parts.hostname)


def build_user_agent(contact_url: str | None, version: str | None = None) -> str:
    """Build the User-Agent header value.

    Args:
        contact_url: ``OnlineSettings.contact_url`` (None when unset).
        version: Version to report; defaults to ``worker_version()``.

    Returns:
        ``StemStudio/<version> ( <contact URL or generic token> )``.
    """
    return f"StemStudio/{version or worker_version()} ( {_contact(contact_url)} )"


def _contact(contact_url: str | None) -> str:
    """The contact URL when valid; otherwise the generic token (with a warning when set)."""
    if contact_url is None:
        return GENERIC_CONTACT
    if is_valid_contact_url(contact_url):
        return contact_url
    _LOG.warning("STEMSTUDIO_CONTACT_URL is not a valid https contact URL; using the generic token")
    return GENERIC_CONTACT
