"""Voice-sample normalisation (010 FR-008): any decoded sample -> 48 kHz mono 16-bit FLAC.

The checked sample is averaged to mono and resampled to 48 kHz. ``pcmSha256`` of the contract
and ``VoiceProfile.SampleSha256`` of the backend are the SHA-256 of that signal as int16
little-endian PCM (full scale 32767, clipped); the FLAC file stores exactly those samples, so
decoding it as int16 gives the same hash.
"""

import hashlib
from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_generator.audio.mix import AudioClip
from stemstudio_generator.audio.resample import resample
from stemstudio_generator.ports import Signal

SAMPLE_RATE: Final = 48_000
"""Rate of a normalised voice sample."""

_FULL_SCALE: Final = 32_767


def normalise_sample(clip: AudioClip) -> Signal:
    """``clip`` as 48 kHz mono float32 ``(frames,)`` (channel average, band-limited resample)."""
    mono = clip.samples.mean(axis=1, dtype=np.float32)[:, None]
    return resample(mono, clip.sample_rate, SAMPLE_RATE)[:, 0].astype(np.float32)


def _int16(mono: Signal) -> npt.NDArray[np.int16]:
    """Scale by 32767, round and clip to the int16 range (symmetric, -32767..32767)."""
    return np.round(np.clip(mono, -1.0, 1.0) * _FULL_SCALE).astype("<i2")


def pcm16(mono: Signal) -> bytes:
    """int16 little-endian PCM bytes of ``mono``."""
    return _int16(mono).tobytes()


def pcm_sha256(mono: Signal) -> str:
    """SHA-256 of ``pcm16(mono)`` (``VoiceSampleCheck.pcmSha256``)."""
    return hashlib.sha256(pcm16(mono)).hexdigest()


def write_sample_flac(path: Path, mono: Signal) -> Path:
    """Write ``mono`` as 48 kHz mono 16-bit FLAC with exactly the samples of ``pcm16``."""
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, _int16(mono), SAMPLE_RATE, subtype="PCM_16", format="FLAC")
    return path
