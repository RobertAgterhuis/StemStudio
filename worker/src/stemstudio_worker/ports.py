"""Ports (``typing.Protocol`` interfaces) between the worker pipeline and its adapters.

Each protocol is implemented by a different agent and faked in unit tests:

* ``SourceResolver``, ``UploadIntake``, ``AudioFetcher``, ``AudioNormalizer``: audio-ingest
  (``ingest/``, factory ``create_ingest_adapters``)
* ``Separator``: stem-separation (``separation/``, factory ``create_separator``)
* ``BeatAnalyzer``: beat-analysis (``analysis/``, factory ``create_beat_analyzer``)
* ``DetailModelProvider`` / ``DetailModel``: stem-separation (``detail/``, factory
  ``create_detail_adapters``), used by the detail jobs of feature 002
* ``DeviceRunner``: stem-separation (``devices/``, factory ``create_device_runner``), feature 003:
  start-up GPU probe, one-GPU-step lease, free-memory check and the CPU retry on GPU
  out-of-memory, shared by the separator and the detail models (ADR-012)
* ``ImageNormalizer``, ``MetadataLookup``: audio-ingest (``metadata/``), feature 005: source and
  Cover Art Archive pictures re-encoded to cover/thumb JPEGs, and the MusicBrainz/Wikidata/
  Wikipedia lookup of a metadata job (ADR-014)
* ``LibraryReader``, ``RenderExecutor``: audio-ingest (``render/``), feature 007: the path guard
  over the read-only library mount and the two-pass renderer of render jobs (ADR-016)
* ``LyricsLookup``: audio-ingest (``lyrics/``), feature 006: LRCLIB lookup and normalisation;
  ``Aligner``: stem-separation (``alignment/``): stable-ts + Whisper through the ``DeviceRunner``
  (ADR-015)
* ``PitchTranscriber``: stem-separation (``transcription/``), ``DrumTranscriber`` and ``Notator``:
  beat-analysis (``analysis/drum_onsets.py``, ``notation/``), ``ScoreEngraver``: audio-ingest
  (``engraving/``), feature 008: sheet music (ADR-017)
* ``StorageMeter``, ``CacheJanitor``, ``LibraryStemReader``: stem-separation (``storage/``),
  feature 013: the storage report, the cache clean-up and the rehydration source (ADR-022)
* ``StructureAnalyzer``: beat-analysis (``analysis/structure/``; the fake by stem-separation),
  ``ScoreViewRenderer`` and ``SetlistDocumentRenderer``: audio-ingest
  (``engraving/score_view.py``, ``documents/``), feature 014: the ``practice`` lane (ADR-023)
* ``HarmonicMixer``: audio-ingest (``analysis_inputs/``); ``ChordRecognizer``: stem-separation
  (``harmony/btc.py``) and beat-analysis (``harmony/chroma_hmm.py``); ``ChordPostProcessor``,
  ``KeyEstimator``, ``PitchAnalyzer``, ``TabFingerer``, ``PartWriter``: beat-analysis
  (``harmony/``, ``analysis/vocal_pitch.py``, ``notation/``), feature 016: the
  ``instrument-analysis`` lane and the score extensions (ADR-031..033)
* ``QualityAnalyzer``: beat-analysis (``insights/``); ``HandoutRenderer`` and ``NotationReader``:
  audio-ingest (``handouts/``), feature 020: the ``insights`` lane (ADR-029)

The job runner (``jobs/``) composes them: resolve (or accept the upload) -> fetch -> normalise ->
separate -> analyse. The composition root (``main.create_app``) builds a ``PipelineAdapters``
from the three factories; tests pass their own ``PipelineAdapters`` made of fakes.
Adapters signal expected failures by raising a ``WorkerError`` subclass; its message is shown to
the user verbatim as the job ``error`` text, so it must be readable and free of secrets.
"""

import math
import re
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Final, Protocol, runtime_checkable
from uuid import UUID

import numpy as np
import numpy.typing as npt

from stemstudio_worker.schemas import (
    BASIC_PITCH_MODEL_ID,
    DOCUMENT_FAILED_TEXT,
    HANDOUT_NOTICE_FOOTER,
    HANDOUT_NOTICE_FULL,
    LIBRARY_UNAVAILABLE_TEXT,
    LYRICS_UNREACHABLE_TEXT,
    LYRICS_UNUSABLE_TEXT,
    NO_MELODY_TEXT,
    NO_NOTES_TEXT,
    PIANO_MODEL_ID,
    SCORE_VIEW_FAILED_TEXT,
    AnalysisKey,
    ArtworkInfo,
    ArtworkSource,
    BarBox,
    BeatGrid,
    BeatGridTempo,
    ChordChart,
    ChordEngine,
    ChordSegment,
    ChordVocabulary,
    DetailModelId,
    DetailParentInput,
    DeviceInfo,
    DrumPiece,
    HandoutJobRequest,
    HandoutWarning,
    JobDevice,
    KeyInfo,
    KeyName,
    KeyRegion,
    LibraryHealth,
    LyricsResult,
    ManualTempo,
    MetadataResult,
    ModelName,
    PartSettings,
    PartSummary,
    PitchEngine,
    ProcessingDeviceKind,
    QualityInputKind,
    QualityResult,
    RenderJobRequest,
    RenderRange,
    RenderResult,
    ScoreChordSymbol,
    ScoreEngine,
    ScoreProfile,
    ScoreResolution,
    ScoreTitles,
    ScoreWarning,
    SectionJobRequest,
    SectionResult,
    SetlistDocumentRequest,
    SourceType,
    StemName,
    StorageCleanupRequest,
    StorageCleanupResult,
    StructureAnalyzerName,
    TablatureSettings,
    TablatureSummary,
    TempoGrid,
    TimedLyrics,
    TuningId,
    VocalPitch,
    VocalSource,
    WorkerStorage,
    WrittenClef,
    WrittenInstrument,
)

DetailAudio = npt.NDArray[np.float32]
"""Float audio ``(channels, frames)`` at 44.1 kHz handed to and returned by detail models."""


class WorkerError(Exception):
    """Base class of expected, user-explainable pipeline failures."""


class InvalidSourceError(WorkerError):
    """The URL or upload is not acceptable (host, playlist, type, size); HTTP 400.

    Upload problems use a subclass so the API can answer with the contract status code.
    """


class EmptyUploadError(InvalidSourceError):
    """The uploaded file contains no bytes; HTTP 400."""


class UploadTooLargeError(InvalidSourceError):
    """The uploaded file exceeds the size limit (200 MiB); HTTP 413."""


class UnsupportedMediaError(InvalidSourceError):
    """The uploaded file is not an MP3, WAV or FLAC file; HTTP 415."""


class SourceUnavailableError(WorkerError):
    """The source exists but cannot be fetched (private video, no YouTube match, network)."""


class AudioTooLongError(WorkerError):
    """The audio exceeds the maximum duration (15 minutes)."""


class AudioDecodeError(WorkerError):
    """ffmpeg could not decode or convert the audio (corrupt or unsupported content)."""


class SeparationError(WorkerError):
    """Neither the primary nor the fallback separation model could process the audio."""


class DetailModelUnavailableError(WorkerError):
    """A detail model could not be downloaded or failed its SHA-256 verification."""


class DetailSeparationError(WorkerError):
    """A detail model could not process a parent stem (inference, memory or disk failure)."""


class DetailCancelledError(WorkerError):
    """The running detail job was cancelled; raised from a progress callback between chunks."""


class ArtworkError(WorkerError):
    """A picture could not be fetched or normalised (feature 005); never fails a standard job."""


class ArtworkFetchError(ArtworkError):
    """A remote picture could not be downloaded (network, host not allowed, too big, timeout)."""


class NotAnImageError(ArtworkError):
    """The bytes are not a decodable image (corrupt, truncated, polyglot); HTTP 400."""


class UnsupportedImageTypeError(ArtworkError):
    """The image is not a JPEG, PNG or WebP file; HTTP 415."""


class ImageTooLargeError(ArtworkError):
    """The image has more than 40 megapixels; HTTP 422."""


class MetadataLookupError(WorkerError):
    """The recording search failed after retries; the metadata job fails (data-model.md 2.7)."""


class RenderError(WorkerError):
    """A render job cannot run or failed (feature 007); the message is the job ``error``."""


class InputPathError(RenderError):
    """An input path is not on the allow-list or escapes the library root; HTTP 400."""


class InputNotFoundError(RenderError):
    """An allowed input path does not exist; HTTP 404 (the message names the path).

    Attributes:
        path: The library-relative path that was not found.
    """

    def __init__(self, path: str) -> None:
        """Create the error for ``path``."""
        super().__init__(f"Input not found: {path}")
        self.path = path


class RenderCancelledError(RenderError):
    """The render job was cancelled; raised from a render progress callback between blocks."""


@runtime_checkable
class ProgressCallback(Protocol):
    """Receives progress of the current pipeline step."""

    def __call__(self, fraction: float, stage: str) -> None:
        """Report progress.

        Args:
            fraction: Completion of the current step, 0.0..1.0, non-decreasing.
            stage: Human-readable stage text, e.g. "Separating stems".
        """
        ...


