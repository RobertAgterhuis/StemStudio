"""Re-create a completed standard job from library stems (``POST /v1/jobs/rehydrate``, 013).

The stems of the model are copied from the read-only library into a staging folder
(``cache/by-audio/<newJobId>.partial``, removed at start-up when a crash left it) while they are
hashed; the cache key is ``sha256("library:" + ",".join(sorted("stem=fileSha256")))``. The entry
is placed with the result cache's atomic rename and a completed ``StoredJob`` flagged
``from_library`` is written. An existing completed rehydrated job of the same key is returned
instead (idempotent); when its entry was evicted it is placed again and the flag is cleared.
"""

import hashlib
import shutil
from collections.abc import Callable, Mapping
from contextlib import AbstractContextManager, nullcontext
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Final
from uuid import UUID

import soundfile as sf

from stemstudio_worker.jobs.cache import ResultCache, rebind, stem_infos
from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import COMPLETED, Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import LibraryStemReader, WorkerError
from stemstudio_worker.schemas import (
    BeatGrid,
    Job,
    JobResult,
    JobState,
    RehydratedJob,
    RehydrateRequest,
    SourceMetadata,
    StemName,
)

LIBRARY_TITLE: Final = "Library track"
"""Title of a rehydrated result (never served: its ``/result`` answers 404)."""

MAX_DURATION_SECONDS: Final = 900.0
_NO_GRID: Final = BeatGrid(bpm=0, beats=[], downbeats=[], confidence=0, reliable=False)
_BLOCK: Final = 1 << 20

DurationProbe = Callable[[Path], float]
"""Seconds of audio in a FLAC file; raises on an unreadable file."""


class StemSetError(WorkerError):
    """The requested stems are not the stem set of the model (HTTP 422)."""

    def __init__(self, model: str) -> None:
        """Create the error for ``model``."""
        super().__init__(f"stems must be exactly the stems of {model}")


class InvalidStemError(WorkerError):
    """A library stem is not a readable FLAC file (HTTP 422).

    Attributes:
        stem: The unreadable stem.
    """

    def __init__(self, stem: StemName) -> None:
        """Create the error for ``stem``."""
        super().__init__(f"The library {stem} stem is not a readable FLAC file.")
        self.stem = stem


def flac_duration(path: Path) -> float:
    """Duration of an audio file from its header (``soundfile.info``)."""
    return float(sf.info(str(path)).duration)


def library_key(digests: Mapping[StemName, str] | Mapping[str, str]) -> str:
    """Cache key of a rehydrated entry from the per-stem file SHA-256 values."""
    pairs = ",".join(sorted(f"{stem}={digest}" for stem, digest in digests.items()))
    return hashlib.sha256(f"library:{pairs}".encode()).hexdigest()


def _nothing() -> None:
    """Default hook: nothing to do."""


@dataclass(frozen=True, slots=True)
class RehydrateContext:
    """What a rehydration reads, writes and holds.

    Attributes:
        paths: Worker data directory layout.
        jobs: The live standard job store.
        reader: Opens the library stems.
        clock: Current UTC time.
        duration: Reads the duration of a staged stem.
        lock: The storage lock shared with the janitor.
        on_change: Called after a rehydration (drops the meter's cached report).
    """

    paths: DataPaths
    jobs: JobStore
    reader: LibraryStemReader
    clock: Clock
    duration: DurationProbe = flac_duration
    lock: Callable[[], AbstractContextManager[object]] = nullcontext
    on_change: Callable[[], None] = _nothing


@dataclass(frozen=True, slots=True)
class _Staged:
    """Stems copied into the staging folder: path and SHA-256 per stem."""

    request: RehydrateRequest
    job_id: str
    files: dict[StemName, tuple[Path, str]]

    @property
    def key(self) -> str:
        return library_key({stem: digest for stem, (_path, digest) in self.files.items()})

    @property
    def stems(self) -> dict[StemName, Path]:
        return {stem: path for stem, (path, _digest) in self.files.items()}


