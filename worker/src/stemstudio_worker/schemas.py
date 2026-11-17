"""Pydantic v2 models of the worker HTTP contract.

These models mirror ``specs/013-library-care/contracts/worker-api.openapi.yaml`` (1.7.0) and
are the single source of truth for JSON on the wire (camelCase aliases) and for ``result.json``
on disk. Models are immutable; use ``model.model_copy(update={...})`` to derive new states.
1.3.0 (feature 005, ``specs/005-artwork-band-info/contracts``) added artwork and metadata jobs;
1.4.0 (feature 007) added render jobs and ``Health.library`` / ``Health.lanes``; 1.5.0 (feature 006,
``specs/006-synced-lyrics-karaoke/contracts``) lyrics jobs and ``Health.lyricsAlignment``; 1.6.0
(feature 008) transcription jobs, ``Health.transcription`` and ``Health.lanes.transcribe``;
1.7.0 (feature 013, ``specs/013-library-care/contracts``) the storage report, the storage clean-up
and the rehydration of a completed standard job from library stems.
"""

import itertools
import re
from collections.abc import Mapping
from datetime import datetime
from enum import StrEnum
from typing import Annotated, Final, Literal, Self
from uuid import UUID

from pydantic import (
    AfterValidator,
    BaseModel,
    BeforeValidator,
    ConfigDict,
    Field,
    TypeAdapter,
    model_validator,
)
from pydantic.alias_generators import to_camel

CONTRACT_VERSION = "1.7.0"
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


# ------------------------------------------------------------------ lyrics jobs (1.5.0, 006)

LYRICS_UNREACHABLE_TEXT: Final = "Lyrics could not be retrieved; try again later."
"""Error of a lookup when LRCLIB stays unreachable after retries (data-model.md 2.4)."""

LYRICS_UNUSABLE_TEXT: Final = "The lyrics found online could not be read."
"""Error of a lookup whose record cannot be parsed or exceeds the limits."""

VOCAL_STEM_MISSING_TEXT: Final = (
    "The vocal stem of this track is no longer available in the worker; re-import the track."
)
"""Error of an alignment whose vocal stem is gone from the worker cache."""

VOCAL_STEM_SILENT_TEXT: Final = "The vocal stem is silent, so the lyrics cannot be aligned."
"""Error of an alignment on a silent vocal stem."""

SPEECH_MODEL_DOWNLOAD_TEXT: Final = (
    "The speech model could not be downloaded; try again when online."
)
"""Error of an alignment whose Whisper weights could not be downloaded."""

ALIGNMENT_FAILED_TEXT: Final = "The lyrics could not be aligned to the vocals."
"""Error of an alignment that failed for any other reason."""

LYRICS_STAGE_SEARCHING: Final = "Searching lyrics"
"""Stage of a running lookup."""

LYRICS_STAGE_DOWNLOADING: Final = "Downloading speech model (1.6 GB)"
"""Stage of an alignment while the Whisper weights are downloaded."""

LYRICS_STAGE_ALIGNING: Final = "Aligning lyrics"
"""Stage of a running alignment."""

ALIGNMENT_NOT_CHECKED_TEXT: Final = "The lyrics alignment has not been checked yet."
"""Default ``Health.lyricsAlignment.reason`` until the composition root reports the real state."""

MAX_LYRICS_CHARS: Final = 20_000
"""Longest lyrics text (``plainText``, align ``text``)."""

MAX_LYRICS_LINES: Final = 400
"""Most lines in one document."""

MAX_WORD_MS: Final = 3_000
"""Longest word (ms); the last word of a line is capped to it (research.md R-03)."""

LyricsJobMode = Literal["lookup", "align"]
"""``lookup`` (LRCLIB, network lane) or ``align`` (stable-ts + Whisper on the vocal stem)."""

LyricsOutcome = Literal["found", "instrumental", "not-found", "disabled"]
"""Outcome of a completed lyrics job; alignments always complete with ``found``."""

SyncLevel = Literal["word", "line", "plain"]
"""Best timing in a document; ``line`` documents carry interpolated word times."""