@dataclass(frozen=True, slots=True)
class ArtworkCandidate:
    """A picture offered by an import source, not yet normalised (feature 005).

    Exactly one of ``url`` (a remote image on an allow-listed host) and ``data`` (embedded
    bytes, e.g. an upload's front cover) is set.

    Attributes:
        source: ``youtube``, ``spotify``, ``embedded``, ``coverartarchive`` or ``user``.
        url: Remote https image URL, or None.
        data: Image bytes, or None.
    """

    source: ArtworkSource
    url: str | None = None
    data: bytes | None = None

    def __post_init__(self) -> None:
        """Reject candidates with neither or both of ``url`` and ``data``."""
        if (self.url is None) == (self.data is None):
            msg = "An artwork candidate needs exactly one of url and data"
            raise ValueError(msg)


@dataclass(frozen=True, slots=True)
class ArtworkImages:
    """The two normalised JPEGs of a picture.

    Attributes:
        cover: 600 x 600 JPEG, at most 200 KB.
        thumb: 160 x 160 JPEG, at most 20 KB.
        sha256: Lower-case hex SHA-256 of ``cover`` (the artwork version).
    """

    cover: bytes
    thumb: bytes
    sha256: str

    def info(self, source: ArtworkSource, origin_url: str | None = None) -> ArtworkInfo:
        """Describe these images for the contract (``Job.artwork``, ``MetadataResult.artwork``).

        Args:
            source: Where the picture came from.
            origin_url: Remote URL of the original picture; None for embedded/user pictures.

        Returns:
            The artwork record with sizes and hash.
        """
        return ArtworkInfo(
            source=source,
            sha256=self.sha256,
            cover_bytes=len(self.cover),
            thumb_bytes=len(self.thumb),
            origin_url=origin_url,
        )


@dataclass(frozen=True, slots=True)
class SourceInfo:
    """A validated, resolved source.

    Attributes:
        source_type: ``youtube``, ``spotify`` or ``upload``.
        source_key: Cache identity, e.g. ``youtube:dQw4w9WgXcQ`` or ``upload:<sha256>``.
        title: Track title (YouTube title, Spotify title or upload file stem).
        artist: Artist when known.
        youtube_url: Canonical ``https://www.youtube.com/watch?v=<id>`` to download, or None
            for uploads.
        album: Album name when the source knows it (Spotify ``album_name``, upload tag), 005.
        isrc: ISRC of a Spotify track, 005.
        artwork: Picture offered by the source (Spotify cover URL, embedded upload cover), 005.
        lookup_title: Cleaned title for the metadata lookup (upload tag title), 005.
        lookup_artist: Cleaned artist for the metadata lookup (upload tag artist), 005.
    """

    source_type: SourceType
    source_key: str
    title: str
    artist: str | None
    youtube_url: str | None
    album: str | None = None
    isrc: str | None = None
    artwork: ArtworkCandidate | None = None
    lookup_title: str | None = None
    lookup_artist: str | None = None


@dataclass(frozen=True, slots=True)
class FetchedAudio:
    """A downloaded audio file plus the metadata the download reported.

    Attributes:
        path: The downloaded file (any container/codec ffmpeg can read).
        title: Title reported by the download (e.g. the YouTube video title), or None.
        artist: Artist or uploader reported by the download, or None.
        album: Album reported by the download (yt-dlp ``album``), or None; 005.
        artwork: Picture offered by the download (largest YouTube thumbnail), or None; 005.
        lookup_title: Cleaned title for the metadata lookup (research R-04), or None; 005.
        lookup_artist: Cleaned artist for the metadata lookup, or None; 005.
    """

    path: Path
    title: str | None = None
    artist: str | None = None
    album: str | None = None
    artwork: ArtworkCandidate | None = None
    lookup_title: str | None = None
    lookup_artist: str | None = None


@dataclass(frozen=True, slots=True)
class NormalizedAudio:
    """Audio converted to the pipeline format: 44.1 kHz, stereo, 16-bit PCM WAV.

    Attributes:
        wav_path: Path of the WAV file.
        duration_seconds: Duration in seconds (> 0, <= 900).
        sha256: Lower-case hex SHA-256 of the WAV file content (second cache key).
    """

    wav_path: Path
    duration_seconds: float
    sha256: str


@dataclass(frozen=True, slots=True)
class SeparationResult:
    """Output of a separator.

    Attributes:
        model: Model that produced the stems (``htdemucs_6s``, ``htdemucs`` or ``fake``).
        stems: Stem name -> FLAC file (44.1 kHz, stereo, 16-bit), sample-aligned with the input.
        device: Where the separation ran (feature 003); None for separators without a device.
    """

    model: ModelName
    stems: dict[StemName, Path]
    device: JobDevice | None = None


class SourceResolver(Protocol):
    """Validates a user URL and resolves it to a downloadable source (no network for YouTube)."""

    def resolve(self, url: str) -> SourceInfo:
        """Validate and resolve a YouTube video URL or a Spotify track URL.

        Spotify URLs are resolved to metadata via spotDL and matched to a public YouTube video;
        DRM-protected Spotify audio is never accessed.

        Args:
            url: The URL as entered by the user.

        Returns:
            The resolved source with its source key.

        Raises:
            InvalidSourceError: Host not allow-listed, playlist/album, or malformed URL.
            SourceUnavailableError: Spotify track has no YouTube match or metadata lookup failed.
        """
        ...


class UploadIntake(Protocol):
    """Accepts an uploaded file that the API has already stored and size-checked."""

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        """Validate an upload and derive its source identity.

        The size limit (200 MiB, HTTP 413) is enforced by the API layer while streaming; this
        method checks the extension and hashes the stored bytes. ``file_name`` is only used for
        the title and the extension check, never as a path component.

        Args:
            file_name: Client-supplied file name, e.g. ``"My Song.mp3"``.
            path: Where the API stored the uploaded bytes.

        Returns:
            Source with ``source_type=upload``, ``source_key="upload:<64 lower-hex sha256>"``,
            ``title`` = file name without extension, ``artist=None``, ``youtube_url=None``.

        Raises:
            UnsupportedMediaError: Extension is not ``.mp3``, ``.wav`` or ``.flac``
                (case-insensitive); HTTP 415.
            EmptyUploadError: The stored file is empty; HTTP 400.
            UploadTooLargeError: The stored file exceeds the size limit; HTTP 413.
        """
        ...


class AudioFetcher(Protocol):
    """Downloads the audio of a resolved URL source."""

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> FetchedAudio:
        """Download the best audio-only stream of ``source.youtube_url`` into ``work_dir``.

        Args:
            source: A resolved YouTube or Spotify source (``youtube_url`` is not None).
            work_dir: Existing, job-private directory to write into.
            progress: Receives download progress.

        Returns:
            The downloaded file plus the title/artist the source reported. The pipeline uses
            them for YouTube jobs; for Spotify jobs the spotDL metadata stays authoritative.

        Raises:
            SourceUnavailableError: Download failed.
            AudioTooLongError: The source reports a duration over the limit (checked early).
        """
        ...


class AudioNormalizer(Protocol):
    """Converts any input audio to the pipeline WAV format with ffmpeg."""

    def normalize(self, input_path: Path, output_wav: Path) -> NormalizedAudio:
        """Convert ``input_path`` to 44.1 kHz stereo 16-bit WAV at ``output_wav``.

        Args:
            input_path: Downloaded or uploaded audio file.
            output_wav: Destination path (parent exists).

        Returns:
            The normalised audio with duration and content hash.

        Raises:
            AudioDecodeError: ffmpeg failed or the file contains no audio stream.
            AudioTooLongError: Duration exceeds 15 minutes.
        """
        ...


class Separator(Protocol):
    """Splits a mix into stems."""

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        """Separate ``wav_path`` into FLAC stems written to ``out_dir/<name>.flac``.

        Implementations try ``htdemucs_6s`` and fall back to ``htdemucs`` when it cannot be loaded.

        Args:
            wav_path: Normalised input audio.
            out_dir: Existing directory for the stem files.
            progress: Receives separation progress.

        Returns:
            The model used and the stem files.

        Raises:
            SeparationError: Both models failed.
        """
        ...


class BeatAnalyzer(Protocol):
    """Detects tempo, beats and downbeats."""

    def analyze(self, wav_path: Path) -> BeatGrid:
        """Analyse the full mix.

        Never raises for "no clear pulse": returns a grid with ``reliable=False`` instead.

        Args:
            wav_path: Normalised input audio.

        Returns:
            The beat grid (beats ascending, downbeats a subset of beats).
        """
        ...


@dataclass(frozen=True, slots=True)
class IngestAdapters:
    """Adapters provided by the audio-ingest package (``ingest.create_ingest_adapters``).

    Attributes:
        resolver: Resolves YouTube/Spotify URLs.
        upload_intake: Accepts uploaded files.
        fetcher: Downloads resolved URL sources.
        normalizer: Converts audio to the pipeline WAV format.
    """

    resolver: SourceResolver
    upload_intake: UploadIntake
    fetcher: AudioFetcher
    normalizer: AudioNormalizer


@dataclass(frozen=True, slots=True)
class PipelineAdapters:
    """Every adapter the job pipeline needs; built by the composition root or by tests.

    Attributes:
        ingest: The audio-ingest adapters.
        separator: The stem separator (Demucs or fake).
        analyzer: The beat analyzer.
    """

    ingest: IngestAdapters
    separator: Separator
    analyzer: BeatAnalyzer


