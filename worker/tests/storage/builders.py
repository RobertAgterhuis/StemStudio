"""Builders of a worker data directory for the storage tests (job JSON, cache entries, files)."""

from datetime import UTC, datetime, timedelta
from pathlib import Path
from uuid import UUID

import numpy as np
import soundfile as sf

from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.lyrics_store import LyricsJobStore, StoredLyricsJob
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import (
    AlignLyricsRequest,
    DetailJob,
    DetailJobState,
    Job,
    JobState,
    LookupLyricsRequest,
    LyricsJob,
    LyricsJobState,
    SourceType,
    StemName,
)

T0 = datetime(2026, 11, 16, 12, 0, tzinfo=UTC)
SHA_A = "a" * 64
SHA_B = "b" * 64
SHA_C = "c" * 64


class StepClock:
    """A settable clock (starts at ``T0``)."""

    def __init__(self, now: datetime = T0) -> None:
        self.now = now

    def __call__(self) -> datetime:
        return self.now

    def advance(self, seconds: float) -> None:
        self.now += timedelta(seconds=seconds)


def write(path: Path, size: int) -> Path:
    """Create ``path`` (and its parents) with ``size`` bytes."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"x" * size)
    return path


def cache_entry(paths: DataPaths, sha: str, sizes: tuple[int, int, int, int]) -> Path:
    """A cache entry with (standard, detail, partial, artwork) bytes; standard = result + 1 stem."""
    standard, detail, partial, artwork = sizes
    entry = paths.by_audio(sha)
    write(entry / "result.json", 10)
    write(entry / "stems" / "vocals.flac", standard - 10)
    if detail:
        write(paths.detail_stem(sha, "drums", "kick"), detail)
    if partial:
        write(paths.detail_partial(sha) / "drums" / "kick.flac", partial)
    if artwork:
        write(paths.cached_artwork(sha) / "cover.jpg", artwork)
    return entry


def stored_job(job_id: str, sha: str | None, status: JobState, **flags: object) -> StoredJob:
    """A standard job record in ``status`` for audio ``sha`` (``updated`` = its update time)."""
    updated = flags.pop("updated", T0)
    assert isinstance(updated, datetime)
    job = Job(
        job_id=job_id, source_key="youtube:dQw4w9WgXcQ", source_type=SourceType.YOUTUBE,
        status=status, stage="Stage", progress=0, created_at=T0, updated_at=updated,
    )  # fmt: skip
    return StoredJob.model_validate({"job": job, "audio_sha256": sha, **flags})


def save_job(paths: DataPaths, record: StoredJob) -> None:
    """Write a standard job file the way the job store does."""
    JobStore(paths.jobs).save(record)


def save_detail(paths: DataPaths, detail_id: str, sha: str, status: DetailJobState) -> None:
    """Write a detail job file for audio ``sha``."""
    job = DetailJob(
        detail_job_id=detail_id, standard_job_id="0" * 32, audio_sha256=sha, status=status,
        stage="Stage", progress=0, created_at=T0, updated_at=T0,
    )  # fmt: skip
    DetailJobStore(paths.detail_jobs).save(StoredDetailJob(job=job))


def save_alignment(
    paths: DataPaths, lyrics_id: str, standard_id: str, status: LyricsJobState
) -> None:
    """Write a lyrics ``align`` job file for the standard job ``standard_id``."""
    job = LyricsJob(
        lyrics_job_id=lyrics_id, mode="align", status=status, stage="Stage", progress=0,
        created_at=T0, updated_at=T0,
    )  # fmt: skip
    request = AlignLyricsRequest(standard_job_id=standard_id, text="la la")
    LyricsJobStore(paths).save(StoredLyricsJob(job=job, request=request))


def save_lookup(paths: DataPaths, lyrics_id: str, standard_id: str) -> None:
    """Write a queued lyrics ``lookup`` job that names ``standard_id`` (correlation only)."""
    job = LyricsJob(
        lyrics_job_id=lyrics_id, mode="lookup", status=LyricsJobState.QUEUED, stage="Stage",
        progress=0, created_at=T0, updated_at=T0,
    )  # fmt: skip
    request = LookupLyricsRequest(title="Song", duration_seconds=10, standard_job_id=standard_id)
    LyricsJobStore(paths).save(StoredLyricsJob(job=job, request=request))


def library_stems(library: Path, track: UUID, stems: list[StemName]) -> dict[StemName, bytes]:
    """Write a short FLAC per stem below ``library/tracks/<track>/stems/``; their bytes."""
    folder = library / "tracks" / str(track) / "stems"
    folder.mkdir(parents=True, exist_ok=True)
    written: dict[StemName, bytes] = {}
    for index, stem in enumerate(stems):
        frames = np.full((4410, 2), index / 10, dtype=np.float32)
        sf.write(folder / f"{stem}.flac", frames, 44100, format="FLAC")
        written[stem] = (folder / f"{stem}.flac").read_bytes()
    return written
