"""Runs lyrics jobs to a terminal state (feature 006, ADR-015 D2/D3, data-model.md 2.2).

``queued -> running -> completed | failed | cancelled``. A lookup asks the injected
:class:`~stemstudio_worker.ports.LyricsLookup` (network lane); an alignment loads the vocal stem
(``vocals.lead`` from the detail cache when present, else ``vocals``), runs the injected
:class:`~stemstudio_worker.ports.Aligner` through the 003 device runner with ``WHISPER_GPU_NEED``
(scheduler unit) and records where it ran. Lyrics text is never logged.
"""

import logging
import threading
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Protocol

import numpy as np
import numpy.typing as npt

from stemstudio_worker.jobs.lyrics_store import LyricsJobStore, StoredLyricsJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    WHISPER_GPU_NEED,
    Aligner,
    AlignmentCancelledError,
    AlignmentError,
    AlignRequest,
    DeviceRunner,
    LyricsError,
    LyricsLookup,
    LyricsQuery,
    merge_device,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_ALIGNING,
    LYRICS_STAGE_SEARCHING,
    LYRICS_UNREACHABLE_TEXT,
    VOCAL_STEM_MISSING_TEXT,
    AlignLyricsRequest,
    DeviceInfo,
    JobDevice,
    LookupLyricsRequest,
    LyricsJobState,
    LyricsResult,
    TimedLyrics,
)

_LOG = logging.getLogger(__name__)
_S = LyricsJobState
_MAX_STAGE = 200
_FINAL_STAGES = {_S.COMPLETED: "Completed", _S.FAILED: "Failed", _S.CANCELLED: "Cancelled"}


class VocalLoader(Protocol):
    """Reads a vocal stem as mono float32 at ``ALIGN_SAMPLE_RATE`` (``alignment.audio_prep``)."""

    def load(self, path: Path) -> npt.NDArray[np.float32]:
        """Load ``path``.

        Raises:
            AlignmentError: The stem is silent (``VOCAL_STEM_SILENT_TEXT``) or unreadable.
        """
        ...


def _never() -> bool:
    return False


@dataclass(frozen=True, slots=True)
class LyricsParts:
    """The adapters of feature 006 (built by the composition root).

    Attributes:
        lookup: LRCLIB lookup (``lyrics`` package) or a disabled stand-in.
        aligner: stable-ts + Whisper aligner (``alignment`` package) or its fake.
        vocals: Loads the vocal stem for the aligner.
        devices: The worker's device runner (one GPU step at a time, CPU fallback).
        model_cached: Whether the Whisper weights are in the model cache (estimate, health).
    """

    lookup: LyricsLookup
    aligner: Aligner
    vocals: VocalLoader
    devices: DeviceRunner
    model_cached: Callable[[], bool] = _never


@dataclass(frozen=True, slots=True)
class LyricsContext:
    """Where lyrics jobs and their standard jobs live and how time is read."""

    store: LyricsJobStore
    jobs: JobStore
    paths: DataPaths
    clock: Clock


def vocal_stem(context: LyricsContext, standard_job_id: str) -> Path:
    """The vocal stem of a standard job: the ``vocals.lead`` detail stem, else ``vocals``.

    Raises:
        AlignmentError: Neither file is in the worker cache (``VOCAL_STEM_MISSING_TEXT``).
    """
    record = context.jobs.get(standard_job_id)
    sha = None if record is None else record.audio_sha256
    if sha is not None:
        paths = context.paths
        for path in (paths.detail_stem(sha, "vocals", "lead"), paths.stem_file(sha, "vocals")):
            if path.is_file():
                return path
    raise AlignmentError(VOCAL_STEM_MISSING_TEXT)


@dataclass(frozen=True, slots=True)
class Outcome:
    """How a job ended: its terminal state, error text and result."""

    status: LyricsJobState
    error: str | None = None
    result: LyricsResult | None = None


class LyricsJobs:
    """Thread-safe changes of live (queued or running) lyrics jobs; terminal jobs never change."""

    def __init__(self, context: LyricsContext) -> None:
        """Change the jobs of ``context.store``."""
        self.context = context
        self._lock = threading.RLock()

    def live(self, job_id: str) -> StoredLyricsJob | None:
        """The record of a queued or running job, else None."""
        record = self.context.store.get(job_id)
        return None if record is None or record.job.status.is_terminal else record

    def update(self, job_id: str, **changes: Any) -> bool:
        """Apply ``changes`` to a live job; False when it is gone or already terminal."""
        with self._lock:
            record = self.live(job_id)
            if record is None:
                return False
            job = record.job.model_copy(update={**changes, "updated_at": self.context.clock()})
            self.context.store.save(record.model_copy(update={"job": job}))
            return True

    def progress(self, job_id: str, fraction: float, stage: str) -> bool:
        """Report progress (never decreasing) and stage; False when the job stopped."""
        with self._lock:
            record = self.live(job_id)
            if record is None:
                return False
            value = max(record.job.progress, min(max(fraction, 0.0), 1.0))
            if (value, stage[:_MAX_STAGE]) != (record.job.progress, record.job.stage):
                self.update(job_id, progress=value, stage=stage[:_MAX_STAGE])
            return True

    def finish(self, job_id: str, outcome: Outcome) -> bool:
        """End a live job (storing a result first); False when it already ended or is gone."""
        with self._lock:
            if self.live(job_id) is None:
                return False
            changes: dict[str, Any] = {"status": outcome.status, "error": outcome.error,
                                       "stage": _FINAL_STAGES[outcome.status]}  # fmt: skip
            if outcome.result is not None:
                self.context.store.save_result(job_id, outcome.result)
                changes["progress"] = 1.0
            return self.update(job_id, **changes)

    def remove(self, job_id: str) -> bool:
        """Delete a job and its result in any state; False when it was not known."""
        with self._lock:
            return self.context.store.remove(job_id)