class DetailModel(Protocol):
    """A loaded detail separation model (MDX23C DrumSep, UVR KARA 2, MVSep Mega 53 or a fake)."""

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        """Split one parent stem (or the sum of several) into the model's raw outputs.

        Output names are the model's own (e.g. ``hh``, ``bowed_strings``); folding them into
        ``DetailStemName`` values is done by the caller. Every output has the input's shape.

        Args:
            audio: Parent audio ``(channels, frames)``.
            progress: Receives the fraction of inference chunks done; it may raise
                ``DetailCancelledError`` to stop the model between chunks.

        Returns:
            Model output name -> audio of the same shape as ``audio``.

        Raises:
            DetailSeparationError: Inference failed (including out of memory).
            DetailCancelledError: Propagated from ``progress``.
        """
        ...


@runtime_checkable
class DetailModelProvider(Protocol):
    """Loads detail models, downloading and verifying their weights on first use."""

    def get(self, model_id: DetailModelId) -> DetailModel:
        """Return the model ``model_id``, loading it once and reusing it afterwards.

        Args:
            model_id: Registry id, e.g. ``mdx23c-drumsep-6s``.

        Returns:
            The ready model.

        Raises:
            DetailModelUnavailableError: Download failed (offline) or the file is corrupt.
        """
        ...


@dataclass(frozen=True, slots=True)
class DetailAdapters:
    """Adapters of the detail pipeline (``detail.create_detail_adapters``).

    Attributes:
        models: Provides the detail models (real or fake).
    """

    models: DetailModelProvider


@dataclass(frozen=True, slots=True)
class GpuNeed:
    """What a model step needs from the GPU (ADR-012).

    Attributes:
        label: Readable step name used in fallback reasons, e.g. ``"Mega 53-stem"``.
        memory_mib: Free GPU memory the step needs (measured peak plus margin); when less is
            free the step runs on the CPU instead.
    """

    label: str
    memory_mib: int


@dataclass(frozen=True, slots=True)
class DeviceUse:
    """Where one model step actually ran.

    Attributes:
        kind: ``gpu`` or ``cpu``.
        torch_device: The torch device string the step was given (``cuda`` or ``cpu``).
        name: GPU name when ``kind`` is ``gpu``.
        fallback_reason: Why the step ran on the CPU although the worker's device is the GPU
            (too little free memory, out of GPU memory); None otherwise.
    """

    kind: ProcessingDeviceKind
    torch_device: str
    name: str | None = None
    fallback_reason: str | None = None


@runtime_checkable
class DeviceRunner(Protocol):
    """Runs model steps on the worker's device with the GPU policy of ADR-012.

    The active device is probed once at start-up (CPU when no usable GPU, with a reason). On a
    GPU worker every step holds the GPU lease (one GPU step at a time), runs on the CPU when the
    free GPU memory is below ``need.memory_mib``, and is retried once on the CPU when it raises a
    GPU out-of-memory error. Other errors propagate unchanged.
    """

    @property
    def info(self) -> DeviceInfo:
        """The active device detected at start-up (reported by ``GET /health``)."""
        ...

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        """Run ``work`` on the GPU or the CPU and report where it ran.

        Args:
            work: The step; called with ``"cuda"`` or ``"cpu"`` (twice at most: GPU, then CPU).
            need: Free GPU memory the step needs.

        Returns:
            The step's result and where it ran.

        Raises:
            Exception: Whatever ``work`` raises, except a GPU out-of-memory error on the first
                (GPU) attempt, which triggers the single CPU retry.
        """
        ...


def merge_device(record: JobDevice | None, use: DeviceUse) -> JobDevice:
    """Fold one step's ``use`` into a job's device record (data-model.md 2.2).

    Args:
        record: The job's record so far (None before its first model step).
        use: Where the step ran.

    Returns:
        The updated record: ``gpu`` once any step ran on the GPU; ``cpu_fallback`` once any step
        fell back to the CPU, with the latest reason.
    """
    base = record or JobDevice(kind="cpu")
    return JobDevice(
        kind=_merged_kind(base.kind, use.kind),
        name=base.name or use.name,
        cpu_fallback=base.cpu_fallback or use.fallback_reason is not None,
        fallback_reason=use.fallback_reason or base.fallback_reason,
    )


def _merged_kind(first: ProcessingDeviceKind, second: ProcessingDeviceKind) -> ProcessingDeviceKind:
    """``gpu`` when either step ran on the GPU, else ``cpu``."""
    return "gpu" if "gpu" in (first, second) else "cpu"


# ------------------------------------------------------------------ feature 005: artwork, metadata


class ImageNormalizer(Protocol):
    """Turns a picture candidate into the two normalised JPEGs (Pillow, audio-ingest)."""

    def normalize(self, candidate: ArtworkCandidate) -> ArtworkImages:
        """Fetch (for URL candidates) and re-encode a picture.

        EXIF orientation is applied and all metadata stripped, alpha flattened, the first frame
        of an animation used, the picture centre-cropped to a square and encoded as a 600 px
        cover (<= 200 KB) and a 160 px thumbnail (<= 20 KB).

        Args:
            candidate: The picture to normalise.

        Returns:
            The cover and thumbnail JPEGs with the cover's SHA-256.

        Raises:
            ArtworkFetchError: A remote picture could not be downloaded.
            NotAnImageError: The bytes cannot be decoded.
            UnsupportedImageTypeError: The image is not JPEG, PNG or WebP.
            ImageTooLargeError: The image has more than 40 megapixels.
        """
        ...


@dataclass(frozen=True, slots=True)
class MetadataQuery:
    """What a metadata job looks up (the body of ``POST /v1/metadata-jobs``).

    Attributes:
        title: Title to search for (1..500 characters).
        artist: Artist, when known.
        album: Album, when known.
        duration_seconds: Duration of the normalised audio, when known (search window +-5 s).
        isrc: ISRC (Spotify sources); tried before the search.
    """

    title: str
    artist: str | None = None
    album: str | None = None
    duration_seconds: float | None = None
    isrc: str | None = None


@dataclass(frozen=True, slots=True)
class MetadataFindings:
    """Output of a lookup: the contract result plus the bytes of its picture.

    Attributes:
        result: The result served by ``GET /v1/metadata-jobs/{id}/result``.
        artwork: The normalised Cover Art Archive picture whose ``info`` is ``result.artwork``,
            or None when there is none.
    """

    result: MetadataResult
    artwork: ArtworkImages | None = None


class MetadataLookup(Protocol):
    """Looks a song up in MusicBrainz, Cover Art Archive, Wikidata and Wikipedia (ADR-014)."""

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        """Find the recording, release group, cover and artist facts for ``query``.

        Throttled to 1 request/s per service with a User-Agent without personal data. Failures
        after the recording was found become ``result.warnings``; lookups switched off give
        outcome ``disabled`` without any request.

        Args:
            query: What to look up.
            progress: Receives the stage ("Searching MusicBrainz", "Fetching album cover",
                "Reading Wikidata", "Reading Wikipedia").

        Returns:
            The result (outcome ``found``, ``not-found`` or ``disabled``) and its picture.

        Raises:
            MetadataLookupError: The recording search itself failed after retries.
        """
        ...


# ------------------------------------------------------------------ feature 007: rendering

_UUID = r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"

STEM_PATH_PATTERN: Final = re.compile(
    rf"tracks/{_UUID}/stems/(vocals|drums|bass|guitar|piano|other)\.flac"
)
"""A standard stem of a track (render inputs; section job ``stems``)."""

DETAIL_PATH_PATTERN: Final = re.compile(
    rf"tracks/{_UUID}/detail/(vocals|drums|guitar|piano|other)/[a-z][a-z-]{{0,31}}\.flac"
)
"""A detail stem of a track (render inputs; section job ``details``)."""

SCORE_MUSICXML_PATTERN: Final = re.compile(rf"assets/{_UUID}/score\.musicxml")
"""The MusicXML of a score asset (score-view job ``musicxml``, feature 014)."""

LIBRARY_PATH_PATTERNS: Final[tuple[re.Pattern[str], ...]] = (
    STEM_PATH_PATTERN,
    DETAIL_PATH_PATTERN,
    re.compile(rf"assets/{_UUID}/[a-z][a-z0-9-]{{0,31}}\.(flac|wav|mp3)"),
    SCORE_MUSICXML_PATTERN,
)
"""Allow-list of library input paths relative to the library root (007 data-model.md 2.1;
feature 014 adds the score MusicXML). Section jobs also require ``STEM_PATH_PATTERN`` /
``DETAIL_PATH_PATTERN`` per field and score-view jobs ``SCORE_MUSICXML_PATTERN`` (400 otherwise);
render and transcription jobs keep accepting any allowed path (a MusicXML fails their decoding).

A path is allowed when one pattern matches it completely (``fullmatch``); the path guard then
resolves it and requires the real path to stay inside the library root.
"""


def is_allowed_library_path(path: str) -> bool:
    """Return True when ``path`` fully matches one of ``LIBRARY_PATH_PATTERNS``."""
    return any(pattern.fullmatch(path) for pattern in LIBRARY_PATH_PATTERNS)