WordOrigin = Literal["source", "interpolated", "aligned"]
"""Where a word time comes from."""

LyricsOrigin = Literal["source", "interpolated", "aligned", "none"]
"""Dominant word origin of a document (``none`` for plain documents)."""

LyricsSource = Literal["lrclib", "user"]
"""Where the text comes from."""

LyricsLanguage = Annotated[str, Field(pattern=r"^[a-z]{2,3}$")]
"""ISO 639 language code (BCP-47 primary tag)."""

AlignModelName = Literal["large-v3-turbo", "small"]
"""Whisper model of the alignment as reported by ``/health``."""

AlignAuto = Literal["gpu", "always", "never"]
"""When an alignment starts without a click (``STEMSTUDIO_ALIGN_AUTO``)."""

Millis = Annotated[int, Field(ge=0)]
"""Non-negative time in integer milliseconds."""

EndMillis = Annotated[int, Field(ge=1)]
"""End time in integer milliseconds (always after a start >= 0)."""

_CONTROL = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")


class LyricsJobState(StrEnum):
    """Lifecycle state of a lyrics job; ``completed``, ``failed`` and ``cancelled`` are terminal."""

    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

    @property
    def is_terminal(self) -> bool:
        """Return True for ``completed``, ``failed`` and ``cancelled``."""
        return self not in (LyricsJobState.QUEUED, LyricsJobState.RUNNING)


class LyricsWord(ContractModel):
    """One word; times and origin are all None in a plain document."""

    text: Annotated[str, Field(min_length=1, max_length=100)]
    start_ms: Millis | None = None
    end_ms: EndMillis | None = None
    origin: WordOrigin | None = None
    low_confidence: bool = Field(
        default=False, description="Aligned words with probability < 0.2 or zero duration."
    )


class LyricsLine(ContractModel):
    """One line with its words; times are None in a plain document."""

    start_ms: Millis | None = None
    end_ms: EndMillis | None = None
    text: Annotated[str, Field(min_length=1, max_length=500)]
    words: Annotated[list[LyricsWord], Field(min_length=1, max_length=40)]


class TimedLyrics(ContractModel):
    """One normalised lyrics document (ADR-015 D4, data-model.md 2.1).

    The model validator enforces the invariants of data-model.md 2.1: times all set (``word``,
    ``line``) or all None (``plain``), ``end > start``, words inside their line, starts never
    decreasing, words at most 3000 ms, and no lines in an instrumental document.
    """

    sync_level: SyncLevel
    origin: LyricsOrigin
    language: LyricsLanguage | None = None
    source: LyricsSource
    source_id: Annotated[str | None, Field(max_length=64)] = Field(
        default=None, description="LRCLIB record id; None for user text."
    )
    source_duration_seconds: Annotated[float | None, Field(gt=0)] = None
    instrumental: bool = False
    lines: Annotated[list[LyricsLine], Field(max_length=MAX_LYRICS_LINES)] = []
    plain_text: Annotated[str, Field(max_length=MAX_LYRICS_CHARS)] = ""

    @model_validator(mode="after")
    def _check_invariants(self) -> Self:
        """Reject documents that break the invariants of data-model.md 2.1."""
        if self.instrumental and self.lines:
            msg = "an instrumental document has no lines"
            raise ValueError(msg)
        check = _check_plain_line if self.sync_level == "plain" else _check_timed_line
        previous = 0
        for line in self.lines:
            previous = check(line, previous)
        return self


def _span(start: int | None, end: int | None) -> tuple[int, int]:
    """Return ``(start, end)`` of a timed item; both must be set and ``end > start``."""
    if start is None or end is None:
        msg = "every line and word of a timed document needs startMs and endMs"
        raise ValueError(msg)
    if end <= start:
        msg = "endMs must be greater than startMs"
        raise ValueError(msg)
    return start, end


def _check_plain_line(line: LyricsLine, previous: int) -> int:
    """A line of a plain document has no times and no origins."""
    values: list[object] = [line.start_ms, line.end_ms]
    values += [value for word in line.words for value in (word.start_ms, word.end_ms, word.origin)]
    if any(value is not None for value in values):
        msg = "a plain document has no times"
        raise ValueError(msg)
    return previous


