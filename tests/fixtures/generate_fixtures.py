"""Deterministic audio fixtures with a known tempo for the StemStudio E2E suite (qa, T021).

Run from the repository root with the worker environment, which provides numpy and soundfile::

    uv run --project worker python tests/fixtures/generate_fixtures.py

The audio goes to ``tests/fixtures/audio/`` (git-ignored) and the facts the tests rely on (tempo,
beats per bar, beat and downbeat times, duration) to ``tests/fixtures/manifest.json`` (committed).
Every run produces byte-identical files on the same machine: synthesis uses fixed seeds, and
ffmpeg encodes the MP3 with ``+bitexact`` and without metadata.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import soundfile as sf  # type: ignore[import-untyped]

Audio = npt.NDArray[np.float64]
"""Mono (frames,) or stereo (frames, 2) float audio in -1..1."""

SAMPLE_RATE = 44_100
FIXTURE_DIR = Path(__file__).resolve().parent
MANIFEST_VERSION = 1
CLICK_SECONDS = 0.06
FIRST_BEAT_SECONDS = 0.5
MUSIC_SEED = 20_261_114
FFMPEG_CODEC_ARGS: tuple[str, ...] = ("-c:a", "libmp3lame", "-b:a", "192k")
# Bass root plus a triad per bar (Hz): Am - F - C - G.
PROGRESSION: tuple[tuple[float, ...], ...] = (
    (55.00, 220.00, 261.63, 329.63),
    (43.65, 174.61, 220.00, 261.63),
    (65.41, 261.63, 329.63, 392.00),
    (49.00, 196.00, 246.94, 293.66),
)


class FixtureGenerationError(RuntimeError):
    """Raised when a fixture cannot be produced (for example ffmpeg fails)."""


class FfmpegNotFoundError(FixtureGenerationError):
    """Raised when ffmpeg, needed for the MP3 fixture, is not on ``PATH``."""


@dataclass(frozen=True)
class Rhythm:
    """A constant-tempo pulse.

    Attributes:
        bpm: Tempo in beats per minute.
        beats_per_bar: Beats per bar; the first beat of each bar is the downbeat.
        duration_s: Clip length in seconds.
        first_beat_s: Time of the first beat (a downbeat) in seconds.
    """

    bpm: int
    beats_per_bar: int
    duration_s: float
    first_beat_s: float = FIRST_BEAT_SECONDS

    @property
    def period_s(self) -> float:
        """Seconds between two beats."""
        return 60.0 / self.bpm


@dataclass(frozen=True)
class Fixture:
    """One catalogue entry.

    Attributes:
        name: File name inside the output directory; the suffix selects the encoder.
        kind: ``click``, ``music``, ``silence``, ``corrupt`` or ``unsupported``.
        duration_s: Length in seconds, or None for files that are not decodable audio.
        rhythm: The pulse of ``click`` and ``music`` fixtures, None otherwise.
    """

    name: str
    kind: str
    duration_s: float | None
    rhythm: Rhythm | None = None


CATALOGUE: tuple[Fixture, ...] = (
    Fixture("click_120bpm_4-4_12s.wav", "click", 12.0, Rhythm(120, 4, 12.0)),
    Fixture("click_90bpm_3-4_12s.wav", "click", 12.0, Rhythm(90, 3, 12.0)),
    Fixture("music_100bpm_20s.mp3", "music", 20.0, Rhythm(100, 4, 20.0)),
    Fixture("music_100bpm_20s.flac", "music", 20.0, Rhythm(100, 4, 20.0)),
    Fixture("upload_4mb.wav", "music", 24.0, Rhythm(110, 4, 24.0)),
    Fixture("silence_5s.wav", "silence", 5.0),
    Fixture("corrupt.mp3", "corrupt", None),
    Fixture("unsupported.ogg", "unsupported", None),
)


def beat_times(rhythm: Rhythm) -> list[float]:
    """Return the beat times (seconds, 6 decimals) whose click fits completely in the clip."""
    last_start = rhythm.duration_s - CLICK_SECONDS
    count = int(np.floor((last_start - rhythm.first_beat_s) / rhythm.period_s + 1e-9)) + 1
    return [round(rhythm.first_beat_s + k * rhythm.period_s, 6) for k in range(count)]


def _frames(seconds: float) -> int:
    return round(seconds * SAMPLE_RATE)


def _place(target: Audio, at_s: float, sound: Audio) -> None:
    """Mix ``sound`` into ``target`` starting at the sample nearest to ``at_s``."""
    start = _frames(at_s)
    end = min(start + len(sound), len(target))
    target[start:end] += sound[: end - start]


def _stereo(mono: Audio) -> Audio:
    return np.column_stack((mono, mono))


def click(frequency: float, amplitude: float) -> Audio:
    """A decaying cosine burst whose first sample is its loudest (sample-exact onset)."""
    n = np.arange(_frames(CLICK_SECONDS), dtype=np.float64)
    envelope = np.exp(-n / (0.012 * SAMPLE_RATE))
    return amplitude * np.cos(2 * np.pi * frequency * n / SAMPLE_RATE) * envelope


def render_clicks(rhythm: Rhythm) -> Audio:
    """Clicks on every beat: accent (1.5 kHz, 0.9) on downbeats, normal (1 kHz, 0.6) otherwise."""
    out = np.zeros(_frames(rhythm.duration_s))
    accent, normal = click(1500.0, 0.9), click(1000.0, 0.6)
    for index, time in enumerate(beat_times(rhythm)):
        _place(out, time, accent if index % rhythm.beats_per_bar == 0 else normal)
    return _stereo(out)


def _chord(frequencies: Sequence[float], length: int) -> Audio:
    """Bass (first frequency, louder) plus pad tones with a short fade in and out."""
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    gains = (0.30, *([0.07] * (len(frequencies) - 1)))
    tone = sum(g * np.sin(2 * np.pi * f * t) for g, f in zip(gains, frequencies, strict=True))
    fade = np.minimum(1.0, np.minimum(t, t[::-1]) / 0.02)
    return np.asarray(tone * fade, dtype=np.float64)


def _harmony(rhythm: Rhythm) -> Audio:
    out = np.zeros(_frames(rhythm.duration_s))
    bar_s = rhythm.period_s * rhythm.beats_per_bar
    for index, start in enumerate(np.arange(0.0, rhythm.duration_s, bar_s)):
        length = min(_frames(bar_s), len(out) - _frames(float(start)))
        _place(out, float(start), _chord(PROGRESSION[index % len(PROGRESSION)], length))
    return out


def _kick() -> Audio:
    n = np.arange(_frames(0.12), dtype=np.float64)
    sweep = np.cumsum(2 * np.pi * np.linspace(120.0, 45.0, len(n)) / SAMPLE_RATE)
    return 0.8 * np.sin(sweep) * np.exp(-n / (0.03 * SAMPLE_RATE))


def _hat(rng: np.random.Generator) -> Audio:
    n = np.arange(_frames(0.03), dtype=np.float64)
    return 0.15 * rng.uniform(-1.0, 1.0, len(n)) * np.exp(-n / (0.006 * SAMPLE_RATE))


def _drums(rhythm: Rhythm, seed: int) -> Audio:
    out = np.zeros(_frames(rhythm.duration_s))
    rng, kick, accent = np.random.default_rng(seed), _kick(), click(1500.0, 0.3)
    for index, time in enumerate(beat_times(rhythm)):
        _place(out, time, kick)
        _place(out, time + rhythm.period_s / 2, _hat(rng))
        if index % rhythm.beats_per_bar == 0:
            _place(out, time, accent)
    return out


def render_music(rhythm: Rhythm, seed: int = MUSIC_SEED) -> Audio:
    """Bass and chord tones plus a kick/hat/click pattern on the beat, peak-normalised to 0.8."""
    mix = _harmony(rhythm) + _drums(rhythm, seed)
    return _stereo(0.8 * mix / np.max(np.abs(mix)))


def _require_rhythm(fixture: Fixture) -> Rhythm:
    if fixture.rhythm is None:
        raise FixtureGenerationError(f"{fixture.name}: a {fixture.kind} fixture needs a rhythm")
    return fixture.rhythm


def _silence(fixture: Fixture) -> Audio:
    return _stereo(np.zeros(_frames(fixture.duration_s or 0.0)))


def _corrupt_mp3(_fixture: Fixture) -> bytes:
    """An empty ID3v2 tag followed by bytes without any MPEG frame sync (no 0xFF byte)."""
    noise = np.random.default_rng(7).integers(0, 255, size=64 * 1024, dtype=np.uint8)
    return b"ID3\x04\x00\x00\x00\x00\x00\x00" + noise.tobytes()


def _unsupported_ogg(_fixture: Fixture) -> bytes:
    """An Ogg page signature with an invalid payload; rejected by its extension."""
    noise = np.random.default_rng(11).integers(0, 255, size=4 * 1024, dtype=np.uint8)
    return b"OggS\x00\x02" + noise.tobytes()


RENDERERS: dict[str, Callable[[Fixture], Audio | bytes]] = {
    "click": lambda f: render_clicks(_require_rhythm(f)),
    "music": lambda f: render_music(_require_rhythm(f)),
    "silence": _silence,
    "corrupt": _corrupt_mp3,
    "unsupported": _unsupported_ogg,
}


def _ffmpeg_args(ffmpeg: str, target: Path) -> list[str]:
    raw_input = ["-f", "s16le", "-ar", str(SAMPLE_RATE), "-ac", "2", "-i", "pipe:0"]
    exact = ["-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact"]
    return [ffmpeg, "-hide_banner", "-loglevel", "error", "-y", *raw_input, *exact,
            *FFMPEG_CODEC_ARGS, str(target)]  # fmt: skip


def encode_mp3(audio: Audio, target: Path) -> None:
    """Encode stereo float audio to MP3 with ffmpeg (argument list, never a shell)."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise FfmpegNotFoundError("ffmpeg is required for the MP3 fixture but is not on PATH")
    pcm = np.round(np.clip(audio, -1.0, 1.0) * 32767).astype("<i2").tobytes()
    result = subprocess.run(  # noqa: S603 - fixed argument list, resolved executable, no shell
        _ffmpeg_args(ffmpeg, target), input=pcm, capture_output=True, check=False
    )
    if result.returncode != 0:
        reason = result.stderr.decode(errors="replace").strip()
        raise FixtureGenerationError(f"ffmpeg failed ({result.returncode}): {reason}")