@runtime_checkable
class RenderProgress(Protocol):
    """Receives the progress of a render job."""

    def __call__(self, fraction: float, /) -> None:
        """Report progress.

        Args:
            fraction: 0.0..1.0, non-decreasing (pass 1 = 0..0.5, pass 2 = 0.5..1).

        Raises:
            RenderCancelledError: The job was cancelled; the renderer stops after this block.
        """
        ...


class LibraryReader(Protocol):
    """Read-only access to the API library mounted into the worker (ADR-016 D5)."""

    def status(self) -> LibraryHealth:
        """Report whether the library root exists and is listable (``Health.library``)."""
        ...

    def resolve(self, path: str) -> Path:
        """Check a library-relative input path and return the real file path.

        Args:
            path: Path from ``RenderInput.path``.

        Returns:
            The resolved file inside the library root.

        Raises:
            InputPathError: Not on the allow-list, or the real path escapes the root.
            InputNotFoundError: The file does not exist.
            RenderError: The library root is unset or unreadable (``LIBRARY_UNAVAILABLE_TEXT``).
        """
        ...


class RenderExecutor(Protocol):
    """Renders a flat render plan to one audio file (two passes, block-wise; data-model.md 3)."""

    def validate(self, request: RenderJobRequest) -> None:
        """Check every input path before the job is queued (``POST /v1/render-jobs``).

        Args:
            request: The request to check.

        Raises:
            InputPathError: An input path is not allowed (HTTP 400).
            InputNotFoundError: An input file does not exist (HTTP 404).
            RenderError: The library is not available.
        """
        ...

    def render(
        self, request: RenderJobRequest, out_dir: Path, progress: RenderProgress
    ) -> RenderResult:
        """Mix the inputs, protect against clipping and encode ``out_dir/output.<ext>``.

        Args:
            request: The validated render request.
            out_dir: Existing, job-private output directory.
            progress: Receives progress after each block; may raise ``RenderCancelledError``.

        Returns:
            The written file's facts.

        Raises:
            RenderError: Inputs unreadable, unsupported sample rate, silent result
                (``SILENT_RENDER_TEXT``) or encoder failure.
            RenderCancelledError: Propagated from ``progress``.
        """
        ...


# ------------------------------------------------------------------ shared: downloadable weights


@dataclass(frozen=True, slots=True)
class WeightsFile:
    """A model weights file downloaded at first use into ``STEMSTUDIO_MODEL_DIR`` (never committed).

    Attributes:
        model_id: Stable id (cache file name stem, ``Health`` key).
        url: HTTPS source.
        sha256: Expected digest; a mismatch deletes the file and fails the download.
        size_bytes: Expected size (shown before the first download).
        licence: SPDX id of the weights licence.
    """

    model_id: str
    url: str
    sha256: str
    size_bytes: int
    licence: str


# ------------------------------------------------------------------ feature 006: lyrics

ALIGN_SAMPLE_RATE: Final = 16_000
"""Sample rate (Hz) of the mono float32 audio handed to the aligner."""

WHISPER_GPU_NEED: Final = GpuNeed("Lyrics alignment (Whisper large-v3-turbo)", 5120)
"""Free GPU memory an alignment needs: measured NVML peak 4,118 MiB + 20 %, rounded up to 256
(006 research.md P0-2, RTX 5070 Ti, 4:22 song)."""

WHISPER_TURBO_WEIGHTS: Final = WeightsFile(
    model_id="large-v3-turbo",
    url=(
        "https://openaipublic.azureedge.net/main/whisper/models/"
        "aff26ae408abcba5fbf8813c21e62b0941638c5f6eebfb145be0c9839262a19a/large-v3-turbo.pt"
    ),
    sha256="aff26ae408abcba5fbf8813c21e62b0941638c5f6eebfb145be0c9839262a19a",
    size_bytes=1_617_941_637,
    licence="MIT",
)
"""Whisper large-v3-turbo weights (``STEMSTUDIO_MODEL_DIR/whisper/large-v3-turbo.pt``)."""

ALIGN_SECONDS_PER_AUDIO_SECOND: Final[dict[ProcessingDeviceKind, float]] = {"gpu": 0.03, "cpu": 0.8}
"""Alignment wall time per audio second (P0-2: GPU 0.010-0.026, CPU 8 threads 0.79)."""

ALIGN_OVERHEAD_SECONDS: Final[dict[ProcessingDeviceKind, float]] = {"gpu": 20.0, "cpu": 15.0}
"""Fixed alignment time per job: model load (P0-2: GPU 17-19 s, CPU 12-14 s)."""

COLLAPSE_SHORT_WORD_MS: Final = 20
"""Aligned words shorter than this count towards a collapse (006 research.md P0-2)."""

COLLAPSE_SHORT_WORD_SHARE: Final = 0.35
"""An alignment has collapsed when more than this share of its words is shorter than
``COLLAPSE_SHORT_WORD_MS`` (P0-2: good runs 9-16 %, collapsed runs 69-98 %). The adapter then
retries once with ``vad=True`` and fails with ``ALIGNMENT_FAILED_TEXT`` if that collapses too."""

COLLAPSE_OVERLAP_SHARE: Final = 0.25
"""An alignment has also collapsed when more than this share of its lines starts before the
previous line ends."""

ALIGN_DOWNLOAD_SECONDS: Final = 140.0
"""Extra time of the first alignment while the 1.6 GB weights are downloaded (P0-1: 136 s)."""


def estimate_alignment_seconds(
    duration_seconds: float, device: ProcessingDeviceKind, *, model_cached: bool
) -> float:
    """Expected wall time of an alignment (``LyricsJob.estimatedSeconds``, research.md R-04).

    Args:
        duration_seconds: Length of the vocal stem.
        device: Device the alignment will run on.
        model_cached: False adds the weights download.

    Returns:
        Seconds, rounded up to whole seconds.
    """
    seconds = duration_seconds * ALIGN_SECONDS_PER_AUDIO_SECOND[device]
    seconds += ALIGN_OVERHEAD_SECONDS[device] + (0.0 if model_cached else ALIGN_DOWNLOAD_SECONDS)
    return float(math.ceil(seconds))


class LyricsError(WorkerError):
    """A lyrics job failed; the message is one of the fixed texts of data-model.md 2.4."""


class LyricsUnavailableError(LyricsError):
    """LRCLIB could not be reached after retries (``LYRICS_UNREACHABLE_TEXT``)."""

    def __init__(self) -> None:
        """Use the fixed text."""
        super().__init__(LYRICS_UNREACHABLE_TEXT)


class LyricsUnusableError(LyricsError):
    """The chosen record cannot be parsed or exceeds the limits (``LYRICS_UNUSABLE_TEXT``)."""

    def __init__(self) -> None:
        """Use the fixed text."""
        super().__init__(LYRICS_UNUSABLE_TEXT)


class AlignmentError(LyricsError):
    """An alignment failed; the message is a fixed text (missing/silent stem, download, other)."""


class AlignmentCancelledError(LyricsError):
    """The alignment was cancelled (raised from the progress callback)."""


@dataclass(frozen=True, slots=True)
class LyricsQuery:
    """What a lookup searches (the body of a ``mode: lookup`` lyrics job).

    Attributes:
        title: Cleaned title (005 ``lookupTitle``).
        duration_seconds: Duration of the normalised audio (records within +-2 s match).
        artist: Cleaned artist, when known.
        album: Album, when known (tried first).
    """

    title: str
    duration_seconds: float
    artist: str | None = None
    album: str | None = None


class LyricsLookup(Protocol):
    """Finds time-synced lyrics on LRCLIB and normalises them (audio-ingest, ``lyrics/``)."""

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        """Search LRCLIB (get with album, get, search) and build one ``TimedLyrics`` document.

        At most 3 requests, >= 500 ms apart, ``Retry-After`` honoured, the 005 User-Agent; lookups
        switched off give outcome ``disabled`` without any request.

        Args:
            query: What to search.
            progress: Receives the stage ``LYRICS_STAGE_SEARCHING``.

        Returns:
            The result (``found`` word/line/plain, ``instrumental``, ``not-found``, ``disabled``).

        Raises:
            LyricsUnavailableError: LRCLIB stayed unreachable.
            LyricsUnusableError: The chosen record could not be read.
        """
        ...


@dataclass(frozen=True, slots=True)
class AlignRequest:
    """Input of one alignment.

    Attributes:
        audio: The vocal stem as mono float32 at ``ALIGN_SAMPLE_RATE`` (``vocals.lead`` when the
            detail split exists, else ``vocals``; P0-2 showed ``vocals.lead`` aligns far better).
        text: Plain lyrics, one line per line break.
        language: ISO 639 hint; None = detect.
        refine: A line-synced document whose line times are kept (only words are aligned).
    """

    audio: npt.NDArray[np.float32]
    text: str
    language: str | None = None
    refine: TimedLyrics | None = None


class Aligner(Protocol):
    """Aligns lyrics text to a vocal stem (stable-ts + Whisper, stem-separation ``alignment/``)."""

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:
        """Align ``request.text`` to ``request.audio``.

        Called inside ``DeviceRunner.run(work, WHISPER_GPU_NEED)``; ``device`` is the torch device
        the runner chose. The weights are downloaded on first use (stage
        ``LYRICS_STAGE_DOWNLOADING``), then aligned with ``original_split=True`` (stage
        ``LYRICS_STAGE_ALIGNING``).

        Args:
            request: Audio, text, language hint and optional line constraints.
            device: ``"cuda"`` or ``"cpu"``.
            progress: Receives fraction and stage; may raise ``AlignmentCancelledError``.

        Returns:
            A word-synced document with ``origin = aligned`` (the caller sets ``source``).

        Raises:
            AlignmentError: Download failed or the alignment failed (fixed texts).
            AlignmentCancelledError: Propagated from ``progress``.
        """
        ...


