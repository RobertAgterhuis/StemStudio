"""Synthetic audio for the separation tests (generated, never read from tests/fixtures)."""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf

RATE = 44_100


def synth_mix(seconds: float) -> npt.NDArray[np.float32]:
    """A stereo mix (frames, 2): two sines plus a click every half second."""
    t = np.arange(int(seconds * RATE)) / RATE
    tone = 0.3 * np.sin(2 * np.pi * 220 * t) + 0.2 * np.sin(2 * np.pi * 330 * t)
    clicks = np.where((t % 0.5) < 0.01, 0.4, 0.0)
    left = tone + clicks
    right = 0.8 * tone + clicks
    return np.stack([left, right], axis=1).astype(np.float32)


@pytest.fixture
def mix_wav(tmp_path: Path) -> Path:
    """A 0.5 s, 44.1 kHz, stereo, 16-bit WAV."""
    path = tmp_path / "mix.wav"
    sf.write(path, synth_mix(0.5), RATE, subtype="PCM_16")
    return path
