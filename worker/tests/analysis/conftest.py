"""Synthetic test signals for the beat analyzer (T051).

Every signal is generated in-test with numpy and written with soundfile into ``tmp_path`` as
44.1 kHz stereo 16-bit WAV, the pipeline format. Click tracks have exactly known beat times.
"""

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf

SR = 44_100
FIRST_BEAT = 0.5
ACCENT_GAIN = 1.0
WEAK_GAIN = 0.35

Signal = npt.NDArray[np.float64]


@dataclass(frozen=True, slots=True)
class ClickTrack:
    """A synthetic track and its ground truth."""

    path: Path
    bpm: float
    beats_per_bar: int
    beats: list[float]

    @property
    def downbeats(self) -> list[float]:
        """Ground-truth downbeat times (every ``beats_per_bar``-th beat from the first)."""
        return self.beats[:: self.beats_per_bar]


def beat_times(bpm: float, seconds: float, first: float = FIRST_BEAT) -> list[float]:
    """Return the ideal beat times of a constant tempo starting at ``first``."""
    period = 60.0 / bpm
    count = int((seconds - first - 0.1) / period) + 1
    return [first + k * period for k in range(count)]


def click(gain: float, freq: float = 1_000.0, decay: float = 0.006) -> Signal:
    """Return one exponentially decaying sine burst."""
    t = np.arange(int(decay * 8 * SR)) / SR
    return np.asarray(gain * np.sin(2 * np.pi * freq * t) * np.exp(-t / decay))


def place(signal: Signal, burst: Signal, at: float) -> None:
    """Add ``burst`` into ``signal`` starting at time ``at`` (truncated at the end)."""
    start = round(at * SR)
    end = min(len(signal), start + len(burst))
    signal[start:end] += burst[: end - start]


def click_signal(beats: list[float], beats_per_bar: int, seconds: float) -> Signal:
    """Render clicks at ``beats``, the first of every bar accented (louder, higher)."""
    signal = np.zeros(int(seconds * SR))
    accent, weak = click(ACCENT_GAIN, 1_500.0), click(WEAK_GAIN)
    for index, at in enumerate(beats):
        place(signal, accent if index % beats_per_bar == 0 else weak, at)
    return signal


def music_signal(beats: list[float], seconds: float) -> Signal:
    """Render a music-like mix: a sustained chord pad, a kick on every beat, accented bars."""
    t = np.arange(int(seconds * SR)) / SR
    signal = np.zeros(len(t))
    for freq in (220.0, 277.18, 329.63):
        signal += 0.08 * np.sin(2 * np.pi * freq * t)
    for index, at in enumerate(beats):
        gain = 0.9 if index % 4 == 0 else 0.5
        place(signal, click(gain, 80.0, 0.03), at)
        place(signal, click(gain * 0.4, 3_000.0, 0.004), at)
    return signal


def white_noise(seconds: float, seed: int = 7) -> Signal:
    """Return deterministic white noise."""
    return np.random.default_rng(seed).normal(0.0, 0.2, int(seconds * SR))


def write_wav(path: Path, mono: Signal) -> Path:
    """Write ``mono`` as 44.1 kHz stereo 16-bit WAV and return the path."""
    peak = max(1.0, float(np.max(np.abs(mono), initial=0.0)))
    stereo = np.column_stack([mono, mono]) / peak
    sf.write(path, stereo, SR, subtype="PCM_16")
    return path


MakeWav = Callable[[str, Signal], Path]
MakeClickTrack = Callable[..., ClickTrack]


@pytest.fixture
def make_wav(tmp_path: Path) -> MakeWav:
    """Write an arbitrary mono signal into ``tmp_path`` under the given file name."""

    def _make(name: str, mono: Signal) -> Path:
        return write_wav(tmp_path / name, mono)

    return _make


@pytest.fixture
def make_click_track(make_wav: MakeWav) -> MakeClickTrack:
    """Create a click track WAV with known tempo, meter and beat times."""

    def _make(bpm: float, beats_per_bar: int = 4, seconds: float = 12.0) -> ClickTrack:
        beats = beat_times(bpm, seconds)
        mono = click_signal(beats, beats_per_bar, seconds)
        path = make_wav(f"click_{bpm:g}_{beats_per_bar}.wav", mono)
        return ClickTrack(path=path, bpm=bpm, beats_per_bar=beats_per_bar, beats=beats)

    return _make


@pytest.fixture
def make_music_track(make_wav: MakeWav) -> MakeClickTrack:
    """Create a music-like 4/4 WAV (pad + kicks) with known tempo and beat times."""

    def _make(bpm: float, seconds: float = 16.0) -> ClickTrack:
        beats = beat_times(bpm, seconds)
        path = make_wav(f"music_{bpm:g}.wav", music_signal(beats, seconds))
        return ClickTrack(path=path, bpm=bpm, beats_per_bar=4, beats=beats)

    return _make