# ------------------------------------------------------------------ feature 008: sheet music

PIANO_GPU_NEED: Final = GpuNeed("Piano transcription", 1280)
"""Free GPU memory of the piano CRNN: measured NVML peak 952 MiB + 20 %, rounded up to 256
(008 research.md S-04, RTX 5070 Ti, 60 s)."""

BASIC_PITCH_WEIGHTS: Final = WeightsFile(
    model_id=BASIC_PITCH_MODEL_ID,
    url=(
        "https://raw.githubusercontent.com/spotify/basic-pitch/v0.4.0/"
        "basic_pitch/saved_models/icassp_2022/nmp.onnx"
    ),
    sha256="2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec",
    size_bytes=230_444,
    licence="Apache-2.0",
)
"""basic-pitch ICASSP 2022 ONNX model (tag v0.4.0, verified in Phase 0)."""

PIANO_WEIGHTS: Final = WeightsFile(
    model_id=PIANO_MODEL_ID,
    url=("https://zenodo.org/records/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth"),
    sha256="c3fa9730725bf4a762f1c14bc80cd5986eacda01b026f5a4a2525cd607876141",
    size_bytes=171_966_578,
    licence="CC-BY-4.0",
)
"""ByteDance high-resolution piano transcription CRNN (note + pedal), Zenodo record 4034264."""

TranscriptionProgress = RenderProgress
"""Receives the progress of a transcription step (0..1 of that step); may raise
``TranscriptionCancelledError``."""


class TranscriptionError(WorkerError):
    """A transcription failed; the message is shown to the user."""


class NoNotesError(TranscriptionError):
    """No notes (or drum hits) at all were detected (``NO_NOTES_TEXT``)."""

    def __init__(self) -> None:
        """Use the fixed text."""
        super().__init__(NO_NOTES_TEXT)


class TranscriptionCancelledError(TranscriptionError):
    """The job was deleted while running (raised from the progress callback)."""


@dataclass(frozen=True, slots=True)
class NoteEvent:
    """One transcribed note; times in seconds relative to the input (range) start.

    Attributes:
        start: Onset (s, >= 0).
        end: Offset (s, > start).
        pitch: MIDI pitch 0..127.
        velocity: Loudness 0..1.
    """

    start: float
    end: float
    pitch: int
    velocity: float


@dataclass(frozen=True, slots=True)
class DrumHit:
    """One drum onset; time in seconds relative to the input (range) start.

    Attributes:
        time: Onset (s, >= 0).
        piece: Detail stem it came from.
        strength: Onset strength 0..1 (relative to the piece's 99th percentile).
    """

    time: float
    piece: DrumPiece
    strength: float


@dataclass(frozen=True, slots=True)
class Transcription:
    """Output of a pitched transcriber.

    Attributes:
        notes: Notes in onset order.
        device: Where the model ran (the piano CRNN through the ``DeviceRunner``); None for the
            CPU-only engines.
    """

    notes: tuple[NoteEvent, ...]
    device: DeviceUse | None = None


class PitchTranscriber(Protocol):
    """Audio -> notes for pitched profiles (basic-pitch ONNX or the piano CRNN)."""

    @property
    def engine(self) -> ScoreEngine:
        """Engine name recorded in ``ScoreSummary.engine``."""
        ...

    @property
    def attribution(self) -> str | None:
        """Model credit (``PIANO_ATTRIBUTION`` for the piano CRNN), else None."""
        ...

    def transcribe(
        self, audio_path: Path, window: RenderRange | None, progress: TranscriptionProgress
    ) -> Transcription:
        """Transcribe ``audio_path`` (or its ``window``) to notes.

        Args:
            audio_path: Resolved library file (44.1 kHz FLAC/WAV/MP3).
            window: Part to transcribe; times in the result are relative to its start.
            progress: Receives 0..1 of the transcription step.

        Returns:
            The raw notes (cleaning is the notator's job).

        Raises:
            TranscriptionError: The model could not be loaded or run.
            TranscriptionCancelledError: Propagated from ``progress``.
        """
        ...


class DrumTranscriber(Protocol):
    """Drum detail stems -> drum hits (onset detection; beat-analysis, ``analysis/``)."""

    def transcribe(
        self,
        inputs: Mapping[DrumPiece, Path],
        window: RenderRange | None,
        progress: TranscriptionProgress,
    ) -> tuple[DrumHit, ...]:
        """Detect the onsets of every given piece (missing pieces give no hits).

        Args:
            inputs: Resolved detail stem file per piece.
            window: Part to transcribe; times in the result are relative to its start.
            progress: Receives 0..1, one step per piece.

        Returns:
            Hits in time order, after the per-piece bleed gate (data-model.md 2.3).

        Raises:
            TranscriptionCancelledError: Propagated from ``progress``.
        """
        ...


@dataclass(frozen=True, slots=True)
class NotationSettings:
    """How notes become a score (the request fields the notator needs).

    Attributes:
        profile: Instrument profile (clefs, range, voices, caps).
        grid: Time base, already shifted to the input start.
        resolution: Grid resolution (``None`` in the request is resolved by the caller to the
            profile default).
        key_override: Key to use instead of the estimate (ignored for drums).
        titles: Title, subtitle and part name.
        duration_seconds: Length of the input (manual grids generate beats over it).
        tablature: Tab staff settings (guitar/bass, 1.9.0); None = no tablature.
        part: Written instrument and clef (1.9.0); None = concert pitch, profile clef.
        chords: Chord symbols above the top staff (1.9.0); empty = none.
        prefer_flats: Spelling hint for the chord symbols at concert pitch (1.9.0).
        single_piece: ``drums`` with one piece (1.9.0): no ``missing-drum-piece`` warnings.
    """

    profile: ScoreProfile
    grid: BeatGridTempo | ManualTempo
    resolution: ScoreResolution
    key_override: KeyName | None
    titles: ScoreTitles
    duration_seconds: float
    tablature: TablatureSettings | None = None
    part: PartSettings | None = None
    chords: tuple[ScoreChordSymbol, ...] = ()
    prefer_flats: bool = False
    single_piece: DrumPiece | None = None


@dataclass(frozen=True, slots=True)
class ScoreDocument:
    """Output of the notator: the files without the PDF, plus the facts for the summary.

    Attributes:
        musicxml: MusicXML 4.0 bytes (engraving input and download).
        midi: Standard MIDI file bytes.
        key: Estimated or overridden key; None for drums.
        bpm: Rounded tempo of the tempo mark.
        time_signature: ``"<beatsPerBar>/4"``.
        bars: Measures including a pickup bar.
        notes: Notes (hits) in the score after cleaning.
        warnings: Cleaning and key warnings (data-model.md 2.7).
        tablature: Tab facts (1.9.0); None without tablature.
        part: Part facts (1.9.0); None for concert pitch.
        chords_included: Chord symbols were written (1.9.0).
    """

    musicxml: bytes
    midi: bytes
    key: KeyInfo | None
    bpm: int
    time_signature: str
    bars: int
    notes: int
    warnings: tuple[ScoreWarning, ...] = ()
    tablature: TablatureSummary | None = None
    part: PartSummary | None = None
    chords_included: bool = False


class Notator(Protocol):
    """Notes -> score (clean, quantise, key and spelling, music21; beat-analysis ``notation/``)."""

    def notate(self, notes: Sequence[NoteEvent], settings: NotationSettings) -> ScoreDocument:
        """Build the score of a pitched profile.

        Raises:
            NoNotesError: Nothing is left after cleaning.
        """
        ...

    def notate_drums(self, hits: Sequence[DrumHit], settings: NotationSettings) -> ScoreDocument:
        """Build the two-voice percussion score of the ``drums`` profile.

        Raises:
            NoNotesError: No hits.
        """
        ...


@dataclass(frozen=True, slots=True)
class EngravedScore:
    """Output of the engraver.

    Attributes:
        pdf: The A4 PDF bytes.
        pages: Page count.
    """

    pdf: bytes
    pages: int


class ScoreEngraver(Protocol):
    """MusicXML -> PDF (Verovio SVG pages, CairoSVG, pypdf; audio-ingest ``engraving/``)."""

    def engrave(self, musicxml: bytes, title: str, subject: str | None) -> EngravedScore:
        """Render the score to an A4 PDF with the options of data-model.md 3.

        Args:
            musicxml: The notator's MusicXML.
            title: PDF document title (the asset name).
            subject: PDF subject (the attribution), or None.

        Returns:
            The PDF and its page count.

        Raises:
            TranscriptionError: The Leipzig font is missing (``MUSIC_FONT_MISSING_TEXT``) or
                engraving failed.
        """
        ...


# ------------------------------------------------------------------ storage (feature 013)