class _Reporter:
    """``ProgressCallback`` of one job; raises ``AlignmentCancelledError`` once it stopped."""

    def __init__(self, jobs: LyricsJobs, job_id: str) -> None:
        self._jobs = jobs
        self._job_id = job_id

    def __call__(self, fraction: float, stage: str) -> None:
        if not self._jobs.progress(self._job_id, fraction, stage):
            raise AlignmentCancelledError("The lyrics job was cancelled or deleted")


def query_of(request: LookupLyricsRequest) -> LyricsQuery:
    """The lookup query of a ``mode: lookup`` request."""
    return LyricsQuery(title=request.title, duration_seconds=request.duration_seconds,
                       artist=request.artist, album=request.album)  # fmt: skip


def with_source(document: TimedLyrics, request: AlignLyricsRequest) -> TimedLyrics:
    """Name the text's source: the refined document's record, else text entered by the user."""
    refine = request.refine
    source: dict[str, object] = {
        "source": "user",
        "source_id": None,
        "source_duration_seconds": None,
    }
    if refine is not None:
        source = {"source": refine.source, "source_id": refine.source_id,
                  "source_duration_seconds": refine.source_duration_seconds}  # fmt: skip
    return document.model_copy(update=source)


def planned_device(torch_device: str, info: DeviceInfo) -> JobDevice:
    """The job device while a step runs on ``torch_device`` (before the runner reports)."""
    if torch_device == "cuda":
        return JobDevice(kind="gpu", name=info.name)
    return JobDevice(kind="cpu")


def _failed(error: str) -> Outcome:
    return Outcome(_S.FAILED, error=error)


class LyricsRunner:
    """Brings lookups (network lane) and alignments (scheduler units) to a terminal state."""

    def __init__(self, context: LyricsContext, parts: LyricsParts) -> None:
        """Run the jobs of ``context`` with the adapters ``parts``."""
        self.jobs = LyricsJobs(context)
        self.parts = parts

    def lookup(self, job_id: str) -> None:
        """Run one lookup; unknown, terminal and align jobs are ignored."""
        record = self.jobs.live(job_id)
        if record is None or not isinstance(record.request, LookupLyricsRequest):
            return
        self.jobs.update(job_id, status=_S.RUNNING, stage=LYRICS_STAGE_SEARCHING)
        self.jobs.finish(job_id, self._lookup(job_id, record.request))

    def align(self, job_id: str) -> None:
        """Run one alignment; unknown, terminal and lookup jobs are ignored."""
        record = self.jobs.live(job_id)
        if record is None or not isinstance(record.request, AlignLyricsRequest):
            return
        self.jobs.update(job_id, status=_S.RUNNING, stage=LYRICS_STAGE_ALIGNING)
        self.jobs.finish(job_id, self._align(job_id, record.request))

    def _lookup(self, job_id: str, request: LookupLyricsRequest) -> Outcome:
        try:
            result = self.parts.lookup.lookup(query_of(request), _Reporter(self.jobs, job_id))
        except LyricsError as exc:
            return _failed(str(exc))
        except Exception as exc:
            _LOG.error("Lyrics lookup %s failed unexpectedly (%s)", job_id, type(exc).__name__)
            return _failed(LYRICS_UNREACHABLE_TEXT)
        return Outcome(_S.COMPLETED, result=result)

    def _align(self, job_id: str, request: AlignLyricsRequest) -> Outcome:
        try:
            document = self._aligned(job_id, request)
        except AlignmentCancelledError:
            return Outcome(_S.CANCELLED)
        except LyricsError as exc:
            return _failed(str(exc))
        except Exception as exc:
            _LOG.error("Lyrics alignment %s failed unexpectedly (%s)", job_id, type(exc).__name__)
            return _failed(ALIGNMENT_FAILED_TEXT)
        return Outcome(_S.COMPLETED, result=LyricsResult(outcome="found", lyrics=document))

    def _aligned(self, job_id: str, request: AlignLyricsRequest) -> TimedLyrics:
        audio = self.parts.vocals.load(vocal_stem(self.jobs.context, request.standard_job_id))
        align = AlignRequest(audio, request.text, request.language, request.refine)
        reporter = _Reporter(self.jobs, job_id)

        def work(device: str) -> TimedLyrics:
            self.jobs.update(job_id, device=planned_device(device, self.parts.devices.info))
            return self.parts.aligner.align(align, device, reporter)

        document, use = self.parts.devices.run(work, WHISPER_GPU_NEED)
        self.jobs.update(job_id, device=merge_device(None, use))
        return with_source(document, request)
