"""Synthetic structured songs with separate stems (014 T020/T050, research R-10).

120 BPM in 4/4 (one bar = 2 s, the first downbeat at 0). A form is a list of ``(kind, bars)``:
``I`` intro, ``V`` verse, ``C`` chorus, ``B`` bridge, ``D`` drums-only break, ``O`` outro and
``S:<channel>`` a solo of ``guitar``, ``piano``, ``bass`` or the detail ``other.woodwinds``.
Verse and chorus differ in harmony, melody register, brightness and drum level; in a solo the other
instruments drop to 35 %. Every stem gets 3 % bleed of the mix. Everything is generated in-test.
"""

from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_worker.schemas import BeatGrid

SR: Final = 22_050
BAR: Final = 2.0
Signal = npt.NDArray[np.float64]
Form = Sequence[tuple[str, int]]

STANDARD: Final = ("vocals", "drums", "bass", "guitar", "piano", "other")
_CHORDS: Final[Mapping[str, tuple[tuple[float, bool], ...]]] = {
    "V": ((110.0, True), (146.83, True), (164.81, True), (110.0, True)),
    "C": ((87.31, False), (98.0, False), (130.81, False), (87.31, False)),
    "B": ((123.47, True), (155.56, False), (123.47, True), (185.0, False)),
    "I": ((110.0, True),),
    "O": ((110.0, True),),
}
_MELODY: Final[Mapping[str, tuple[float, ...]]] = {
    "V": (220.0, 246.94, 261.63, 220.0), "C": (523.25, 587.33, 659.25, 783.99),
    "B": (392.0, 440.0, 349.23, 392.0),
}  # fmt: skip
_DRUMS: Final[Mapping[str, float]] = {"I": 0.25, "V": 0.45, "C": 0.9, "B": 0.6, "D": 1.0,
                                      "O": 0.25, "S": 0.7}  # fmt: skip
_SOLO_NOTES: Final = (329.63, 392.0, 440.0, 493.88, 587.33, 523.25, 440.0, 392.0)

GUITAR_SOLO: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("V", 8), ("C", 8), ("S:guitar", 8),
                            ("C", 8), ("O", 4))  # fmt: skip
PIANO_SOLO: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("S:piano", 8), ("V", 8), ("C", 8),
                           ("O", 4))  # fmt: skip
BASS_SOLO: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("V", 8), ("S:bass", 8), ("C", 8),
                          ("O", 4))  # fmt: skip
WOODWINDS_SOLO: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("S:other.woodwinds", 8), ("C", 8),
                               ("O", 4))  # fmt: skip
BRIDGE: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("V", 8), ("C", 8), ("B", 4), ("C", 8),
                       ("O", 4))  # fmt: skip
DRUM_BREAK: Final[Form] = (("I", 4), ("V", 8), ("C", 8), ("D", 4), ("C", 8), ("O", 4))
SIX_FORMS: Final[Mapping[str, Form]] = {
    "guitar": GUITAR_SOLO, "piano": PIANO_SOLO, "bass": BASS_SOLO,
    "woodwinds": WOODWINDS_SOLO, "bridge": BRIDGE, "break": DRUM_BREAK,
}  # fmt: skip


@dataclass(frozen=True, slots=True)
class Song:
    """Generated stems and the ground truth of one form."""

    stems: dict[str, Signal]
    form: Form
    duration: float

    @property
    def boundaries(self) -> list[float]:
        """Interior section starts in seconds."""
        starts = np.cumsum([bars for _, bars in self.form])[:-1] * BAR
        return [float(s) for s in starts]

    def grid(self, *, reliable: bool = True) -> BeatGrid:
        """The known grid: a beat every 0.5 s, a downbeat every bar."""
        beats = [round(i * 0.5, 3) for i in range(int(self.duration / 0.5))]
        return BeatGrid(bpm=120, beats=beats, downbeats=beats[::4], beats_per_bar=4,
                        confidence=0.9 if reliable else 0.3, reliable=reliable)  # fmt: skip

    def write(self, folder: Path) -> dict[str, Path]:
        """Write every stem as a mono FLAC; returns channel -> path."""
        folder.mkdir(parents=True, exist_ok=True)
        paths = {}
        for channel, signal in self.stems.items():
            path = folder / f"{channel}.flac"
            sf.write(path, np.clip(signal, -1, 1), SR, subtype="PCM_16")
            paths[channel] = path
        return paths


def _time(seconds: float) -> Signal:
    return np.arange(round(seconds * SR)) / SR


