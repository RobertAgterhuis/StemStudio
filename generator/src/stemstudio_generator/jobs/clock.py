"""The system clock (``Clock`` port)."""

import time
from datetime import UTC, datetime


class SystemClock:
    """UTC wall clock and ``time.sleep``."""

    def now(self) -> datetime:
        """Current UTC time."""
        return datetime.now(UTC)

    def sleep(self, seconds: float) -> None:
        """Block for ``seconds``."""
        time.sleep(seconds)
