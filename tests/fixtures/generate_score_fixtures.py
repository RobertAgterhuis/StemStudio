"""Synthetic note material with known notes for the sheet music tests (qa, feature 008, T011).

Run from the repository root with the worker environment (numpy, soundfile)::

    uv run --project worker python tests/fixtures/generate_score_fixtures.py

Writes into ``tests/fixtures/audio/`` (git-ignored, 44.1 kHz stereo PCM16) and records every
note and drum hit in ``tests/fixtures/score-manifest.json`` (committed), so the real-model
accuracy tests (SC-002) and the drum-onset tests compare against exact ground truth:

- ``score_bass_line_120bpm.wav``: research.md S-03 single voice: 78 notes of a pentatonic set
  (MIDI 40-52), eighths and quarters at 120 BPM with about 15 % rests, five harmonics with an
  exponential decay (target note F1 >= 0.90).
- ``score_piano_texture_20s.wav``: research.md S-04 polyphonic texture: a treble melody note every
  0.5 s over a bass-staff triad every second, inharmonic piano-like partials (target F1 >= 0.85).
- ``score_drums_<piece>.wav`` for kick, snare, toms, hihat, cymbals: the five detail-stem inputs
  of a drums transcription (``drumInputs``): kick on 1 and 3, snare on 2 and 4, hi-hat on every
  eighth, a tom fill in the last bar, crashes on bars 1 and 5; 8 bars.

All files share one grid (120 BPM, 4/4, first downbeat 0.5 s), recorded as a ``TempoGrid`` of
kind ``beatGrid``. ``onset_f_measure`` is the reference metric (onset within 50 ms, same pitch).
Reruns are byte-identical (fixed seeds).
"""

from __future__ import annotations

import argparse
import sys
from collections.abc import Callable, Sequence
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]

from generate_fixtures import SAMPLE_RATE, Audio, cli_args, write_manifest

MANIFEST_VERSION = 1
BPM = 120
BEAT_S = 60.0 / BPM
BEATS_PER_BAR = 4
FIRST_DOWNBEAT_S = 0.5
SEED = 20_261_115
PEAK = 0.8
PCM_MAX = 32_767
ONSET_TOLERANCE_S = 0.05
BASS_SET = (40, 43, 45, 47, 50, 52)
BASS_NOTES = 78
REST_SHARE = 0.15
BASS_HARMONICS = (1.0, 0.5, 0.33, 0.25, 0.2)
PIANO_SECONDS = 20.0
MELODY_SET = (64, 65, 67, 69, 71, 72, 74, 76, 77, 79)
TRIADS = ((48, 52, 55), (53, 57, 48), (55, 59, 50), (45, 48, 52))
STIFFNESS = 0.0004
DRUM_BARS = 8
DRUM_PIECES = ("kick", "snare", "toms", "hihat", "cymbals")


@dataclass(frozen=True)
class Note:
    """A ground-truth note (seconds, MIDI pitch, velocity 0..1) as data-model 2.2 ``NoteEvent``."""

    start: float
    end: float
    pitch: int
    velocity: float = 0.8


@dataclass(frozen=True)
class Hit:
    """A ground-truth drum hit as data-model 2.2 ``DrumHit``."""

    time: float
    piece: str
    strength: float = 0.9


def _hz(pitch: int) -> float:
    return float(440.0 * 2.0 ** ((pitch - 69) / 12.0))


def _t(seconds: float) -> Audio:
    return np.arange(round(seconds * SAMPLE_RATE), dtype=np.float64) / SAMPLE_RATE


def harmonic_tone(pitch: int, seconds: float, velocity: float) -> Audio:
    """Five harmonics with an exponential decay and a 5 ms release (S-03 bass tone)."""
    t = _t(seconds)
    tone = sum(
        w * np.sin(2 * np.pi * (k + 1) * _hz(pitch) * t) for k, w in enumerate(BASS_HARMONICS)
    )
    release = np.minimum(1.0, (t[-1] - t) / 0.005 + 1e-9)
    return np.asarray(velocity * tone * np.exp(-t / 0.6) * release, dtype=np.float64)


def piano_tone(pitch: int, seconds: float, velocity: float) -> Audio:
    """Inharmonic piano-like partials ``f_k = k f0 sqrt(1 + B k^2)``, faster decay per partial."""
    t = _t(seconds)
    f0 = _hz(pitch)
    tone = np.zeros_like(t)
    for k in range(1, 9):
        partial = k * f0 * np.sqrt(1 + STIFFNESS * k * k)
        tone += np.sin(2 * np.pi * partial * t) * np.exp(-t * (1.5 + 0.6 * k)) / k
    release = np.minimum(1.0, (t[-1] - t) / 0.01 + 1e-9)
    return np.asarray(velocity * tone * release, dtype=np.float64)