def tone(freq: float, seconds: float, harmonics: int = 3, gain: float = 0.2) -> Signal:
    """A harmonic tone (falling partials) with a short fade in and out."""
    t = _time(seconds)
    signal = sum(np.sin(2 * np.pi * freq * k * t) / k**1.2 for k in range(1, harmonics + 1))
    fade = np.minimum(1.0, np.minimum(t, t[::-1]) / 0.01) if len(t) else t
    return np.asarray(gain * signal * fade)


def _chord(root: float, minor: bool, harmonics: int) -> Signal:
    third = root * (1.1892 if minor else 1.2599)
    return sum((tone(f, BAR, harmonics, 0.07) for f in (root * 2, third * 2, root * 3)),
               np.zeros(round(BAR * SR)))  # fmt: skip


def _drum_bar(level: float, rng: np.random.Generator) -> Signal:
    bar = np.zeros(round(BAR * SR))
    hit = _time(0.12)
    kick = np.sin(2 * np.pi * 60 * hit) * np.exp(-hit / 0.04)
    snare = rng.normal(0, 0.3, len(hit)) * np.exp(-hit / 0.03)
    for beat in range(4):
        start = round(beat * 0.5 * SR)
        bar[start : start + len(hit)] += level * (kick if beat % 2 == 0 else snare)
    return bar


def _vocal_bar(kind: str, bar_index: int) -> Signal:
    notes = _MELODY.get(kind)
    if notes is None:
        return np.zeros(round(BAR * SR))
    freq = notes[bar_index % len(notes)]
    t = _time(BAR)
    vibrato = 1 + 0.01 * np.sin(2 * np.pi * 5.5 * t)
    phase = 2 * np.pi * np.cumsum(freq * vibrato) / SR
    voice = sum(np.sin(k * phase) / k for k in range(1, 6))
    return np.asarray(0.25 * voice)


def _solo_bar(channel: str, bar_index: int) -> Signal:
    freq = _SOLO_NOTES[bar_index % len(_SOLO_NOTES)] / (4 if channel == "bass" else 1)
    return np.concatenate([tone(freq * r, BAR / 4, 6, 0.4) for r in (1, 1.122, 1.26, 1.335)])


def _accompaniment(kind: str, bar_index: int, rng: np.random.Generator) -> dict[str, Signal]:
    base = "C" if kind.startswith("S") else kind
    chords = _CHORDS.get(base)
    empty = np.zeros(round(BAR * SR))
    if chords is None:  # drums-only break
        return {"bass": empty, "guitar": empty, "piano": empty, "other": empty}
    root, minor = chords[bar_index % len(chords)]
    bright = 6 if base == "C" else 2
    return {
        "bass": tone(root / 2, BAR, 3, 0.25),
        "guitar": _chord(root, minor, bright),
        "piano": _chord(root * 2, minor, bright // 2 + 1),
        "other": tone(root * 4, BAR, bright, 0.05) + rng.normal(0, 0.002, len(empty)),
    }


def _section(kind: str, bars: int, rng: np.random.Generator) -> dict[str, list[Signal]]:
    parts: dict[str, list[Signal]] = {name: [] for name in (*STANDARD, "other.woodwinds")}
    solo = kind.split(":", 1)[1] if kind.startswith("S:") else None
    for index in range(bars):
        bar = _accompaniment(kind, index, rng)
        bar["drums"] = _drum_bar(_DRUMS[kind[0]], rng)
        bar["vocals"] = _vocal_bar(kind, index)
        bar["other.woodwinds"] = np.zeros(round(BAR * SR))
        if solo is not None:
            bar = {name: 0.35 * sig for name, sig in bar.items()}
            bar[solo] = bar.get(solo, 0) + _solo_bar(solo, index)
        for name, bars_of in parts.items():
            bars_of.append(bar[name])
    return parts


def make_song(form: Form, seed: int = 3) -> Song:
    """Render ``form``; the woodwinds detail is folded into ``other`` like a real split parent."""
    rng = np.random.default_rng(seed)
    sections = [_section(kind, bars, rng) for kind, bars in form]
    stems = {
        name: np.concatenate([s for sec in sections for s in sec[name]]) for name in sections[0]
    }
    stems["other"] = stems["other"] + stems["other.woodwinds"]
    mix = sum(stems[name] for name in STANDARD)
    stems = {name: signal + 0.03 * mix for name, signal in stems.items()}
    duration = sum(bars for _, bars in form) * BAR
    return Song(stems=stems, form=form, duration=duration)
