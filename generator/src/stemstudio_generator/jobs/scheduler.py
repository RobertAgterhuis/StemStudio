"""Single-thread FIFO scheduler of generation jobs (FR-007, FR-013, data-model 2.4).

One background thread takes queued jobs in submission order and runs them one at a time.
When the queue becomes empty it calls the runner's idle hook, which unloads the models so the
GPU is free for the worker, games or Ollama (ADR-018 D5). Terminal jobs not deleted by the
backend are removed 24 h after their last update. Cancel and delete are safe from any thread.
"""

import logging
import threading
from collections import deque
from collections.abc import Iterable
from datetime import timedelta
from typing import Final

from stemstudio_generator.config import JOB_RETENTION_SECONDS
from stemstudio_generator.jobs.progress import JobReporter
from stemstudio_generator.jobs.runner import JobRunner
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import Clock
from stemstudio_generator.schemas import TERMINAL_STATUSES, Generation, QueueStatus

_LOG: Final = logging.getLogger(__name__)
_JOIN_SECONDS: Final = 5.0


class Scheduler:
    """Queue, current job, cancel/delete and retention."""

    sweep_seconds: float = 60.0
    """Interval of the retention sweep while idle."""

    def __init__(self, store: JsonJobStore, runner: JobRunner, clock: Clock) -> None:
        """Schedule jobs of ``store`` on ``runner``."""
        self._store = store
        self._runner = runner
        self._clock = clock
        self._queue: deque[str] = deque()
        self._cond = threading.Condition()
        self._current: JobReporter | None = None
        self._delete_after: set[str] = set()
        self._thread: threading.Thread | None = None
        self._stopping = False

    def submit(self, job: Generation) -> Generation:
        """Save a new ``queued`` job and append it to the queue."""
        with self._cond:
            self._store.save(job)
            self._queue.append(job.generation_id)
            self._cond.notify_all()
        return job

    def resume(self, generation_ids: Iterable[str]) -> None:
        """Re-queue stored ``queued`` jobs after a restart (reconciliation order)."""
        with self._cond:
            for generation_id in generation_ids:
                job = self._store.get(generation_id)
                if job is not None and job.status == "queued":
                    self._queue.append(generation_id)
            self._cond.notify_all()

    def get(self, generation_id: str) -> Generation | None:
        """The job (live state for the running one), or None."""
        with self._cond:
            current = self._running(generation_id)
            return current.job if current is not None else self._store.get(generation_id)

    def cancel(self, generation_id: str) -> Generation | None:
        """Cancel the job (see ``JobReporter.request_cancel``); None when unknown."""
        with self._cond:
            current = self._running(generation_id)
            if current is not None:
                return current.request_cancel()
            job = self._store.get(generation_id)
            if job is None or job.status != "queued":
                return job
            self._unqueue(generation_id)
            return JobReporter(job, self._store, self._clock).request_cancel()

    def delete(self, generation_id: str) -> bool:
        """Delete the job and its files (the running job is cancelled and removed after it)."""
        with self._cond:
            current = self._running(generation_id)
            if current is not None:
                current.request_cancel()
                self._delete_after.add(generation_id)
                return True
            if self._store.get(generation_id) is None:
                return False
            self._unqueue(generation_id)
            self._store.delete(generation_id)
            return True

    def queue_status(self) -> QueueStatus:
        """Queued jobs (excluding the running one) and the running job's id."""
        with self._cond:
            running = None if self._current is None else self._current.job.generation_id
            return QueueStatus(queued=len(self._queue), running=running)

    def run_next(self) -> bool:
        """Run the next queued job to its end; False when the queue is empty."""
        reporter = self._take_next()
        if reporter is None:
            return False
        try:
            self._runner.run(reporter)
        finally:
            self._finish(reporter)
        return True

    def sweep(self) -> list[str]:
        """Delete terminal jobs last updated 24 h ago or earlier; return their ids."""
        cutoff = self._clock.now() - timedelta(seconds=JOB_RETENTION_SECONDS)
        with self._cond:
            expired = [
                job.generation_id
                for job in self._store.list_all()
                if job.status in TERMINAL_STATUSES and job.updated_at <= cutoff
            ]
            for generation_id in expired:
                self._store.delete(generation_id)
        return expired

    def start(self) -> None:
        """Start the background thread."""
        self._thread = threading.Thread(target=self._loop, name="generator-jobs", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Stop the background thread after the current job."""
        with self._cond:
            self._stopping = True
            self._cond.notify_all()
        if self._thread is not None:
            self._thread.join(_JOIN_SECONDS)

    def _loop(self) -> None:
        while not self._stopping:
            try:
                self._work()
            except Exception:  # the thread must survive a broken job file or disk error
                _LOG.exception("Generator job loop error")
            with self._cond:
                if not self._queue and not self._stopping:
                    self._cond.wait(self.sweep_seconds)

    def _work(self) -> None:
        self.sweep()
        while not self._stopping and self.run_next():
            pass

    def _running(self, generation_id: str) -> JobReporter | None:
        current = self._current
        return (
            current if current is not None and current.job.generation_id == generation_id else None
        )

    def _unqueue(self, generation_id: str) -> None:
        if generation_id in self._queue:
            self._queue.remove(generation_id)

    def _take_next(self) -> JobReporter | None:
        with self._cond:
            while self._queue:
                job = self._store.get(self._queue.popleft())
                if job is not None and job.status == "queued":
                    self._current = JobReporter(job, self._store, self._clock)
                    return self._current
            return None

    def _finish(self, reporter: JobReporter) -> None:
        generation_id = reporter.job.generation_id
        with self._cond:
            self._current = None
            if generation_id in self._delete_after:
                self._delete_after.discard(generation_id)
                self._store.delete(generation_id)
            idle = not self._queue
        if idle:
            self._runner.idle()
        _LOG.info("Generation %s ended %s", generation_id, reporter.job.status)
