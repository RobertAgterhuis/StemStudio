"""Persisted job record: the wire ``Job`` plus fields only the worker needs."""

import re
from typing import Annotated

from pydantic import Field

from stemstudio_worker.ports import FetchedAudio, SourceInfo
from stemstudio_worker.schemas import (
    ArtworkSource,
    ContractModel,
    Job,
    JobResult,
    SourceMetadata,
    SourceType,
)

_MAX_TEXT = 500
_ISRC = re.compile(r"[A-Z]{2}[A-Z0-9]{3}[0-9]{7}")


class PendingArtwork(ContractModel):
    """A source picture offered at job creation, captured by the "Saving cover" step (005).

    Attributes:
        source: Where the picture comes from (``spotify`` cover URL, ``embedded`` upload cover).
        url: Remote image URL, or None for embedded bytes.
        staged: True when the bytes wait in the job's upload directory (``cover.bin``).
    """

    source: ArtworkSource
    url: Annotated[str | None, Field(max_length=2048)] = None
    staged: bool = False


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
    artwork_candidate: PendingArtwork | None = None


def pending_artwork(source: SourceInfo) -> PendingArtwork | None:
    """The picture a resolved source offers, as persisted with its queued job."""
    candidate = source.artwork
    if candidate is None:
        return None
    return PendingArtwork(source=candidate.source, url=candidate.url, staged=candidate.url is None)


def _clip(text: str | None) -> str | None:
    return text[:_MAX_TEXT] if text else None


def metadata_of(source: SourceInfo) -> SourceMetadata:
    """Build wire metadata from a resolved source (texts clipped to the contract limits)."""
    isrc = source.isrc if source.isrc and _ISRC.fullmatch(source.isrc) else None
    return SourceMetadata(
        title=source.title[:_MAX_TEXT] or "Untitled", artist=_clip(source.artist),
        source_url=source.youtube_url, album=_clip(source.album), isrc=isrc,
        lookup_title=_clip(source.lookup_title), lookup_artist=_clip(source.lookup_artist),
    )  # fmt: skip


def fetched_metadata(job: Job, fetched: FetchedAudio) -> SourceMetadata | None:
    """Metadata of a YouTube job after its download, or None when it stays unchanged.

    The downloaded title replaces the resolver's placeholder ("YouTube video <id>"); the
    reported artist, album and cleaned lookup names replace missing ones. Spotify metadata from
    spotDL is authoritative, so Spotify (and upload) jobs are never changed.
    """
    if job.source_type is not SourceType.YOUTUBE or not fetched.title:
        return None
    current = job.metadata or SourceMetadata(title=fetched.title[:_MAX_TEXT])
    filled = {name: _prefer(getattr(fetched, name), getattr(current, name)) for name in _FETCHED}
    return current.model_copy(update={"title": fetched.title[:_MAX_TEXT], **filled})


_FETCHED = ("artist", "album", "lookup_title", "lookup_artist")


def _prefer(reported: str | None, current: str | None) -> str | None:
    """The clipped value the download reported, else the current one."""
    return _clip(reported) or current


def source_of(job: Job) -> SourceInfo:
    """Rebuild the resolved source of a job from its persisted fields."""
    metadata = job.metadata or SourceMetadata(title="Untitled")
    return SourceInfo(
        source_type=job.source_type,
        source_key=job.source_key,
        title=metadata.title,
        artist=metadata.artist,
        youtube_url=metadata.source_url,
        album=metadata.album,
        isrc=metadata.isrc,
    )
