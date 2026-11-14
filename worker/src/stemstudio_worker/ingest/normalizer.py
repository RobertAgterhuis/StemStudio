"""``AudioNormalizer`` implementation: ffprobe + ffmpeg to 44.1 kHz stereo 16-bit PCM WAV.

ffmpeg and ffprobe are run through an injected ``CommandRunner`` with argument lists (never a
shell). Paths are passed as absolute ``file:`` URLs so that no file name can be interpreted as an
option or as another ffmpeg protocol. The duration is checked from the probe (before converting)
and again from the written WAV.
"""

import hashlib
import json
import subprocess
import wave
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final, Protocol

from stemstudio_worker.ingest.limits import DEFAULT_MAX_DURATION_SECONDS, too_long_error
from stemstudio_worker.ports import AudioDecodeError, NormalizedAudio
from stemstudio_worker.schemas import CHANNELS, SAMPLE_RATE

DECODE_FAILED_MESSAGE: Final = (
    "The audio could not be decoded; the file may be corrupt or in an unsupported format."
)
"""Job error text when ffprobe/ffmpeg fail; tool output is never shown to the user."""

NO_AUDIO_MESSAGE: Final = "The file contains no audio stream."
"""Job error text when the input has no audio stream."""

PROBE_TIMEOUT_SECONDS: Final = 60.0
"""Upper bound for one ffprobe run."""

CONVERT_TIMEOUT_SECONDS: Final = 600.0
"""Upper bound for one ffmpeg conversion."""

_SAMPLE_WIDTH_BYTES: Final = 2


@dataclass(frozen=True, slots=True)
class CommandResult:
    """Outcome of an external command.

    Attributes:
        returncode: Exit code; non-zero (or negative for launch failures/timeouts) means failure.
        stdout: Captured standard output (text).
        stderr: Captured standard error (text); for logs only, never for users.
    """

    returncode: int
    stdout: str
    stderr: str


class CommandRunner(Protocol):
    """Runs an external program from an argument list."""

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        """Run ``args`` (program first) without a shell and capture its output."""
        ...


class SubprocessRunner:
    """``CommandRunner`` backed by ``subprocess.run`` (argument list, no shell, no stdin)."""

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        """Run ``args``; timeouts and missing binaries become a failed ``CommandResult``."""
        try:
            completed = subprocess.run(  # noqa: S603 - argument list, never a shell
                list(args),
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout_seconds,
                check=False,
                stdin=subprocess.DEVNULL,
            )
        except (subprocess.TimeoutExpired, OSError) as exc:
            return CommandResult(-1, "", f"{type(exc).__name__}: {exc}")
        return CommandResult(completed.returncode, completed.stdout, completed.stderr)


class FfmpegNormalizer:
    """Converts any ffmpeg-readable audio to the pipeline WAV format."""

    def __init__(
        self,
        runner: CommandRunner | None = None,
        max_duration_seconds: float = DEFAULT_MAX_DURATION_SECONDS,
        ffmpeg: str = "ffmpeg",
        ffprobe: str = "ffprobe",
    ) -> None:
        """Create the normaliser.

        Args:
            runner: Runs ffprobe/ffmpeg; defaults to ``SubprocessRunner``.
            max_duration_seconds: Longest accepted duration.
            ffmpeg: ffmpeg executable (name on PATH or absolute path).
            ffprobe: ffprobe executable (name on PATH or absolute path).
        """
        self.max_duration_seconds = max_duration_seconds
        self._runner = runner or SubprocessRunner()
        self._ffmpeg = ffmpeg
        self._ffprobe = ffprobe

    def normalize(self, input_path: Path, output_wav: Path) -> NormalizedAudio:
        """Convert ``input_path`` to WAV at ``output_wav`` (see ``ports.AudioNormalizer``).

        Raises:
            AudioDecodeError: ffprobe/ffmpeg failed, no audio stream, or an invalid WAV.
            AudioTooLongError: The probed or converted duration exceeds the limit.
        """
        self._check_duration(self._probe(input_path))
        self._convert(input_path, output_wav)
        duration = _wav_duration(output_wav)
        self._check_duration(duration)
        return NormalizedAudio(output_wav, duration, _sha256(output_wav))

    def _probe(self, input_path: Path) -> float | None:
        """Return the container duration (None when unknown); require an audio stream."""
        args = [self._ffprobe, "-v", "error", "-select_streams", "a:0"]
        args += ["-show_entries", "stream=codec_type:format=duration", "-of", "json"]
        result = self._runner.run([*args, _file_url(input_path)], PROBE_TIMEOUT_SECONDS)
        if result.returncode != 0:
            raise AudioDecodeError(DECODE_FAILED_MESSAGE)
        return _parse_probe(result.stdout)

    def _convert(self, input_path: Path, output_wav: Path) -> None:
        """Run ffmpeg; output is cut just past the limit so endless inputs still terminate."""
        args = [self._ffmpeg, "-nostdin", "-hide_banner", "-loglevel", "error", "-y"]
        args += ["-i", _file_url(input_path), "-map", "0:a:0", "-vn", "-sn", "-dn"]
        args += ["-t", f"{self.max_duration_seconds + 1:g}", "-ac", str(CHANNELS)]
        args += ["-ar", str(SAMPLE_RATE), "-c:a", "pcm_s16le", "-f", "wav"]
        result = self._runner.run([*args, _file_url(output_wav)], CONVERT_TIMEOUT_SECONDS)
        if result.returncode != 0:
            raise AudioDecodeError(DECODE_FAILED_MESSAGE)

    def _check_duration(self, duration: float | None) -> None:
        """Raise ``AudioTooLongError`` when ``duration`` is known and over the limit."""
        if duration is not None and duration > self.max_duration_seconds:
            raise too_long_error(self.max_duration_seconds)


def _file_url(path: Path) -> str:
    """Absolute ``file:`` URL for ffmpeg, so the name is never read as an option or protocol."""
    return f"file:{path.resolve()}"


def _parse_probe(stdout: str) -> float | None:
    """Parse ffprobe JSON: require an audio stream, return the duration when it is numeric."""
    try:
        data: Any = json.loads(stdout)
        streams = data["streams"]
        duration = data.get("format", {}).get("duration")
    except (ValueError, TypeError, KeyError, AttributeError):
        raise AudioDecodeError(DECODE_FAILED_MESSAGE) from None
    if not isinstance(streams, list) or not streams:
        raise AudioDecodeError(NO_AUDIO_MESSAGE)
    try:
        return float(duration)
    except (TypeError, ValueError):
        return None


def _wav_duration(path: Path) -> float:
    """Duration of the written WAV; its format must be 44.1 kHz, stereo, 16-bit and non-empty."""
    try:
        with wave.open(str(path), "rb") as handle:
            params = handle.getparams()
    except (OSError, EOFError, wave.Error):
        raise AudioDecodeError(DECODE_FAILED_MESSAGE) from None
    expected = (CHANNELS, _SAMPLE_WIDTH_BYTES, SAMPLE_RATE)
    if (params.nchannels, params.sampwidth, params.framerate) != expected or not params.nframes:
        raise AudioDecodeError(DECODE_FAILED_MESSAGE)
    return params.nframes / params.framerate


def _sha256(path: Path) -> str:
    """Lower-case hex SHA-256 of the file content."""
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest()
