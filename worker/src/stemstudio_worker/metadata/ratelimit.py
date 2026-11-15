"""Process-wide rate limiting per host family, Retry-After parsing and back-off (research R-06).

MusicBrainz and Cover Art Archive share one family, Wikidata and Wikipedia another; every other
host is its own family. Each family gets at most one request per ``interval`` (1 s). The clock
and the sleeper are injected so tests run instantly.
"""

import threading
import time
from collections.abc import Callable
from datetime import datetime
from email.utils import parsedate_to_datetime
from typing import Final

MAX_RETRIES: Final = 3
"""Retries after a 503/429 answer before the request fails."""

RETRY_AFTER_CAP_SECONDS: Final = 30.0
"""Longest wait honoured from a ``Retry-After`` header."""

BACKOFF_BASE_SECONDS: Final = 2.0
"""First back-off delay; doubled per retry (2, 4, 8 s)."""

JITTER_RATIO: Final = 0.2
"""Back-off delays vary by +-20 %."""

_FAMILIES: Final[tuple[tuple[str, str], ...]] = (
    ("musicbrainz.org", "musicbrainz"),
    ("coverartarchive.org", "musicbrainz"),
    ("wikidata.org", "wikimedia"),
    ("wikipedia.org", "wikimedia"),
    ("archive.org", "archive.org"),
)


def host_family(host: str) -> str:
    """Return the rate-limit family of ``host`` (``musicbrainz``, ``wikimedia`` or the host)."""
    name = host.lower()
    for domain, family in _FAMILIES:
        if name == domain or name.endswith(f".{domain}"):
            return family
    return name


class RateLimiter:
    """Token bucket of one request per ``interval`` and host family, safe across threads."""

    def __init__(
        self,
        clock: Callable[[], float] = time.monotonic,
        sleep: Callable[[float], None] = time.sleep,
        interval: float = 1.0,
    ) -> None:
        """Create the limiter.

        Args:
            clock: Monotonic clock in seconds.
            sleep: Blocks for the given number of seconds.
            interval: Minimum spacing of two requests of one family.
        """
        self._clock = clock
        self._sleep = sleep
        self._interval = interval
        self._next: dict[str, float] = {}
        self._lock = threading.Lock()

    def acquire(self, host: str) -> None:
        """Block until a request to ``host`` may be sent, then reserve that slot."""
        wait = self._reserve(host_family(host))
        if wait > 0:
            self._sleep(wait)

    def _reserve(self, family: str) -> float:
        """Reserve the next free slot of ``family``; return how long to wait for it."""
        with self._lock:
            now = self._clock()
            slot = max(now, self._next.get(family, now))
            self._next[family] = slot + self._interval
            return slot - now


def retry_after_seconds(value: str | None, now: datetime) -> float | None:
    """Parse a ``Retry-After`` header (seconds or HTTP date), capped at 30 s.

    Args:
        value: The header value, or None when absent.
        now: Current wall-clock time (timezone-aware), for HTTP dates.

    Returns:
        Seconds to wait (0..30), or None when the header is absent or unusable.
    """
    text = (value or "").strip()
    if text.isdigit():
        return min(float(text), RETRY_AFTER_CAP_SECONDS)
    seconds = _seconds_until(text, now)
    return None if seconds is None else min(max(seconds, 0.0), RETRY_AFTER_CAP_SECONDS)


def _seconds_until(text: str, now: datetime) -> float | None:
    """Seconds from ``now`` until the HTTP date ``text``, or None when it is not a date."""
    try:
        return (parsedate_to_datetime(text) - now).total_seconds()
    except (TypeError, ValueError):
        return None


def backoff_seconds(attempt: int, jitter: float) -> float:
    """Back-off delay of retry ``attempt`` (0-based): 2, 4, 8 s, each +-20 %.

    Args:
        attempt: 0 for the first retry.
        jitter: Random value in [0, 1]; 0.5 gives the nominal delay.

    Returns:
        The delay in seconds.
    """
    nominal = BACKOFF_BASE_SECONDS * (1 << attempt)
    return nominal * (1 - JITTER_RATIO + 2 * JITTER_RATIO * jitter)
