"""The "Saving cover" step of the standard job (feature 005, ADR-014 D2).

Source pictures are best effort: the candidates of a job (the source's own picture, e.g. the
Spotify cover or an upload's embedded cover, and the picture the download reports, e.g. the
YouTube thumbnail) are tried in precedence order with a time budget each; any failure is logged
and the job goes on without artwork. The picture is kept with the cached result, so a later job
answered from the cache gets it too.
"""

import logging
import threading
from collections.abc import Callable, Iterable
from concurrent.futures import Future
from pathlib import Path

from pydantic import ValidationError

from stemstudio_worker.jobs.artwork_files import write_artwork
from stemstudio_worker.jobs.models import PendingArtwork, StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import SAVING_COVER, JobTracker
from stemstudio_worker.ports import ArtworkCandidate, ArtworkImages, ImageNormalizer, SourceInfo
from stemstudio_worker.schemas import ArtworkInfo, JobState

_LOG = logging.getLogger(__name__)

STAGED_COVER = "cover.bin"
"""File name of an upload's embedded cover below ``uploads/<jobId>/``."""

ARTWORK_BUDGET_SECONDS = 15.0
"""Longest time one picture may take to download and normalise."""

_RANK = {"user": 0, "spotify": 1, "embedded": 2, "coverartarchive": 3, "youtube": 4}
_INFO = "info.json"


def stage_upload_artwork(upload_dir: Path, source: SourceInfo) -> None:
    """Keep the embedded cover bytes of an accepted upload for the job's "Saving cover" step."""
    if source.artwork is not None and source.artwork.data is not None:
        upload_dir.mkdir(parents=True, exist_ok=True)
        (upload_dir / STAGED_COVER).write_bytes(source.artwork.data)


def _run_with_budget[T](work: Callable[[], T], seconds: float) -> T:
    """Run ``work`` on a daemon thread; raise ``TimeoutError`` when it takes too long."""
    future: Future[T] = Future()

    def target() -> None:
        try:
            future.set_result(work())
        except BaseException as exc:
            future.set_exception(exc)

    threading.Thread(target=target, name="stemstudio-artwork", daemon=True).start()
    return future.result(timeout=seconds)


class ArtworkCapture:
    """Normalises the best working candidate within a time budget per candidate."""

    def __init__(self, normalizer: ImageNormalizer, budget_seconds: float = ARTWORK_BUDGET_SECONDS):
        """Use ``normalizer``; abandon a candidate after ``budget_seconds``."""
        self._normalizer = normalizer
        self._budget = budget_seconds

    def first(
        self, candidates: Iterable[ArtworkCandidate]
    ) -> tuple[ArtworkInfo, ArtworkImages] | None:
        """Normalise the first candidate (by precedence) that works; None when none does.

        Errors and timeouts are logged, never raised.
        """
        for candidate in sorted(candidates, key=lambda c: _RANK[c.source]):
            images = self._one(candidate)
            if images is not None:
                return images.info(candidate.source, candidate.url), images
        return None

    def _one(self, candidate: ArtworkCandidate) -> ArtworkImages | None:
        try:
            return _run_with_budget(lambda: self._normalizer.normalize(candidate), self._budget)
        except Exception as exc:
            _LOG.warning("The %s cover could not be saved: %r", candidate.source, exc)
            return None


class CoverStep:
    """Captures, stores and restores the source picture of standard jobs."""

    def __init__(self, paths: DataPaths, capture: ArtworkCapture | None) -> None:
        """Store pictures below ``paths``; without ``capture`` no picture is captured."""
        self._paths = paths
        self._capture = capture

    def save(
        self, record: StoredJob, fetched: ArtworkCandidate | None, tracker: JobTracker
    ) -> None:
        """Run "Saving cover" when the job has candidates: set ``Job.artwork`` and its files."""
        candidates = self._candidates(record, fetched)
        if self._capture is None or not candidates:
            return
        tracker.update(JobState.DOWNLOADING, SAVING_COVER, 0)
        captured = self._capture.first(candidates)
        if captured is not None:
            write_artwork(self._paths.job_artwork(record.job.job_id), captured[1])
            tracker.set_artwork(captured[0])

    def sync(self, tracker: JobTracker, audio_sha256: str) -> None:
        """Keep the job's picture with the cached result, or give the job the cached one."""
        job, cached = tracker.job, self._paths.cached_artwork(audio_sha256)
        own = self._paths.job_artwork(job.job_id)
        if job.artwork is not None:
            _copy(own, cached, job.artwork)
            return
        info = _read_info(cached)
        if info is not None and _copy(cached, own, info):
            tracker.set_artwork(info)

    def _candidates(
        self, record: StoredJob, fetched: ArtworkCandidate | None
    ) -> list[ArtworkCandidate]:
        return [c for c in (self._pending(record), fetched) if c is not None]

    def _pending(self, record: StoredJob) -> ArtworkCandidate | None:
        pending: PendingArtwork | None = record.artwork_candidate
        if pending is None:
            return None
        if pending.url is not None:
            return ArtworkCandidate(pending.source, url=pending.url)
        staged = self._paths.uploads(record.job.job_id) / STAGED_COVER
        return (
            ArtworkCandidate(pending.source, data=staged.read_bytes()) if staged.is_file() else None
        )


def _read_info(directory: Path) -> ArtworkInfo | None:
    try:
        return ArtworkInfo.model_validate_json((directory / _INFO).read_text(encoding="utf-8"))
    except (OSError, ValidationError):
        return None


def _copy(source: Path, target: Path, info: ArtworkInfo) -> bool:
    """Copy both JPEGs and ``info.json`` (atomic rename); False when ``source`` lacks a file."""
    files = [source / "cover.jpg", source / "thumb.jpg"]
    if not all(path.is_file() for path in files):
        return False
    images = ArtworkImages(files[0].read_bytes(), files[1].read_bytes(), info.sha256)
    write_artwork(target, images)
    (target / _INFO).write_text(info.model_dump_json(by_alias=True), encoding="utf-8")
    return True
