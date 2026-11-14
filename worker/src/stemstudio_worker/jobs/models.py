"""Persisted job record: the wire ``Job`` plus fields only the worker needs."""

from typing import Annotated

from pydantic import Field

from stemstudio_worker.ports import FetchedAudio, SourceInfo
from stemstudio_worker.schemas import ContractModel, Job, JobResult, SourceMetadata, SourceType

_MAX_TEXT = 500


class StoredJob(ContractModel):
    """Content of ``jobs/<jobId>.json``.

    Attributes:
        job: The job as reported on the wire.
        upload_file: Generated file name of the upload under ``uploads/<jobId>/`` (uploads only).
        audio_sha256: SHA-256 of the normalised audio once known (cache key, stem location).
        result: The job result once completed.
    """

    job: Job
    upload_file: Annotated[str | None, Field(pattern=r"^input\.(mp3|wav|flac)$")] = None
    audio_sha256: Annotated[str | None, Field(pattern=r"^[0-9a-f]{64}$")] = None
    result: JobResult | None = None


def metadata_of(source: SourceInfo) -> SourceMetadata:
    """Build wire metadata from a resolved source (texts clipped to the contract limits)."""
    artist = source.artist[:_MAX_TEXT] if source.artist else None
    return SourceMetadata(
        title=source.title[:_MAX_TEXT] or "Untitled",
        artist=artist,
        source_url=source.youtube_url,
    )


def fetched_metadata(job: Job, fetched: FetchedAudio) -> SourceMetadata | None:
    """Metadata of a YouTube job after its download, or None when it stays unchanged.

    The downloaded title replaces the resolver's placeholder ("YouTube video <id>") and the
    reported artist replaces a missing one. Spotify metadata from spotDL is authoritative, so
    Spotify (and upload) jobs are never changed.
    """
    if job.source_type is not SourceType.YOUTUBE or not fetched.title:
        return None
    current = job.metadata or SourceMetadata(title=fetched.title[:_MAX_TEXT])
    artist = fetched.artist[:_MAX_TEXT] if fetched.artist else current.artist
    return current.model_copy(update={"title": fetched.title[:_MAX_TEXT], "artist": artist})


def source_of(job: Job) -> SourceInfo:
    """Rebuild the resolved source of a job from its persisted fields."""
    metadata = job.metadata or SourceMetadata(title="Untitled")
    return SourceInfo(
        source_type=job.source_type,
        source_key=job.source_key,
        title=metadata.title,
        artist=metadata.artist,
        youtube_url=metadata.source_url,
    )
