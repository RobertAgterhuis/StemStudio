"""Pydantic v2 models of the worker HTTP contract.

These models mirror ``specs/007-export-library/contracts/worker-api.openapi.yaml`` (1.4.0) and
are the single source of truth for JSON on the wire (camelCase aliases) and for ``result.json``
on disk. Models are immutable; use ``model.model_copy(update={...})`` to derive new states.
1.3.0 (feature 005, ``specs/005-artwork-band-info/contracts``) added artwork and metadata jobs;
1.4.0 (feature 007) added render jobs and ``Health.library`` / ``Health.lanes``.
"""

from datetime import datetime
from enum import StrEnum
from typing import Annotated, Final, Literal, Self

from pydantic import BaseModel, ConfigDict, Field, model_validator
from pydantic.alias_generators import to_camel

CONTRACT_VERSION = "1.4.0"
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

Sha256 = Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
"""SHA-256 digest: 64 lower-case hex characters."""

Isrc = Annotated[str, Field(pattern=r"^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$")]
"""International Standard Recording Code (feature 005)."""

Mbid = Annotated[
    str, Field(pattern=r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
]
"""MusicBrainz identifier (lower-case UUID)."""


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
    """Descriptive metadata of the source audio (1.3.0 adds the lookup fields of feature 005)."""

    title: Annotated[str, Field(min_length=1, max_length=500)]
    artist: Annotated[str | None, Field(max_length=500)] = None
    source_url: str | None = Field(
        default=None, description="The YouTube URL the audio was fetched from (null for uploads)."
    )
    album: Annotated[str | None, Field(max_length=500)] = None
    isrc: Isrc | None = Field(default=None, description="Spotify sources only.")
    duration_seconds: Annotated[float | None, Field(gt=0, le=900)] = Field(
        default=None, description="Set after normalisation; null before."
    )
    lookup_title: Annotated[str | None, Field(max_length=500)] = Field(
        default=None, description="Cleaned title for the metadata lookup (research R-04)."
    )
    lookup_artist: Annotated[str | None, Field(max_length=500)] = Field(
        default=None, description="Cleaned artist for the metadata lookup."
    )


ArtworkSource = Literal["spotify", "embedded", "youtube", "coverartarchive", "user"]
"""Where a picture came from (feature 005, data-model.md 2.2)."""

ArtworkVariant = Literal["cover", "thumb"]
"""The two stored JPEG sizes: ``cover`` (600 px) and ``thumb`` (160 px)."""

COVER_MAX_BYTES: Final = 204_800
"""Largest normalised cover JPEG (200 KB)."""

THUMB_MAX_BYTES: Final = 20_480
"""Largest normalised thumbnail JPEG (20 KB)."""


class ArtworkInfo(ContractModel):
    """A normalised picture: ``Job.artwork``, ``MetadataResult.artwork``, ``ArtworkUpload.info``."""

    source: ArtworkSource
    sha256: Sha256 = Field(description="SHA-256 of the cover JPEG bytes; the artwork version.")
    cover_bytes: Annotated[int, Field(ge=1, le=COVER_MAX_BYTES)]
    thumb_bytes: Annotated[int, Field(ge=1, le=THUMB_MAX_BYTES)]
    origin_url: Annotated[str | None, Field(max_length=2048)] = Field(
        default=None, description="Remote image URL; null for embedded and user pictures."
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
    artwork: ArtworkInfo | None = Field(
        default=None,
        description="Source picture, set after the download step; null without one (1.3.0).",
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


# ------------------------------------------------------------------ metadata jobs (1.3.0, 005)

METADATA_FAILED_TEXT: Final = "Band information could not be retrieved; try again later."
"""Error of a metadata job whose recording search failed after retries (data-model.md 2.7)."""

WIKIPEDIA_WARNING_TEXT: Final = "Wikipedia could not be reached."
"""Warning when the Wikipedia step failed (the job still completes)."""

COVER_WARNING_TEXT: Final = "The album cover could not be loaded."
"""Warning when the Cover Art Archive picture could not be downloaded or decoded."""

WIKIPEDIA_LICENCE: Final = "CC BY-SA 4.0"
"""Licence of Wikipedia excerpts (shown with full attribution)."""

WIKIPEDIA_LICENCE_URL: Final = "https://creativecommons.org/licenses/by-sa/4.0/"
"""Licence URL of Wikipedia excerpts."""

MetadataOutcome = Literal["found", "not-found", "disabled"]
"""Outcome of a completed metadata job; ``disabled`` when online lookups are off."""

ArtistType = Literal["group", "person", "orchestra", "choir", "character", "other"]
"""MusicBrainz artist type."""

Year = Annotated[int, Field(ge=1000, le=2999)]
"""A calendar year."""

HttpsUrl = Annotated[str, Field(pattern=r"^https://", max_length=2048)]
"""An absolute https URL."""


class MetadataJobState(StrEnum):
    """Lifecycle state of a metadata job; ``completed`` and ``failed`` are terminal."""

    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"

    @property
    def is_terminal(self) -> bool:
        """Return True for ``completed`` and ``failed``."""
        return self in (MetadataJobState.COMPLETED, MetadataJobState.FAILED)


class CreateMetadataJobRequest(ContractModel):
    """JSON body of ``POST /v1/metadata-jobs`` (names as the backend wants them looked up)."""

    title: Annotated[str, Field(min_length=1, max_length=500)]
    artist: Annotated[str | None, Field(min_length=1, max_length=500)] = None
    album: Annotated[str | None, Field(min_length=1, max_length=500)] = None
    duration_seconds: Annotated[float | None, Field(gt=0, le=900)] = None
    isrc: Isrc | None = None
    standard_job_id: JobId | None = Field(
        default=None, description="Only for logging and correlation."
    )


class MetadataJob(ContractModel):
    """Status of a metadata job: ``POST /v1/metadata-jobs`` and ``GET /v1/metadata-jobs/{id}``."""

    metadata_job_id: JobId
    status: MetadataJobState
    stage: Annotated[str, Field(max_length=200)]
    error: Annotated[str | None, Field(max_length=1000)] = None
    created_at: datetime
    updated_at: datetime


class RecordingMatch(ContractModel):
    """The MusicBrainz recording the lookup matched."""

    mbid: Mbid
    title: Annotated[str, Field(min_length=1, max_length=500)]
    length_seconds: Annotated[float | None, Field(gt=0)] = None
    score: Annotated[int, Field(ge=0, le=100)]
    matched_by: Literal["isrc", "search"]


class ReleaseGroupInfo(ContractModel):
    """The release group (album) of the matched recording: earliest official album."""

    mbid: Mbid
    title: Annotated[str, Field(min_length=1, max_length=500)]
    primary_type: Annotated[str | None, Field(max_length=50)] = None
    first_release_year: Year | None = None


class ArtistMember(ContractModel):
    """A member of a group (Wikidata P527 with qualifiers P580/P582)."""

    name: Annotated[str, Field(min_length=1, max_length=500)]
    from_year: Year | None = None
    to_year: Year | None = None


class WikipediaSummary(ContractModel):
    """A Wikipedia excerpt with the data its CC BY-SA 4.0 attribution needs (data-model.md 2.6)."""

    language: Annotated[str, Field(pattern=r"^[a-z]{2,3}$")]
    title: Annotated[str, Field(min_length=1, max_length=500)]
    extract: Annotated[str, Field(min_length=1, max_length=2000)]
    page_url: HttpsUrl
    history_url: HttpsUrl
    revision_id: Annotated[str, Field(min_length=1, max_length=50)]
    licence: Literal["CC BY-SA 4.0"] = WIKIPEDIA_LICENCE
    licence_url: Literal["https://creativecommons.org/licenses/by-sa/4.0/"] = WIKIPEDIA_LICENCE_URL


class ArtistInfo(ContractModel):
    """Artist facts from MusicBrainz, Wikidata and Wikipedia (data-model.md 2.5)."""

    mbid: Mbid
    name: Annotated[str, Field(min_length=1, max_length=500)]
    type: ArtistType | None = None
    country: Annotated[str | None, Field(pattern=r"^[A-Z]{2}$")] = None
    begin_year: Year | None = None
    end_year: Year | None = Field(default=None, description="Null while the artist is active.")
    genres: Annotated[list[Annotated[str, Field(max_length=100)]], Field(max_length=8)] = []
    members: Annotated[list[ArtistMember], Field(max_length=12)] = []
    website: HttpsUrl | None = None
    wikidata_id: Annotated[str | None, Field(pattern=r"^Q[0-9]+$")] = None
    summary: WikipediaSummary | None = None
    fetched_at: datetime


class MetadataResult(ContractModel):
    """Body of ``GET /v1/metadata-jobs/{id}/result`` (data-model.md 2.4)."""

    outcome: MetadataOutcome
    recording: RecordingMatch | None = None
    release_group: ReleaseGroupInfo | None = None
    artwork: ArtworkInfo | None = Field(
        default=None, description="Cover Art Archive front of the release group."
    )
    artist: ArtistInfo | None = None
    warnings: Annotated[list[Annotated[str, Field(max_length=500)]], Field(max_length=5)] = []


class ArtworkUpload(ContractModel):
    """Body of ``POST /v1/artwork`` (201): a user picture normalised and kept for one hour."""

    artwork_id: JobId
    info: ArtworkInfo


# ------------------------------------------------------------------ render jobs (1.4.0, 007)

LIBRARY_UNAVAILABLE_TEXT: Final = "The library is not available to the worker"
"""Render error and ``Health.library.reason`` when ``STEMSTUDIO_LIBRARY_DIR`` is unusable."""

RESTART_INTERRUPTED_TEXT: Final = "interrupted by restart"
"""Error of jobs that were not terminal when the worker restarted (ADR-003)."""

SILENT_RENDER_TEXT: Final = "Nothing to export: the selected channels are silent"
"""Error of a render whose peak is 0 (data-model.md 3)."""

MIN_RENDER_RANGE_SECONDS: Final = 0.5
"""Shortest render range in seconds."""

RenderFormat = Literal["flac", "wav", "mp3"]
"""Output container of a render."""

RenderContentType = Literal["audio/flac", "audio/wav", "audio/mpeg"]
"""Media type of a render output file."""

RENDER_CONTENT_TYPES: Final[dict[RenderFormat, RenderContentType]] = {
    "flac": "audio/flac",
    "wav": "audio/wav",
    "mp3": "audio/mpeg",
}
"""Media type per output format."""


class RenderInput(ContractModel):
    """One input of a render: a library file with its gain and pan.

    ``path`` is relative to the library root and must match one of the allow-list patterns
    (``ports.LIBRARY_PATH_PATTERNS``). That check belongs to the path guard, which answers
    ``400 invalid-input-path`` (not a 422 validation error), so the schema only bounds the length.
    """

    path: Annotated[str, Field(min_length=1, max_length=300)]
    gain: Annotated[float, Field(ge=0, le=1)]
    pan: Annotated[float, Field(ge=-1, le=1)]


class RenderRange(ContractModel):
    """Part of the inputs to render; ``end`` beyond the longest input is clamped to it."""

    start_seconds: Annotated[float, Field(ge=0)]
    end_seconds: Annotated[float, Field(gt=0)]

    @model_validator(mode="after")
    def _check_length(self) -> Self:
        """Require ``end - start >= 0.5`` (which also implies ``start < end``)."""
        if self.end_seconds - self.start_seconds < MIN_RENDER_RANGE_SECONDS:
            msg = "endSeconds must be at least 0.5 s after startSeconds"
            raise ValueError(msg)
        return self


class RenderOutput(ContractModel):
    """Output format; ``bit_depth`` is ignored for MP3, ``mp3_bitrate_kbps`` used only for MP3."""

    format: RenderFormat
    bit_depth: Literal[16, 24] = 24
    mp3_bitrate_kbps: Literal[192, 256, 320] = Field(default=320, alias="mp3BitrateKbps")
    dither: bool = Field(
        default=True, description="TPDF dither; false for bit-identical raw single-input renders."
    )


class RenderMetadata(ContractModel):
    """Tags written into WAV/FLAC outputs (MP3 gets none); control characters are stripped."""

    title: Annotated[str | None, Field(max_length=200)] = None
    artist: Annotated[str | None, Field(max_length=200)] = None
    comment: Annotated[str | None, Field(max_length=200)] = None


class RenderJobRequest(ContractModel):
    """JSON body of ``POST /v1/render-jobs`` (data-model.md 2.1)."""

    inputs: Annotated[list[RenderInput], Field(min_length=1, max_length=64)]
    master_gain: Annotated[float, Field(ge=0, le=1)]
    range: RenderRange | None = None
    output: RenderOutput
    metadata: RenderMetadata = RenderMetadata()


class RenderJobStatus(StrEnum):
    """Lifecycle state of a render job; every state but ``queued``/``running`` is terminal."""

    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

    @property
    def is_terminal(self) -> bool:
        """Return True for ``completed``, ``failed`` and ``cancelled``."""
        return self not in (RenderJobStatus.QUEUED, RenderJobStatus.RUNNING)


class RenderResult(ContractModel):
    """The written file of a completed render job."""

    file_name: Annotated[str, Field(pattern=r"^output\.(flac|wav|mp3)$")]
    content_type: RenderContentType
    size_bytes: Annotated[int, Field(ge=1)]
    sha256: Sha256
    duration_seconds: Annotated[float, Field(gt=0, le=900)]
    sample_rate: Literal[44100] = 44100
    channels: Literal[2] = 2
    peak_dbfs: Annotated[float, Field(le=0)] = Field(
        description="Peak of the written samples (before encoding for MP3)."
    )
    applied_gain_db: Annotated[float, Field(le=0)] = Field(
        description="Clip protection: 0, or the negative gain that kept the peak at -0.1 dBFS."
    )


class RenderJob(ContractModel):
    """Status of a render job: ``POST /v1/render-jobs`` and ``GET /v1/render-jobs/{id}``."""

    render_job_id: JobId
    status: RenderJobStatus
    progress: Annotated[float, Field(ge=0, le=1)] = Field(
        description="Pass 1 (peak) is 0..0.5, pass 2 (write) 0.5..1."
    )
    error: Annotated[str | None, Field(max_length=500)] = None
    result: RenderResult | None = None
    created_at: datetime
    updated_at: datetime


class LibraryHealth(ContractModel):
    """Whether the worker can read the API library (read-only mount, ADR-016 D5)."""

    readable: bool
    reason: Annotated[str | None, Field(max_length=500)] = None


LIBRARY_NOT_CHECKED: Final = LibraryHealth(readable=False, reason=LIBRARY_UNAVAILABLE_TEXT)
"""Default ``Health.library`` until the composition root reports the real state."""


class LaneHealth(ContractModel):
    """Queue state of one worker job lane."""

    queued: Annotated[int, Field(ge=0)] = 0
    running: bool = False


class LanesHealth(ContractModel):
    """Queue state of the lanes beside the separation queue (1.4.0: the render lane)."""

    render: LaneHealth = LaneHealth()


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
    online_lookups: bool = Field(
        default=True, description="False when STEMSTUDIO_ONLINE_LOOKUPS=off (1.3.0)."
    )
    library: LibraryHealth = Field(
        default=LIBRARY_NOT_CHECKED, description="Read access to the API library (1.4.0)."
    )
    lanes: LanesHealth = Field(default=LanesHealth(), description="Render lane state (1.4.0).")
    contract_version: str = CONTRACT_VERSION


class ProblemDetails(BaseModel):
    """RFC 9457 problem details (``application/problem+json``) for every error response."""

    model_config = ConfigDict(extra="allow", frozen=True)

    type: str = "about:blank"
    title: str
    status: Annotated[int, Field(ge=400, le=599)]
    detail: str | None = None
    instance: str | None = None
