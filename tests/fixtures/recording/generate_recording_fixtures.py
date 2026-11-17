"""Synthetic play-along inputs for the recording E2E suite (qa, feature 015, T009).

Run from ``tests/fixtures`` with the worker environment (numpy, soundfile)::

    cd tests/fixtures
    uv run --project ../../worker python -m recording.generate_recording_fixtures

Writes into ``tests/fixtures/recording/`` (the WAV files are git-ignored; 44.1 kHz PCM16, 20 s)
and records the known facts and the SHA-256 of every file in ``recording/manifest.json``
(committed). One file per instrument family, played by Chromium's fake capture device
(``--use-file-for-fake-audio-capture=<file>``), so every family is recorded once (SC-008):

- ``input-vocals.wav`` (mono): sung vowels by the formant synthesiser ``sung_voice.py``.
- ``input-guitar.wav`` (mono): plucked strings (Karplus-Strong) around 196-392 Hz.
- ``input-bass.wav`` (mono): low plucks around 41-82 Hz.
- ``input-piano.wav`` (stereo): a slow chord pad, the voices spread left and right (keys).
- ``input-drums.wav`` (stereo): kick, snare and hat hits, hat panned right.
- ``input-other.wav`` (mono): a bowed tone with vibrato (strings, winds and the rest).
- ``click-track.wav`` (stereo): clicks every 0.5 s from 0.5 s, the fixture *track* the loopback
  test imports (the fake separator copies it into every stem).
- ``silent.wav`` (mono): digital silence (refused as ``silent-take``).

No real person's voice or playing is involved; the audio is qa's own work (CC0 1.0). Reruns are
byte-identical (fixed seeds, no clock).
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

from generate_fixtures import SAMPLE_RATE, Audio, click, write_manifest
from generate_voice_fixtures import PHRASE, melody_segments
from sung_voice import synthesise

MANIFEST_VERSION = 1
SECONDS = 20.0
PEAK = 0.7
PCM_MAX = 32_767
SEED = 20_261_117
CLICK_PERIOD_S = 0.5
FIRST_CLICK_S = 0.5
NOTE_S = 0.5
OUT_DIR = Path(__file__).resolve().parent
LICENCE = "CC0-1.0"
PROVENANCE = "synthesised by tests/fixtures/recording/generate_recording_fixtures.py"
GUITAR_HZ = (196.0, 246.94, 293.66, 392.0)
BASS_HZ = (41.2, 55.0, 61.74, 82.41)
PAD_HZ = ((261.63, 329.63, 392.0), (220.0, 261.63, 329.63))


def _frames(seconds: float) -> int:
    return round(seconds * SAMPLE_RATE)


def _place(target: Audio, start: int, sound: Audio) -> None:
    end = min(start + len(sound), len(target))
    target[start:end] += sound[: end - start]


def _normalise(audio: Audio) -> Audio:
    return np.asarray(audio / np.max(np.abs(audio)), dtype=np.float64)


def pluck(frequency: float, seconds: float, rng: np.random.Generator) -> Audio:
    """Karplus-Strong plucked string: a noise burst through a damped delay line of one period."""
    period = max(2, round(SAMPLE_RATE / frequency))
    line = rng.uniform(-1.0, 1.0, period)
    blocks = []
    for _ in range(_frames(seconds) // period + 1):
        blocks.append(line)
        line = 0.996 * 0.5 * (line + np.roll(line, -1))
    return np.concatenate(blocks)[: _frames(seconds)]


def _plucked(frequencies: Sequence[float], seed: int) -> Audio:
    rng = np.random.default_rng(seed)
    out = np.zeros(_frames(SECONDS))
    for index, start in enumerate(range(0, _frames(SECONDS - 1.0), _frames(NOTE_S))):
        _place(out, start, pluck(frequencies[index % len(frequencies)], 1.0, rng))
    return _normalise(out)


def render_vocals() -> Audio:
    """Sung vowels (mono, peak 1)."""
    return synthesise(melody_segments(PHRASE, SECONDS), SECONDS)


def render_guitar() -> Audio:
    """Plucked strings every 0.5 s (mono, peak 1)."""
    return _plucked(GUITAR_HZ, SEED)


def render_bass() -> Audio:
    """Low plucks every 0.5 s (mono, peak 1)."""
    return _plucked(BASS_HZ, SEED + 1)


def _tone(frequency: float, length: int) -> Audio:
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    return np.asarray(np.sin(2 * np.pi * frequency * t), dtype=np.float64)


def render_piano() -> Audio:
    """A chord pad changing every 2 s; voice k sits left, centre or right (stereo, peak 1)."""
    out = np.zeros((_frames(SECONDS), 2))
    chord_len = _frames(2.0)
    fade = np.minimum(1.0, np.minimum(np.arange(chord_len), np.arange(chord_len)[::-1]) / 882)
    for index, start in enumerate(range(0, len(out), chord_len)):
        for voice, frequency in enumerate(PAD_HZ[index % len(PAD_HZ)]):
            tone = _tone(frequency, chord_len) * fade
            _place(out, start, np.column_stack((tone * (1 - voice / 2), tone * (voice / 2))))
    return _normalise(out)


def _drum_hit(kind: int, rng: np.random.Generator) -> Audio:
    n = np.arange(_frames(0.15), dtype=np.float64)
    decay = np.exp(-n / (0.02 * SAMPLE_RATE))
    if kind == 0:
        return np.sin(np.cumsum(2 * np.pi * np.linspace(110.0, 45.0, len(n)) / SAMPLE_RATE)) * decay
    return rng.uniform(-1.0, 1.0, len(n)) * decay * (0.8 if kind == 1 else 0.3)


def render_drums() -> Audio:
    """Kick on beats 1/3, snare on 2/4, hat on every half beat at 120 BPM (stereo, peak 1)."""
    rng = np.random.default_rng(SEED + 2)
    out = np.zeros((_frames(SECONDS), 2))
    for step, start in enumerate(range(0, _frames(SECONDS - 0.2), _frames(0.25))):
        hat = _drum_hit(2, rng)
        _place(out, start, np.column_stack((0.3 * hat, hat)))
        if step % 2 == 0:
            body = _drum_hit(step // 2 % 2, rng)
            _place(out, start, np.column_stack((body, body)))
    return _normalise(out)


def render_other() -> Audio:
    """A bowed tone at 330 Hz with 5 Hz vibrato and slow bow changes (mono, peak 1)."""
    t = np.arange(_frames(SECONDS), dtype=np.float64) / SAMPLE_RATE
    phase = 2 * np.pi * np.cumsum(330.0 + 4.0 * np.sin(2 * np.pi * 5.0 * t)) / SAMPLE_RATE
    bow = 0.6 + 0.4 * np.abs(np.sin(np.pi * t / 2.0))
    tone = sum(np.sin(k * phase) / k for k in range(1, 6))
    return _normalise(np.asarray(tone * bow, dtype=np.float64))


def click_times() -> list[float]:
    """Click onsets of ``click-track.wav`` (seconds)."""
    count = int((SECONDS - FIRST_CLICK_S - 0.1) / CLICK_PERIOD_S) + 1
    return [round(FIRST_CLICK_S + k * CLICK_PERIOD_S, 6) for k in range(count)]


def render_click_track() -> Audio:
    """Sample-exact clicks every 0.5 s (stereo, peak 1); the first sample of a click is its peak."""
    out = np.zeros(_frames(SECONDS))
    sound = click(1000.0, 1.0)
    for at in click_times():
        _place(out, _frames(at), sound)
    return _normalise(np.column_stack((out, out)))


def render_silent() -> Audio:
    """Digital silence (mono)."""
    return np.zeros(_frames(SECONDS))


@dataclass(frozen=True)
class RecordingFixture:
    """One file of the catalogue.

    Attributes:
        name: File name in the output folder.
        family: ``RecordingPart`` value the E2E case records it as; ``None`` for track files.
        role: ``input`` (fake capture device), ``track`` (imported) or ``silence``.
        render: Builds the audio (peak 1, or silence).
    """

    name: str
    family: str | None
    role: str
    render: Callable[[], Audio]


CATALOGUE: tuple[RecordingFixture, ...] = (
    RecordingFixture("input-vocals.wav", "vocals", "input", render_vocals),
    RecordingFixture("input-guitar.wav", "guitar", "input", render_guitar),
    RecordingFixture("input-bass.wav", "bass", "input", render_bass),
    RecordingFixture("input-piano.wav", "piano", "input", render_piano),
    RecordingFixture("input-drums.wav", "drums", "input", render_drums),
    RecordingFixture("input-other.wav", "other", "input", render_other),
    RecordingFixture("click-track.wav", None, "track", render_click_track),
    RecordingFixture("silent.wav", None, "silence", render_silent),
)


def to_pcm(audio: Audio) -> np.ndarray[Any, np.dtype[np.int16]]:
    """Scales peak-1 audio to ``PEAK`` and quantises it to PCM16."""
    return np.round(PEAK * audio * PCM_MAX).astype(np.int16)


def peak_dbfs(pcm: np.ndarray[Any, np.dtype[np.int16]]) -> float | None:
    """Peak of PCM16 samples in dBFS (2 decimals); None for digital silence."""
    peak = float(np.max(np.abs(pcm))) / PCM_MAX
    return round(20 * float(np.log10(peak)), 2) if peak > 0 else None


def write_fixture(fixture: RecordingFixture, out_dir: Path) -> dict[str, Any]:
    """Writes one file and returns its manifest entry (facts and SHA-256)."""
    pcm = to_pcm(fixture.render())
    target = out_dir / fixture.name
    sf.write(str(target), pcm, SAMPLE_RATE, subtype="PCM_16", format="WAV")
    entry: dict[str, Any] = {
        "family": fixture.family,
        "role": fixture.role,
        "channels": 1 if pcm.ndim == 1 else 2,
        "durationSeconds": SECONDS,
        "peakDbfs": peak_dbfs(pcm),
        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
    }
    if fixture.role == "track":
        entry["clickTimesSeconds"] = click_times()
    return entry


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Writes every catalogue file into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    manifest = {
        "version": MANIFEST_VERSION,
        "sampleRate": SAMPLE_RATE,
        "licence": LICENCE,
        "provenance": PROVENANCE,
        "fixtures": {f.name: write_fixture(f, out_dir) for f in CATALOGUE},
    }
    return write_manifest(manifest, manifest_path)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point (``--out-dir``, ``--manifest``); returns the process exit code."""
    parser = argparse.ArgumentParser(description="Generate the play-along recording fixtures.")
    parser.add_argument("--out-dir", type=Path, default=OUT_DIR)
    parser.add_argument("--manifest", type=Path, default=None)
    args = parser.parse_args(argv)
    manifest = generate(args.out_dir, args.manifest or args.out_dir / "manifest.json")
    count = len(manifest["fixtures"])
    sys.stdout.write(f"{count} recording fixtures in {args.out_dir}\n")
    return 0


if __name__ == "__main__":  # pragma: no cover
    sys.exit(main())
