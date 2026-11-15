"""A small deterministic formant singing synthesiser for the alignment test clip (qa, 006:T012).

Klatt-style cascade of five time-varying two-pole resonators driven by a Rosenberg glottal pulse
whose pitch follows the sung notes (with a slow vibrato), plus band-limited noise for fricatives,
bursts and aspiration. Parameters are held per block of ``BLOCK`` samples and smoothed across
phone boundaries, so transitions sound coarticulated rather than spliced.

The synthesiser knows only the phones listed in ``PHONES`` (an ARPAbet subset; stops are written
as closure + burst, see ``STOPS``). It has no third-party voice data: every sample comes from the
formula tables below, so the output is the qa team's own work (CC0, see tests/fixtures/lyrics/).
"""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass

import numpy as np
import numpy.typing as npt
from scipy.signal import lfilter  # type: ignore[import-untyped]

from generate_fixtures import SAMPLE_RATE, Audio

BLOCK = 220
"""Samples per parameter block (5 ms at 44.1 kHz)."""

BANDWIDTHS = (70.0, 100.0, 140.0, 200.0, 260.0)
UPPER_FORMANTS = (3500.0, 4500.0)
FORMANT_SMOOTH_BLOCKS = 7
LEVEL_SMOOTH_BLOCKS = 3
PITCH_SMOOTH_BLOCKS = 9
VIBRATO_HZ = 5.5
VIBRATO_SEMITONES = 0.25
OPEN_PHASE = 0.4
CLOSING_PHASE = 0.16
BREATH = 0.02
NOISE_SEED = 20_261_115
# Fixed band-pass (centre Hz, bandwidth Hz) of each noise colour; "h" goes through the formants.
NOISE_BANDS: dict[str, tuple[float, float]] = {
    "s": (5200.0, 2600.0),
    "sh": (2800.0, 1500.0),
    "f": (4500.0, 5000.0),
}
NOISE_KINDS = ("s", "sh", "f", "h")


@dataclass(frozen=True)
class Phone:
    """Targets of one phone.

    Attributes:
        formants: F1, F2, F3 in Hz at the start of the phone.
        voicing: Glottal source level 0..1.
        noise: Noise colour (``s``, ``sh``, ``f``, ``h``) or ``""``.
        noise_level: Noise level 0..1.
        seconds: Fixed length; 0 for a vowel, which fills the rest of its syllable.
        glide_to: F1, F2, F3 at the end (diphthongs); ``None`` keeps ``formants``.
    """

    formants: tuple[float, float, float]
    voicing: float = 1.0
    noise: str = ""
    noise_level: float = 0.0
    seconds: float = 0.0
    glide_to: tuple[float, float, float] | None = None

    def formants_at(self, share: float) -> npt.NDArray[np.float64]:
        """F1..F3 at ``share`` (0..1) of the phone's length."""
        start = np.array(self.formants)
        end = np.array(self.glide_to or self.formants)
        return np.asarray(start + share * (end - start), dtype=np.float64)


def _vowel(f1: float, f2: float, f3: float) -> Phone:
    return Phone((f1, f2, f3))


def _glide(start: Phone, end: Phone) -> Phone:
    return Phone(start.formants, glide_to=end.formants)


_AA, _IY, _EH, _UH = (
    _vowel(730, 1090, 2440),
    _vowel(270, 2290, 3010),
    _vowel(530, 1840, 2480),
    _vowel(440, 1020, 2240),
)
_CLOSED = (250.0, 1200.0, 2400.0)

