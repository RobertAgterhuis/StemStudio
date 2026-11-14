"""FIFO job queue served by one background thread (one job at a time, FR-008)."""

import logging
import queue
import threading
from collections.abc import Callable

_LOG = logging.getLogger(__name__)


class JobQueue:
    """Runs ``process(job_id)`` for submitted ids, strictly in order, on a single thread."""

    def __init__(self, process: Callable[[str], None]) -> None:
        """Create a stopped queue that will call ``process`` for each job id."""
        self._process = process
        self._queue: queue.Queue[str | None] = queue.Queue()
        self._thread: threading.Thread | None = None

    @property
    def running(self) -> bool:
        """Whether the worker thread is alive."""
        return self._thread is not None and self._thread.is_alive()

    def start(self) -> None:
        """Start the worker thread (no-op when already started)."""
        if self._thread is None:
            self._thread = threading.Thread(target=self._loop, name="stemstudio-jobs", daemon=True)
            self._thread.start()

    def submit(self, job_id: str) -> None:
        """Append ``job_id`` to the queue."""
        self._queue.put(job_id)

    def join(self) -> None:
        """Block until every submitted job has been processed."""
        self._queue.join()

    def stop(self, timeout: float | None = None) -> None:
        """Stop after the current job; waits at most ``timeout`` seconds (daemon thread)."""
        thread, self._thread = self._thread, None
        if thread is not None:
            self._queue.put(None)
            thread.join(timeout)

    def _loop(self) -> None:
        while (job_id := self._queue.get()) is not None:
            self._run_one(job_id)
        self._queue.task_done()

    def _run_one(self, job_id: str) -> None:
        try:
            self._process(job_id)
        except Exception:
            _LOG.exception("Job %s crashed the runner", job_id)
        finally:
            self._queue.task_done()
