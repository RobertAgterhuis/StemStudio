"""Synthetic structured song for the practice-flow tests (qa, feature 014, T018, FR-033).

Run from the repository root with the worker environment (numpy, soundfile)::

    uv run --project worker python tests/fixtures/practice-flow/generate_practice_flow_fixture.py

Writes into ``tests/fixtures/practice-flow/`` (audio git-ignored, 44.1 kHz PCM16) and records the
known facts in ``tests/fixtures/practice-flow/manifest.json`` (committed):

- ``practice-song.wav`` (stereo, 82 s): 120 BPM in 4/4 (one bar = 2 s, the first downbeat at
  0.5 s after a lead-in), form ``Intro 4 - Verse 8 - Chorus 8 - Guitar solo 8 - Chorus 8 -
  Outro 4`` bars. Verse and chorus differ in harmony, melody register, brightness and drum level;
  in the solo the other instruments drop to 35 %. Upload it on the fake stack (its separator
  copies the mix into every stem; the fake analyser cuts by bars).
- ``stems/<vocals|drums|bass|guitar|piano|other>.flac`` (mono): the same song as separate stems,
  for the real ``stems-novelty`` analyser (the sum of the stems is the mix).
- ``practice-song-free.wav`` (stereo, 30 s): a steady chord pad in soft noise, without drums or
  note onsets: the beat analyser finds no reliable grid (sections scenario 5).

Everything is generated from fixed seeds (reruns are byte-identical); the music is qa's own work,
dedicated to the public domain (CC0 1.0).
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import soundfile as sf  # type: ignore[import-untyped]

HERE = Path(__file__).resolve().parent
SAMPLE_RATE = 44_100
BPM = 120
BEATS_PER_BAR = 4
BAR_S = BEATS_PER_BAR * 60 / BPM
LEAD_IN_S = 0.5
TAIL_S = 1.5
SEED = 20_261_118
PEAK = 0.7
FREE_SECONDS = 30.0
LICENCE = "CC0-1.0"
STEMS = ("vocals", "drums", "bass", "guitar", "piano", "other")
FORM: tuple[tuple[str, str, int], ...] = (
    ("intro", "Intro", 4), ("verse", "Verse", 8), ("chorus", "Chorus 1", 8),
    ("solo", "Guitar solo", 8), ("chorus", "Chorus 2", 8), ("outro", "Outro", 4),
)  # fmt: skip
CHORDS = {
    "verse": ((110.0, True), (146.83, True), (164.81, True), (110.0, True)),
    "chorus": ((87.31, False), (98.0, False), (130.81, False), (87.31, False)),
    "intro": ((110.0, True),), "outro": ((110.0, True),),
}  # fmt: skip
MELODY = {"verse": (220.0, 246.94, 261.63, 220.0), "chorus": (523.25, 587.33, 659.25, 783.99)}
DRUM_LEVEL = {"intro": 0.25, "verse": 0.45, "chorus": 0.9, "solo": 0.7, "outro": 0.25}
SOLO_NOTES = (329.63, 392.0, 440.0, 493.88, 587.33, 523.25, 440.0, 392.0)

Audio = npt.NDArray[np.float64]


def _time(seconds: float) -> Audio:
    return np.arange(round(seconds * SAMPLE_RATE)) / SAMPLE_RATE


def tone(freq: float, seconds: float, harmonics: int = 3, gain: float = 0.2) -> Audio:
    """A harmonic tone with 10 ms fades."""
    t = _time(seconds)
    wave = sum(np.sin(2 * np.pi * freq * k * t) / k**1.2 for k in range(1, harmonics + 1))
    fade = np.minimum(1.0, np.minimum(t, t[::-1]) / 0.01)
    return np.asarray(gain * wave * fade)


def _chord(root: float, minor: bool, harmonics: int, seconds: float = BAR_S) -> Audio:
    third = root * (1.1892 if minor else 1.2599)
    notes = [tone(f, seconds, harmonics, 0.07) for f in (root * 2, third * 2, root * 3)]
    return np.asarray(np.sum(notes, axis=0))


def _drum_bar(level: float, rng: np.random.Generator) -> Audio:
    bar = np.zeros(round(BAR_S * SAMPLE_RATE))
    hit = _time(0.12)
    kick = np.sin(2 * np.pi * 60 * hit) * np.exp(-hit / 0.04)
    snare = rng.normal(0, 0.3, len(hit)) * np.exp(-hit / 0.03)
    for beat in range(BEATS_PER_BAR):
        start = round(beat * 60 / BPM * SAMPLE_RATE)
        bar[start : start + len(hit)] += level * (kick if beat % 2 == 0 else snare)
    return bar


def _vocal_bar(kind: str, index: int) -> Audio:
    notes = MELODY.get(kind)
    if notes is None:
        return np.zeros(round(BAR_S * SAMPLE_RATE))
    t = _time(BAR_S)
    phase = 2 * np.pi * np.cumsum(notes[index % 4] * (1 + 0.01 * np.sin(2 * np.pi * 5.5 * t)))
    return np.asarray(0.25 * sum(np.sin(k * phase / SAMPLE_RATE) / k for k in range(1, 6)))


def _bar(kind: str, index: int, rng: np.random.Generator) -> dict[str, Audio]:
    chords = CHORDS.get(kind, CHORDS["chorus"])  # the solo plays over the chorus chords
    root, minor = chords[index % len(chords)]
    bright = 6 if kind in ("chorus", "solo") else 2
    parts = {
        "vocals": _vocal_bar(kind, index), "drums": _drum_bar(DRUM_LEVEL[kind], rng),
        "bass": tone(root / 2, BAR_S, 3, 0.25), "guitar": _chord(root, minor, bright),
        "piano": _chord(root * 2, minor, bright // 2 + 1),
        "other": tone(root * 4, BAR_S, bright, 0.05),
    }  # fmt: skip
    if kind == "solo":
        parts = {name: 0.35 * part for name, part in parts.items()}
        note = SOLO_NOTES[index % len(SOLO_NOTES)]
        parts["guitar"] = parts["guitar"] + np.concatenate(
            [tone(note * r, BAR_S / 4, 6, 0.4) for r in (1, 1.122, 1.26, 1.335)]
        )
    return parts


def render_stems() -> dict[str, Audio]:
    """The six mono stems of the song, the lead-in silent, 3 % bleed of the mix in each."""
    rng = np.random.default_rng(SEED)
    bars = [_bar(kind, k, rng) for kind, _, count in FORM for k in range(count)]
    lead = np.zeros(round(LEAD_IN_S * SAMPLE_RATE))
    stems = {name: np.concatenate([lead, *(bar[name] for bar in bars)]) for name in STEMS}
    tail = round(total_seconds() * SAMPLE_RATE) - len(stems["vocals"])
    stems = {name: np.pad(signal, (0, tail)) for name, signal in stems.items()}
    mix = np.sum(list(stems.values()), axis=0)
    scale = PEAK / float(np.max(np.abs(mix * 1.03)))
    return {name: (signal + 0.03 * mix) * scale for name, signal in stems.items()}


def render_free() -> Audio:
    """A steady chord pad in soft noise: no onsets, no pulse (the noise is seeded)."""
    pad = np.sum([_chord(root, True, 2, FREE_SECONDS) for root in (110.0, 87.31, 146.83)], axis=0)
    noise = np.random.default_rng(SEED + 1).normal(0.0, 0.05, len(pad))
    free = pad + noise
    return np.asarray(free * (PEAK / float(np.max(np.abs(free)))))


def total_seconds() -> float:
    """Length of the song: lead-in, the bars of the form and the tail (82 s)."""
    return LEAD_IN_S + sum(count for _, _, count in FORM) * BAR_S + TAIL_S


def sections() -> list[dict[str, Any]]:
    """The known form in seconds (the lead-in belongs to the intro, the tail to the outro)."""
    out, start_bar = [], 0
    for kind, label, count in FORM:
        start = 0.0 if start_bar == 0 else LEAD_IN_S + start_bar * BAR_S
        out.append({"kind": kind, "label": label, "startSeconds": round(start, 3),
                    "bars": count, "startBar": start_bar + 1})  # fmt: skip
        start_bar += count
    for current, following in zip(out, [*out[1:], None], strict=True):
        current["endSeconds"] = following["startSeconds"] if following else total_seconds()
    return out


def downbeats() -> list[float]:
    """Every bar start of the form (the grid the analyser should find)."""
    count = sum(c for _, _, c in FORM)
    return [round(LEAD_IN_S + k * BAR_S, 3) for k in range(count)]


@dataclass(frozen=True, slots=True)
class Written:
    """A written file and its facts for the manifest."""

    name: str
    channels: int
    seconds: float


def _write(path: Path, audio: Audio, subtype_format: tuple[str, str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(np.clip(audio, -1, 1) * 32_767) / 32_767
    sf.write(path, pcm, SAMPLE_RATE, subtype=subtype_format[0], format=subtype_format[1])


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write the audio below ``out_dir`` and the manifest; return the manifest."""
    stems = render_stems()
    mix = np.sum(list(stems.values()), axis=0)
    _write(out_dir / "practice-song.wav", np.column_stack([mix, mix]), ("PCM_16", "WAV"))
    for name, signal in stems.items():
        _write(out_dir / "stems" / f"{name}.flac", signal, ("PCM_16", "FLAC"))
    free = render_free()
    _write(out_dir / "practice-song-free.wav", np.column_stack([free, free]), ("PCM_16", "WAV"))
    manifest = build_manifest()
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    return manifest


