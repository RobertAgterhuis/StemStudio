"""Pydantic v2 models of the worker HTTP contract.

These models mirror ``specs/020-insights-handouts/contracts/worker-api.openapi.yaml`` (1.11.0) and
are the single source of truth for JSON on the wire (camelCase aliases) and for ``result.json``
on disk. Models are immutable; use ``model.model_copy(update={...})`` to derive new states.
1.3.0 (feature 005, ``specs/005-artwork-band-info/contracts``) added artwork and metadata jobs;
1.4.0 (feature 007) added render jobs and ``Health.library`` / ``Health.lanes``; 1.5.0 (feature 006,
``specs/006-synced-lyrics-karaoke/contracts``) lyrics jobs and ``Health.lyricsAlignment``; 1.6.0
(feature 008) transcription jobs, ``Health.transcription`` and ``Health.lanes.transcribe``;
1.7.0 (feature 013, ``specs/013-library-care/contracts``) the storage report, the storage clean-up
and the rehydration of a completed standard job from library stems; 1.8.0 (feature 014,
``specs/014-practice-flow/contracts``) section, score-view and setlist document jobs on the
``practice`` lane, ``Health.lanes.practice`` and ``Health.structure``; 1.9.0 (feature 016,
``specs/016-instrument-views/contracts``) instrument analysis jobs (chords with the key, drum hits,
reference melody) on the ``instrument-analysis`` lane, tablature, parts and chord symbols in scores;
1.11.0 (feature 020, ``specs/020-insights-handouts/contracts``) quality and handout jobs on the
``insights`` lane, ``Health.insights`` and ``Health.lanes.insights``.
"""

import itertools
import re
from collections.abc import Mapping
from datetime import date, datetime
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

CONTRACT_VERSION = "1.11.0"
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

ScoreProfile = Literal[
    "vocals", "bass", "guitar", "keys", "piano", "drums", "strings", "brass", "woodwinds",
]  # fmt: skip
"""Instrument profile (008 data-model 2.1; ``strings``, ``brass``, ``woodwinds``: 1.9.0)."""

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
    "cpu-piano", "few-notes", "missing-drum-piece", "tab-octave-moved", "tab-notes-dropped",
    "tab-wide-stretch", "part-out-of-range", "section-reduced",
]  # fmt: skip
"""Warning codes of a score (008 data-model.md 2.7; the last five 1.9.0, 016 data-model 4.2-4.5)."""

