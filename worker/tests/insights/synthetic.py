"""Synthetic stems for the quality analyzer tests (research.md R-04 V1).

One generator per instrument family, so every family is tested the same way: a vocals-like
formant tone, drums from decaying sines and noise bursts, a bass, a guitar-like plucked tone, a
piano-like tone and a pad as ``other``. All signals are mono float64 at 44.1 kHz and are
deterministic for a seed. The injections are exact: bleed adds ``alpha * stem_j`` (amplitude),
artefacts drop STFT cells in 1-8 kHz with probability ``p`` (musical noise) or cut gating holes
of 50-200 ms at ``rate`` per second.
"""

from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf
from scipy import signal as sps

SR = 44_100
SECONDS = 30.0
BEAT = 0.5  # 120 BPM
TARGET_RMS_DB = -26.0
FAMILIES = ("vocals", "drums", "bass", "guitar", "piano", "other")

Signal = npt.NDArray[np.float64]


def _time(seconds: float) -> Signal:
    return np.arange(round(seconds * SR)) / SR


@dataclass(frozen=True, slots=True)
class Tone:
    """Partial count, exponential decay (s) and inharmonicity of an additive tone."""

    count: int
    decay: float
    stretch: float = 0.0


def _partials(f0: float, seconds: float, tone: Tone) -> Signal:
    """Additive tone with 1/k partials."""
    t = _time(seconds)
    out = np.zeros_like(t)
    for k in range(1, tone.count + 1):
        freq = f0 * k * np.sqrt(1 + tone.stretch * k * k)
        if freq >= SR / 2 - 500:
            break
        out += np.sin(2 * np.pi * freq * t + k) / k
    attack = np.minimum(1.0, t / 0.004)
    return out * attack * np.exp(-t / tone.decay)


def _place(buffer: Signal, start: float, sound: Signal) -> None:
    begin = round(start * SR)
    end = min(len(buffer), begin + len(sound))
    if begin < end:
        buffer[begin:end] += sound[: end - begin]


def _normalise(sound: Signal) -> Signal:
    rms = float(np.sqrt(np.mean(sound**2)))
    return sound if rms == 0 else sound * (10 ** (TARGET_RMS_DB / 20) / rms)


def vocals(rng: np.random.Generator, seconds: float) -> Signal:
    """Sung phrases (2 s on, 0.5 s rest) of a vibrato tone with three formants."""
    out = np.zeros(round(seconds * SR))
    t = _time(0.5)
    formants = np.array([700.0, 1200.0, 2600.0])
    for start in np.arange(0.0, seconds, 0.5):
        if start % 2.5 >= 2.0:
            continue
        f0 = float(rng.choice([220.0, 247.0, 262.0, 294.0, 330.0, 392.0]))
        phase = 2 * np.pi * f0 * (t + 0.01 / 5.5 * np.sin(2 * np.pi * 5.5 * t))
        note = np.zeros_like(t)
        for k in range(1, int(5000 / f0) + 1):
            gain = np.sum(np.exp(-((((k * f0) - formants) / 150.0) ** 2))) + 0.02
            note += gain * np.sin(k * phase)
        envelope = np.minimum(1.0, t / 0.03) * np.minimum(1.0, (0.5 - t) / 0.03)
        _place(out, start, note * envelope)
    return _normalise(out)


def _kick(seconds: float = 0.4) -> Signal:
    t = _time(seconds)
    freq = 50 + 70 * np.exp(-t / 0.03)
    return np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t / 0.12)


def _noise_burst(rng: np.random.Generator, seconds: float, decay: float, *, high: bool) -> Signal:
    t = _time(seconds)
    noise = rng.standard_normal(len(t))
    if high:
        noise = np.diff(noise, prepend=0.0)
    return noise * np.exp(-t / decay)


def drums(rng: np.random.Generator, seconds: float) -> Signal:
    """Kick on beats 1 and 3, snare (noise + 190 Hz tone) on 2 and 4, hi-hat on eighths."""
    out = np.zeros(round(seconds * SR))
    for index, start in enumerate(np.arange(0.0, seconds, BEAT)):
        if index % 2 == 0:
            _place(out, start, _kick())
        else:
            snare = 0.5 * _noise_burst(rng, 0.25, 0.06, high=False)
            snare += 0.6 * _partials(190.0, 0.25, Tone(2, 0.08))
            _place(out, start, snare)
    for start in np.arange(0.0, seconds, BEAT / 2):
        _place(out, start, 0.25 * _noise_burst(rng, 0.1, 0.025, high=True))
    return _normalise(out)


def kick_on_beats(seconds: float) -> Signal:
    """A kick on every beat (the "playing together" control)."""
    out = np.zeros(round(seconds * SR))
    for start in np.arange(0.0, seconds, BEAT):
        _place(out, start, _kick())
    return _normalise(out)


