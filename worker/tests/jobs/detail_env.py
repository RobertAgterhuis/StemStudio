"""Builders for the detail-job tests: a completed standard job with real cached FLAC stems."""

from pathlib import Path

import numpy as np
import soundfile as sf

from stemstudio_worker.jobs.cache import ResultCache, stem_infos
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import JobResult, JobState, ModelName, SourceMetadata, StemName

from .conftest import T0, build_job
from .fakes import GRID

STD = "b" * 32
SHA = "9" * 64
SIX: tuple[StemName, ...] = ("vocals", "drums", "bass", "guitar", "piano", "other")
FOUR: tuple[StemName, ...] = ("vocals", "drums", "bass", "other")


def write_tone(path: Path, seconds: float = 0.5, amplitude: float = 0.3) -> None:
    """Write a stereo 16-bit FLAC tone."""
    t = np.arange(int(seconds * 44_100)) / 44_100
    mono = amplitude * np.sin(2 * np.pi * 220 * t)
    sf.write(path, np.stack([mono, mono], axis=1), 44_100, format="FLAC", subtype="PCM_16")


def completed_standard(
    paths: DataPaths,
    store: JobStore,
    stems: tuple[StemName, ...] = SIX,
    job_id: str = STD,
) -> StoredJob:
    """Save a completed standard job for :data:`SHA` and cache its stems as real FLACs."""
    work = paths.root / "seed" / job_id
    work.mkdir(parents=True, exist_ok=True)
    files = {name: work / f"{name}.flac" for name in stems}
    for path in files.values():
        write_tone(path)
    model: ModelName = "htdemucs_6s" if len(stems) == 6 else "htdemucs"
    result = JobResult(
        job_id=job_id, source_key="youtube:dQw4w9WgXcQ",
        metadata=SourceMetadata(title="Song"), duration_seconds=0.5, audio_sha256=SHA,
        model=model, stems=stem_infos(job_id, files), beat_grid=GRID, completed_at=T0,
    )  # fmt: skip
    ResultCache(paths).store(result, files)
    job = build_job(job_id=job_id, status=JobState.COMPLETED)
    record = StoredJob(job=job, audio_sha256=SHA, result=result)
    store.save(record)
    return record