def _check_timed_line(line: LyricsLine, previous: int) -> int:
    """Check one line of a timed document; return the last start seen."""
    start, end = _span(line.start_ms, line.end_ms)
    if start < previous:
        msg = "starts must not decrease across the document"
        raise ValueError(msg)
    cursor = start
    for word in line.words:
        cursor = _check_timed_word(word, (start, end), cursor)
    return cursor


def _check_timed_word(word: LyricsWord, line: tuple[int, int], previous: int) -> int:
    """Check one word of a timed line; return its start."""
    start, end = _span(word.start_ms, word.end_ms)
    if word.origin is None or start < previous:
        msg = "a timed word needs an origin and starts must not decrease"
        raise ValueError(msg)
    if not _fits((start, end), line):
        msg = f"a word lies inside its line and lasts at most {MAX_WORD_MS} ms"
        raise ValueError(msg)
    return start


def _fits(word: tuple[int, int], line: tuple[int, int]) -> bool:
    """A word lies inside its line and lasts at most ``MAX_WORD_MS``."""
    return line[0] <= word[0] and word[1] <= line[1] and word[1] - word[0] <= MAX_WORD_MS


def _no_control_characters(text: str) -> str:
    """Reject control characters other than tab and line breaks."""
    if _CONTROL.search(text):
        msg = "the text must not contain control characters"
        raise ValueError(msg)
    return text


LyricsText = Annotated[
    str,
    Field(min_length=1, max_length=MAX_LYRICS_CHARS),
    AfterValidator(_no_control_characters),
]
"""Lyrics text: 1..20,000 characters without control characters (tab and line breaks allowed)."""


class LookupLyricsRequest(ContractModel):
    """Body of ``POST /v1/lyrics-jobs`` with ``mode: lookup`` (005 cleaned lookup names)."""

    mode: Literal["lookup"] = "lookup"
    title: Annotated[str, Field(min_length=1, max_length=500)]
    artist: Annotated[str | None, Field(min_length=1, max_length=500)] = None
    album: Annotated[str | None, Field(min_length=1, max_length=500)] = None
    duration_seconds: Annotated[float, Field(gt=0, le=900)]
    standard_job_id: JobId | None = Field(
        default=None, description="Only for logging and correlation."
    )


class AlignLyricsRequest(ContractModel):
    """Body of ``POST /v1/lyrics-jobs`` with ``mode: align``."""

    mode: Literal["align"] = "align"
    standard_job_id: JobId
    text: LyricsText
    language: LyricsLanguage | None = None
    refine: TimedLyrics | None = Field(
        default=None, description="A line-synced document whose line times are kept."
    )

    @model_validator(mode="after")
    def _check_refine(self) -> Self:
        """Only a line-synced document can be refined."""
        if self.refine is not None and self.refine.sync_level != "line":
            msg = "refine must be a line-synced document"
            raise ValueError(msg)
        return self


CreateLyricsJobRequest = Annotated[
    LookupLyricsRequest | AlignLyricsRequest, Field(discriminator="mode")
]
"""Body of ``POST /v1/lyrics-jobs``, selected by ``mode``."""

LYRICS_REQUEST: Final[TypeAdapter[LookupLyricsRequest | AlignLyricsRequest]] = TypeAdapter(
    CreateLyricsJobRequest
)
"""Validates a ``CreateLyricsJobRequest`` (e.g. ``LYRICS_REQUEST.validate_python(body)``)."""


class LyricsJob(ContractModel):
    """Status of a lyrics job: ``POST /v1/lyrics-jobs`` and ``GET /v1/lyrics-jobs/{id}``."""

    lyrics_job_id: JobId
    mode: LyricsJobMode
    status: LyricsJobState
    stage: Annotated[str, Field(max_length=200)]
    progress: Annotated[float, Field(ge=0, le=1)]
    device: JobDevice | None = Field(default=None, description="Align only; None for lookups.")
    estimated_seconds: Annotated[float | None, Field(ge=0)] = Field(
        default=None, description="Expected alignment time on the worker's device (align only)."
    )
    error: Annotated[str | None, Field(max_length=1000)] = None
    created_at: datetime
    updated_at: datetime