SCORE_WARNING_TEXTS: Final[dict[ScoreWarningCode, str]] = {
    "manual-tempo": "Tempo and bars come from the values you entered.",
    "low-key-confidence": "The key is uncertain (confidence {n} %).",
    "notes-cleaned": "{n} very short or out-of-range notes were removed.",
    "dense-capped": "{n} notes were left out in very busy bars.",
    "detail-bleed": "Detail stems can contain other instruments; expect extra notes.",
    "cpu-piano": "The piano model ran on the CPU.",
    "few-notes": "Very few notes were detected.",
    "missing-drum-piece": "No {piece} stem was available; that part is empty.",
    "tab-octave-moved": "{n} notes were moved by an octave to fit the tuning.",
    "tab-notes-dropped": "{n} chord notes were left out because there are not enough strings.",
    "tab-wide-stretch": "{n} chords need a wide stretch.",
    "part-out-of-range": "{n} notes are outside the usual range of the instrument.",
    "section-reduced": "This stem may hold a whole section; the part shows its loudest line.",
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

_DOWNBEAT_TOLERANCE: Final = 1e-3
"""A downbeat this far before the input start still starts bar 1 (the API rounds shifts)."""


class BeatGridTempo(ContractModel):
    """The track's beat grid, shifted so that 0 is the input start (data-model.md 2.4)."""

    kind: Literal["beat-grid"] = "beat-grid"
    bpm: Bpm
    beats_per_bar: BeatsPerBar
    beats: Annotated[GridTimes, Field(min_length=2, max_length=10_000)]
    downbeats: Annotated[GridTimes, Field(min_length=1, max_length=10_000)]

    @property
    def bar_one_downbeat(self) -> float:
        """The downbeat that starts bar 1: the first one at or after the input start.

        A shifted grid keeps the track's downbeats before a range start (negative); bar 1 of the
        range is its first downbeat inside it (a step of the API's 6-decimal rounding before 0
        still counts), so no empty bars precede the range. Without a downbeat inside the input
        it is the last downbeat.
        """
        inside = (d for d in self.downbeats if d >= -_DOWNBEAT_TOLERANCE)
        return next(inside, self.downbeats[-1])


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


# ---------------------------------------------------- score pipeline additions (1.9.0, feature 016)

TuningId = Literal[
    "guitar-standard", "guitar-drop-d", "guitar-half-step-down", "guitar-full-step-down",
    "guitar-dadgad", "guitar-open-g", "guitar-open-d", "bass-4-standard", "bass-4-drop-d",
    "bass-4-half-step-down", "bass-5-standard", "bass-6-standard",
]  # fmt: skip
"""Tuning catalogue ids (data-model 4.1; pitches in ``ports.TUNINGS``)."""

TabLayout = Literal["notation-and-tab", "tab-only"]
"""Tablature layout: a notation staff plus a TAB staff, or the TAB staff only."""

WrittenInstrument = Literal[
    "concert", "trumpet-bb", "clarinet-bb", "soprano-sax-bb", "tenor-sax-bb", "bass-clarinet-bb",
    "alto-sax-eb", "baritone-sax-eb", "clarinet-eb", "horn-f", "english-horn-f", "flute", "oboe",
    "violin", "viola", "cello", "double-bass", "trombone", "bassoon", "tuba",
]  # fmt: skip
"""Written instrument catalogue ids (data-model 4.2; facts in ``ports.WRITTEN_INSTRUMENTS``)."""

ClefChoice = Literal["auto", "treble", "bass", "alto", "tenor", "treble-8vb", "bass-8vb"]
"""Clef of a part (data-model 4.3); ``auto`` = the instrument's default clef."""

WrittenClef = Literal["treble", "bass", "alto", "tenor", "treble-8vb", "bass-8vb"]
"""The clef a part was written with (``PartSummary.clef``)."""

PitchClass = Annotated[int, Field(ge=0, le=11)]
"""Pitch class, C = 0 ... B = 11."""

ChordQuality = Literal[
    "maj", "min", "dim", "aug", "min6", "maj6", "min7", "minmaj7", "maj7", "7", "dim7", "hdim7",
    "sus2", "sus4",
]  # fmt: skip
"""Chord qualities of the ``full`` vocabulary (data-model 2.1); ``simple`` uses ``maj``/``min``."""

TAB_PROFILES: Final[frozenset[str]] = frozenset({"guitar", "bass"})
"""Profiles that accept ``tablature``."""

CHORD_SYMBOL_PROFILES: Final[frozenset[str]] = frozenset(
    {"vocals", "guitar", "keys", "piano", "strings", "brass", "woodwinds"}
)
"""Profiles that accept ``chords`` (data-model 4.7)."""

GRAND_STAFF_PROFILES: Final[frozenset[str]] = frozenset({"keys", "piano"})
"""Profiles whose clef cannot be chosen (``clef`` must be ``auto``)."""


class TablatureSettings(ContractModel):
    """``TranscriptionJobRequest.tablature``: guitar/bass only; capo only for guitar tunings."""

    tuning: TuningId
    capo: Annotated[int, Field(ge=0, le=12)] = 0
    layout: TabLayout = "notation-and-tab"


class PartSettings(ContractModel):
    """``TranscriptionJobRequest.part``: written instrument and clef (data-model 4.6)."""

    instrument: WrittenInstrument
    clef: ClefChoice = "auto"


class ScoreChordSymbol(ContractModel):
    """A chord symbol above the top staff; pitch classes at concert pitch (data-model 4.7)."""

    start_ms: Annotated[int, Field(ge=0, description="Relative to the transcription input start.")]
    root: PitchClass
    quality: ChordQuality
    bass: PitchClass | None = None


class TablatureSummary(ContractModel):
    """``ScoreSummary.tablature`` (data-model 4.5)."""

    tuning: TuningId
    strings: Annotated[int, Field(ge=4, le=6)]
    capo: Annotated[int, Field(ge=0, le=12)]
    layout: TabLayout
    octave_moved: Annotated[int, Field(ge=0)] = 0
    notes_dropped: Annotated[int, Field(ge=0)] = 0
    wide_stretches: Annotated[int, Field(ge=0)] = 0


class PartSummary(ContractModel):
    """``ScoreSummary.part`` (data-model 4.6)."""

    instrument: WrittenInstrument
    name: Annotated[str, Field(min_length=1, max_length=80)]
    interval: Annotated[str, Field(pattern=r"^-?[PMm]\d{1,2}$")]
    written_key: KeyName | None = None
    clef: WrittenClef
    out_of_range: Annotated[int, Field(ge=0)] = 0


def _is_none(value: object) -> bool:
    """``exclude_if`` of the optional 1.9.0 request fields: absent = 008 request, byte-identical."""
    return value is None


def _tablature_problem(profile: str, tablature: TablatureSettings | None) -> str | None:
    """The 422 reason of a ``tablature`` setting, or None."""
    if tablature is None:
        return None
    if profile not in TAB_PROFILES:
        return "tablature is for guitar and bass only"
    if not tablature.tuning.startswith(f"{profile}-"):
        return "tuning does not match the profile"
    return "capo is for guitar tunings only" if tablature.capo > 0 and profile != "guitar" else None


def _part_problem(profile: str, part: PartSettings | None) -> str | None:
    """The 422 reason of a ``part`` setting, or None."""
    if part is None:
        return None
    if profile == "drums":
        return "part is not allowed for drums"
    return (
        "clef is not allowed for keys and piano"
        if part.clef != "auto" and profile in GRAND_STAFF_PROFILES
        else None
    )


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
    tablature: TablatureSettings | None = Field(default=None, exclude_if=_is_none)
    part: PartSettings | None = Field(default=None, exclude_if=_is_none)
    chords: Annotated[list[ScoreChordSymbol] | None, Field(max_length=4000)] = Field(
        default=None, exclude_if=_is_none
    )
    prefer_flats: bool | None = Field(default=None, exclude_if=_is_none)
    single_piece: DrumPiece | None = Field(default=None, exclude_if=_is_none)

    @model_validator(mode="after")
    def _check_inputs(self) -> Self:
        """Pitched profiles need ``input`` only, ``drums`` needs ``drumInputs`` only."""
        drums = self.profile == "drums"
        if (self.drum_inputs is not None) != drums or (self.input is not None) == drums:
            msg = "drums needs drumInputs and no input; pitched profiles need input only"
            raise ValueError(msg)
        return self

    @model_validator(mode="after")
    def _check_extensions(self) -> Self:
        """The 1.9.0 combinations rejected with 422 (016 DELTA 1)."""
        problem = (
            _tablature_problem(self.profile, self.tablature)
            or _part_problem(self.profile, self.part)
            or self._chords_problem()
            or self._single_piece_problem()
        )
        if problem is not None:
            raise ValueError(problem)
        return self

    def _chords_problem(self) -> str | None:
        """Chord symbols only for the profiles of data-model 4.7."""
        allowed = self.chords is None or self.profile in CHORD_SYMBOL_PROFILES
        return None if allowed else "chords are not allowed for this profile"

    def _single_piece_problem(self) -> str | None:
        """``singlePiece`` only with ``drums`` and ``drumInputs`` holding exactly that piece."""
        if self.single_piece is None:
            return None
        pieces = list(self.drum_inputs.pieces()) if self.drum_inputs is not None else []
        return (
            None if pieces == [self.single_piece] else "singlePiece needs exactly that drum piece"
        )

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
    tablature: TablatureSummary | None = None
    part: PartSummary | None = None
    chords_included: bool = False


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
    """Lanes beside the separation queue (1.4.0, 1.6.0, 1.8.0, 1.9.0, insights 1.11.0)."""

    render: LaneHealth = LaneHealth()
    transcribe: LaneHealth = LaneHealth()
    practice: LaneHealth = LaneHealth()
    instrument_analysis: LaneHealth = LaneHealth()
    insights: LaneHealth = LaneHealth()


# ------------------------------------------------------------ practice lane (1.8.0, feature 014)

SectionKind = Literal[
    "intro", "verse", "pre-chorus", "chorus", "bridge", "solo", "instrumental", "outro", "other",
]  # fmt: skip
"""Functional kind of a detected section (data-model 2.3)."""

SectionSnap = Literal["downbeat", "beat", "time"]
"""What the boundaries were snapped to: bars of a reliable grid, beats, or 0.1 s."""

SectionWarningCode = Literal[
    "grid-unreliable", "no-grid", "few-repetitions", "no-vocals", "merged-to-limit",
]  # fmt: skip
"""Codes of ``SectionResult.warnings``; the texts are ``SECTION_WARNING_TEXTS``."""

StructureAnalyzerName = Literal["stems-novelty", "fake"]
"""Section analysers: the stems novelty analyser (``analysis/structure``) or the E2E fake."""

ChannelPath = Annotated[
    str, Field(pattern=r"^(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?$")
]
"""A standard stem (``bass``) or a detail channel ``<parent>.<detail>``."""

DetailChannel = Annotated[
    str, Field(pattern=r"^(vocals|drums|guitar|piano|other)\.[a-z][a-z-]{0,31}$")
]
"""A detail channel ``<parent>.<detail>`` (``guitar.electric-guitar``)."""

SECTION_WARNING_TEXTS: Final[Mapping[SectionWarningCode, str]] = {
    "grid-unreliable": "Tempo grid uncertain - boundaries may be off.",
    "no-grid": "No tempo grid - boundaries are placed in seconds.",
    "few-repetitions": "Few repeating parts found - sections are named Part A, Part B.",
    "no-vocals": "No vocals found - verse and chorus could not be told apart.",
    "merged-to-limit": "Short sections were merged to stay within the section limit.",
}
"""User-facing text of every section warning code (shown in the mixer's section status)."""

SCORE_VIEW_FAILED_TEXT: Final = "The score view could not be made."
"""Error of a score-view job whose MusicXML cannot be laid out (``ScoreViewError``)."""

DOCUMENT_FAILED_TEXT: Final = "The setlist PDF could not be made."
"""Error of a setlist document job that fails (``DocumentError``)."""

MAX_SECTIONS: Final = 64
"""Most sections per track (upper bound of ``maxSections``)."""

MAX_SCORE_PAGES: Final = 200
"""Most pages of one score view."""

MAX_SCORE_BARS: Final = 2000
"""Most bars of one score view."""

MAX_SETLIST_SONGS: Final = 100
"""Most songs in one setlist document."""

_CONTIGUOUS_TOLERANCE: Final = 0.001
"""Largest gap or overlap (seconds) between neighbouring sections or bars."""


class StemPaths(ContractModel):
    """Library paths of a track's standard stems (``guitar``/``piano`` absent for ``htdemucs``)."""

    vocals: LibraryPath
    drums: LibraryPath
    bass: LibraryPath
    other: LibraryPath
    guitar: LibraryPath | None = None
    piano: LibraryPath | None = None


class DetailPath(ContractModel):
    """A kept detail stem of a split parent (used only to name the solo instrument)."""

    channel: DetailChannel
    path: LibraryPath


class SectionJobRequest(ContractModel):
    """Body of ``POST /v1/section-jobs`` (data-model 2.1)."""

    stems: StemPaths
    details: Annotated[list[DetailPath], Field(max_length=30)] = []
    grid: BeatGrid | None
    duration_seconds: Annotated[float, Field(ge=1, le=900)]
    max_sections: Annotated[int, Field(ge=1, le=MAX_SECTIONS)] = MAX_SECTIONS


class DetectedSection(ContractModel):
    """One detected section; the sections of a result are ordered and contiguous."""

    kind: SectionKind
    label: Annotated[str, Field(min_length=1, max_length=40)]
    start_seconds: Seconds
    end_seconds: Annotated[float, Field(gt=0)]
    cluster: Annotated[str, Field(pattern=r"^[A-Z]$")]
    instrument: ChannelPath | None
    confidence: Annotated[float, Field(ge=0, le=1)]


class SectionWarning(ContractModel):
    """A warning of a section result (``Warning`` in the contract)."""

    code: SectionWarningCode
    message: Annotated[str, Field(max_length=300)]


class AnalyzerInfo(ContractModel):
    """Name and version of the analyser that produced a section result."""

    name: StructureAnalyzerName
    version: Annotated[str, Field(max_length=20)]


def _contiguous(starts_ends: list[tuple[float, float]]) -> bool:
    """True when every span is non-empty and ends where the next one starts."""
    if any(end <= start for start, end in starts_ends):
        return False
    return all(
        abs(first[1] - second[0]) <= _CONTIGUOUS_TOLERANCE
        for first, second in itertools.pairwise(starts_ends)
    )


class SectionResult(ContractModel):
    """Result of a completed section job: sections covering ``0..durationSeconds`` without gaps."""

    analyzer: AnalyzerInfo
    snap: SectionSnap
    sections: Annotated[list[DetectedSection], Field(min_length=1, max_length=MAX_SECTIONS)]
    warnings: Annotated[list[SectionWarning], Field(max_length=10)]

    @model_validator(mode="after")
    def _check_contiguous(self) -> Self:
        """Require ascending, contiguous sections (1 ms tolerance)."""
        if not _contiguous([(s.start_seconds, s.end_seconds) for s in self.sections]):
            msg = "sections must be ascending and contiguous"
            raise ValueError(msg)
        return self


class PracticeJobStatus(ContractModel):
    """Fields shared by the three job kinds of the ``practice`` lane (1.8.0)."""

    job_id: JobId
    status: RenderJobStatus
    progress: Annotated[float, Field(ge=0, le=1)]
    error: Annotated[str | None, Field(max_length=500)] = None
    created_at: datetime


class SectionJob(PracticeJobStatus):
    """Status of a section job: ``POST /v1/section-jobs`` and ``GET /v1/section-jobs/{id}``."""

    result: SectionResult | None = None


class ScoreViewJobRequest(ContractModel):
    """Body of ``POST /v1/score-view-jobs`` (data-model 2.5)."""

    musicxml: LibraryPath
    grid: TempoGrid
    duration_seconds: Annotated[float, Field(ge=1, le=900)]


class BarBox(ContractModel):
    """One bar: box in page fractions, times relative to the score's input start."""

    index: Annotated[int, Field(ge=0)]
    number: Annotated[int, Field(ge=0)]
    page: Annotated[int, Field(ge=1, le=MAX_SCORE_PAGES)]
    x: Annotated[float, Field(ge=0, le=1)]
    y: Annotated[float, Field(ge=0, le=1)]
    width: Annotated[float, Field(gt=0, le=1)]
    height: Annotated[float, Field(gt=0, le=1)]
    start_seconds: float
    end_seconds: float


class ScoreViewPageInfo(ContractModel):
    """Size of one SVG page of a score view."""

    page: Annotated[int, Field(ge=1, le=MAX_SCORE_PAGES)]
    width_px: Annotated[int, Field(ge=1)]
    height_px: Annotated[int, Field(ge=1)]


class ScoreViewEngine(ContractModel):
    """The engraver that laid out the pages."""

    name: Literal["verovio"] = "verovio"
    version: Annotated[str, Field(max_length=40)]


def _on_pages(bars: list[BarBox], pages: list[ScoreViewPageInfo]) -> bool:
    """True when every bar lies on one of the listed pages."""
    listed = {page.page for page in pages}
    return all(bar.page in listed for bar in bars)


class ScoreViewResult(ContractModel):
    """Result of a completed score-view job: page sizes and the bar map."""

    pages: Annotated[list[ScoreViewPageInfo], Field(min_length=1, max_length=MAX_SCORE_PAGES)]
    bars: Annotated[list[BarBox], Field(min_length=1, max_length=MAX_SCORE_BARS)]
    engine: ScoreViewEngine

    @model_validator(mode="after")
    def _check_bars(self) -> Self:
        """Require contiguous bars that lie on listed pages."""
        spans = [(bar.start_seconds, bar.end_seconds) for bar in self.bars]
        if not (_contiguous(spans) and _on_pages(self.bars, self.pages)):
            msg = "bars must be ascending, contiguous and on listed pages"
            raise ValueError(msg)
        return self


class ScoreViewJob(PracticeJobStatus):
    """Status of a score-view job: ``POST /v1/score-view-jobs`` and ``GET .../{id}``."""

    result: ScoreViewResult | None = None


class SetlistSection(ContractModel):
    """A section line of a song in the setlist PDF ("0:12 Verse 1")."""

    label: Annotated[str, Field(max_length=40)]
    kind: Annotated[str, Field(max_length=20)]
    start_seconds: Seconds


LyricsLineText = Annotated[str, Field(max_length=500)]
"""One plain lyrics line of a setlist song."""


class SetlistLyrics(ContractModel):
    """Plain lyrics lines of a song (at most 10,000 characters after the API cut)."""

    lines: Annotated[list[LyricsLineText], Field(max_length=2000)]
    shortened: bool


class SetlistSongContent(ContractModel):
    """One song of a setlist document (data-model 2.6)."""

    position: Annotated[int, Field(ge=1, le=MAX_SETLIST_SONGS)]
    title: Annotated[str, Field(min_length=1, max_length=200)]
    artist: Annotated[str | None, Field(max_length=200)] = None
    bpm: Annotated[float | None, Field(ge=0, le=400)] = None
    beats_per_bar: BeatsPerBar | None = None
    key: Annotated[str | None, Field(max_length=40)] = None
    duration_seconds: Seconds
    mix_name: Annotated[str | None, Field(max_length=100)] = None
    note: Annotated[str | None, Field(max_length=1000)] = None
    sections: Annotated[list[SetlistSection], Field(max_length=MAX_SECTIONS)] = []
    lyrics: SetlistLyrics | None = None


class SetlistDocumentOptions(ContractModel):
    """Which parts the setlist PDF contains."""

    include_lyrics: bool
    include_sections: bool
    include_notes: bool
    page_break_per_song: bool


class SetlistDocumentRequest(ContractModel):
    """Body of ``POST /v1/setlist-document-jobs``: the whole content inline, no paths."""

    title: Annotated[str, Field(min_length=1, max_length=80)]
    note: Annotated[str, Field(max_length=2000)] = ""
    generated_at: datetime
    options: SetlistDocumentOptions
    songs: Annotated[list[SetlistSongContent], Field(min_length=1, max_length=MAX_SETLIST_SONGS)]


class SetlistDocumentJob(PracticeJobStatus):
    """Status of a setlist document job: ``POST /v1/setlist-document-jobs`` and ``GET``."""

    pages: Annotated[int | None, Field(ge=1)] = None


# ----------------------------------------------- instrument analysis lane (1.9.0, feature 016)

InstrumentAnalysisKind = Literal["chords", "drum-hits", "vocal-pitch"]
"""Kinds of instrument analysis jobs (discriminator ``kind``)."""

ChordVocabulary = Literal["simple", "full"]
"""``simple`` = major/minor (25 classes); ``full`` = 170 classes + inversions (data-model 2.1)."""

ChordEngine = Literal["btc", "chroma-hmm", "fake"]
"""Chord engine recorded in the chart (``btc`` falls back to ``chroma-hmm``)."""

PitchEngine = Literal["pyin", "yin", "fake"]
"""Reference melody engine."""

KeyMode = Literal["major", "minor"]
"""Mode of a detected key."""

VocalSource = Literal["vocals.lead", "vocals"]
"""Which stem the reference melody was taken from."""

InstrumentAnalysisStage = Literal["loading-model", "preparing", "analysing", "finishing"]
"""Stage of a running instrument analysis job."""

AnalysisWarningCode = Literal[
    "vocabulary-reduced", "grid-unreliable", "fallback-engine", "few-chords", "key-uncertain",
    "few-voiced", "detail-bleed",
]  # fmt: skip
"""Warning codes of the analysis results (data-model 2.4, 3.1)."""

ANALYSIS_WARNING_TEXTS: Final[Mapping[AnalysisWarningCode, str]] = {
    "vocabulary-reduced": "The fast method knows only major and minor chords.",
    "grid-unreliable": "Chord changes are not aligned to beats because the beat grid is uncertain.",
    "fallback-engine": "The chord model was not available; the fast method was used.",
    "few-chords": "Very few chords were detected.",
    "key-uncertain": "The key is uncertain (confidence {n} %).",
    "few-voiced": "Very little sung melody was found.",
    "detail-bleed": "Detail stems can contain other instruments; expect extra notes.",
}
"""English warning texts; ``{n}`` is filled with ``str.format``."""

NO_MELODY_TEXT: Final = "The vocal stem is silent, so no melody can be shown."
"""Failure text of a vocal-pitch job on a silent stem (``NoMelodyError``)."""

MAX_CHORD_SEGMENTS: Final = 4000
"""Most chord segments per chart."""

MAX_KEY_REGIONS: Final = 50
"""Most key regions per chart."""

MAX_DRUM_HITS: Final = 40_000
"""Most drum hits per result."""

MAX_PITCH_FRAMES: Final = 90_000
"""Most 10 ms reference frames (15 minutes)."""

MAX_CENTS: Final = 12_700
"""Highest centi-semitone value (MIDI 127)."""


class ChordsJobRequest(ContractModel):
    """``kind: chords``: sum the harmonic stems, recognise and post-process (data-model 2.3)."""

    kind: Literal["chords"]
    inputs: Annotated[list[LibraryPath], Field(min_length=1, max_length=4)]
    bass_input: LibraryPath | None = None
    grid: BeatGridTempo | None = None
    vocabulary: ChordVocabulary
    engine: ChordEngine | None = Field(default=None, description="None = STEMSTUDIO_CHORD_ENGINE.")

    def input_paths(self) -> list[str]:
        """Every library path of the request (for the path guard)."""
        return [*self.inputs, *([self.bass_input] if self.bass_input is not None else [])]


class DrumHitsJobRequest(ContractModel):
    """``kind: drum-hits``: the 008 drum onsets per piece."""

    kind: Literal["drum-hits"]
    drum_inputs: DrumInputs

    def input_paths(self) -> list[str]:
        """Every library path of the request (for the path guard)."""
        return list(self.drum_inputs.pieces().values())


class VocalPitchJobRequest(ContractModel):
    """``kind: vocal-pitch``: pYIN (or YIN) reference melody of one vocal stem."""

    kind: Literal["vocal-pitch"]
    input: LibraryPath
    source: VocalSource

    def input_paths(self) -> list[str]:
        """Every library path of the request (for the path guard)."""
        return [self.input]


AnyAnalysisRequest = ChordsJobRequest | DrumHitsJobRequest | VocalPitchJobRequest
"""One of the three request bodies."""

INSTRUMENT_ANALYSIS_REQUEST: Final[TypeAdapter[AnyAnalysisRequest]] = TypeAdapter(
    Annotated[AnyAnalysisRequest, Field(discriminator="kind")]
)
"""Validates the body of ``POST /v1/instrument-analysis-jobs`` by its ``kind``."""


class ChordSegment(ContractModel):
    """One chord; segments are ordered, contiguous and cover the track (data-model 2.2)."""

    start_ms: Annotated[int, Field(ge=0)]
    end_ms: Annotated[int, Field(ge=1)]
    label: Annotated[str, Field(min_length=1, max_length=32)]
    root: PitchClass | None = None
    quality: ChordQuality | None = None
    bass: PitchClass | None = None
    confidence: Annotated[float, Field(ge=0, le=1)]


class KeyAlternative(ContractModel):
    """The relative key that fits almost as well (research R-13)."""

    tonic: PitchClass
    mode: KeyMode


class AnalysisKey(ContractModel):
    """The global song key (scope addition S11, data-model 2.8)."""

    tonic: PitchClass
    mode: KeyMode
    confidence: Annotated[float, Field(ge=0, le=1)]
    alternative: KeyAlternative | None = None


class KeyRegion(ContractModel):
    """A key region of at least 8 bars (16 s without a grid); regions are contiguous and ordered."""

    start_ms: Annotated[int, Field(ge=0)]
    end_ms: Annotated[int, Field(ge=1)]
    tonic: PitchClass
    mode: KeyMode
    confidence: Annotated[float, Field(ge=0, le=1)]


class AnalysisWarning(ContractModel):
    """A warning of an analysis result."""

    code: AnalysisWarningCode
    text: Annotated[str, Field(min_length=1, max_length=300)]


def _contiguous_ms(spans: list[tuple[int, int]]) -> None:
    """Require ordered, contiguous ``(start, end)`` spans with ``start < end``."""
    if any(start >= end for start, end in spans) or any(
        later[0] != earlier[1] for earlier, later in itertools.pairwise(spans)
    ):
        msg = "spans must be ordered and contiguous with start < end"
        raise ValueError(msg)


class ChordChart(ContractModel):
    """Result of a chords job (data-model 2.4)."""

    kind: Literal["chords"] = "chords"
    vocabulary: ChordVocabulary
    engine: ChordEngine
    key: AnalysisKey | None = None
    key_regions: Annotated[list[KeyRegion], Field(max_length=MAX_KEY_REGIONS)] = []
    prefer_flats: bool = False
    beat_snapped: bool = False
    no_chords: bool = False
    segments: Annotated[list[ChordSegment], Field(max_length=MAX_CHORD_SEGMENTS)]
    warnings: Annotated[list[AnalysisWarning], Field(max_length=8)] = []

    @model_validator(mode="after")
    def _check_order(self) -> Self:
        """Segments and key regions are each ordered and contiguous."""
        _contiguous_ms([(s.start_ms, s.end_ms) for s in self.segments])
        _contiguous_ms([(r.start_ms, r.end_ms) for r in self.key_regions])
        return self


class DrumHitEvent(ContractModel):
    """One drum hit (data-model 5.1)."""

    time_ms: Annotated[int, Field(ge=0)]
    piece: DrumPiece
    strength: Annotated[float, Field(ge=0, le=1)]


class DrumHits(ContractModel):
    """Result of a drum-hits job: hits in time order."""

    kind: Literal["drum-hits"] = "drum-hits"
    engine: Literal["drum-onsets", "fake"]
    pieces: Annotated[list[DrumPiece], Field(max_length=5)]
    hits: Annotated[list[DrumHitEvent], Field(max_length=MAX_DRUM_HITS)]

    @model_validator(mode="after")
    def _check_order(self) -> Self:
        """Hit times never decrease."""
        if any(later.time_ms < earlier.time_ms for earlier, later in itertools.pairwise(self.hits)):
            msg = "hits must be in time order"
            raise ValueError(msg)
        return self


class PitchRange(ContractModel):
    """5th/95th percentile of the voiced frames in centi-semitones."""

    low_cents: Annotated[int, Field(ge=1, le=MAX_CENTS)]
    high_cents: Annotated[int, Field(ge=1, le=MAX_CENTS)]


PitchFrame = Annotated[int, Field(ge=0, le=MAX_CENTS)]
"""One 10 ms reference frame in centi-semitones; 0 = unvoiced."""


class VocalPitch(ContractModel):
    """Result of a vocal-pitch job (data-model 3.1)."""

    kind: Literal["vocal-pitch"] = "vocal-pitch"
    hop_ms: Literal[10] = 10
    frames: Annotated[list[PitchFrame], Field(max_length=MAX_PITCH_FRAMES)]
    source: VocalSource
    engine: PitchEngine
    voiced_ratio: Annotated[float, Field(ge=0, le=1)]
    range: PitchRange | None = None
    warnings: Annotated[list[AnalysisWarning], Field(max_length=8)] = []


AnalysisResult = Annotated[ChordChart | DrumHits | VocalPitch, Field(discriminator="kind")]
"""Inline result of a completed instrument analysis job."""


class InstrumentAnalysisJob(ContractModel):
    """Status of an analysis job: ``POST`` and ``GET /v1/instrument-analysis-jobs/{id}``."""

    analysis_job_id: JobId
    kind: InstrumentAnalysisKind
    status: RenderJobStatus
    stage: InstrumentAnalysisStage | None = Field(
        default=None, description="None while queued and after the job ended."
    )
    progress: Annotated[float, Field(ge=0, le=1)]
    device: JobDevice | None = Field(default=None, description="Set for chords on the BTC engine.")
    error: Annotated[str | None, Field(max_length=500)] = None
    result: AnalysisResult | None = None
    created_at: datetime
    updated_at: datetime


class InstrumentAnalysisModels(ContractModel):
    """Readiness of the BTC chord weights (``Health.instrumentAnalysis.models``)."""

    btc_majmin: ModelReadiness = Field(default="not-downloaded", alias="btc-majmin")
    btc_large_voca: ModelReadiness = Field(default="not-downloaded", alias="btc-large-voca")


class InstrumentAnalysisHealth(ContractModel):
    """``Health.instrumentAnalysis`` (1.9.0)."""

    models: InstrumentAnalysisModels = InstrumentAnalysisModels()
    chord_engine: ChordEngine = "btc"
    pitch_engine: PitchEngine = "pyin"


# ------------------------------------------------- insights lane (1.11.0, feature 020)

QualityInputKind = Literal["standard", "detail", "rest"]
"""Kind of a quality input: a standard stem, a kept detail stem or a parent's ``rest`` stem."""

QualityStage = Literal["loading", "analysing", "rating"]
"""Stages of a quality job (``loading`` 0-0.2, ``analysing`` 0.2-0.9, ``rating`` 0.9-1.0)."""

QualityLevel = Literal["low", "medium", "high", "unknown"]
"""Level of one quality dimension (data-model 4.5)."""

Reliability = Literal["validated", "indicative"]
"""``validated`` only for stem types and dimensions that passed gate G1 (research.md R-04)."""

QualityRating = Literal["good", "fair", "poor", "not-present", "unknown"]
"""Rating of a stem (data-model 4.5)."""

QualityReasonCode = Literal[
    "clean", "bleed", "bleed-together", "artefacts", "bleed-and-artefacts", "not-present",
    "too-short",
]  # fmt: skip
"""Reason of a rating; the texts are in data-model 4.6 (made by the API)."""

ArtefactKind = Literal["flicker", "dropouts", "model-residual", "rest-share"]
"""Artefact kinds of a stem (data-model 4.4)."""

Fraction = Annotated[float, Field(ge=0, le=1)]
"""A number from 0 to 1."""

MAX_QUALITY_STEMS: Final = 64
"""Most stems of one quality job (six standard stems plus detail and rest stems)."""

STANDARD_STEMS: Final[tuple[StemName, ...]] = (
    "vocals",
    "drums",
    "bass",
    "guitar",
    "piano",
    "other",
)
"""The six standard stems; a quality job needs each exactly once (their sum is the mix)."""


class QualityInput(ContractModel):
    """One stem of a quality job."""

    channel: ChannelPath
    path: LibraryPath
    kind: QualityInputKind

    @model_validator(mode="after")
    def _kind_matches_channel(self) -> Self:
        """``standard`` = no dot, ``rest`` = ``<parent>.rest``, ``detail`` = any other detail."""
        expected = (
            "standard" if "." not in self.channel
            else "rest" if self.channel.endswith(".rest")
            else "detail"
        )  # fmt: skip
        if self.kind != expected:
            msg = f"{self.channel} must have kind {expected}"
            raise ValueError(msg)
        return self


class DetailParentInput(ContractModel):
    """The 002 metrics of a split parent (``DetailOutcome``)."""

    parent: ParentStemName
    model_residual_db: float | None
    reconstruction_error_db: float | None


class QualityTitles(ContractModel):
    """Song title and artist of a quality job (logs only)."""

    title: Annotated[str, Field(max_length=200)]
    artist: Annotated[str | None, Field(max_length=200)] = None


class QualityJobRequest(ContractModel):
    """Body of ``POST /v1/quality-jobs`` (data-model 2.3)."""

    stems: Annotated[list[QualityInput], Field(min_length=6, max_length=MAX_QUALITY_STEMS)]
    detail_parents: Annotated[list[DetailParentInput], Field(max_length=5)]
    titles: QualityTitles

    @model_validator(mode="after")
    def _six_standard_stems(self) -> Self:
        """Every channel once; the six standard stems present."""
        channels = [stem.channel for stem in self.stems]
        if len(set(channels)) != len(channels):
            msg = "a channel is given more than once"
            raise ValueError(msg)
        if not set(STANDARD_STEMS) <= set(channels):
            msg = "all six standard stems are needed (their sum is the mix)"
            raise ValueError(msg)
        return self

    def input_paths(self) -> tuple[str, ...]:
        """Library paths the job reads, in request order."""
        return tuple(stem.path for stem in self.stems)


class BleedEstimate(ContractModel):
    """Bleed of a stem (data-model 4.2/4.3)."""

    level: QualityLevel
    index: Fraction
    main_source: StemName | None
    co_occurrence: bool
    reliability: Reliability


class ArtefactEstimate(ContractModel):
    """Artefacts of a stem (data-model 4.4)."""

    level: QualityLevel
    index: Fraction
    kinds: Annotated[list[ArtefactKind], Field(max_length=4)]
    reliability: Reliability

    @model_validator(mode="after")
    def _unique_kinds(self) -> Self:
        """``kinds`` has no duplicates (``uniqueItems``)."""
        if len(set(self.kinds)) != len(self.kinds):
            msg = "artefact kinds must be unique"
            raise ValueError(msg)
        return self


class StemQuality(ContractModel):
    """Estimate of one stem (data-model 2.4)."""

    channel: ChannelPath
    rating: QualityRating
    rating_reason: QualityReasonCode
    active_seconds: Annotated[float, Field(ge=0)]
    active_share: Fraction
    level_db: Annotated[float, Field(ge=-120, le=20)]
    bleed: BleedEstimate
    artefacts: ArtefactEstimate


class QualityResult(ContractModel):
    """Result of a completed quality job (stems in request order)."""

    analyzer_version: Annotated[str, Field(max_length=20)]
    duration_seconds: Annotated[float, Field(ge=0)]
    stems: Annotated[list[StemQuality], Field(max_length=MAX_QUALITY_STEMS)]


class QualityJob(ContractModel):
    """Status of a quality job: ``POST /v1/quality-jobs`` and ``GET``."""

    quality_job_id: JobId
    status: RenderJobStatus
    stage: QualityStage | None = None
    progress: Fraction
    error: Annotated[str | None, Field(max_length=500)] = None
    result: QualityResult | None = None
    created_at: datetime
    updated_at: datetime


# ------------------------------------------------- handout jobs (1.11.0, feature 020)

HandoutKind = Literal["song", "setlist"]
"""A handout of one song or of a setlist."""

HandoutFamily = Literal["overview", "vocals", "guitar", "bass", "keys", "drums", "melodic"]
"""The seven equal handout families (data-model 6.1)."""

IngredientCode = Literal[
    "facts", "form-chart", "chords-over-lyrics", "lyrics", "chord-chart", "notation",
    "practice-notes", "practice-progress", "setlist-notes",
]  # fmt: skip
"""Handout ingredients (data-model 6.2)."""

Paper = Literal["a4", "letter"]
"""Paper size of a handout."""

NotationKind = Literal["score", "tab", "drums", "transposed"]
"""Kind of a notation item (008 score, 016 tablature, drum notation, transposed part)."""

HandoutStage = Literal["layout", "notation", "writing"]
"""Stages of a handout job (``layout`` 0-0.6, ``notation`` 0.6-0.9, ``writing`` 0.9-1.0)."""

HandoutWarningCode = Literal["missing-glyphs", "notation-scaled", "notation-unreadable"]
"""Warnings of a finished handout."""

HANDOUT_NOTICE_FOOTER: Final = "For personal practice and teaching only"
"""Fixed footer notice of every handout page (data-model 6.6)."""

HANDOUT_NOTICE_FULL: Final = (
    "This handout was made with StemStudio for your own practice and lessons. The song, its lyrics"
    " and its music belong to their rights holders. Do not publish, sell or share it beyond your"
    " lesson. Chords, notation and timings were made automatically and may contain errors."
)
"""Fixed full notice of the last handout page (data-model 6.6)."""

MAX_HANDOUT_SONGS: Final = 50
"""Most songs of one handout (FR-009)."""

ChordSymbolText = Annotated[
    str,
    Field(
        max_length=20,
        pattern=r"^[A-G](#|b)?[A-Za-z0-9+#b()-]{0,12}(/[A-G](#|b)?)?$|^N\.C\.$",
    ),
]
"""Chord symbol with ASCII accidentals (``Bb``, ``F#m7``, ``C/G``, ``N.C.``; research.md R-08)."""

HandoutText = Annotated[str | None, Field(max_length=4000)]
"""Optional plain text of up to 4,000 characters (notes)."""


class HandoutNotice(ContractModel):
    """Notice texts; they must equal the fixed texts (else 422)."""

    footer: Annotated[str, Field(max_length=80)]
    full: Annotated[str, Field(max_length=1000)]

    @model_validator(mode="after")
    def _fixed_texts(self) -> Self:
        """Reject any notice text that differs from data-model 6.6."""
        if (self.footer, self.full) != (HANDOUT_NOTICE_FOOTER, HANDOUT_NOTICE_FULL):
            msg = "notice texts must be the fixed notice text"
            raise ValueError(msg)
        return self


class SetlistEntryBlock(ContractModel):
    """One running-order line of a setlist handout."""

    position: Annotated[int, Field(ge=1, le=500)]
    title: Annotated[str, Field(max_length=300)]
    artist: Annotated[str | None, Field(max_length=500)]
    key: Annotated[str | None, Field(max_length=40)]
    tempo_bpm: Annotated[int | None, Field(ge=20, le=400)]
    duration_seconds: Annotated[float | None, Field(ge=0)]
    notes: HandoutText


class SetlistBlock(ContractModel):
    """Cover and running order of a setlist handout."""

    name: Annotated[str, Field(min_length=1, max_length=200)]
    notes: HandoutText
    entries: Annotated[list[SetlistEntryBlock], Field(max_length=500)]


class SongFacts(ContractModel):
    """Facts row of a song (data-model 2.5)."""

    tempo_bpm: Annotated[int | None, Field(ge=20, le=400)]
    time_signature: Annotated[str | None, Field(pattern=r"^[1-9][0-9]?/(2|4|8)$")]
    key: Annotated[str | None, Field(max_length=40)]
    key_source: Literal["chords", "score"] | None
    duration_seconds: Annotated[float, Field(ge=0)]
    transposition_note: Annotated[str | None, Field(max_length=120)]


class HandoutSection(ContractModel):
    """One form-chart row: a section with bars and chords per bar."""

    name: Annotated[str, Field(min_length=1, max_length=80)]
    start_seconds: Annotated[float, Field(ge=0)]
    end_seconds: Annotated[float, Field(ge=0)]
    start_bar: Annotated[int | None, Field(ge=0)]
    bars: Annotated[int | None, Field(ge=0, le=999)]
    chord_bars: Annotated[
        list[Annotated[list[ChordSymbolText], Field(max_length=8)]] | None, Field(max_length=999)
    ]
    practice_tempo_percent: Annotated[int | None, Field(ge=25, le=200)]


class LyricChord(ContractModel):
    """A chord above a lyric line at a character index."""

    symbol: ChordSymbolText
    char_index: Annotated[int, Field(ge=0, le=500)]


class LyricLine(ContractModel):
    """A lyric line (empty for an instrumental chord row) with its chords."""

    text: Annotated[str, Field(max_length=500)]
    section: Annotated[int | None, Field(ge=0)]
    chords: Annotated[list[LyricChord], Field(max_length=32)]


class HandoutLyrics(ContractModel):
    """Lyrics with chords (the 016 chord sheet rule; ``chordPlacement`` says how)."""

    lines: Annotated[list[LyricLine], Field(max_length=1000)]
    chord_placement: Literal["word", "line", "none"]
    source: Annotated[str, Field(max_length=200)]


class HandoutNotation(ContractModel):
    """A notation PDF to merge (one frame page per PDF page)."""

    title: Annotated[str, Field(min_length=1, max_length=120)]
    kind: NotationKind
    pdf_path: LibraryPath


class HandoutSong(ContractModel):
    """One song of a handout, fully resolved by the API (data-model 2.5)."""

    title: Annotated[str, Field(min_length=1, max_length=300)]
    artist: Annotated[str | None, Field(max_length=500)]
    artwork_path: LibraryPath | None
    facts: SongFacts
    sections: Annotated[list[HandoutSection], Field(max_length=64)]
    lyrics: HandoutLyrics | None
    chord_symbols: Annotated[list[ChordSymbolText], Field(max_length=64)]
    notation: Annotated[list[HandoutNotation], Field(max_length=8)]
    practice_notes: HandoutText
    entry_notes: HandoutText
    include: list[IngredientCode]

    def input_paths(self) -> tuple[str, ...]:
        """Artwork first, then the notation PDFs in order."""
        artwork = (self.artwork_path,) if self.artwork_path else ()
        return (*artwork, *(item.pdf_path for item in self.notation))


class HandoutDocument(ContractModel):
    """The resolved handout (data-model 2.5); the worker performs no lookups."""

    kind: HandoutKind
    family: HandoutFamily
    family_label: Annotated[str, Field(min_length=1, max_length=60)]
    title: Annotated[str, Field(min_length=1, max_length=300)]
    subtitle: Annotated[str | None, Field(max_length=300)]
    generated_on: date
    notice: HandoutNotice
    setlist: SetlistBlock | None
    songs: Annotated[list[HandoutSong], Field(min_length=1, max_length=MAX_HANDOUT_SONGS)]
    attributions: Annotated[list[Annotated[str, Field(max_length=300)]], Field(max_length=20)]
    app_version: Annotated[str, Field(max_length=40)]

    @model_validator(mode="after")
    def _kind_rules(self) -> Self:
        """``setlist`` iff kind ``setlist``; a song handout has exactly one song."""
        if (self.setlist is not None) != (self.kind == "setlist"):
            msg = "setlist must be set exactly when kind is setlist"
            raise ValueError(msg)
        if self.kind == "song" and len(self.songs) != 1:
            msg = "a song handout has exactly one song"
            raise ValueError(msg)
        return self


class HandoutJobRequest(ContractModel):
    """Body of ``POST /v1/handout-jobs``."""

    document: HandoutDocument
    paper: Paper

    def input_paths(self) -> tuple[str, ...]:
        """Every library path the renderer may read (checked when the job is created)."""
        return tuple(itertools.chain.from_iterable(s.input_paths() for s in self.document.songs))


class HandoutWarning(ContractModel):
    """A warning of a finished handout."""

    code: HandoutWarningCode
    text: Annotated[str, Field(max_length=300)]


class HandoutResult(ContractModel):
    """Result of a completed handout job; the file is ``GET /v1/handout-jobs/{id}/file``."""

    file_name: Literal["handout.pdf"] = "handout.pdf"
    content_type: Literal["application/pdf"] = "application/pdf"
    size_bytes: Annotated[int, Field(ge=1)]
    sha256: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    pages: Annotated[int, Field(ge=1)]
    outline: Annotated[list[Annotated[str, Field(max_length=200)]], Field(max_length=1000)]
    warnings: Annotated[list[HandoutWarning], Field(max_length=50)]


class HandoutJob(ContractModel):
    """Status of a handout job: ``POST /v1/handout-jobs`` and ``GET``."""

    handout_job_id: JobId
    status: RenderJobStatus
    stage: HandoutStage | None = None
    progress: Fraction
    error: Annotated[str | None, Field(max_length=500)] = None
    result: HandoutResult | None = None
    created_at: datetime
    updated_at: datetime


class RendererHealth(ContractModel):
    """``Health.insights.renderer``: versions of WeasyPrint and Pango (None = not importable)."""

    weasyprint: str | None = None
    pango: str | None = None


class HandoutFontsHealth(ContractModel):
    """``Health.insights.fonts``: Archivo packaged, DejaVu Sans fallback installed."""

    archivo: bool = False
    fallback: bool = False


class InsightsHealth(ContractModel):
    """``Health.insights`` (1.11.0)."""

    analyzer: Literal["real", "fake"]
    analyzer_version: Annotated[str, Field(max_length=20)]
    renderer: RendererHealth = RendererHealth()
    fonts: HandoutFontsHealth = HandoutFontsHealth()


INSIGHTS_NOT_CHECKED: Final = InsightsHealth(analyzer="real", analyzer_version="0.0.0")
"""Default ``Health.insights`` until the composition root reports the configured analyzer."""


class StructureHealth(ContractModel):
    """``Health.structure``: the configured section analyser (1.8.0)."""

    analyzer: StructureAnalyzerName = "stems-novelty"
    version: Annotated[str, Field(max_length=20)] = "1.0.0"


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
    structure: StructureHealth = Field(
        default=StructureHealth(), description="Configured section analyser (1.8.0)."
    )
    instrument_analysis: InstrumentAnalysisHealth = Field(
        default=InstrumentAnalysisHealth(), description="Chord and pitch engines (1.9.0)."
    )
    insights: InsightsHealth = Field(
        default=INSIGHTS_NOT_CHECKED, description="Quality analyzer and handout renderer (1.11.0)."
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
