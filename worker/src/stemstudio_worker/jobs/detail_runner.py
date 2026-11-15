"""Runs detail jobs one scheduler unit at a time: start (cache or plan), parent steps, finalise.

Progress = floor(100 x (finished step weights + fraction x current weight) / total weight), kept
between 1 and 99 while running. Errors become the fixed texts of data-model.md 4.2; a failed or
cancelled job never leaves a ``detail/`` result, only its checkpoints for a retry.
"""

import errno
import logging
import math
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

from stemstudio_worker.jobs import detail_texts as texts
from stemstudio_worker.jobs.cache import ResultCache
from stemstudio_worker.jobs.detail_cache import DetailCache, bind_outcome, rebind
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort, DetailStepUnit
from stemstudio_worker.jobs.detail_progress import DetailTracker
from stemstudio_worker.jobs.detail_service import DetailContext
from stemstudio_worker.jobs.detail_store import StoredDetailJob
from stemstudio_worker.ports import DetailCancelledError, WorkerError
from stemstudio_worker.schemas import DetailResult, ParentStemName, StemName

_LOG = logging.getLogger(__name__)

FINALISE_WEIGHT: Final = 0.01
"""Share of the final unit (research.md D-03)."""


class StandardStemsMissingError(WorkerError):
    """The standard stems of the detail job left the worker cache."""


@dataclass(slots=True)
class _Active:
    """In-memory plan of a running detail job."""

    stems: Mapping[StemName, Path]
    steps: Sequence[DetailStepUnit]
    index: int = 0
    done: float = 0.0

    @property
    def total(self) -> float:
        return sum(step.weight for step in self.steps) + FINALISE_WEIGHT

    def percent(self, done: float) -> int:
        return min(99, max(1, math.floor(100 * done / self.total)))


def failure_text(exc: Exception) -> str:
    """Map an exception of a detail unit to its user text (unexpected ones are logged)."""
    if isinstance(exc, WorkerError):
        return str(exc) or texts.UNEXPECTED
    known = _resource_text(exc)
    if known is None:
        _LOG.error("Detail job failed unexpectedly", exc_info=exc)
    return known or texts.UNEXPECTED


def _resource_text(exc: Exception) -> str | None:
    """Text for running out of memory or disk space, else None."""
    if isinstance(exc, MemoryError):
        return texts.NO_MEMORY
    if isinstance(exc, OSError) and exc.errno == errno.ENOSPC:
        return texts.NO_DISK
    return None


class _Reporter:
    """Step progress -> job progress; raises ``DetailCancelledError`` once cancel is requested."""

    def __init__(self, tracker: DetailTracker, active: _Active, stage: str) -> None:
        self._tracker = tracker
        self._active = active
        self._stage = stage
        self._step = active.steps[active.index]

    def __call__(self, fraction: float) -> None:
        if self._tracker.cancel_requested:
            raise DetailCancelledError(texts.CANCELLED)
        done = self._active.done + min(max(fraction, 0.0), 1.0) * self._step.weight
        self._tracker.running(self._stage, self._active.percent(done), self._step.parents[0])


class DetailRunner:
    """Executes the units of detail jobs (called by the scheduler thread)."""

    def __init__(self, context: DetailContext, pipeline: DetailPipelinePort) -> None:
        """Run detail jobs of ``context`` through ``pipeline``."""
        self._context = context
        self._pipeline = pipeline
        self._cache = DetailCache(context.paths)
        self._active: dict[str, _Active] = {}

    def step(self, detail_id: str) -> bool:
        """Run the next unit of ``detail_id``; returns True while units remain."""
        record = self._context.details.get(detail_id)
        more = False
        if record is not None and not record.job.status.is_terminal:
            tracker = DetailTracker(self._context.details, detail_id, self._context.clock)
            try:
                more = self._unit(record, tracker)
            except Exception as exc:
                _end(tracker, exc)
        if not more:
            self._active.pop(detail_id, None)
        return more

    def _unit(self, record: StoredDetailJob, tracker: DetailTracker) -> bool:
        active = self._active.get(record.job.detail_job_id)
        if active is None:
            return self._start(record, tracker)
        if active.index < len(active.steps):
            self._run_step(active, tracker)
            return True
        self._finalise(record, active, tracker)
        return False

    def _start(self, record: StoredDetailJob, tracker: DetailTracker) -> bool:
        sha = record.job.audio_sha256
        cached = self._cache.lookup(sha, self._pipeline.separator_version)
        if cached is not None:
            tracker.complete(rebind(cached, record.job.detail_job_id, self._context.clock()))
            return False
        stems = self._standard_stems(sha)
        tracker.running(texts.STARTING, 1, None)
        steps = self._pipeline.plan(stems, self._context.paths.detail_partial(sha))
        self._active[record.job.detail_job_id] = _Active(stems, steps)
        return True

    def _standard_stems(self, audio_sha256: str) -> dict[StemName, Path]:
        result = ResultCache(self._context.paths).lookup_audio(audio_sha256)
        if result is None:
            raise StandardStemsMissingError(texts.STEMS_MISSING)
        return {s.name: self._context.paths.stem_file(audio_sha256, s.name) for s in result.stems}

    def _run_step(self, active: _Active, tracker: DetailTracker) -> None:
        step = active.steps[active.index]
        stage = texts.splitting(step.parents, active.index + 1, len(active.steps))
        parent: ParentStemName = step.parents[0]
        tracker.running(stage, active.percent(active.done), parent)
        step.run(_Reporter(tracker, active, stage))
        active.done += step.weight
        active.index += 1

    def _finalise(self, record: StoredDetailJob, active: _Active, tracker: DetailTracker) -> None:
        tracker.running(texts.FINALISING, active.percent(active.done), None)
        sha, detail_id = record.job.audio_sha256, record.job.detail_job_id
        outcomes = self._pipeline.outcomes(
            tuple(active.stems), self._context.paths.detail_partial(sha)
        )
        result = DetailResult(
            detail_job_id=detail_id, audio_sha256=sha,
            separator_version=self._pipeline.separator_version,
            parents=[bind_outcome(o, detail_id) for o in outcomes],
            completed_at=self._context.clock(),
        )  # fmt: skip
        self._cache.store(result)
        tracker.complete(result)


def _end(tracker: DetailTracker, exc: Exception) -> None:
    if isinstance(exc, DetailCancelledError):
        tracker.cancel()
    else:
        tracker.fail(failure_text(exc))