class LyricsResult(ContractModel):
    """Body of ``GET /v1/lyrics-jobs/{id}/result`` (served with ``Cache-Control: no-store``)."""

    outcome: LyricsOutcome
    lyrics: TimedLyrics | None = None

    @model_validator(mode="after")
    def _check_outcome(self) -> Self:
        """``found``/``instrumental`` carry a document (instrumental = True only for the latter)."""
        has_document = self.outcome in ("found", "instrumental")
        if (self.lyrics is not None) != has_document or (
            self.lyrics is not None and self.lyrics.instrumental != (self.outcome == "instrumental")
        ):
            msg = "found and instrumental results carry a document, the others none"
            raise ValueError(msg)
        return self


class LyricsAlignmentHealth(ContractModel):
    """``Health.lyricsAlignment``: whether alignments can run and start automatically."""

    available: bool
    model: AlignModelName = "large-v3-turbo"
    model_cached: bool = Field(default=False, description="Whisper weights are in the cache.")
    auto: AlignAuto = "gpu"
    reason: Annotated[str | None, Field(max_length=500)] = None


ALIGNMENT_NOT_CHECKED: Final = LyricsAlignmentHealth(
    available=False, reason=ALIGNMENT_NOT_CHECKED_TEXT
)
"""Default ``Health.lyricsAlignment`` until the composition root reports the real state."""


# ------------------------------------------------------------------ transcription jobs (1.6.0, 008)

NO_NOTES_TEXT: Final = "No notes were detected in this audio."
"""Error of a transcription that found no notes at all (data-model.md 2.7)."""

MUSIC_FONT_MISSING_TEXT: Final = "The music font is not installed"
"""Error of the engraver when fontconfig does not list Leipzig (SC-004)."""

PIANO_ATTRIBUTION: Final = (
    "Piano transcription model: Q. Kong et al., ByteDance, "
    "High-resolution piano transcription (CC BY 4.0)."
)
"""Credit of the piano CRNN weights (CC BY 4.0), set as ``ScoreSummary.attribution``."""

BASIC_PITCH_MODEL_ID: Final = "basic-pitch-icassp2022"
"""Model id of the basic-pitch ONNX weights (``Health.transcription.models`` key)."""

PIANO_MODEL_ID: Final = "piano-crnn-note-pedal"
"""Model id of the piano CRNN weights (``Health.transcription.models`` key)."""

ScoreProfile = Literal["vocals", "bass", "guitar", "keys", "piano", "drums"]
"""Instrument profile (data-model.md 2.1)."""

ScoreQuality = Literal["accurate", "fast"]
"""Piano only: ``accurate`` = piano CRNN, ``fast`` = basic-pitch."""

ScoreResolution = Literal["sixteenth", "eighth"]
"""Smallest note value of the quantisation grid (``eighth`` = "simplified")."""

KeyName = Literal[
    "C major", "G major", "D major", "A major", "E major", "B major", "F# major", "C# major",
    "F major", "B- major", "E- major", "A- major", "D- major", "G- major", "C- major",
    "A minor", "E minor", "B minor", "F# minor", "C# minor", "G# minor", "D# minor", "A# minor",
    "D minor", "G minor", "C minor", "F minor", "B- minor", "E- minor", "A- minor",
]  # fmt: skip
"""The 30 key signatures in music21 spelling (``-`` = flat)."""

DrumPiece = Literal["kick", "snare", "toms", "hihat", "cymbals"]
"""Drum detail stems read by the ``drums`` profile."""

TranscriptionStage = Literal["loading-model", "transcribing", "notating", "engraving"]
"""Stage of a running transcription job (progress 0-0.05, 0.05-0.80, 0.80-0.90, 0.90-1)."""

ScoreFileRole = Literal["pdf", "musicxml", "midi"]
"""File roles of a completed transcription (and of a score asset)."""

ScoreFileName = Literal["score.pdf", "score.musicxml", "score.mid"]
"""File name per role in ``transcription-jobs/<id>/``."""