class Rehydrator:
    """Creates (or returns) completed standard jobs over library stems."""

    def __init__(self, context: RehydrateContext) -> None:
        """Create a rehydrator over ``context``."""
        self._context = context
        self._cache = ResultCache(context.paths)

    def rehydrate(self, request: RehydrateRequest) -> tuple[RehydratedJob, bool]:
        """Rehydrate ``request``; returns the job and whether it was newly created.

        Raises:
            StemSetError: ``request.stems`` is not the model's stem set.
            LibraryUnavailableError: The library mount is missing or unreadable.
            StemMissingError: A stem file of the model is missing.
            InvalidStemError: A stem file is not readable audio.
        """
        if not request.stems_match_model():
            raise StemSetError(request.model)
        with self._context.lock():
            outcome = self._locked(request, new_id())
        self._context.on_change()
        return outcome

    def _locked(self, request: RehydrateRequest, job_id: str) -> tuple[RehydratedJob, bool]:
        staging = self._context.paths.rehydrate_staging(job_id)
        try:
            staged = _Staged(request, job_id, self._copy(request, staging / "stems"))
            canonical = self._place(staged)
        finally:
            shutil.rmtree(staging, ignore_errors=True)
        return self._job(staged, canonical)

    def _copy(self, request: RehydrateRequest, folder: Path) -> dict[StemName, tuple[Path, str]]:
        folder.mkdir(parents=True, exist_ok=True)
        copied: dict[StemName, tuple[Path, str]] = {}
        for stem in request.stems:
            target = folder / f"{stem}.flac"
            copied[stem] = target, self._copy_stem(request.track_id, stem, target)
        return copied

    def _copy_stem(self, track_id: UUID, stem: StemName, target: Path) -> str:
        with self._context.reader.open(track_id, stem) as source, target.open("wb") as sink:
            return _copy_hashed(source, sink)

    def _place(self, staged: _Staged) -> JobResult:
        """The entry's result: the cached one, else a new one stored from the staged stems."""
        cached = self._cache.lookup_audio(staged.key)
        if cached is not None:
            return cached
        result = self._new_result(staged)
        self._cache.store(result, staged.stems)
        return result

    def _new_result(self, staged: _Staged) -> JobResult:
        request, stems = staged.request, staged.stems
        return JobResult(
            job_id=staged.job_id, source_key=request.source_key,
            metadata=SourceMetadata(title=LIBRARY_TITLE), duration_seconds=self._duration(stems),
            audio_sha256=staged.key, model=request.model, stems=stem_infos(staged.job_id, stems),
            beat_grid=_NO_GRID, completed_at=self._context.clock(),
        )  # fmt: skip

    def _duration(self, stems: dict[StemName, Path]) -> float:
        """The longest stem duration (every stem must be readable), at most 900 s."""
        durations = [_probe(self._context.duration, stem, path) for stem, path in stems.items()]
        return min(max(durations), MAX_DURATION_SECONDS)

    def _job(self, staged: _Staged, canonical: JobResult) -> tuple[RehydratedJob, bool]:
        existing = self._existing(staged.key)
        if existing is not None:
            if existing.cache_evicted:
                self._context.jobs.save(existing.model_copy(update={"cache_evicted": False}))
            return _wire(existing), False
        record = self._new_record(staged.request, canonical, staged.job_id)
        self._context.jobs.save(record)
        return _wire(record), True

    def _existing(self, key: str) -> StoredJob | None:
        matches = (r for r in self._context.jobs.all() if _rehydrated(r, key))
        return next(matches, None)

    def _new_record(
        self, request: RehydrateRequest, canonical: JobResult, job_id: str
    ) -> StoredJob:
        now = self._context.clock()
        job = Job(
            job_id=job_id, source_key=request.source_key, source_type=request.source_type,
            status=JobState.COMPLETED, stage=COMPLETED, progress=100,
            metadata=canonical.metadata, created_at=now, updated_at=now,
        )  # fmt: skip
        result = rebind(canonical, job, now)
        return StoredJob(
            job=job, audio_sha256=canonical.audio_sha256, result=result, from_library=True
        )


def _rehydrated(record: StoredJob, key: str) -> bool:
    """A completed rehydrated job of the entry ``key``."""
    completed = record.job.status is JobState.COMPLETED
    return record.from_library and record.audio_sha256 == key and completed


def _copy_hashed(source: BinaryIO, sink: BinaryIO) -> str:
    digest = hashlib.sha256()
    while block := source.read(_BLOCK):
        digest.update(block)
        sink.write(block)
    return digest.hexdigest()


def _probe(duration: DurationProbe, stem: StemName, path: Path) -> float:
    try:
        seconds = duration(path)
    except (RuntimeError, OSError, ValueError) as exc:  # soundfile.LibsndfileError: RuntimeError
        raise InvalidStemError(stem) from exc
    if seconds <= 0:
        raise InvalidStemError(stem)
    return seconds


def _wire(record: StoredJob) -> RehydratedJob:
    result = record.result
    assert result is not None  # noqa: S101 - rehydrated jobs are written completed
    return RehydratedJob(
        job_id=record.job.job_id, audio_sha256=result.audio_sha256, model=result.model,
        stems=[s.name for s in result.stems], created_at=record.job.created_at,
    )  # fmt: skip