class LibraryUnavailableError(WorkerError):
    """The read-only library mount (``STEMSTUDIO_LIBRARY_DIR``) is unset or unreadable; HTTP 409."""

    def __init__(self) -> None:
        """Create the error with ``LIBRARY_UNAVAILABLE_TEXT``."""
        super().__init__(LIBRARY_UNAVAILABLE_TEXT)


class StemMissingError(WorkerError):
    """A library stem of the requested model is missing; HTTP 404 with ``stem``.

    Attributes:
        stem: The missing stem.
    """

    def __init__(self, stem: StemName) -> None:
        """Create the error for ``stem``."""
        super().__init__(f"The library has no {stem} stem for this track.")
        self.stem = stem


@runtime_checkable
class StorageMeter(Protocol):
    """Measures the worker volumes without reading file contents (``GET /v1/storage``)."""

    def measure(self) -> WorkerStorage:
        """Walk the data and model directories (links are not followed) and report their use.

        Returns:
            Free/total bytes per volume, the per-audio cache entries with their job ids and
            ``inUse`` flag, job files, scratch and the model caches per top-level folder.
            Implementations may cache the report for 30 seconds.
        """
        ...


@runtime_checkable
class CacheJanitor(Protocol):
    """Removes regenerable worker data that no job uses (``POST /v1/storage/cleanup``)."""

    def clean(self, request: StorageCleanupRequest) -> StorageCleanupResult:
        """Remove the requested cache entries, scratch and old job files under the queue lock.

        Args:
            request: Entries to remove (``["*"]`` = every entry not in use), ``scratch`` and
                ``oldJobFiles`` switches.

        Returns:
            Freed bytes, removed entries and the skipped ones (``inUse``, ``notFound``). Removing
            an entry also removes its ``cache/by-source`` pointers and marks the standard jobs
            that referenced it as evicted.
        """
        ...


@runtime_checkable
class LibraryStemReader(Protocol):
    """Opens API library stems from the read-only ``/library`` mount (rehydration, ADR-022 D10)."""

    def open(self, track_id: UUID, stem: StemName) -> BinaryIO:
        """Open ``<library>/tracks/<track_id>/stems/<stem>.flac`` for binary reading.

        The path is built only from the UUID (``str(track_id)``) and the stem enum; no request
        string becomes a path component. The caller closes the stream.

        Args:
            track_id: Backend track id.
            stem: Stem name.

        Returns:
            A readable binary stream of the FLAC file.

        Raises:
            LibraryUnavailableError: The library mount is unset or unreadable.
            StemMissingError: The stem file does not exist.
        """
        ...


# ------------------------------------------------------------------ practice lane (feature 014)


class StructureError(WorkerError):
    """Section detection failed; the readable message becomes the section job's ``error``."""


class ScoreViewError(WorkerError):
    """A score view could not be laid out (unreadable MusicXML, Verovio failure, limits)."""

    def __init__(self, message: str = SCORE_VIEW_FAILED_TEXT) -> None:
        """Create the error; the default text is ``SCORE_VIEW_FAILED_TEXT``."""
        super().__init__(message)


class DocumentError(WorkerError):
    """A setlist PDF could not be made (missing DejaVu fonts, ReportLab failure)."""

    def __init__(self, message: str = DOCUMENT_FAILED_TEXT) -> None:
        """Create the error; the default text is ``DOCUMENT_FAILED_TEXT``."""
        super().__init__(message)


SectionProgress = RenderProgress
"""Progress callback of a section job (0..1, non-decreasing)."""

StemFiles = Mapping[str, Path]
"""Resolved input files of a section job by channel: the standard stem names of
``SectionJobRequest.stems`` and the detail channels of ``details`` (``guitar.electric-guitar``).
The job service resolves every path through the path guard before the analyser runs."""


@runtime_checkable
class StructureAnalyzer(Protocol):
    """Finds the sections of a song from its stems and beat grid (beat-analysis, ADR-023 D1).

    Attributes:
        name: ``stems-novelty`` or ``fake`` (``SectionResult.analyzer.name``).
        version: Analyser version (``1.0.0``).
    """

    name: StructureAnalyzerName
    version: str

    def analyze(
        self, request: SectionJobRequest, files: StemFiles, progress: SectionProgress
    ) -> SectionResult:
        """Detect, label and snap the sections (data-model 2.2-2.4).

        Args:
            request: The job request (grid, duration, ``maxSections``).
            files: The resolved stem and detail files of ``request``.
            progress: Receives 0..1.

        Returns:
            Ordered, contiguous sections covering ``0..durationSeconds`` with snap and warnings.

        Raises:
            StructureError: The stems cannot be read or analysed.
        """
        ...


@dataclass(frozen=True, slots=True)
class ScoreView:
    """A laid-out score: SVG page documents and the bar map (data-model 2.5).

    Attributes:
        pages: One SVG document per page, the Leipzig ``@font-face`` as the first child.
        bars: One box per bar in document order, times relative to the score's input start.
        engine_version: Verovio version string.
        page_width_px: Width of every page in CSS pixels (840 with the 008 options).
        page_height_px: Height of every page in CSS pixels (1188 with the 008 options).
    """

    pages: tuple[str, ...]
    bars: tuple[BarBox, ...]
    engine_version: str
    page_width_px: int = 840
    page_height_px: int = 1188


@runtime_checkable
class ScoreViewRenderer(Protocol):
    """Lays out MusicXML as SVG pages with a bar map (audio-ingest, ``engraving/score_view.py``)."""

    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView:
        """Run the two Verovio passes (plain pages, bounding boxes) and time the bars.

        Args:
            musicxml: The score's MusicXML file content.
            grid: The 008 time base the score was made with.
            duration_seconds: Length of the score's input (the last bar ends here).

        Returns:
            Pages (at most ``MAX_SCORE_PAGES``) and bars (at most ``MAX_SCORE_BARS``).

        Raises:
            ScoreViewError: The MusicXML cannot be read or laid out, or exceeds the limits.
        """
        ...


@dataclass(frozen=True, slots=True)
class SetlistDocument:
    """A rendered setlist PDF.

    Attributes:
        pdf: The PDF bytes.
        pages: Page count (``SetlistDocumentJob.pages``).
    """

    pdf: bytes
    pages: int


@runtime_checkable
class SetlistDocumentRenderer(Protocol):
    """Renders a setlist as an A4 PDF (audio-ingest, ``documents/setlist_pdf.py``, ReportLab)."""

    def render(self, request: SetlistDocumentRequest) -> SetlistDocument:
        """Lay out the overview, the songs and the footer (data-model 2.6).

        Args:
            request: The complete content; no file is read except the DejaVu fonts.

        Returns:
            The PDF and its page count.

        Raises:
            DocumentError: The fonts are missing or ReportLab fails.
        """
        ...


# ------------------------------------------------------------------ feature 016: instrument views

CHORD_SAMPLE_RATE: Final = 22_050
"""Sample rate of the summed harmonic stems for chord recognition (BTC, chroma-hmm)."""

CHORD_HOP: Final = 2048
"""CQT hop of the chord features; one frame = ``CHORD_HOP / CHORD_SAMPLE_RATE`` s (92.88 ms)."""

PITCH_SAMPLE_RATE: Final = 16_000
"""Sample rate of the vocal stem for the reference melody (hop 160 = 10 ms)."""

BTC_MAJMIN_WEIGHTS: Final = WeightsFile(
    model_id="btc-majmin",
    url=(
        "https://raw.githubusercontent.com/jayg996/BTC-ISMIR19/"
        "2682317be668032e6e4b269ded36adaa2ad57df0/test/btc_model.pt"
    ),
    sha256="71c2c5db17e8c43b8a9a9da5db36ef2d667158c07a214eba16344c154c00bf54",
    size_bytes=12_154_754,
    licence="MIT",
)
"""BTC (Park et al. 2019) major/minor checkpoint, vocabulary ``simple`` (data-model 1)."""

BTC_LARGE_VOCA_WEIGHTS: Final = WeightsFile(
    model_id="btc-large-voca",
    url=(
        "https://raw.githubusercontent.com/jayg996/BTC-ISMIR19/"
        "2682317be668032e6e4b269ded36adaa2ad57df0/test/btc_model_large_voca.pt"
    ),
    sha256="1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51",
    size_bytes=12_229_576,
    licence="MIT",
)
"""BTC large-vocabulary checkpoint (170 classes), vocabulary ``full``."""

BTC_GPU_NEED: Final = GpuNeed("Chord recognition (BTC)", 512)
"""Free GPU memory of one BTC pass. Phase 0 (016 research R-03): no GPU run was possible in the
design phase; 512 MiB is far above the estimate from the 3.1 M parameters and 108-frame windows
(< 64 MiB) and is confirmed with an NVML peak at integration (T140)."""

BTC_SAFE_GLOBALS: Final[tuple[str, ...]] = (
    "numpy.core.multiarray.scalar",
    "numpy.dtype",
    "numpy.dtypes.Float64DType",
)
"""Qualified names that ``torch.load(weights_only=True)`` may resolve in the BTC checkpoints
(their ``mean``/``std`` entries are numpy scalars); everything else is refused (ADR-031,
016 research R-02 Phase 0 outcome)."""