def _write_wav(audio: Audio, target: Path) -> None:
    sf.write(str(target), audio, SAMPLE_RATE, subtype="PCM_16", format="WAV")


def _write_flac(audio: Audio, target: Path) -> None:
    sf.write(str(target), audio, SAMPLE_RATE, subtype="PCM_16", format="FLAC")


WRITERS: dict[str, Callable[[Audio, Path], None]] = {
    ".wav": _write_wav,
    ".flac": _write_flac,
    ".mp3": encode_mp3,
}


def write_fixture(fixture: Fixture, out_dir: Path) -> Path:
    """Render one fixture and write it into ``out_dir``; returns the file path."""
    content = RENDERERS[fixture.kind](fixture)
    target = out_dir / fixture.name
    if isinstance(content, bytes):
        target.write_bytes(content)
    else:
        WRITERS[target.suffix](content, target)
    return target


def _rhythm_facts(rhythm: Rhythm | None) -> dict[str, Any]:
    if rhythm is None:
        return {"bpm": None, "beatsPerBar": None, "beatTimes": [], "downbeatTimes": []}
    times = beat_times(rhythm)
    return {
        "bpm": rhythm.bpm,
        "beatsPerBar": rhythm.beats_per_bar,
        "beatTimes": times,
        "downbeatTimes": times[:: rhythm.beats_per_bar],
    }