def build_manifest() -> dict[str, Any]:
    """The known facts of every file (committed as ``manifest.json``)."""
    return {
        "manifestVersion": 1, "licence": LICENCE, "sampleRate": SAMPLE_RATE,
        "song": {"file": "practice-song.wav", "channels": 2, "seconds": total_seconds(),
                 "bpm": BPM, "beatsPerBar": BEATS_PER_BAR, "firstDownbeatSeconds": LEAD_IN_S,
                 "downbeats": downbeats(), "sections": sections(),
                 "stems": {name: f"stems/{name}.flac" for name in STEMS}},
        "free": {"file": "practice-song-free.wav", "channels": 2, "seconds": FREE_SECONDS,
                 "expectReliableGrid": False},
    }  # fmt: skip


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point: ``--out-dir`` and ``--manifest`` (defaults next to the script)."""
    parser = argparse.ArgumentParser(description=(__doc__ or "").splitlines()[0])
    parser.add_argument("--out-dir", type=Path, default=HERE)
    parser.add_argument("--manifest", type=Path, default=HERE / "manifest.json")
    args = parser.parse_args(argv)
    generate(args.out_dir, args.manifest)
    return 0


if __name__ == "__main__":  # pragma: no cover - thin CLI wrapper
    sys.exit(main())
