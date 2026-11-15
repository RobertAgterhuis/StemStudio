"""Reading stems, summing them at unity gain and cutting the restyle range (009 FR-008).

Stems arrive as the multipart parts ``stem`` (1-6 WAV/FLAC files of the same sample rate and
length within one frame). The sum is the source of a restyle; when it clips, the peaks are
limited to full scale (a gain change) and the job reports ``CLIPPED_WARNING``.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np
import soundfile as sf

from stemstudio_generator.audio.levels import limit_peak
from stemstudio_generator.ports import InvalidInputError, Signal

MAX_STEMS: Final = 6
"""Most stems a restyle sums."""

STEREO: Final = 2
"""Channels of a summed source."""

LENGTH_TOLERANCE_FRAMES: Final = 1
"""Stems may differ by this many frames; the sum is trimmed to the shortest."""

UNREADABLE_TEXT: Final = "An uploaded file could not be read as WAV or FLAC audio."
STEM_COUNT_TEXT: Final = "A restyle needs 1 to 6 stems."
SAMPLE_RATE_TEXT: Final = "All stems must have the same sample rate."
LENGTH_TEXT: Final = "All stems must have the same length."
CHANNELS_TEXT: Final = "Stems must be mono or stereo."
RANGE_END_TEXT: Final = "The range ends after the end of the source."


@dataclass(frozen=True, slots=True)
class AudioClip:
    """Decoded audio.

    Attributes:
        samples: float32 samples, shape ``(frames, channels)``.
        sample_rate: Frames per second.
    """

    samples: Signal
    sample_rate: int

    @property
    def frames(self) -> int:
        """Number of frames."""
        return int(self.samples.shape[0])

    @property
    def channels(self) -> int:
        """Number of channels."""
        return int(self.samples.shape[1])

    @property
    def duration_seconds(self) -> float:
        """Length in seconds."""
        return self.frames / self.sample_rate


@dataclass(frozen=True, slots=True)
class MixResult:
    """Sum of the stems.

    Attributes:
        clip: The stereo sum (limited to full scale).
        clipped: True when the unlimited sum exceeded full scale.
    """

    clip: AudioClip
    clipped: bool


def read_audio(path: Path) -> AudioClip:
    """Decode a WAV or FLAC file as float32 ``(frames, channels)``.

    Raises:
        InvalidInputError: The file is not readable audio.
    """
    try:
        data, rate = sf.read(path, dtype="float32", always_2d=True)
    except (sf.LibsndfileError, RuntimeError, OSError) as exc:
        raise InvalidInputError(UNREADABLE_TEXT) from exc
    return AudioClip(np.ascontiguousarray(data, dtype=np.float32), int(rate))


def write_wav(path: Path, clip: AudioClip) -> Path:
    """Write ``clip`` as 32-bit float WAV (no clipping of values above full scale)."""
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, clip.samples, clip.sample_rate, subtype="FLOAT", format="WAV")
    return path


def to_stereo(samples: Signal) -> Signal:
    """Spread mono ``(frames, 1)`` to two channels; stereo is returned unchanged.

    Raises:
        InvalidInputError: More than two channels.
    """
    channels = samples.shape[1]
    if channels > STEREO:
        raise InvalidInputError(CHANNELS_TEXT)
    return samples if channels == STEREO else np.repeat(samples, STEREO, axis=1)


def sum_stems(stems: Sequence[AudioClip]) -> MixResult:
    """Sum 1-6 stems at unity gain into a stereo clip.

    Raises:
        InvalidInputError: Wrong stem count, sample rates or lengths differ, or > 2 channels.
    """
    if len(stems) not in range(1, MAX_STEMS + 1):
        raise InvalidInputError(STEM_COUNT_TEXT)
    total = add_stems(stems)
    limited, clipped = limit_peak(total.samples, 0.0)
    return MixResult(AudioClip(limited, total.sample_rate), clipped)


def add_stems(stems: Sequence[AudioClip]) -> AudioClip:
    """Unlimited stereo sum of one or more stems, trimmed to the shortest.

    Raises:
        InvalidInputError: Sample rates or lengths differ, or a stem has > 2 channels.
    """
    _check_alike(stems)
    frames = min(stem.frames for stem in stems)
    total = np.zeros((frames, STEREO), dtype=np.float32)
    for stem in stems:
        total += to_stereo(stem.samples[:frames])
    return AudioClip(total, stems[0].sample_rate)


def _check_alike(stems: Sequence[AudioClip]) -> None:
    """Sample rates must be equal and lengths within ``LENGTH_TOLERANCE_FRAMES``."""
    if _spread([stem.sample_rate for stem in stems]) != 0:
        raise InvalidInputError(SAMPLE_RATE_TEXT)
    if _spread([stem.frames for stem in stems]) > LENGTH_TOLERANCE_FRAMES:
        raise InvalidInputError(LENGTH_TEXT)


def _spread(values: Sequence[int]) -> int:
    """Largest minus smallest value."""
    return max(values) - min(values)


def cut_range(clip: AudioClip, start_seconds: float, end_seconds: float) -> AudioClip:
    """The frames of ``[start_seconds, end_seconds)``; an end one frame past the source is clamped.

    Raises:
        InvalidInputError: The range ends more than one frame after the source.
    """
    start = round(start_seconds * clip.sample_rate)
    end = round(end_seconds * clip.sample_rate)
    if end > clip.frames + LENGTH_TOLERANCE_FRAMES:
        raise InvalidInputError(RANGE_END_TEXT)
    return AudioClip(clip.samples[start : min(end, clip.frames)], clip.sample_rate)
