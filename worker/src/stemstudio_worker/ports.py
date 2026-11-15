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

The job runner (``jobs/``) composes them: resolve (or accept the upload) -> fetch -> normalise ->
separate -> analyse. The composition root (``main.create_app``) builds a ``PipelineAdapters``
from the three factories; tests pass their own ``PipelineAdapters`` made of fakes.
Adapters signal expected failures by raising a ``WorkerError`` subclass; its message is shown to
the user verbatim as the job ``error`` text, so it must be readable and free of secrets.
"""

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol, runtime_checkable

import numpy as np
import numpy.typing as npt

from stemstudio_worker.schemas import (
    BeatGrid,
    DetailModelId,
    DeviceInfo,
    JobDevice,
    ModelName,
    ProcessingDeviceKind,
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