ScoreContentType = Literal[
    "application/pdf", "application/vnd.recordare.musicxml+xml", "audio/midi"
]
"""Media type per role."""

SCORE_FILES: Final[dict[ScoreFileRole, tuple[ScoreFileName, ScoreContentType]]] = {
    "pdf": ("score.pdf", "application/pdf"),
    "musicxml": ("score.musicxml", "application/vnd.recordare.musicxml+xml"),
    "midi": ("score.mid", "audio/midi"),
}
"""File name and media type per role, in result order."""

ScoreWarningCode = Literal[
    "manual-tempo", "low-key-confidence", "notes-cleaned", "dense-capped", "detail-bleed",
    "cpu-piano", "few-notes", "missing-drum-piece",
]  # fmt: skip
"""Warning codes of a score (data-model.md 2.7)."""

SCORE_WARNING_TEXTS: Final[dict[ScoreWarningCode, str]] = {
    "manual-tempo": "Tempo and bars come from the values you entered.",
    "low-key-confidence": "The key is uncertain (confidence {n} %).",
    "notes-cleaned": "{n} very short or out-of-range notes were removed.",
    "dense-capped": "{n} notes were left out in very busy bars.",
    "detail-bleed": "Detail stems can contain other instruments; expect extra notes.",
    "cpu-piano": "The piano model ran on the CPU.",
    "few-notes": "Very few notes were detected.",
    "missing-drum-piece": "No {piece} stem was available; that part is empty.",
}
"""English warning texts; ``{n}`` and ``{piece}`` are filled with ``str.format``."""

ScoreEngine = Literal["basic-pitch", "piano-crnn", "drum-onsets", "fake"]
"""Transcription engine recorded in the summary."""

ModelReadiness = Literal["ready", "not-downloaded"]
"""Whether model weights are in the model cache."""

LibraryPath = Annotated[str, Field(min_length=1, max_length=300)]
"""Library-relative input path; checked by the path guard (400), not by the schema."""

Bpm = Annotated[float, Field(ge=30, le=300)]
"""Tempo in beats per minute."""

BeatsPerBar = Annotated[int, Field(ge=1, le=12)]
"""Beats per bar (time signature ``<n>/4``)."""

GridTime = Annotated[float, Field(ge=-900, le=900)]
"""Beat time relative to the input start (negative before it)."""


class TranscriptionInput(ContractModel):
    """The audio file of a pitched profile."""

    path: LibraryPath


class DrumInputs(ContractModel):
    """The drum detail stems; a missing piece is transcribed as silence (with a warning)."""

    kick: LibraryPath | None = None
    snare: LibraryPath | None = None
    toms: LibraryPath | None = None
    hihat: LibraryPath | None = None
    cymbals: LibraryPath | None = None

    @model_validator(mode="after")
    def _check_any(self) -> Self:
        """At least one piece is required."""
        if not self.pieces():
            msg = "drumInputs needs at least one piece"
            raise ValueError(msg)
        return self

    def pieces(self) -> dict[DrumPiece, str]:
        """Return the given pieces with their paths (order kick, snare, toms, hihat, cymbals)."""
        pieces: tuple[DrumPiece, ...] = ("kick", "snare", "toms", "hihat", "cymbals")
        given = {piece: getattr(self, piece) for piece in pieces}
        return {piece: path for piece, path in given.items() if path is not None}


def _ascending(values: list[float]) -> list[float]:
    """Require strictly ascending times."""
    if any(later <= earlier for earlier, later in itertools.pairwise(values)):
        msg = "times must be strictly ascending"
        raise ValueError(msg)
    return values


GridTimes = Annotated[list[GridTime], AfterValidator(_ascending)]
"""Strictly ascending beat or downbeat times."""


class BeatGridTempo(ContractModel):
    """The track's beat grid, shifted so that 0 is the input start (data-model.md 2.4)."""

    kind: Literal["beat-grid"] = "beat-grid"
    bpm: Bpm
    beats_per_bar: BeatsPerBar
    beats: Annotated[GridTimes, Field(min_length=2, max_length=10_000)]
    downbeats: Annotated[GridTimes, Field(min_length=1, max_length=10_000)]