@dataclass(frozen=True, slots=True)
class Tuning:
    """One entry of the tuning catalogue (data-model 4.1).

    Attributes:
        tuning_id: Catalogue id.
        instrument: ``guitar`` or ``bass`` (the profile that may use it).
        label: Display label.
        pitches: Sounding MIDI pitches, lowest string first.
    """

    tuning_id: TuningId
    instrument: str
    label: str
    pitches: tuple[int, ...]


TUNINGS: Final[Mapping[TuningId, Tuning]] = {
    t.tuning_id: t
    for t in (
        Tuning("guitar-standard", "guitar", "Standard (E A D G B E)", (40, 45, 50, 55, 59, 64)),
        Tuning("guitar-drop-d", "guitar", "Drop D", (38, 45, 50, 55, 59, 64)),
        Tuning("guitar-half-step-down", "guitar", "Half step down (Eb)", (39, 44, 49, 54, 58, 63)),
        Tuning("guitar-full-step-down", "guitar", "Full step down (D)", (38, 43, 48, 53, 57, 62)),
        Tuning("guitar-dadgad", "guitar", "DADGAD", (38, 45, 50, 55, 57, 62)),
        Tuning("guitar-open-g", "guitar", "Open G", (38, 43, 50, 55, 59, 62)),
        Tuning("guitar-open-d", "guitar", "Open D", (38, 45, 50, 54, 57, 62)),
        Tuning("bass-4-standard", "bass", "4-string (E A D G)", (28, 33, 38, 43)),
        Tuning("bass-4-drop-d", "bass", "4-string drop D", (26, 33, 38, 43)),
        Tuning("bass-4-half-step-down", "bass", "4-string half step down", (27, 32, 37, 42)),
        Tuning("bass-5-standard", "bass", "5-string (B E A D G)", (23, 28, 33, 38, 43)),
        Tuning("bass-6-standard", "bass", "6-string (B E A D G C)", (23, 28, 33, 38, 43, 48)),
    )
}
"""Tuning catalogue in data-model order (the backend catalogue has the same rows)."""

TAB_MAX_FRET: Final = 24
"""Highest fret counted from the nut (``fret + capo <= 24``)."""


@dataclass(frozen=True, slots=True)
class WrittenInstrumentInfo:
    """One entry of the written instrument catalogue (data-model 4.2).

    Attributes:
        instrument_id: Catalogue id.
        name: Part name.
        key_name: Instrument key (``C``, ``Bb``, ``Eb``, ``F``).
        interval: Sounding -> written interval in music21 notation.
        semitones: The interval in semitones (written = sounding + semitones).
        default_clef: Clef used for ``auto``; None = the profile's clef (``concert``).
        low_midi: Lowest sounding MIDI pitch of the usual range; None = the profile's range.
        high_midi: Highest sounding MIDI pitch of the usual range; None = the profile's range.
    """

    instrument_id: WrittenInstrument
    name: str
    key_name: str
    interval: str
    semitones: int
    default_clef: WrittenClef | None
    low_midi: int | None
    high_midi: int | None


WRITTEN_INSTRUMENTS: Final[Mapping[WrittenInstrument, WrittenInstrumentInfo]] = {
    i.instrument_id: i
    for i in (
        WrittenInstrumentInfo("concert", "", "C", "P1", 0, None, None, None),
        WrittenInstrumentInfo("trumpet-bb", "Trumpet in B-flat", "Bb", "M2", 2, "treble", 52, 82),
        WrittenInstrumentInfo("clarinet-bb", "Clarinet in B-flat", "Bb", "M2", 2, "treble", 50, 91),
        WrittenInstrumentInfo(
            "soprano-sax-bb", "Soprano Saxophone in B-flat", "Bb", "M2", 2, "treble", 56, 87
        ),
        WrittenInstrumentInfo(
            "tenor-sax-bb", "Tenor Saxophone in B-flat", "Bb", "M9", 14, "treble", 44, 75
        ),
        WrittenInstrumentInfo(
            "bass-clarinet-bb", "Bass Clarinet in B-flat", "Bb", "M9", 14, "treble", 38, 77
        ),
        WrittenInstrumentInfo(
            "alto-sax-eb", "Alto Saxophone in E-flat", "Eb", "M6", 9, "treble", 49, 80
        ),
        WrittenInstrumentInfo(
            "baritone-sax-eb", "Baritone Saxophone in E-flat", "Eb", "M13", 21, "treble", 36, 68
        ),
        WrittenInstrumentInfo(
            "clarinet-eb", "Clarinet in E-flat", "Eb", "-m3", -3, "treble", 53, 91
        ),
        WrittenInstrumentInfo("horn-f", "Horn in F", "F", "P5", 7, "treble", 34, 77),
        WrittenInstrumentInfo(
            "english-horn-f", "English Horn in F", "F", "P5", 7, "treble", 52, 81
        ),
        WrittenInstrumentInfo("flute", "Flute", "C", "P1", 0, "treble", 60, 96),
        WrittenInstrumentInfo("oboe", "Oboe", "C", "P1", 0, "treble", 58, 91),
        WrittenInstrumentInfo("violin", "Violin", "C", "P1", 0, "treble", 55, 100),
        WrittenInstrumentInfo("viola", "Viola", "C", "P1", 0, "alto", 48, 88),
        WrittenInstrumentInfo("cello", "Cello", "C", "P1", 0, "bass", 36, 76),
        WrittenInstrumentInfo("double-bass", "Double Bass", "C", "P8", 12, "bass", 28, 67),
        WrittenInstrumentInfo("trombone", "Trombone", "C", "P1", 0, "bass", 40, 72),
        WrittenInstrumentInfo("bassoon", "Bassoon", "C", "P1", 0, "bass", 34, 75),
        WrittenInstrumentInfo("tuba", "Tuba", "C", "P1", 0, "bass", 28, 65),
    )
}
"""Written instrument catalogue in data-model order (``concert`` first)."""

GM_DRUM_MAP: Final[Mapping[DrumPiece, int]] = {
    "kick": 36,
    "snare": 38,
    "toms": 47,
    "hihat": 42,
    "cymbals": 49,
}
"""General MIDI percussion notes (channel 10) of the drum pieces (research R-10)."""

MonoAudio = npt.NDArray[np.float32]
"""Mono samples in -1..1 at the rate the port names."""

AnalysisProgress = RenderProgress
"""Receives 0..1 of an analysis step; may raise ``AnalysisCancelledError``."""


class AnalysisError(WorkerError):
    """An instrument analysis failed; the message is shown to the user."""


class NoMelodyError(AnalysisError):
    """The vocal stem is silent (``NO_MELODY_TEXT``)."""

    def __init__(self) -> None:
        """Use the fixed text."""
        super().__init__(NO_MELODY_TEXT)


class AnalysisCancelledError(AnalysisError):
    """The job was deleted while running (raised from the progress callback)."""


class HarmonicMixer(Protocol):
    """Sums library stems to mono at a given rate (audio-ingest, ``analysis_inputs/mixdown.py``)."""

    def mix(self, paths: Sequence[Path], sample_rate: int) -> MonoAudio:
        """Sum 1-4 stems of possibly different lengths (longest wins), clipping-safe.

        Raises:
            InputNotFoundError: A file is missing.
        """
        ...


@dataclass(frozen=True, slots=True)
class ChordFrames:
    """Frame-wise output of a chord recognizer (data-model 2.3 step 2).

    Attributes:
        posteriors: ``(frames, classes)`` class probabilities (one-hot for chroma-hmm).
        labels: Harte label per class (``N`` included), in column order.
        frame_seconds: Frame hop in seconds (``CHORD_HOP / CHORD_SAMPLE_RATE``).
        chroma: ``(frames, 12)`` chroma of the mix (key estimation input).
        engine: Engine that ran (``chroma-hmm`` after a BTC fallback).
        device: Where BTC ran; None for the CPU-only engines.
        fallback: True when BTC was requested but chroma-hmm ran (warning ``fallback-engine``).
    """

    posteriors: npt.NDArray[np.float32]
    labels: tuple[str, ...]
    frame_seconds: float
    chroma: npt.NDArray[np.float32]
    engine: ChordEngine
    device: DeviceUse | None = None
    fallback: bool = False


class ChordRecognizer(Protocol):
    """Mono mix -> chord frames (``harmony/btc.py``, ``harmony/chroma_hmm.py``)."""

    @property
    def engine(self) -> ChordEngine:
        """Engine name recorded in the chart."""
        ...

    def recognize(
        self, audio: MonoAudio, vocabulary: ChordVocabulary, progress: AnalysisProgress
    ) -> ChordFrames:
        """Recognise chords frame by frame (``audio`` at ``CHORD_SAMPLE_RATE``).

        Raises:
            AnalysisError: The model cannot run.
            AnalysisCancelledError: Propagated from ``progress``.
        """
        ...


@dataclass(frozen=True, slots=True)
class PostProcessOptions:
    """What the chord post-processor needs besides the frames (data-model 2.3 steps 3-4).

    Attributes:
        grid: Reliable beat grid for snapping; None = 0.5 s minimum segments.
        vocabulary: Requested vocabulary (``full`` adds slash bass from ``bass``).
        bass: Mono bass stem at ``CHORD_SAMPLE_RATE``; None = no inversions.
    """

    grid: BeatGridTempo | None
    vocabulary: ChordVocabulary
    bass: MonoAudio | None = None


