"""The voice-conversion remix (010 FR-008): converted vocal + accompaniment -> 48 kHz take.

SoulX-Singer-SVC returns the converted vocal at 24 kHz mono. It is resampled to 48 kHz,
spread to stereo and aligned (silence-padded or trimmed) to the accompaniment: the take's other
0-5 stems, summed at unity gain and resampled to 48 kHz. The vocal is added with
``vocalGainDb`` and the mix is peak-limited to -1 dBFS (a gain change). The vocal alone is the
take's vocal stem (``getTakeVocal``), limited to the same ceiling.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

import numpy as np

from stemstudio_generator.audio.levels import db_to_gain, limit_peak
from stemstudio_generator.audio.mix import STEREO, AudioClip, add_stems, to_stereo
from stemstudio_generator.audio.resample import resample
from stemstudio_generator.ports import InvalidInputError, Signal
from stemstudio_generator.schemas import TAKE_SAMPLE_RATE

MAX_ACCOMPANIMENT: Final = 5
"""Most accompaniment stems of a conversion (the take's stems other than vocals)."""

MIX_CEILING_DBFS: Final = -1.0
"""Peak ceiling of the remix and of the vocal stem."""

ACCOMPANIMENT_COUNT_TEXT: Final = "A voice conversion takes 0 to 5 accompaniment stems."


@dataclass(frozen=True, slots=True)
class RemixResult:
    """The two files of a voice-conversion take.

    Attributes:
        mix: The remix, 48 kHz stereo (the take).
        vocal: The converted vocal alone, 48 kHz stereo, aligned to the mix (the vocal stem).
        limited: True when the mix had to be scaled down to ``MIX_CEILING_DBFS``.
    """

    mix: AudioClip
    vocal: AudioClip
    limited: bool


def align_length(samples: Signal, frames: int) -> Signal:
    """Trim ``samples`` to ``frames`` or pad the end with silence."""
    if samples.shape[0] >= frames:
        return samples[:frames]
    padding = np.zeros((frames - samples.shape[0], samples.shape[1]), dtype=np.float32)
    return np.concatenate([samples, padding])


def upsample_vocal(vocal: AudioClip) -> AudioClip:
    """The converted vocal at 48 kHz stereo."""
    samples = resample(vocal.samples, vocal.sample_rate, TAKE_SAMPLE_RATE)
    return AudioClip(to_stereo(samples), TAKE_SAMPLE_RATE)


def remix(
    vocal: AudioClip, accompaniment: Sequence[AudioClip], vocal_gain_db: float
) -> RemixResult:
    """Mix the converted vocal into the accompaniment (see the module docstring).

    Raises:
        InvalidInputError: More than 5 accompaniment stems, or they differ in rate or length.
    """
    if len(accompaniment) > MAX_ACCOMPANIMENT:
        raise InvalidInputError(ACCOMPANIMENT_COUNT_TEXT)
    voice = upsample_vocal(vocal).samples
    backing = _backing(accompaniment, voice.shape[0])
    voice, _ = limit_peak(align_length(voice, backing.shape[0]), MIX_CEILING_DBFS)
    summed = backing + voice * np.float32(db_to_gain(vocal_gain_db))
    mix, limited = limit_peak(summed, MIX_CEILING_DBFS)
    take = AudioClip(mix, TAKE_SAMPLE_RATE)
    return RemixResult(take, AudioClip(voice, TAKE_SAMPLE_RATE), limited)


def _backing(accompaniment: Sequence[AudioClip], vocal_frames: int) -> Signal:
    """Unity sum of the accompaniment at 48 kHz; silence of the vocal's length when empty."""
    if not accompaniment:
        return np.zeros((vocal_frames, STEREO), dtype=np.float32)
    total = add_stems(accompaniment)
    return resample(total.samples, total.sample_rate, TAKE_SAMPLE_RATE)