class ManualTempo(ContractModel):
    """A constant tempo entered by the user; beats are generated over the input length."""

    kind: Literal["manual"] = "manual"
    bpm: Bpm
    beats_per_bar: BeatsPerBar
    first_downbeat_seconds: Annotated[float, Field(ge=0, le=900)]


TempoGrid = Annotated[BeatGridTempo | ManualTempo, Field(discriminator="kind")]
"""The time base of a score, selected by ``kind``."""


class ScoreTitles(ContractModel):
    """Titles written into the MusicXML and the PDF."""

    title: Annotated[str, Field(min_length=1, max_length=200)]
    subtitle: Annotated[str | None, Field(max_length=200)] = None
    instrument_name: Annotated[str | None, Field(max_length=200)] = None


class TranscriptionJobRequest(ContractModel):
    """JSON body of ``POST /v1/transcription-jobs`` (data-model.md 2.8)."""

    profile: ScoreProfile
    quality: ScoreQuality = "accurate"
    input: TranscriptionInput | None = None
    drum_inputs: DrumInputs | None = None
    range: RenderRange | None = None
    grid: TempoGrid
    resolution: ScoreResolution | None = Field(
        default=None, description="None = the profile's default."
    )
    key_override: KeyName | None = None
    titles: ScoreTitles

    @model_validator(mode="after")
    def _check_inputs(self) -> Self:
        """Pitched profiles need ``input`` only, ``drums`` needs ``drumInputs`` only."""
        drums = self.profile == "drums"
        if (self.drum_inputs is not None) != drums or (self.input is not None) == drums:
            msg = "drums needs drumInputs and no input; pitched profiles need input only"
            raise ValueError(msg)
        return self

    def input_paths(self) -> list[str]:
        """Return every library path of the request (for the path guard), in input order."""
        if self.drum_inputs is not None:
            return list(self.drum_inputs.pieces().values())
        return [self.input.path] if self.input is not None else []


class TranscriptionFile(ContractModel):
    """One written file of a completed transcription."""

    role: ScoreFileRole
    file_name: ScoreFileName
    content_type: ScoreContentType
    size_bytes: Annotated[int, Field(ge=1)]
    sha256: Sha256


class KeyInfo(ContractModel):
    """The key of a score: estimated from the notes or given as an override."""

    tonic: Annotated[str, Field(pattern=r"^[A-G](#|-)?$")]
    mode: Literal["major", "minor"]
    confidence: Annotated[float, Field(ge=0, le=1)]
    source: Literal["estimated", "override"]


class ScoreWarning(ContractModel):
    """A warning shown with the score (data-model.md 2.7)."""

    code: ScoreWarningCode
    text: Annotated[str, Field(min_length=1, max_length=300)]


class ScoreSummary(ContractModel):
    """Facts of a written score (data-model.md 2.9)."""

    profile: ScoreProfile
    engine: ScoreEngine
    quality: ScoreQuality | None = Field(default=None, description="Set for piano only.")
    key: KeyInfo | None = Field(default=None, description="None for drums.")
    bpm: Annotated[int, Field(ge=30, le=300)]
    time_signature: Annotated[str, Field(pattern=r"^([1-9]|1[0-2])/4$")]
    bars: Annotated[int, Field(ge=1)]
    notes: Annotated[int, Field(ge=1)]
    pages: Annotated[int, Field(ge=1)]
    resolution: ScoreResolution
    warnings: Annotated[list[ScoreWarning], Field(max_length=8)] = []
    attribution: Annotated[str | None, Field(max_length=300)] = None


class TranscriptionResult(ContractModel):
    """The three files and the summary of a completed transcription."""

    files: Annotated[list[TranscriptionFile], Field(min_length=3, max_length=3)]
    summary: ScoreSummary

    @model_validator(mode="after")
    def _check_files(self) -> Self:
        """Files come in the order pdf, musicxml, midi with their fixed names and media types."""
        actual = [(f.role, f.file_name, f.content_type) for f in self.files]
        expected = [(role, *facts) for role, facts in SCORE_FILES.items()]
        if actual != expected:
            msg = "files must be pdf, musicxml, midi with their fixed names and media types"
            raise ValueError(msg)
        return self


