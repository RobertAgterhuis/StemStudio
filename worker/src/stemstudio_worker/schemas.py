"""Pydantic v2 models of the worker HTTP contract.

These models mirror ``specs/003-gpu-acceleration/contracts/worker-api.openapi.yaml`` exactly and
are the single source of truth for JSON on the wire (camelCase aliases) and for ``result.json``
on disk. Models are immutable; use ``model.model_copy(update={...})`` to derive new states.
"""

from datetime import datetime
from enum import StrEnum
from typing import Annotated, Final, Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

CONTRACT_VERSION = "1.2.0"
"""Version of the worker API contract (``info.version`` in the OpenAPI document)."""

SAMPLE_RATE: Final = 44_100
"""Sample rate of normalised audio and of every stem (Hz)."""

CHANNELS: Final = 2
"""Channel count of normalised audio and of every stem (stereo)."""

StemName = Literal["vocals", "drums", "bass", "guitar", "piano", "other"]
"""Stem names; ``htdemucs_6s`` yields all six, ``htdemucs`` yields vocals, drums, bass, other."""

ModelName = Literal["htdemucs_6s", "htdemucs", "fake"]
"""Separation model identifiers recorded in a job result."""

ParentStemName = Literal["vocals", "drums", "guitar", "piano", "other"]
"""Standard stems that can be split further (``bass`` is never split)."""

DetailStemName = Literal[
    "kick", "snare", "toms", "hihat", "cymbals", "lead", "backing", "strings", "brass",
    "woodwinds", "keys", "synth", "acoustic-guitar", "electric-guitar", "percussion", "rest",
]  # fmt: skip
"""Detail stem names (closed set); ``rest`` is the last stem of every split parent."""

DetailModelId = Literal["mdx23c-drumsep-6s", "uvr-mdxnet-kara-2", "mvsep-mega-53", "fake-detail"]
"""Detail models recorded per parent outcome (ADR-010)."""

ProcessingDeviceKind = Literal["cpu", "gpu"]
"""Device a model step ran on (feature 003): the CPU or the NVIDIA GPU (CUDA)."""

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


class DetailJobState(StrEnum):
    """Lifecycle state of a detail job; ``completed``, ``failed``, ``cancelled`` are terminal."""

    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

    @property
    def is_terminal(self) -> bool:
        """Return True for ``completed``, ``failed`` and ``cancelled``."""
        return self in (DetailJobState.COMPLETED, DetailJobState.FAILED, DetailJobState.CANCELLED)


class ParentOutcomeKind(StrEnum):
    """What the detail phase did with one standard stem (data-model.md 2.2)."""

    SPLIT = "split"
    SKIPPED_SILENT = "skipped-silent"
    NOTHING_FURTHER = "nothing-further"
    NOT_RELIABLE = "not-reliable"
    NOT_SUPPORTED = "not-supported"


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


class JobDevice(ContractModel):
    """Device record of one standard or detail job (feature 003, data-model.md 2.2).

    ``kind`` is ``gpu`` when at least one model step of the job ran on the GPU, else ``cpu``.
    ``cpu_fallback`` is True when at least one model step ran on the CPU although the worker's
    active device is the GPU (too little free GPU memory, or out of GPU memory and retried).
    """

    kind: ProcessingDeviceKind
    name: Annotated[str | None, Field(max_length=200)] = Field(
        default=None, description="GPU name when kind is gpu, else null."
    )
    cpu_fallback: bool = False
    fallback_reason: Annotated[str | None, Field(max_length=500)] = Field(
        default=None, description="Readable reason of the last CPU fallback; null without one."
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
    device: JobDevice | None = Field(
        default=None,
        description="Set when the separation starts; null before and for cached results (1.2.0).",
    )
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


JobId = Annotated[str, Field(pattern=r"^[0-9a-f]{32}$")]
"""Job identifier: 32 lower-case hex characters."""

AudioSha256 = Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
"""SHA-256 of the normalised audio (cache key)."""


class CreateDetailJobRequest(ContractModel):
    """JSON body of ``POST /v1/detail-jobs``."""

    standard_job_id: JobId


class DetailJob(ContractModel):
    """Status of a detail job: body of ``POST /v1/detail-jobs`` and ``GET /v1/detail-jobs/{id}``."""

    detail_job_id: JobId
    standard_job_id: JobId
    audio_sha256: AudioSha256
    status: DetailJobState
    stage: Annotated[str, Field(max_length=200)]
    current_parent: ParentStemName | None = None
    progress: Progress
    error: Annotated[str | None, Field(max_length=1000)] = None
    device: JobDevice | None = Field(
        default=None,
        description="Set when the first parent step starts; null before and for cached results.",
    )
    created_at: datetime
    updated_at: datetime


class DetailStemInfo(ContractModel):
    """One detail stem file of a completed detail job."""

    name: DetailStemName
    path: str = Field(description="Relative URL: /v1/detail-jobs/{id}/stems/{parent}/{name}.")
    size_bytes: Annotated[int, Field(ge=0)]


class OutcomeMetrics(ContractModel):
    """Gate and check measurements of one parent (diagnostics)."""

    parent_level_dbfs: Annotated[float, Field(le=0)]
    active_share: Annotated[float, Field(ge=0, le=1)]
    model_residual_db: float | None = None
    reconstruction_error_db: float | None = None


class ParentOutcome(ContractModel):
    """Outcome of the detail phase for one standard stem."""

    parent: StemName
    outcome: ParentOutcomeKind
    model: DetailModelId | None
    stems: Annotated[list[DetailStemInfo], Field(max_length=10)]
    metrics: OutcomeMetrics


class DetailResult(ContractModel):
    """Body of ``GET /v1/detail-jobs/{id}/result`` and content of ``detail/result.json``."""

    detail_job_id: JobId
    audio_sha256: AudioSha256
    separator_version: str
    parents: Annotated[list[ParentOutcome], Field(min_length=1, max_length=6)]
    completed_at: datetime


class DeviceInfo(ContractModel):
    """The worker's active processing device, detected once at start-up (feature 003).

    ``kind`` is ``gpu`` only when a GPU was requested (``STEMSTUDIO_DEVICE`` ``cuda``/``auto``)
    and passed the start-up probe; otherwise ``cpu`` with ``reason`` explaining why the GPU is not
    used (null when the CPU was chosen on purpose).
    """

    kind: ProcessingDeviceKind
    name: Annotated[str | None, Field(max_length=200)] = None
    compute_capability: Annotated[str | None, Field(pattern=r"^[0-9]{1,2}\.[0-9]{1,2}$")] = None
    total_memory_mib: Annotated[int | None, Field(ge=0)] = Field(
        default=None, alias="totalMemoryMiB"
    )
    free_memory_mib: Annotated[int | None, Field(ge=0)] = Field(
        default=None, alias="freeMemoryMiB", description="Free GPU memory at start-up (NVML)."
    )
    reason: Annotated[str | None, Field(max_length=500)] = None


CPU_DEVICE: Final = DeviceInfo(kind="cpu")
"""Device info of a worker that runs on the CPU on purpose (``STEMSTUDIO_DEVICE=cpu``)."""


class Health(ContractModel):
    """Body of ``GET /health``."""

    status: Literal["ok"] = "ok"
    separator: Literal["demucs", "fake"]
    device: Literal["cpu", "cuda", "auto"] = Field(
        description="Configured torch device preference."
    )
    device_info: DeviceInfo = Field(
        default=CPU_DEVICE, description="Active device detected at start-up (1.2.0)."
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
