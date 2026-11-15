"""Labelled take files: FLAC 24-bit stereo 48 kHz with the AI-generated Vorbis comments.

ffmpeg re-encodes the engine's output and writes the comments of the contract
(``getTakeAudio``): ``AI_GENERATED=true``, ``GENERATOR=<model (revision)>``,
``STEMSTUDIO_GENERATION``, ``STEMSTUDIO_TAKE``, ``COMMENT=AI-generated with StemStudio`` and, for
takes with an AI voice (1.1.0), ``AI_VOICE`` and ``AI_VOICE_PROFILE``. Lyrics are never embedded.
ffmpeg runs from an argument list with absolute ``file:`` URLs (no shell, no option injection);
its output is never shown to users.
"""

import hashlib
import logging
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import soundfile as sf

from stemstudio_generator.audio.commands import CommandRunner, SubprocessRunner
from stemstudio_generator.audio.levels import peak_dbfs
from stemstudio_generator.ports import GeneratorError
from stemstudio_generator.schemas import TAKE_SAMPLE_RATE, VocalLabel

AI_COMMENT: Final = "AI-generated with StemStudio"
"""Value of the ``COMMENT`` Vorbis comment."""

WRITE_FAILED_TEXT: Final = "The take could not be written as FLAC."
"""Job error text when ffmpeg fails (the tool output goes to the log only)."""

FFMPEG_TIMEOUT_SECONDS: Final = 600.0
"""Upper bound for one FLAC encode (an 8-minute take takes a few seconds)."""

_HASH_CHUNK: Final = 1024 * 1024
_CONTROL_CHARS: Final = re.compile(r"[\x00-\x1f\x7f]+")

_log = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class TakeLabels:
    """What the Vorbis comments of one take say.

    Attributes:
        generator: Model and revision, e.g. ``ACE-Step 1.5 (19671f4...)``.
        generation_id: The job id.
        take_index: Take number from 1.
        voice: AI voice label (``synthetic``/``converted``; ``none``/None writes no label).
        voice_profile: Voice-profile display name (voice conversions only).
    """

    generator: str
    generation_id: str
    take_index: int
    voice: VocalLabel | None = None
    voice_profile: str | None = None


@dataclass(frozen=True, slots=True)
class WrittenFile:
    """A labelled take file with the facts of ``GeneratedTake``.

    Attributes:
        path: The FLAC file.
        sha256: SHA-256 of the file bytes.
        bytes: File size.
        duration_seconds: Length.
        peak_dbfs: Sample peak in dBFS.
    """

    path: Path
    sha256: str
    bytes: int
    duration_seconds: float
    peak_dbfs: float


def vorbis_comments(labels: TakeLabels) -> list[tuple[str, str]]:
    """The Vorbis comments of a take, in a fixed order; control characters become spaces."""
    comments = [
        ("AI_GENERATED", "true"),
        ("GENERATOR", labels.generator),
        ("STEMSTUDIO_GENERATION", labels.generation_id),
        ("STEMSTUDIO_TAKE", str(labels.take_index)),
        ("COMMENT", AI_COMMENT),
    ]
    if labels.voice in ("synthetic", "converted"):
        comments.append(("AI_VOICE", labels.voice))
    if labels.voice_profile:
        comments.append(("AI_VOICE_PROFILE", labels.voice_profile))
    return [(key, _CONTROL_CHARS.sub(" ", value)) for key, value in comments]


def ffmpeg_flac_args(ffmpeg: str, source: Path, dest: Path, labels: TakeLabels) -> list[str]:
    """Ffmpeg argument list: ``source`` -> 24-bit stereo 48 kHz FLAC with only our comments."""
    args = [ffmpeg, "-nostdin", "-hide_banner", "-loglevel", "error", "-y"]
    args += ["-i", _file_url(source), "-map_metadata", "-1", "-map", "0:a:0"]
    args += ["-ar", str(TAKE_SAMPLE_RATE), "-ac", "2", "-c:a", "flac"]
    args += ["-sample_fmt", "s32", "-bits_per_raw_sample", "24"]
    for key, value in vorbis_comments(labels):
        args += ["-metadata", f"{key}={value}"]
    return [*args, _file_url(dest)]


def file_sha256(path: Path) -> str:
    """SHA-256 of a file's bytes (read in 1 MiB chunks)."""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while chunk := handle.read(_HASH_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()


class FlacWriter:
    """Writes labelled take files through ffmpeg."""

    def __init__(self, runner: CommandRunner | None = None, ffmpeg: str = "ffmpeg") -> None:
        """Create the writer.

        Args:
            runner: Runs ffmpeg; defaults to ``SubprocessRunner``.
            ffmpeg: ffmpeg executable (name on PATH or absolute path).
        """
        self._runner = runner or SubprocessRunner()
        self._ffmpeg = ffmpeg

    def write(self, source: Path, dest: Path, labels: TakeLabels) -> WrittenFile:
        """Encode ``source`` (any ffmpeg-readable audio) to the labelled FLAC ``dest``.

        Raises:
            GeneratorError: ffmpeg failed or wrote no file (``engineError``).
        """
        dest.parent.mkdir(parents=True, exist_ok=True)
        args = ffmpeg_flac_args(self._ffmpeg, source, dest, labels)
        result = self._runner.run(args, FFMPEG_TIMEOUT_SECONDS)
        if result.returncode != 0 or not dest.is_file():
            _log.warning("ffmpeg FLAC encode failed (%s): %s", result.returncode, result.stderr)
            raise GeneratorError(WRITE_FAILED_TEXT)
        return describe_file(dest)


def describe_file(path: Path) -> WrittenFile:
    """Hash, size, duration and peak of an audio file."""
    data, rate = sf.read(path, dtype="float32", always_2d=True)
    return WrittenFile(
        path=path,
        sha256=file_sha256(path),
        bytes=path.stat().st_size,
        duration_seconds=data.shape[0] / rate,
        peak_dbfs=peak_dbfs(data),
    )


def _file_url(path: Path) -> str:
    """Absolute ``file:`` URL for ffmpeg, so a name is never read as an option or protocol."""
    return f"file:{path.resolve()}"
