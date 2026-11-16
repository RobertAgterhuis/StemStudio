"""Deterministic test signals and small audio file helpers shared by the generator tests."""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf

Array = npt.NDArray[np.float32]


def tone(seconds: float, rate: int = 48_000, freq: float = 440.0, amplitude: float = 0.5) -> Array:
    """Mono sine of ``seconds`` at ``rate`` as float32 ``(frames,)``."""
    t = np.arange(round(seconds * rate), dtype=np.float64) / rate
    return (amplitude * np.sin(2 * np.pi * freq * t)).astype(np.float32)


def noise(seconds: float, rate: int = 48_000, amplitude: float = 0.5, seed: int = 7) -> Array:
    """Mono uniform white noise of ``seconds`` at ``rate`` (seeded) as float32 ``(frames,)``."""
    rng = np.random.default_rng(seed)
    return rng.uniform(-amplitude, amplitude, round(seconds * rate)).astype(np.float32)


def stereo(mono: Array) -> Array:
    """Duplicate a mono signal into ``(frames, 2)``."""
    return np.column_stack([mono, mono]).astype(np.float32)


def write(path: Path, samples: Array, rate: int = 48_000, subtype: str = "PCM_24") -> Path:
    """Write ``samples`` as WAV or FLAC (by the suffix) and return ``path``."""
    sf.write(path, samples, rate, subtype=subtype)
    return path
