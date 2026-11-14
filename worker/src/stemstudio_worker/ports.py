"""Ports (``typing.Protocol`` interfaces) between the worker pipeline and its adapters.

Each protocol is implemented by a different agent and faked in unit tests:

* ``SourceResolver``, ``UploadIntake``, ``AudioFetcher``, ``AudioNormalizer``: audio-ingest
  (``ingest/``, factory ``create_ingest_adapters``)
* ``Separator``: stem-separation (``separation/``, factory ``create_separator``)
* ``BeatAnalyzer``: beat-analysis (``analysis/``, factory ``create_beat_analyzer``)

The job runner (``jobs/``) composes them: resolve (or accept the upload) -> fetch -> normalise ->
separate -> analyse. The composition root (``main.create_app``) builds a ``PipelineAdapters``
from the three factories; tests pass their own ``PipelineAdapters`` made of fakes.
Adapters signal expected failures by raising a ``WorkerError`` subclass; its message is shown to
the user verbatim as the job ``error`` text, so it must be readable and free of secrets.
"""

from dataclasses import dataclass
from pathlib import Path
from typing import Protocol, runtime_checkable

from stemstudio_worker.schemas import BeatGrid, ModelName, SourceType, StemName


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
class SourceInfo:
    """A validated, resolved source.

    Attributes:
        source_type: ``youtube``, ``spotify`` or ``upload``.
        source_key: Cache identity, e.g. ``youtube:dQw4w9WgXcQ`` or ``upload:<sha256>``.
        title: Track title (YouTube title, Spotify title or upload file stem).
        artist: Artist when known.
        youtube_url: Canonical ``https://www.youtube.com/watch?v=<id>`` to download, or None
            for uploads.
    """

    source_type: SourceType
    source_key: str
    title: str
    artist: str | None
    youtube_url: str | None


@dataclass(frozen=True, slots=True)
class FetchedAudio:
    """A downloaded audio file plus the metadata the download reported.

    Attributes:
        path: The downloaded file (any container/codec ffmpeg can read).
        title: Title reported by the download (e.g. the YouTube video title), or None.
        artist: Artist or uploader reported by the download, or None.
    """

    path: Path
    title: str | None = None
    artist: str | None = None


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
    """

    model: ModelName
    stems: dict[StemName, Path]


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
