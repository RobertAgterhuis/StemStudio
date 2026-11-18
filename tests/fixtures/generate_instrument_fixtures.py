"""Fake-microphone singing inputs for the instrument-view E2E suite (qa, feature 016, T014).

Run from the repository root with the worker environment (numpy, soundfile)::

    uv run --project worker python tests/fixtures/generate_instrument_fixtures.py

Writes into ``tests/fixtures/audio/`` (git-ignored, 44.1 kHz mono PCM16, 20 s) and records the
known facts, the reference melody and the SHA-256 of every file in
``tests/fixtures/instrument-manifest.json`` (committed). Chromium plays a file as the microphone
(``--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=<file>``, the fake-mic
Playwright projects):

- ``sing-on-pitch.wav``: sines with two harmonics (partials 1-3) following the fake reference
  melody (research R-11) one octave lower, to prove octave independence (spec US4 scenario 4).
- ``sing-semitone-off.wav``: the same voice 100 cents above ``sing-on-pitch.wav``.
- ``silence.wav``: digital silence (score 0).

The reference melody is one note per beat at 120 BPM from 0.5 s (the beat grid of the 015
``click-track.wav``), walking the C major scale from MIDI 60 to 72 and back: the note of beat
*k* (0-based) is ``reference_walk()[k % 14]``. The fake reference melody of the fake stack
(``views_fake.py``) uses the same rule, so the sung note always matches the reference of its
beat. Everything is qa's own work (CC0 1.0); reruns are byte-identical (no randomness, no clock).
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]

from generate_fixtures import SAMPLE_RATE, Audio, cli_args, write_manifest
from recording.generate_recording_fixtures import peak_dbfs, to_pcm

MANIFEST_VERSION = 1
SECONDS = 20.0
BPM = 120
FIRST_BEAT_S = 0.5
BEAT_S = 60.0 / BPM
FADE_S = 0.01
PARTIALS = (1.0, 0.5, 0.25)
"""Amplitudes of the fundamental and its two harmonics."""
LICENCE = "CC0-1.0"
PROVENANCE = "synthesised by tests/fixtures/generate_instrument_fixtures.py"
SCALE_UP = (60, 62, 64, 65, 67, 69, 71, 72)


def reference_walk() -> tuple[int, ...]:
    """The C major walk 60 -> 72 -> 62 (14 notes; the next cycle starts at 60 again)."""
    return SCALE_UP + SCALE_UP[-2:0:-1]


def reference_notes() -> list[dict[str, Any]]:
    """The reference notes (start, end in seconds; concert MIDI) inside the file."""
    walk = reference_walk()
    count = int((SECONDS - FIRST_BEAT_S) / BEAT_S)
    return [
        {
            "startSeconds": round(FIRST_BEAT_S + k * BEAT_S, 6),
            "endSeconds": round(FIRST_BEAT_S + (k + 1) * BEAT_S, 6),
            "midi": walk[k % len(walk)],
        }
        for k in range(count)
    ]


def _frames(seconds: float) -> int:
    return round(seconds * SAMPLE_RATE)


def _voice(frequency: float, length: int) -> Audio:
    """One sung note: three partials with 10 ms fades (peak not normalised)."""
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    tone = sum(a * np.sin(2 * np.pi * (k + 1) * frequency * t) for k, a in enumerate(PARTIALS))
    ramp = np.minimum(1.0, np.minimum(np.arange(length), np.arange(length)[::-1]) / _frames(FADE_S))
    return np.asarray(tone * ramp, dtype=np.float64)


def render_sung(offset_cents: int) -> Audio:
    """The reference melody shifted by ``offset_cents`` (mono, peak 1)."""
    out = np.zeros(_frames(SECONDS))
    for note in reference_notes():
        start, end = _frames(note["startSeconds"]), _frames(note["endSeconds"])
        hz = 440.0 * 2 ** ((note["midi"] * 100 + offset_cents - 6900) / 1200)
        out[start:end] += _voice(hz, end - start)
    return np.asarray(out / np.max(np.abs(out)), dtype=np.float64)


def render_silence() -> Audio:
    """Digital silence (mono)."""
    return np.zeros(_frames(SECONDS))


@dataclass(frozen=True)
class InstrumentFixture:
    """One file of the catalogue.

    Attributes:
        name: File name in the output folder.
        offset_cents: Sung pitch minus reference pitch; ``None`` for silence.
        render: Builds the audio (peak 1, or silence).
    """

    name: str
    offset_cents: int | None
    render: Callable[[], Audio]


CATALOGUE: tuple[InstrumentFixture, ...] = (
    InstrumentFixture("sing-on-pitch.wav", -1200, lambda: render_sung(-1200)),
    InstrumentFixture("sing-semitone-off.wav", -1100, lambda: render_sung(-1100)),
    InstrumentFixture("silence.wav", None, render_silence),
)


def write_fixture(fixture: InstrumentFixture, out_dir: Path) -> dict[str, Any]:
    """Writes one PCM16 file (peak -3.1 dBFS) and returns its manifest entry with the SHA-256."""
    target = out_dir / fixture.name
    pcm = to_pcm(fixture.render())
    sf.write(str(target), pcm, SAMPLE_RATE, "PCM_16", format="WAV")
    return {
        "channels": 1,
        "durationSeconds": SECONDS,
        "offsetCentsFromReference": fixture.offset_cents,
        "peakDbfs": peak_dbfs(pcm),
        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
    }


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Writes every catalogue file into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    manifest = {
        "version": MANIFEST_VERSION,
        "sampleRate": SAMPLE_RATE,
        "licence": LICENCE,
        "provenance": PROVENANCE,
        "referenceMelody": {
            "bpm": BPM,
            "firstBeatSeconds": FIRST_BEAT_S,
            "walk": list(reference_walk()),
            "notes": reference_notes(),
        },
        "fixtures": {f.name: write_fixture(f, out_dir) for f in CATALOGUE},
    }
    return write_manifest(manifest, manifest_path)


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse ``--out-dir`` and ``--manifest`` (default ``instrument-manifest.json``)."""
    return cli_args(argv, "instrument-manifest.json", __doc__)


def main(argv: Sequence[str] | None = None) -> int:
    """Writes the instrument fixtures where the command line says; returns the exit code (0)."""
    args = parse_args(argv)
    count = len(generate(args.out_dir, args.manifest)["fixtures"])
    sys.stdout.write(f"wrote {count} instrument fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":  # pragma: no cover
    sys.exit(main())
