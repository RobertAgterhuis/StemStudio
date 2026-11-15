"""Source details (artwork candidates, lookup names) injected into the ingest adapters (005).

``ingest`` must not import ``metadata`` (plan.md dependency direction), so the reader that knows
how to pick a YouTube thumbnail, a Spotify cover or an embedded upload cover is handed in by the
composition root (``metadata.source_artwork.SourceDetailsReader``). Reading details is best
effort: a failure is logged (exception type only) and the import continues without them (FR-002).
"""

import logging
from collections.abc import Callable
from pathlib import Path
from typing import Any, Protocol

from stemstudio_worker.ports import FetchedAudio, SourceInfo

_LOG = logging.getLogger(__name__)


class SourceDetails(Protocol):
    """Adds what a source offers beyond the audio: a picture and the names to look up."""

    def youtube(self, fetched: FetchedAudio, info: dict[str, Any]) -> FetchedAudio:
        """Return ``fetched`` with artwork and lookup names taken from the yt-dlp ``info``."""
        ...

    def spotify(self, source: SourceInfo, song: object) -> SourceInfo:
        """Return ``source`` with the album image and lookup names of the spotDL ``song``."""
        ...

    def upload(self, source: SourceInfo, path: Path) -> SourceInfo:
        """Return ``source`` with the embedded picture and tag names of the stored upload."""
        ...


class NoSourceDetails:
    """Default ``SourceDetails``: adds nothing (no artwork, no lookup names)."""

    def youtube(
        self,
        fetched: FetchedAudio,
        info: dict[str, Any],  # noqa: ARG002
    ) -> FetchedAudio:
        """Return ``fetched`` unchanged."""
        return fetched

    def spotify(self, source: SourceInfo, song: object) -> SourceInfo:  # noqa: ARG002
        """Return ``source`` unchanged."""
        return source

    def upload(self, source: SourceInfo, path: Path) -> SourceInfo:  # noqa: ARG002
        """Return ``source`` unchanged."""
        return source


def with_details[T](add: Callable[[], T], fallback: T) -> T:
    """Run ``add``; on any error log its type and return ``fallback`` (never fails the import).

    Args:
        add: Produces the value with the source details added.
        fallback: The value without details.

    Returns:
        ``add()``, or ``fallback`` when it raised.
    """
    try:
        return add()
    except Exception as exc:  # details are best effort (FR-002)
        _LOG.warning("Source details could not be read: %s", type(exc).__name__)
        return fallback
