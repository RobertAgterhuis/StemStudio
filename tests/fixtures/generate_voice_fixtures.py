"""Synthetic own-voice samples for the voice-profile and consent tests (qa, feature 010, T009).

Run from the repository root with the worker environment (numpy, scipy, soundfile)::

    uv run --project worker python tests/fixtures/generate_voice_fixtures.py

Writes into ``tests/fixtures/audio/`` (git-ignored, 44.1 kHz mono PCM16) and records the known
facts of every file in ``tests/fixtures/voice-manifest.json`` (committed):

- ``own-voice-20s.wav``: the "own voice" of the suite: 20 s of sung vowels ("la", "mo", "nee",
  ...) on a self-written melody, sung by the formant synthesiser ``sung_voice.py``. No real
  person's voice is involved; the sample passes the singer's check (10-60 s, >= 50 % voiced).
  Chromium plays it as the fake microphone (``--use-file-for-fake-audio-capture``).
- ``own-voice-6s.wav`` / ``own-voice-70s.wav``: the same voice, too short / too long.
- ``unvoiced-20s.wav``: 20 s of low-passed noise (no pitch): refused as ``unvoiced``.
- ``sung-target-10s.wav``: a different melody a fourth lower, the 10 s "vocal stem" for the
  generator's real SVC integration test (010:T043; the prompt is ``own-voice-20s.wav``).

The melodies and the rendered audio are qa's own work, dedicated to the public domain (CC0 1.0).
Reruns are byte-identical (fixed seeds).
"""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]

from generate_fixtures import SAMPLE_RATE, Audio, cli_args, write_manifest
from generate_lyrics_fixtures import Syllable, syllable_segments
from sung_voice import PHONES, Segment, synthesise

MANIFEST_VERSION = 1
BPM = 90
BEAT_S = 60.0 / BPM
LEAD_IN_S = 0.4
TAIL_S = 0.4
PEAK = 0.7
PCM_MAX = 32_767
NOISE_SEED = 20_261_116
NOISE_SMOOTH = 6
VOICED_LEVEL = 0.5
"""Phones whose glottal source level is at least this count as voiced in ``voicedShare``."""
LICENCE = "CC0-1.0"
PROVENANCE = "self-written melodies; formant synthesiser tests/fixtures/sung_voice.py"
LIMITS = {"minSeconds": 10.0, "maxSeconds": 60.0, "minVoicedRatio": 0.5}

PHRASE: tuple[Syllable, ...] = tuple(
    Syllable(phones, beats, midi)
    for phones, beats, midi in (
        ("L AA", 1, 60), ("L AA", 1, 62), ("M OW", 2, 64), ("N IY", 1, 65), ("L AA", 1, 64),
        ("AA", 2, 62), ("M UW", 1, 60), ("N OW", 1, 62), ("L EY", 2, 64), ("AA", 3, 60),
    )
)  # fmt: skip
"""The "own voice" melody (C major, one syllable per note, then one beat of rest)."""

TARGET_PHRASE: tuple[Syllable, ...] = tuple(
    Syllable(phones, beats, midi)
    for phones, beats, midi in (
        ("N AA", 1, 55), ("N AA", 1, 57), ("L OW", 1, 59), ("M AA", 1, 60), ("IY", 2, 59),
        ("L AA", 1, 57), ("M OW", 1, 55), ("N UW", 3, 52),
    )
)  # fmt: skip
"""A second melody for the conversion target (G major, lower register)."""


@dataclass(frozen=True)
class VoiceFixture:
    """One file of the catalogue.

    Attributes:
        name: File name in the output folder.
        seconds: Length in seconds.
        phrase: Melody sung over and over; empty for noise.
        expected_check: Problem type the API answers (``ok`` when the sample is accepted).
    """

    name: str
    seconds: float
    phrase: tuple[Syllable, ...]
    expected_check: str


CATALOGUE: tuple[VoiceFixture, ...] = (
    VoiceFixture("own-voice-20s.wav", 20.0, PHRASE, "ok"),
    VoiceFixture("own-voice-6s.wav", 6.0, PHRASE, "too-short"),
    VoiceFixture("own-voice-70s.wav", 70.0, PHRASE, "too-long"),
    VoiceFixture("unvoiced-20s.wav", 20.0, (), "unvoiced"),
    VoiceFixture("sung-target-10s.wav", 10.0, TARGET_PHRASE, "ok"),
)


def melody_segments(phrase: Sequence[Syllable], seconds: float) -> list[Segment]:
    """Phone segments of ``phrase``, repeated with a beat of rest, that end before the tail."""
    segments: list[Segment] = []
    at, end = LEAD_IN_S, seconds - TAIL_S
    while True:
        for syllable in phrase:
            length = syllable.beats * BEAT_S
            if at + length > end:
                return segments
            segments.extend(syllable_segments(syllable, at, at + length - 0.03))
            at += length
        at += BEAT_S


def voiced_share(segments: Sequence[Segment], seconds: float) -> float:
    """Share of the file covered by voiced phones (glottal level >= ``VOICED_LEVEL``)."""
    voiced = sum(s.end_s - s.start_s for s in segments if PHONES[s.phone].voicing >= VOICED_LEVEL)
    return round(voiced / seconds, 3)


def render_noise(seconds: float) -> Audio:
    """Low-passed white noise of ``seconds`` seconds, peak 1."""
    rng = np.random.default_rng(NOISE_SEED)
    noise = rng.standard_normal(round(seconds * SAMPLE_RATE))
    smooth = np.convolve(noise, np.ones(NOISE_SMOOTH) / NOISE_SMOOTH, mode="same")
    return np.asarray(smooth / np.max(np.abs(smooth)), dtype=np.float64)


def render(fixture: VoiceFixture) -> tuple[Audio, float]:
    """Mono audio of ``fixture`` (peak 1) and its voiced share."""
    if not fixture.phrase:
        return render_noise(fixture.seconds), 0.0
    segments = melody_segments(fixture.phrase, fixture.seconds)
    return synthesise(segments, fixture.seconds), voiced_share(segments, fixture.seconds)


def write_fixture(fixture: VoiceFixture, out_dir: Path) -> dict[str, Any]:
    """Writes one file (mono PCM16, peak ``PEAK``) and returns its manifest entry."""
    audio, share = render(fixture)
    pcm = np.round(PEAK * audio * PCM_MAX).astype(np.int16)
    sf.write(str(out_dir / fixture.name), pcm, SAMPLE_RATE, subtype="PCM_16", format="WAV")
    return {
        "kind": "sung-voice" if fixture.phrase else "noise",
        "durationSeconds": fixture.seconds,
        "channels": 1,
        "voicedShare": share,
        "expectedCheck": fixture.expected_check,
    }


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Writes every catalogue file into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    fixtures = {fixture.name: write_fixture(fixture, out_dir) for fixture in CATALOGUE}
    manifest = {
        "version": MANIFEST_VERSION,
        "sampleRate": SAMPLE_RATE,
        "licence": LICENCE,
        "provenance": PROVENANCE,
        "limits": LIMITS,
        "fixtures": fixtures,
    }
    return write_manifest(manifest, manifest_path)


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse ``--out-dir`` and ``--manifest`` (defaults: ``audio/``, ``voice-manifest.json``)."""
    return cli_args(argv, "voice-manifest.json", __doc__)


def main(argv: Sequence[str] | None = None) -> int:
    """Writes the voice fixtures where the command line says; returns the exit code (0)."""
    args = parse_args(argv)
    count = len(generate(args.out_dir, args.manifest)["fixtures"])
    sys.stdout.write(f"wrote {count} voice fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