class TranscriptionJob(ContractModel):
    """Status of a transcription job: ``POST`` and ``GET /v1/transcription-jobs/{id}``."""

    transcription_job_id: JobId
    status: RenderJobStatus
    stage: TranscriptionStage | None = Field(
        default=None, description="None while queued and after the job ended."
    )
    progress: Annotated[float, Field(ge=0, le=1)]
    device: JobDevice | None = None
    error: Annotated[str | None, Field(max_length=500)] = None
    result: TranscriptionResult | None = None
    created_at: datetime
    updated_at: datetime


class TranscriptionModels(ContractModel):
    """Readiness of the two transcription models (keys are the model ids)."""

    basic_pitch: ModelReadiness = Field(default="not-downloaded", alias=BASIC_PITCH_MODEL_ID)
    piano: ModelReadiness = Field(default="not-downloaded", alias=PIANO_MODEL_ID)


class FontHealth(ContractModel):
    """Whether fontconfig lists the Leipzig music font."""

    leipzig: bool = False


class TranscriptionHealth(ContractModel):
    """``Health.transcription``: model and font readiness (1.6.0)."""

    models: TranscriptionModels = TranscriptionModels()
    font: FontHealth = FontHealth()


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
    """Queue state of the lanes beside the separation queue (render 1.4.0, transcribe 1.6.0)."""

    render: LaneHealth = LaneHealth()
    transcribe: LaneHealth = LaneHealth()


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
    lanes: LanesHealth = Field(default=LanesHealth(), description="Lane states (1.4.0, 1.6.0).")
    lyrics_alignment: LyricsAlignmentHealth = Field(
        default=ALIGNMENT_NOT_CHECKED, description="Lyrics alignment readiness (1.5.0)."
    )
    transcription: TranscriptionHealth = Field(
        default=TranscriptionHealth(), description="Transcription readiness (1.6.0)."
    )
    contract_version: str = CONTRACT_VERSION


# ------------------------------------------------------------------ storage (1.7.0, feature 013)

MODEL_STEMS: Final[Mapping[ModelName, frozenset[StemName]]] = {
    "htdemucs_6s": frozenset({"vocals", "drums", "bass", "guitar", "piano", "other"}),
    "htdemucs": frozenset({"vocals", "drums", "bass", "other"}),
    "fake": frozenset({"vocals", "drums", "bass", "guitar", "piano", "other"}),
}
"""Stem set of every separation model (``ModelName``); a rehydrate request must match it."""

_LOWER_UUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
ByteCount = Annotated[int, Field(ge=0)]
"""A size or count in bytes (non-negative)."""


def _lower_d_uuid(value: object) -> object:
    """Accept a UUID only in its lower-case ``D`` format (or an existing ``UUID``)."""
    if isinstance(value, str) and _LOWER_UUID.fullmatch(value):
        return value
    if isinstance(value, UUID):
        return value
    msg = "must be a lower-case UUID in D format"
    raise ValueError(msg)


def _unique(values: list[StemName]) -> list[StemName]:
    """Reject repeated stems."""
    if len(set(values)) != len(values):
        msg = "stems must be unique"
        raise ValueError(msg)
    return values


TrackUuid = Annotated[UUID, BeforeValidator(_lower_d_uuid)]
"""Backend track id; lower-case ``D`` format on the wire."""


class VolumeUsage(ContractModel):
    """Free and total bytes of a worker volume (``data`` or ``models``)."""

    id: Literal["data", "models"]
    free_bytes: ByteCount
    total_bytes: ByteCount


