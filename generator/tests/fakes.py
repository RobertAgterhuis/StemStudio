"""Shared test doubles: a manual clock and recorders (no sleeping, no GPU)."""

from collections.abc import Callable
from datetime import UTC, datetime, timedelta

START = datetime(2026, 11, 15, 12, 0, tzinfo=UTC)


class FakeClock:
    """``Clock`` whose ``sleep`` advances time instantly and runs an optional hook."""

    def __init__(self, start: datetime = START) -> None:
        self.current = start
        self.sleeps: list[float] = []
        self.on_sleep: Callable[[], None] | None = None

    def now(self) -> datetime:
        return self.current

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.current += timedelta(seconds=seconds)
        if self.on_sleep is not None:
            self.on_sleep()

    def advance(self, seconds: float) -> None:
        self.current += timedelta(seconds=seconds)