PHONES: dict[str, Phone] = {
    "AA": _AA,
    "AE": _vowel(660, 1720, 2410),
    "AH": _vowel(620, 1220, 2550),
    "AO": _vowel(570, 840, 2410),
    "EH": _EH,
    "IH": _vowel(390, 1990, 2550),
    "IY": _IY,
    "UH": _UH,
    "UW": _vowel(300, 870, 2240),
    "ER": _vowel(490, 1350, 1690),
    "AX": _vowel(500, 1500, 2500),
    "AY": _glide(_AA, _IY),
    "EY": _glide(_EH, _IY),
    "OW": Phone((570, 900, 2400), glide_to=(330, 850, 2300)),
    "AW": _glide(_AA, _UH),
    "M": Phone((280, 1000, 2200), 0.45, seconds=0.07),
    "N": Phone((280, 1700, 2600), 0.45, seconds=0.07),
    "NG": Phone((280, 2300, 2750), 0.45, seconds=0.08),
    "L": Phone((360, 1100, 2700), 0.7, seconds=0.07),
    "R": Phone((420, 1300, 1600), 0.7, seconds=0.07),
    "W": Phone((300, 610, 2200), 0.6, seconds=0.07),
    "Y": Phone((260, 2070, 3020), 0.6, seconds=0.06),
    "S": Phone((400, 1700, 2600), 0.0, "s", 0.35, 0.10),
    "Z": Phone((300, 1700, 2600), 0.35, "s", 0.2, 0.08),
    "SH": Phone((400, 1900, 2500), 0.0, "sh", 0.35, 0.10),
    "F": Phone((400, 1100, 2400), 0.0, "f", 0.25, 0.09),
    "V": Phone((300, 1100, 2400), 0.4, "f", 0.12, 0.07),
    "TH": Phone((400, 1400, 2600), 0.0, "f", 0.15, 0.08),
    "DH": Phone((300, 1400, 2600), 0.4, "f", 0.08, 0.05),
    "HH": Phone((500, 1500, 2500), 0.0, "h", 0.35, 0.07),
    "CL": Phone(_CLOSED, 0.0, seconds=0.05),
    "CLV": Phone(_CLOSED, 0.15, seconds=0.04),
    "TB": Phone((400, 1800, 2600), 0.0, "s", 0.5, 0.02),
    "DB": Phone((300, 1800, 2600), 0.3, "s", 0.3, 0.015),
    "KB": Phone((400, 1900, 2500), 0.0, "sh", 0.5, 0.025),
    "GB": Phone((300, 1900, 2500), 0.3, "sh", 0.3, 0.02),
    "PB": Phone((400, 900, 2300), 0.0, "f", 0.5, 0.015),
    "BB": Phone((300, 900, 2300), 0.3, "f", 0.3, 0.015),
}
"""Phone table (Peterson-Barney style vowel formants; consonant targets after Klatt 1980)."""

STOPS: dict[str, tuple[str, str]] = {
    "T": ("CL", "TB"), "D": ("CLV", "DB"), "K": ("CL", "KB"),
    "G": ("CLV", "GB"), "P": ("CL", "PB"), "B": ("CLV", "BB"),
}  # fmt: skip
"""Stops expand to closure + burst."""


class UnknownPhoneError(ValueError):
    """Raised for a phone symbol the synthesiser does not know."""


def expand(symbols: str) -> list[str]:
    """Phone symbols of a space-separated ARPAbet string, stops expanded to closure + burst.

    Raises:
        UnknownPhoneError: For a symbol missing from ``PHONES`` and ``STOPS``.
    """
    out: list[str] = []
    for symbol in symbols.split():
        if symbol not in PHONES and symbol not in STOPS:
            raise UnknownPhoneError(f"unknown phone {symbol!r}")
        out.extend(STOPS.get(symbol, (symbol,)))
    return out


@dataclass(frozen=True)
class Segment:
    """One phone placed in time with the sung pitch.

    Attributes:
        phone: Key into ``PHONES``.
        start_s: Start in seconds.
        end_s: End in seconds.
        midi: Sung pitch (MIDI note number).
    """

    phone: str
    start_s: float
    end_s: float
    midi: int


@dataclass
class Tracks:
    """Per-block control tracks of the synthesiser."""

    formants: npt.NDArray[np.float64]
    voicing: Audio
    noise: dict[str, Audio]
    midi: Audio

    @classmethod
    def empty(cls, blocks: int) -> Tracks:
        """Silent tracks of ``blocks`` blocks (closed-tract formants, pitch A3)."""
        formants = np.tile(np.array(_CLOSED), (blocks, 1))
        noise = {kind: np.zeros(blocks) for kind in NOISE_KINDS}
        return cls(formants, np.zeros(blocks), noise, np.full(blocks, 57.0))


def _block(seconds: float) -> int:
    return round(seconds * SAMPLE_RATE / BLOCK)


def paint(tracks: Tracks, segment: Segment) -> None:
    """Writes one segment's targets into ``tracks``."""
    first, last = _block(segment.start_s), max(_block(segment.end_s), _block(segment.start_s) + 1)
    phone = PHONES[segment.phone]
    shares = np.linspace(0.0, 1.0, last - first)
    tracks.formants[first:last] = [phone.formants_at(float(s)) for s in shares]
    tracks.voicing[first:last] = phone.voicing
    if phone.noise:
        tracks.noise[phone.noise][first:last] = phone.noise_level
    tracks.midi[first:last] = segment.midi