class CacheEntryUsage(ContractModel):
    """One per-audio cache entry (``cache/by-audio/<sha>/``) with its sizes and users."""

    audio_sha256: Sha256
    standard_bytes: ByteCount = Field(description="result.json and stems/.")
    detail_bytes: ByteCount = Field(description="detail/ (finished detail result).")
    partial_bytes: ByteCount = Field(description="detail.partial/ (checkpoints).")
    artwork_bytes: ByteCount
    job_ids: list[JobId] = Field(description="Standard jobs whose audio is this entry.")
    in_use: bool = Field(description="A queued or running job uses it; clean-up skips it.")
    from_library: bool = Field(description="Created by POST /v1/jobs/rehydrate.")
    last_used_at: datetime | None


class ModelCacheUsage(ContractModel):
    """Size of one top-level folder under ``STEMSTUDIO_MODEL_DIR`` and the known models in it."""

    id: Annotated[str, Field(pattern=r"^[A-Za-z0-9._-]{1,64}$")]
    bytes: ByteCount
    models: list[str]


class WorkerStorage(ContractModel):
    """Body of ``GET /v1/storage``: disk use of the worker data and model volumes."""

    measured_at: datetime
    volumes: list[VolumeUsage]
    cache_entries: list[CacheEntryUsage]
    job_files_bytes: ByteCount
    job_files_count: ByteCount
    scratch_bytes: ByteCount = Field(description="work/, uploads/ and artwork/.")
    model_caches: list[ModelCacheUsage]


CacheEntrySelector = Annotated[str, Field(pattern=r"^([0-9a-f]{64}|\*)$")]
"""An ``audioSha256`` value or the wildcard ``*`` (only as the single element)."""

ALL_CACHE_ENTRIES: Final = "*"
"""Wildcard of ``StorageCleanupRequest.cacheEntries``: every entry not in use."""


class StorageCleanupRequest(ContractModel):
    """Body of ``POST /v1/storage/cleanup``; the defaults remove nothing."""

    cache_entries: Annotated[list[CacheEntrySelector], Field(max_length=100_000)] = Field(
        default_factory=list
    )
    scratch: bool = False
    old_job_files: bool = False

    @model_validator(mode="after")
    def _wildcard_alone(self) -> Self:
        """``*`` is only allowed as ``["*"]``."""
        if ALL_CACHE_ENTRIES in self.cache_entries and len(self.cache_entries) != 1:
            msg = 'cacheEntries must be exactly ["*"] or a list of audioSha256 values'
            raise ValueError(msg)
        return self

    @property
    def all_entries(self) -> bool:
        """True for ``["*"]`` (every entry not in use)."""
        return self.cache_entries == [ALL_CACHE_ENTRIES]


class SkippedEntry(ContractModel):
    """A requested cache entry that was not removed, with the reason."""

    audio_sha256: Sha256
    reason: Literal["inUse", "notFound"]


class StorageCleanupResult(ContractModel):
    """Body of the ``POST /v1/storage/cleanup`` response."""

    freed_bytes: ByteCount
    removed_entries: list[Sha256]
    skipped: list[SkippedEntry]


class RehydrateRequest(ContractModel):
    """Body of ``POST /v1/jobs/rehydrate``: re-create a standard job from library stems."""

    track_id: TrackUuid
    source_type: SourceType
    source_key: Annotated[str, Field(pattern=r"^(youtube|spotify|upload):[A-Za-z0-9_-]{1,64}$")]
    model: ModelName
    stems: Annotated[list[StemName], Field(min_length=4, max_length=6), AfterValidator(_unique)]

    def stems_match_model(self) -> bool:
        """True when ``stems`` equals the model's stem set (otherwise the route answers 422)."""
        return frozenset(self.stems) == MODEL_STEMS[self.model]


class RehydratedJob(ContractModel):
    """Body of the ``POST /v1/jobs/rehydrate`` response (201 created, 200 existing)."""

    job_id: JobId
    audio_sha256: Sha256
    status: Literal["completed"] = "completed"
    model: ModelName
    stems: list[StemName]
    created_at: datetime


class ProblemDetails(BaseModel):
    """RFC 9457 problem details (``application/problem+json``) for every error response."""

    model_config = ConfigDict(extra="allow", frozen=True)

    type: str = "about:blank"
    title: str
    status: Annotated[int, Field(ge=400, le=599)]
    detail: str | None = None
    instance: str | None = None