def _mix(
    length_s: float, notes: Sequence[Note], voice: Callable[[int, float, float], Audio]
) -> Audio:
    out = np.zeros(round(length_s * SAMPLE_RATE))
    for note in notes:
        sound = voice(note.pitch, note.end - note.start, note.velocity)
        begin = round(note.start * SAMPLE_RATE)
        out[begin : begin + len(sound)] += sound[: len(out) - begin]
    return out


def bass_line(rng: np.random.Generator) -> list[Note]:
    """78 notes from ``BASS_SET``; eighths or quarters; about ``REST_SHARE`` rests in between."""
    notes: list[Note] = []
    at = FIRST_DOWNBEAT_S
    while len(notes) < BASS_NOTES:
        length = BEAT_S * float(rng.choice((0.5, 1.0)))
        if rng.random() >= REST_SHARE:
            pitch = int(rng.choice(BASS_SET))
            notes.append(
                Note(round(at, 4), round(at + length, 4), pitch, round(0.6 + 0.3 * rng.random(), 3))
            )
        at += length
    return notes


def piano_texture() -> list[Note]:
    """Melody every 0.5 s (treble) over a triad every second (bass staff, below MIDI 60)."""
    steps = round((PIANO_SECONDS - 2 * FIRST_DOWNBEAT_S) / BEAT_S)
    starts = [round(FIRST_DOWNBEAT_S + i * BEAT_S, 4) for i in range(steps)]
    melody = [
        Note(s, round(s + BEAT_S, 4), MELODY_SET[(i * 3) % len(MELODY_SET)])
        for i, s in enumerate(starts)
    ]
    chords = [
        Note(s, round(s + 2 * BEAT_S, 4), p, 0.6)
        for i, s in enumerate(starts[::2])
        for p in TRIADS[(i // 2) % len(TRIADS)]
    ]
    return sorted([*melody, *chords], key=lambda n: (n.start, n.pitch))


def _beat(index: float) -> float:
    return round(FIRST_DOWNBEAT_S + index * BEAT_S, 4)


def _groove(beats: int) -> list[Hit]:
    hands_and_feet = [Hit(_beat(b), ("kick", "snare")[b % 2]) for b in range(beats)]
    return hands_and_feet + [Hit(_beat(e / 2), "hihat", 0.5) for e in range(2 * beats)]


def _accents(beats: int) -> list[Hit]:
    crashes = [Hit(_beat(bar * BEATS_PER_BAR), "cymbals") for bar in (0, 4)]
    return crashes + [Hit(_beat(beats - 2 + s / 4), "toms", 0.8) for s in range(8)]


def drum_hits() -> list[Hit]:
    """Kick 1+3, snare 2+4, hi-hat eighths, crashes on bars 1 and 5, a 16th tom fill in bar 8."""
    beats = DRUM_BARS * BEATS_PER_BAR
    return sorted([*_groove(beats), *_accents(beats)], key=lambda h: (h.time, h.piece))


def _decay(seconds: float, tau: float) -> Audio:
    return np.asarray(np.exp(-_t(seconds) / tau), dtype=np.float64)


def drum_sound(piece: str, rng: np.random.Generator) -> Audio:
    """One hit of ``piece``: pitched sweeps for kick/toms, filtered noise for the rest."""
    if piece in ("kick", "toms"):
        start, end = (110.0, 45.0) if piece == "kick" else (220.0, 140.0)
        envelope = _decay(0.25, 0.06)
        phase = np.cumsum(2 * np.pi * np.linspace(start, end, len(envelope)) / SAMPLE_RATE)
        return np.asarray(np.sin(phase) * envelope, dtype=np.float64)
    seconds, tau = {"snare": (0.2, 0.05), "hihat": (0.06, 0.01), "cymbals": (1.2, 0.4)}[piece]
    envelope = _decay(seconds, tau)
    noise = rng.standard_normal(len(envelope) + 1)
    return np.asarray(0.6 * np.diff(noise) * envelope, dtype=np.float64)


def render_drum(piece: str, hits: Sequence[Hit], length_s: float) -> Audio:
    """The detail stem of one piece: its hits only (mono)."""
    out = np.zeros(round(length_s * SAMPLE_RATE))
    rng = np.random.default_rng(SEED + DRUM_PIECES.index(piece))
    for hit in (h for h in hits if h.piece == piece):
        sound = hit.strength * drum_sound(piece, rng)
        begin = round(hit.time * SAMPLE_RATE)
        out[begin : begin + len(sound)] += sound[: len(out) - begin]
    return out


def to_pcm(mono: Audio) -> np.ndarray[Any, np.dtype[np.int16]]:
    """Peak-normalised (``PEAK``) stereo 16-bit PCM of a mono signal."""
    scaled = PEAK * mono / np.max(np.abs(mono))
    pcm = np.round(scaled * PCM_MAX).astype(np.int16)
    return np.column_stack((pcm, pcm))


def tempo_grid(length_s: float) -> dict[str, Any]:
    """The shared grid as a worker ``TempoGrid`` of kind ``beatGrid`` (beats up to ``length_s``)."""
    count = int((length_s - FIRST_DOWNBEAT_S) / BEAT_S) + 1
    beats = [_beat(i) for i in range(count)]
    return {
        "kind": "beatGrid",
        "bpm": float(BPM),
        "beatsPerBar": BEATS_PER_BAR,
        "beats": beats,
        "downbeats": beats[::BEATS_PER_BAR],
    }


def _take_match(note: Note, unused: list[Note], tolerance_s: float) -> bool:
    """Removes and reports the first unused estimate matching ``note`` (same pitch, close onset)."""
    for candidate in unused:
        if candidate.pitch == note.pitch and abs(candidate.start - note.start) <= tolerance_s:
            unused.remove(candidate)
            return True
    return False


def onset_f_measure(
    reference: Sequence[Note], estimate: Sequence[Note], tolerance_s: float = ONSET_TOLERANCE_S
) -> float:
    """Note onset F-measure (offsets ignored, as research.md S-03).

    A reference note matches one unused estimate of the same pitch whose onset is within
    ``tolerance_s``; matching is greedy in onset order.
    """
    unused = sorted(estimate, key=lambda n: n.start)
    ordered = sorted(reference, key=lambda n: n.start)
    matched = sum(_take_match(note, unused, tolerance_s) for note in ordered)
    if matched == 0:
        return 0.0
    precision, recall = matched / len(estimate), matched / len(reference)
    return 2 * precision * recall / (precision + recall)


@dataclass(frozen=True)
class Rendered:
    """A fixture file with its manifest facts."""

    name: str
    pcm: np.ndarray[Any, np.dtype[np.int16]]
    facts: dict[str, Any]


def _length(notes: Sequence[Note]) -> float:
    return float(np.ceil(max(n.end for n in notes) + 1.0))


def _pitched(name: str, profile: str, notes: Sequence[Note], target: float) -> Rendered:
    voice = harmonic_tone if profile == "bass" else piano_tone
    length = _length(notes) if profile == "bass" else PIANO_SECONDS
    facts = {
        "profile": profile,
        "durationSeconds": length,
        "targetF1": target,
        "grid": tempo_grid(length),
        "notes": [asdict(n) for n in notes],
    }
    return Rendered(name, to_pcm(_mix(length, notes, voice)), facts)


def render_pitched() -> list[Rendered]:
    """The single-voice bass line and the piano texture."""
    bass = bass_line(np.random.default_rng(SEED))
    return [
        _pitched("score_bass_line_120bpm.wav", "bass", bass, 0.90),
        _pitched("score_piano_texture_20s.wav", "piano", piano_texture(), 0.85),
    ]


def _drum(piece: str, hits: Sequence[Hit], length: float) -> Rendered:
    facts = {
        "profile": "drums",
        "piece": piece,
        "durationSeconds": length,
        "grid": tempo_grid(length),
        "hits": [asdict(h) for h in hits if h.piece == piece],
    }
    return Rendered(f"score_drums_{piece}.wav", to_pcm(render_drum(piece, hits, length)), facts)


def render_drums() -> list[Rendered]:
    """One file per drum piece, all with the shared hit list's grid."""
    hits = drum_hits()
    length = float(DRUM_BARS * BEATS_PER_BAR * BEAT_S + 2 * FIRST_DOWNBEAT_S + 1.0)
    return [_drum(piece, hits, length) for piece in DRUM_PIECES]


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Writes every score fixture into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    rendered = [*render_pitched(), *render_drums()]
    for item in rendered:
        sf.write(str(out_dir / item.name), item.pcm, SAMPLE_RATE, subtype="PCM_16", format="WAV")
    facts = {item.name: item.facts for item in rendered}
    return write_manifest(
        {
            "version": MANIFEST_VERSION,
            "sampleRate": SAMPLE_RATE,
            "onsetToleranceSeconds": ONSET_TOLERANCE_S,
            "fixtures": facts,
        },
        manifest_path,
    )


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Command line: ``--out-dir`` (default ``audio/``) and ``--manifest``."""
    return cli_args(argv, "score-manifest.json", __doc__)


def main(argv: Sequence[str] | None = None) -> int:
    """Entry point; returns the process exit code."""
    args = parse_args(argv)
    written = generate(args.out_dir, args.manifest)["fixtures"]
    sys.stdout.write(f"wrote {len(written)} score fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
