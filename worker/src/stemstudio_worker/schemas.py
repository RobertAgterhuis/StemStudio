"""Pydantic v2 models of the worker HTTP contract.

These models mirror ``specs/001-stemstudio-mvp/contracts/worker-api.openapi.yaml`` exactly and
are the single source of truth for JSON on the wire (camelCase aliases) and for ``result.json``
on disk. Models are immutable; use ``model.model_copy(update={...})`` to derive new states.
"""

from datetime import datetime
from enum import StrEnum
from typing import Annotated, Final, Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

CONTRACT_VERSION = "1.0.1"
"""Version of the worker API contract (``info.version`` in the OpenAPI document)."""

SAMPLE_RATE: Final = 44_100
"""Sample rate of normalised audio and of every stem (Hz)."""

CHANNELS: Final = 2
"""Channel count of normalised audio and of every stem (stereo)."""

StemName = Literal["vocals", "drums", "bass", "guitar", "piano", "other"]
"""Stem names; ``htdemucs_6s`` yields all six, ``htdemucs`` yields vocals, drums, bass, other."""

ModelName = Literal["htdemucs_6s", "htdemucs", "fake"]
"""Separation model identifiers recorded in a job result."""

Progress = Annotated[int, Field(ge=0, le=100)]
"""Overall job progress in percent."""

Seconds = Annotated[float, Field(ge=0)]
"""Non-negative time in seconds."""


class SourceType(StrEnum):
    """Origin of the audio of a job."""

    YOUTUBE = "youtube"
    SPOTIFY = "spotify"
    UPLOAD = "upload"


class JobState(StrEnum):
    """Lifecycle state of a job; ``completed`` and ``failed`` are terminal."""

    QUEUED = "queued"
    DOWNLOADING = "downloading"
    SEPARATING = "separating"
    ANALYSING = "analysing"
    COMPLETED = "completed"
    FAILED = "failed"

    @property
    def is_terminal(self) -> bool:
        """Return True for ``completed`` and ``failed``."""
        return self in (JobState.COMPLETED, JobState.FAILED)


class ContractModel(BaseModel):
    """Base model: camelCase JSON aliases, unknown fields rejected, immutable."""

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        extra="forbid",
        frozen=True,
    )


class CreateJobRequest(ContractModel):
    """JSON body of ``POST /v1/jobs`` for URL sources (uploads use multipart field ``file``)."""

    source_type: Literal[SourceType.YOUTUBE, SourceType.SPOTIFY]
    url: Annotated[str, Field(min_length=1, max_length=2048)]


class SourceMetadata(ContractModel):
    """Descriptive metadata of the source audio."""

    title: Annotated[str, Field(min_length=1, max_length=500)]
    artist: Annotated[str | None, Field(max_length=500)] = None
    source_url: str | None = Field(
        default=None, description="The YouTube URL the audio was fetched from (null for uploads)."
    )


class Job(ContractModel):
    """Status of a job: body of ``POST /v1/jobs`` and ``GET /v1/jobs/{jobId}``."""

    job_id: Annotated[str, Field(pattern=r"^[0-9a-f]{32}$")]
    source_key: Annotated[str, Field(pattern=r"^(youtube|spotify|upload):[A-Za-z0-9_-]{1,64}$")]
    source_type: SourceType
    status: JobState
    stage: Annotated[str, Field(max_length=200)]
    progress: Progress
    error: Annotated[str | None, Field(max_length=1000)] = None
    metadata: SourceMetadata | None = None
    created_at: datetime
    updated_at: datetime


class BeatGrid(ContractModel):
    """Detected tempo and beat positions of the full mix."""

    bpm: Annotated[float, Field(ge=0, le=400)]
    beats: list[Seconds]
    downbeats: list[Seconds]
    beats_per_bar: Annotated[int, Field(ge=1, le=12)] = 4
    confidence: Annotated[float, Field(ge=0, le=1)]
    reliable: bool


class StemInfo(ContractModel):
    """One stem file of a completed job."""

    name: StemName
    path: str = Field(description="Relative URL: /v1/jobs/{jobId}/stems/{name}.")
    size_bytes: Annotated[int, Field(ge=0)]


class JobResult(ContractModel):
    """Body of ``GET /v1/jobs/{jobId}/result`` and content of ``result.json``."""

    job_id: Annotated[str, Field(pattern=r"^[0-9a-f]{32}$")]
    source_key: str
    metadata: SourceMetadata
    duration_seconds: Annotated[float, Field(gt=0, le=900)]
    sample_rate: Literal[44100] = 44100
    channels: Literal[2] = 2
    audio_sha256: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    model: ModelName
    stems: Annotated[list[StemInfo], Field(min_length=1, max_length=6)]
    beat_grid: BeatGrid
    completed_at: datetime


class Health(ContractModel):
    """Body of ``GET /health``."""

    status: Literal["ok"] = "ok"
    separator: Literal["demucs", "fake"]
    device: Literal["cpu", "cuda", "auto"] = Field(
        description="Configured torch device preference."
    )
    contract_version: str = CONTRACT_VERSION


class ProblemDetails(BaseModel):
    """RFC 9457 problem details (``application/problem+json``) for every error response."""

    model_config = ConfigDict(extra="allow", frozen=True)

    type: str = "about:blank"
    title: str
    status: Annotated[int, Field(ge=400, le=599)]
    detail: str | None = None
    instance: str | None = None