class ChordPostProcessor(Protocol):
    """Frames -> segments: beat snapping, merge, slash bass (``harmony/postprocess.py``)."""

    def segments(self, frames: ChordFrames, options: PostProcessOptions) -> ChordChart:
        """Build the chart without the key (``key``/``keyRegions`` empty, ``preferFlats`` false)."""
        ...


@dataclass(frozen=True, slots=True)
class KeyEstimate:
    """Output of the key estimator (data-model 2.8).

    Attributes:
        key: Global key; None when every segment is ``N``.
        regions: Contiguous key regions over the non-``N`` part (at most 50).
    """

    key: AnalysisKey | None
    regions: tuple[KeyRegion, ...] = ()


class KeyEstimator(Protocol):
    """Song key and key regions (beat-analysis ``harmony/key.py``, research R-13)."""

    def estimate(
        self,
        chroma: npt.NDArray[np.float32],
        segments: Sequence[ChordSegment],
        grid: BeatGridTempo | None,
    ) -> KeyEstimate:
        """Estimate the global key and regions >= 8 bars (16 s without a grid)."""
        ...


class PitchAnalyzer(Protocol):
    """Vocal stem -> reference melody (beat-analysis ``analysis/vocal_pitch.py``)."""

    @property
    def engine(self) -> PitchEngine:
        """Engine name recorded in the result."""
        ...

    def analyze(
        self, audio: MonoAudio, source: VocalSource, progress: AnalysisProgress
    ) -> VocalPitch:
        """10 ms frames in centi-semitones (``audio`` at ``PITCH_SAMPLE_RATE``).

        Raises:
            NoMelodyError: No voiced frame at all.
            AnalysisCancelledError: Propagated from ``progress``.
        """
        ...


@dataclass(frozen=True, slots=True)
class TabAssignment:
    """String and fret of every note of a tablature (data-model 4.5).

    Attributes:
        positions: Per event, ``(string, fret)`` per kept pitch; string 1 = highest-pitched string,
            fret 0 = the capo.
        octave_moved: Notes moved by an octave to fit the tuning.
        notes_dropped: Chord notes left out (more pitches than strings).
        wide_stretches: Chords spanning more than 4 fretted frets.
    """

    positions: tuple[tuple[tuple[int, int], ...], ...]
    octave_moved: int = 0
    notes_dropped: int = 0
    wide_stretches: int = 0


class TabFingerer(Protocol):
    """Pitches -> strings and frets (Viterbi, research R-08; ``notation/tablature.py``)."""

    def assign(self, events: Sequence[Sequence[int]], tuning: Tuning, capo: int) -> TabAssignment:
        """Assign every event (sounding MIDI pitches sounding together) to strings and frets."""
        ...


@dataclass(frozen=True, slots=True)
class PartResult:
    """A transposed part (data-model 4.6).

    Attributes:
        musicxml: The written part.
        summary: ``ScoreSummary.part``.
    """

    musicxml: bytes
    summary: PartSummary


class PartWriter(Protocol):
    """Concert score -> written part (music21; beat-analysis ``notation/parts.py``)."""

    def write(self, musicxml: bytes, part: PartSettings, profile: ScoreProfile) -> PartResult:
        """Transpose, respell, set part name, ``<transpose>`` and clef; MIDI is untouched."""
        ...


# ------------------------------------------------- insights lane (1.11.0, feature 020)

InsightsProgress = RenderProgress
"""Receives 0..1 of a quality or handout job; may raise the job's cancellation error."""

QUALITY_SAMPLE_RATE: Final = 22_050
"""Rate of the analysis representation (data-model 4.1)."""

QUALITY_MIN_ACTIVE_SECONDS: Final = 10.0
"""Less active audio than this rates a stem ``unknown`` (reason ``too-short``, spec edge case)."""

HANDOUT_FAILED_TEXT: Final = "The handout could not be written"
"""Error of a handout job that fails (``HandoutRenderError`` without its own text)."""

NOTICE_FOOTER: Final = HANDOUT_NOTICE_FOOTER
"""Footer notice of every handout page (data-model 6.6; validated in ``HandoutNotice``)."""

NOTICE_FULL: Final = HANDOUT_NOTICE_FULL
"""Full notice on the last handout page (data-model 6.6)."""

HANDOUT_PDF_PATTERN: Final = re.compile(rf"assets/{_UUID}/[a-z][a-z0-9-]{{0,31}}\.pdf")
"""Notation PDFs of score items (008/016) a handout job may read (data-model 2.2)."""

ARTWORK_PATTERN: Final = re.compile(rf"tracks/{_UUID}/artwork/(auto|user)/cover\.jpg")
"""Cover pictures (005) a handout job may read (data-model 2.2)."""

QUALITY_PATH_PATTERNS: Final[tuple[re.Pattern[str], ...]] = (STEM_PATH_PATTERN, DETAIL_PATH_PATTERN)
"""A quality job reads only stems and detail stems."""

HANDOUT_PATH_PATTERNS: Final[tuple[re.Pattern[str], ...]] = (HANDOUT_PDF_PATTERN, ARTWORK_PATTERN)
"""A handout job reads only notation PDFs and artwork; the general allow-list stays unchanged."""


def is_quality_input_path(path: str) -> bool:
    """Return True when ``path`` fully matches one of ``QUALITY_PATH_PATTERNS``."""
    return any(pattern.fullmatch(path) for pattern in QUALITY_PATH_PATTERNS)


def is_handout_input_path(path: str) -> bool:
    """Return True when ``path`` fully matches one of ``HANDOUT_PATH_PATTERNS``."""
    return any(pattern.fullmatch(path) for pattern in HANDOUT_PATH_PATTERNS)


def notation_unreadable_text(title: str) -> str:
    """Warning text of a notation PDF that could not be read (its pages become a note)."""
    return f"A notation file could not be read: {title}"


def notation_omitted_text(title: str) -> str:
    """Warning text of a notation item left out by the per-job render budget."""
    raise NotImplementedError


def artwork_omitted_text(song_title: str) -> str:
    """Warning text of a cover picture left out (not a JPEG or over 40 megapixels)."""
    raise NotImplementedError


class QualityAnalysisError(WorkerError):
    """A quality job failed; the message is shown to the user."""


class QualityCancelledError(QualityAnalysisError):
    """The quality job was deleted while running (raised from the progress callback)."""


class HandoutRenderError(WorkerError):
    """A handout job failed (default text ``HANDOUT_FAILED_TEXT``)."""


class HandoutCancelledError(HandoutRenderError):
    """The handout job was deleted while running (raised from the progress callback)."""


class NotationUnreadableError(HandoutRenderError):
    """A notation PDF cannot be read; the renderer replaces its pages by a note and warns."""


@dataclass(frozen=True, slots=True)
class StemAudio:
    """One input of a quality job, resolved and checked by the job layer.

    Attributes:
        channel: ChannelId (``vocals``, ``drums.kick``, ``drums.rest``).
        kind: ``standard``, ``detail`` or ``rest``.
        path: Real path inside the library root (FLAC, 44.1 kHz stereo).
    """

    channel: str
    kind: QualityInputKind
    path: Path


@dataclass(frozen=True, slots=True)
class QualityInputs:
    """What a ``QualityAnalyzer`` needs: the resolved stems and the 002 metrics per parent."""

    stems: tuple[StemAudio, ...]
    detail_parents: tuple[DetailParentInput, ...]


@runtime_checkable
class QualityAnalyzer(Protocol):
    """Stems -> reference-free quality estimates (beat-analysis ``insights/``, incl. the fake)."""

    @property
    def version(self) -> str:
        """``analyzerVersion`` of the results (``insights/thresholds.py`` ``ANALYZER_VERSION``)."""
        ...

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Estimate bleed and artefacts of every stem (data-model 4; stems in input order).

        Raises:
            QualityAnalysisError: A stem cannot be read or analysed.
            QualityCancelledError: Propagated from ``progress``.
        """
        ...


@dataclass(frozen=True, slots=True)
class RenderedHandout:
    """Output of a ``HandoutRenderer``: the PDF and the facts the job result reports."""

    pdf: bytes
    pages: int
    outline: tuple[str, ...]
    warnings: tuple[HandoutWarning, ...]


@runtime_checkable
class HandoutRenderer(Protocol):
    """Handout document -> one PDF (audio-ingest ``handouts/renderer.py``; no network, D7)."""

    def render(
        self, request: HandoutJobRequest, files: Mapping[str, Path], progress: InsightsProgress
    ) -> RenderedHandout:
        """Lay out the document on ``request.paper`` and merge the notation pages.

        Args:
            request: The validated job request (notice texts already fixed).
            files: Real path of every library path of ``request.input_paths()``, resolved and
                checked by the job layer; the renderer reads no other file except its packaged
                assets.
            progress: Stage progress (``layout`` 0-0.6, ``notation`` 0.6-0.9, ``writing`` 0.9-1).

        Raises:
            HandoutRenderError: The PDF cannot be written.
            HandoutCancelledError: Propagated from ``progress``.
        """
        ...


class NotationReader(Protocol):
    """Page sizes of a notation PDF (audio-ingest ``handouts/notation.py``, pypdf)."""

    def page_sizes(self, path: Path) -> tuple[tuple[float, float], ...]:
        """Width and height in points of every page.

        Raises:
            NotationUnreadableError: The file is not a readable PDF.
        """
        ...
