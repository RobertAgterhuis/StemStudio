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

The job runner (``jobs/``) composes them: resolve (or accept the upload) -> fetch -> normalise ->
separate -> analyse. The composition root (``main.create_app``) builds a ``PipelineAdapters``
from the three factories; tests pass their own ``PipelineAdapters`` made of fakes.
Adapters signal expected failures by raising a ``WorkerError`` subclass; its message is shown to
the user verbatim as the job ``error`` text, so it must be readable and free of secrets.
"""

import re
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Final, Protocol, runtime_checkable

import numpy as np
import numpy.typing as npt

from stemstudio_worker.schemas import (
    ArtworkInfo,
    ArtworkSource,
    BeatGrid,
    DetailModelId,
    DeviceInfo,
    JobDevice,
    LibraryHealth,
    MetadataResult,
    ModelName,
    ProcessingDeviceKind,
    RenderJobRequest,
    RenderResult,
    SourceType,
    StemName,
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

LIBRARY_PATH_PATTERNS: Final[tuple[re.Pattern[str], ...]] = (
    re.compile(rf"tracks/{_UUID}/stems/(vocals|drums|bass|guitar|piano|other)\.flac"),
    re.compile(
        rf"tracks/{_UUID}/detail/(vocals|drums|guitar|piano|other)/[a-z][a-z-]{{0,31}}\.flac"
    ),
    re.compile(rf"assets/{_UUID}/[a-z][a-z0-9-]{{0,31}}\.(flac|wav|mp3)"),
)
"""Allow-list of render input paths relative to the library root (data-model.md 2.1).

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