def _smooth(values: Audio, width: int) -> Audio:
    kernel = np.ones(width) / width
    padded = np.pad(values, (width // 2, width - 1 - width // 2), mode="edge")
    return np.asarray(np.convolve(padded, kernel, mode="valid"), dtype=np.float64)


def _per_sample(values: Audio, frames: int) -> Audio:
    centres = (np.arange(len(values)) + 0.5) * BLOCK
    return np.asarray(np.interp(np.arange(frames), centres, values), dtype=np.float64)


def glottal_source(midi: Audio, frames: int) -> Audio:
    """Differentiated Rosenberg pulse train following ``midi`` (per block) with vibrato."""
    t = np.arange(frames) / SAMPLE_RATE
    semitones = _per_sample(_smooth(midi, PITCH_SMOOTH_BLOCKS), frames)
    semitones = semitones + VIBRATO_SEMITONES * np.sin(2 * np.pi * VIBRATO_HZ * t)
    phase = np.cumsum(440.0 * 2.0 ** ((semitones - 69.0) / 12.0) / SAMPLE_RATE) % 1.0
    opening = 0.5 * (1 - np.cos(np.pi * phase / OPEN_PHASE))
    closing = np.cos(np.pi * (phase - OPEN_PHASE) / (2 * CLOSING_PHASE))
    pulse = np.where(
        phase < OPEN_PHASE, opening, np.where(phase < OPEN_PHASE + CLOSING_PHASE, closing, 0.0)
    )
    return np.asarray(np.diff(pulse, prepend=0.0), dtype=np.float64)


def _coefficients(freq: float, bandwidth: float) -> tuple[list[float], list[float]]:
    c = -np.exp(-2 * np.pi * bandwidth / SAMPLE_RATE)
    b = 2 * np.exp(-np.pi * bandwidth / SAMPLE_RATE) * np.cos(2 * np.pi * freq / SAMPLE_RATE)
    return [1.0 - b - c], [1.0, -b, -c]


def resonate(signal: Audio, freqs: Audio, bandwidth: float) -> Audio:
    """Two-pole resonator (unity gain at DC) whose frequency changes per block."""
    out = np.empty_like(signal)
    state = np.zeros(2)
    for index, freq in enumerate(freqs):
        part = slice(index * BLOCK, (index + 1) * BLOCK)
        b, a = _coefficients(float(freq), bandwidth)
        out[part], state = lfilter(b, a, signal[part], zi=state)
    return out


def cascade(signal: Audio, formants: npt.NDArray[np.float64]) -> Audio:
    """``signal`` through F1..F3 (per block) and the fixed upper formants."""
    blocks = len(formants)
    columns = [formants[:, k] for k in range(3)] + [np.full(blocks, f) for f in UPPER_FORMANTS]
    for column, bandwidth in zip(columns, BANDWIDTHS, strict=True):
        signal = resonate(signal, _smooth(column, FORMANT_SMOOTH_BLOCKS), bandwidth)
    return signal


def _band(noise: Audio, kind: str) -> Audio:
    centre, bandwidth = NOISE_BANDS[kind]
    b, a = _coefficients(centre, bandwidth)
    return np.asarray(lfilter(b, a, np.diff(noise, prepend=0.0)), dtype=np.float64)


def _level(track: Audio, frames: int) -> Audio:
    return _per_sample(_smooth(track, LEVEL_SMOOTH_BLOCKS), frames)


def render(tracks: Tracks) -> Audio:
    """Mono audio of ``tracks`` (length = blocks x ``BLOCK``), unnormalised."""
    frames = len(tracks.voicing) * BLOCK
    noise = np.random.default_rng(NOISE_SEED).standard_normal(frames)
    voicing = _level(tracks.voicing, frames)
    excitation = voicing * (glottal_source(tracks.midi, frames) + BREATH * noise)
    excitation = excitation + 0.05 * _level(tracks.noise["h"], frames) * noise
    voiced = cascade(excitation, tracks.formants)
    hiss = sum(_level(tracks.noise[k], frames) * _band(noise, k) for k in NOISE_BANDS)
    return np.asarray(voiced + hiss, dtype=np.float64)


def synthesise(segments: Sequence[Segment], duration_s: float) -> Audio:
    """Renders ``segments`` into ``duration_s`` seconds of mono audio, peak 1."""
    tracks = Tracks.empty(_block(duration_s) + 1)
    for segment in segments:
        paint(tracks, segment)
    audio = render(tracks)[: round(duration_s * SAMPLE_RATE)]
    return np.asarray(audio / np.max(np.abs(audio)), dtype=np.float64)
