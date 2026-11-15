"""Synthetic stereo signals for the detail tests (deterministic numpy, 44.1 kHz)."""

import numpy as np
import numpy.typing as npt

RATE = 44_100

Audio = npt.NDArray[np.float32]


def tone(seconds: float, amplitude: float, freq: float = 220.0) -> Audio:
    """A stereo sine of ``amplitude`` (peak) lasting ``seconds``."""
    t = np.arange(int(seconds * RATE)) / RATE
    mono = (amplitude * np.sin(2 * np.pi * freq * t)).astype(np.float32)
    return np.stack([mono, mono])


def noise(seconds: float, amplitude: float, seed: int = 1) -> Audio:
    """Stereo uniform noise in ``[-amplitude, amplitude]``."""
    rng = np.random.default_rng(seed)
    frames = int(seconds * RATE)
    return rng.uniform(-amplitude, amplitude, size=(2, frames)).astype(np.float32)


def silence(seconds: float) -> Audio:
    """Stereo digital silence."""
    return np.zeros((2, int(seconds * RATE)), dtype=np.float32)


def burst(seconds: float, active_share: float, amplitude: float) -> Audio:
    """A tone that is on for the first ``active_share`` of the time and silent afterwards."""
    total = int(seconds * RATE)
    on = int(total * active_share)
    audio = np.zeros((2, total), dtype=np.float32)
    audio[:, :on] = tone(on / RATE, amplitude)[:, :on]
    return audio


def db(ratio: float) -> float:
    """Amplitude ratio in dB."""
    return float(20 * np.log10(ratio))
