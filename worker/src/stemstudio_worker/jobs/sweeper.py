"""Periodic clean-up timer: removes expired metadata, artwork and render job files."""

import logging
import threading
from collections.abc import Callable, Sequence

_LOG = logging.getLogger(__name__)

SWEEP_INTERVAL_SECONDS = 3600.0
"""How often the composition root runs the sweep tasks (and once at start-up)."""


class Sweeper:
    """Runs every task at start and then every ``interval`` seconds on a daemon thread."""

    def __init__(self, interval: float, tasks: Sequence[Callable[[], object]]) -> None:
        """Create a stopped sweeper.

        Args:
            interval: Seconds between two rounds.
            tasks: Called in order each round; an exception is logged, the next task runs.
        """
        self._interval = interval
        self._tasks = tuple(tasks)
        self._stopped = threading.Event()
        self._thread: threading.Thread | None = None

    @property
    def running(self) -> bool:
        """Whether the timer thread is alive."""
        return self._thread is not None and self._thread.is_alive()

    def start(self) -> None:
        """Start the timer thread (no-op when already started)."""
        if self._thread is None:
            self._stopped.clear()
            self._thread = threading.Thread(target=self._loop, name="stemstudio-sweep", daemon=True)
            self._thread.start()

    def stop(self, timeout: float | None = None) -> None:
        """Stop the timer; waits at most ``timeout`` seconds for a running round."""
        thread, self._thread = self._thread, None
        if thread is not None:
            self._stopped.set()
            thread.join(timeout)

    def _loop(self) -> None:
        while True:
            self._round()
            if self._stopped.wait(self._interval):
                return

    def _round(self) -> None:
        for task in self._tasks:
            try:
                task()
            except Exception:
                _LOG.exception("A sweep task failed")