def bass(rng: np.random.Generator, seconds: float) -> Signal:
    """A plucked bass note on every beat (41-98 Hz)."""
    out = np.zeros(round(seconds * SR))
    for start in np.arange(0.0, seconds, BEAT):
        f0 = float(rng.choice([41.2, 49.0, 55.0, 61.7, 73.4, 82.4, 98.0]))
        _place(out, start, _partials(f0, BEAT, Tone(10, 0.35)))
    return _normalise(out)


def guitar(rng: np.random.Generator, seconds: float) -> Signal:
    """A strummed three-note chord every second (150-400 Hz, bright decaying partials)."""
    out = np.zeros(round(seconds * SR))
    roots = [146.8, 164.8, 196.0, 220.0]
    for start in np.arange(0.0, seconds, 1.0):
        root = float(rng.choice(roots))
        for offset, ratio in enumerate((1.0, 1.26, 1.5)):
            _place(out, start + 0.012 * offset, _partials(root * ratio, 1.0, Tone(16, 0.6)))
    return _normalise(out)


def piano(rng: np.random.Generator, seconds: float) -> Signal:
    """Slightly inharmonic decaying notes every 0.75 s (260-520 Hz)."""
    out = np.zeros(round(seconds * SR))
    for start in np.arange(0.0, seconds, 0.75):
        f0 = float(rng.choice([261.6, 293.7, 329.6, 349.2, 392.0, 440.0, 493.9, 523.3]))
        _place(out, start, _partials(f0, 1.5, Tone(12, 0.9, 0.0004)))
    return _normalise(out)


def other(rng: np.random.Generator, seconds: float) -> Signal:
    """A sustained pad: detuned three-note chords that change every 2 s, slow attack."""
    out = np.zeros(round(seconds * SR))
    t = _time(2.0)
    envelope = np.minimum(1.0, t / 0.3) * np.minimum(1.0, (2.0 - t) / 0.3)
    for start in np.arange(0.0, seconds, 2.0):
        root = float(rng.choice([130.8, 146.8, 174.6, 196.0]))
        chord = np.zeros_like(t)
        for ratio in (1.0, 1.25, 1.5):
            for detune in (0.997, 1.003):
                f0 = root * ratio * detune
                for k in range(1, 7):
                    chord += np.sin(2 * np.pi * f0 * k * t + k * detune) / (k * k)
        _place(out, start, chord * envelope)
    return _normalise(out)


GENERATORS: Mapping[str, Callable[[np.random.Generator, float], Signal]] = {
    "vocals": vocals,
    "drums": drums,
    "bass": bass,
    "guitar": guitar,
    "piano": piano,
    "other": other,
}


def song(seed: int = 7, seconds: float = SECONDS) -> dict[str, Signal]:
    """Six clean standard stems of one synthetic song."""
    return {name: GENERATORS[name](np.random.default_rng(seed + i), seconds)
            for i, name in enumerate(FAMILIES)}  # fmt: skip


def inject_bleed(stem: Signal, source: Signal, alpha: float) -> Signal:
    """``stem + alpha * source`` (amplitude): exact bleed of ``source`` into ``stem``."""
    return stem + alpha * source


def tf_mask(stem: Signal, probability: float, seed: int = 0) -> Signal:
    """Drop STFT cells in 1-8 kHz with ``probability`` (random binary mask, musical noise)."""
    if probability <= 0:
        return stem.copy()
    rng = np.random.default_rng(seed)
    freqs, _, spec = sps.stft(stem, fs=SR, nperseg=2048, noverlap=1536)
    band = (freqs >= 1000) & (freqs <= 8000)
    keep = rng.random((int(band.sum()), spec.shape[1])) >= probability
    spec[band] *= keep
    _, out = sps.istft(spec, fs=SR, nperseg=2048, noverlap=1536)
    return np.asarray(out[: len(stem)], dtype=np.float64)


def gate_holes(stem: Signal, rate: float, seed: int = 0) -> Signal:
    """Cut silent holes of 50-200 ms at ``rate`` per second (2 ms fades)."""
    out = stem.copy()
    rng = np.random.default_rng(seed)
    seconds = len(stem) / SR
    count = round(rate * seconds)
    fade = round(0.002 * SR)
    for start in rng.uniform(0.2, seconds - 0.4, size=count):
        begin = round(start * SR)
        end = begin + round(rng.uniform(0.05, 0.2) * SR)
        gain = np.zeros(end - begin + 2 * fade)
        gain[:fade] = np.linspace(1, 0, fade)
        gain[-fade:] = np.linspace(0, 1, fade)
        out[begin - fade : end + fade] *= gain[: len(out[begin - fade : end + fade])]
    return out


def write_stem(path: Path, mono: Signal, *, rate: int = SR, channels: int = 2) -> Path:
    """Write ``mono`` unscaled as a FLAC stem (24-bit, ``channels`` copies), the pipeline format."""
    path.parent.mkdir(parents=True, exist_ok=True)
    if len(mono) and float(np.max(np.abs(mono))) >= 1.0:
        msg = f"{path.name} would clip"
        raise ValueError(msg)
    data = np.repeat(mono[:, None], channels, axis=1) if channels > 1 else mono
    sf.write(path, data, rate, format="FLAC", subtype="PCM_24")
    return path