def manifest_entry(fixture: Fixture) -> dict[str, Any]:
    """The manifest facts of one fixture (camelCase, as read by the TypeScript E2E suite)."""
    audio = fixture.duration_s is not None
    return {
        "kind": fixture.kind,
        "format": Path(fixture.name).suffix.lstrip("."),
        "durationSeconds": fixture.duration_s,
        "sampleRate": SAMPLE_RATE if audio else None,
        "channels": 2 if audio else None,
        **_rhythm_facts(fixture.rhythm),
    }


def build_manifest() -> dict[str, Any]:
    """The full manifest: version, sample rate and one entry per catalogue file."""
    fixtures = {f.name: manifest_entry(f) for f in CATALOGUE}
    return {"version": MANIFEST_VERSION, "sampleRate": SAMPLE_RATE, "fixtures": fixtures}


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write every catalogue fixture into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    for fixture in CATALOGUE:
        write_fixture(fixture, out_dir)
    manifest = build_manifest()
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(manifest, indent=2) + "\n"
    manifest_path.write_text(text, encoding="utf-8", newline="\n")
    return manifest


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse ``--out-dir`` and ``--manifest`` (defaults: next to this script)."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0] if __doc__ else None)
    parser.add_argument("--out-dir", type=Path, default=FIXTURE_DIR / "audio")
    parser.add_argument("--manifest", type=Path, default=FIXTURE_DIR / "manifest.json")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the process exit code."""
    args = parse_args(argv)
    manifest = generate(args.out_dir, args.manifest)
    sys.stdout.write(f"wrote {len(manifest['fixtures'])} fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
