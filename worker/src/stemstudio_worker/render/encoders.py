"""Output encoders of render jobs: FLAC/WAV via libsndfile, MP3 via lameenc (ADR-016 D8).

Encoders take blocks that are already quantised (``dither.quantize``) at their ``bits``, so the
peak of the written samples is known exactly. WAV and FLAC carry ``title``/``artist``/``comment``
tags; MP3 gets none (no non-GPL tag writer in the stack, research.md E-09).
"""

import re
from pathlib import Path
from typing import Any, BinaryIO, Final, Protocol

import lameenc
import numpy as np
import soundfile as sf

from stemstudio_worker.render.dither import IntBlock
from stemstudio_worker.schemas import RenderMetadata, RenderOutput

_MP3_QUALITY: Final = 2
"""LAME quality setting (research.md E-09)."""

_CONTROL_CHARACTERS: Final = re.compile(r"[\x00-\x1f\x7f-\x9f]")


class Encoder(Protocol):
    """Writes quantised stereo blocks to one output file."""

    @property
    def bits(self) -> int:
        """Bit depth the blocks passed to ``write`` must be quantised to."""
        ...

    def write(self, block: IntBlock) -> None:
        """Append one block of ``(frames, 2)`` integers."""
        ...

    def close(self) -> None:
        """Finish and close the file."""
        ...


def open_encoder(output: RenderOutput, metadata: RenderMetadata, path: Path) -> Encoder:
    """Create the encoder of ``output.format`` writing to ``path``.

    Args:
        output: Format, bit depth (FLAC/WAV) or bitrate (MP3).
        metadata: Tags for FLAC/WAV; ignored for MP3.
        path: File to create (overwritten).

    Returns:
        An open encoder; the caller must ``close`` it.
    """
    if output.format == "mp3":
        return Mp3Encoder(path, output.mp3_bitrate_kbps)
    return PcmEncoder(path, output, metadata)


def clean_tag(value: str | None) -> str | None:
    """Strip control characters; None when nothing printable is left."""
    cleaned = _CONTROL_CHARACTERS.sub("", value or "")
    return cleaned or None


class PcmEncoder:
    """FLAC or WAV, 16- or 24-bit PCM, 44.1 kHz stereo, with tags."""

    def __init__(self, path: Path, output: RenderOutput, metadata: RenderMetadata) -> None:
        """Open ``path`` and write the tags (libsndfile needs them before the audio)."""
        self._bits = output.bit_depth
        self._file: Any = sf.SoundFile(
            path,
            mode="w",
            samplerate=44100,
            channels=2,
            subtype=f"PCM_{self._bits}",
            format=output.format.upper(),
        )
        for name in ("title", "artist", "comment"):
            value = clean_tag(getattr(metadata, name))
            if value is not None:
                setattr(self._file, name, value)

    @property
    def bits(self) -> int:
        """16 or 24."""
        return self._bits

    def write(self, block: IntBlock) -> None:
        """Append one block (24-bit values are shifted into libsndfile's 32-bit int range)."""
        if self._bits == 16:  # noqa: PLR2004  (bit depth)
            self._file.write(block.astype(np.int16))
        else:
            self._file.write(block << 8)

    def close(self) -> None:
        """Close the file."""
        self._file.close()


class Mp3Encoder:
    """CBR MP3 of 16-bit samples via lameenc."""

    def __init__(self, path: Path, bitrate_kbps: int) -> None:
        """Open ``path`` and configure LAME (44.1 kHz, stereo, CBR ``bitrate_kbps``)."""
        self._lame = lameenc.Encoder()
        self._lame.set_bit_rate(bitrate_kbps)
        self._lame.set_in_sample_rate(44100)
        self._lame.set_channels(2)
        self._lame.set_quality(_MP3_QUALITY)
        self._file: BinaryIO = path.open("wb")
        self._started = False

    @property
    def bits(self) -> int:
        """Always 16 (LAME's input)."""
        return 16

    def write(self, block: IntBlock) -> None:
        """Encode one block of interleaved 16-bit frames."""
        self._file.write(bytes(self._lame.encode(block.astype("<i2").tobytes())))
        self._started = True

    def close(self) -> None:
        """Flush LAME (it refuses a flush before the first block) and close the file."""
        if self._started:
            self._file.write(bytes(self._lame.flush()))
        self._file.close()
