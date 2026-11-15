"""Deterministic detail-separation fixtures with known gating outcomes (qa, feature 002, T010).

Run from the repository root with the worker environment, which provides numpy and soundfile::

    uv run --project worker python tests/fixtures/generate_detail_fixtures.py

The audio goes to ``tests/fixtures/audio/`` (git-ignored, next to the 001 fixtures) and the facts
the tests rely on to ``tests/fixtures/detail-manifest.json`` (committed): per file the level and
active-frame share measured as research.md D-04 defines them, and per parent the relative energy
and active share of every component plus the outcome the D-04 gates must produce (SC-002, SC-003).

Every sum fixture is the exact 16-bit sample sum of its component files, so "sub-stems plus rest
reproduce the parent" holds bit for bit. The band fixture is the 10 s input of the real-model
integration test and of the ``@detail`` E2E specs. Reruns are byte-identical (fixed seeds).
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import soundfile as sf  # type: ignore[import-untyped]

from generate_fixtures import FIXTURE_DIR, SAMPLE_RATE, Audio

Pcm = npt.NDArray[np.int16]
"""Stereo (frames, 2) 16-bit PCM."""

Renderer = Callable[[np.random.Generator], Audio]
"""Renders one mono component of ``DURATION_S`` seconds from a seeded generator."""

MANIFEST_VERSION = 1
DURATION_S = 10.0
FRAMES = round(DURATION_S * SAMPLE_RATE)
BPM = 120
BEAT_S = 60.0 / BPM
SEED = 20_261_115
GROUP_PEAK = 0.7
BAND_PEAK = 0.8
PCM_MAX = 32_767
SILENT_RMS = 1e-4  # -80 dBFS
RECONSTRUCTION_FLOOR_DB = -200.0  # recorded when the reconstruction is exact

# Gating constants of research.md D-04 (detail/gating.py, detail/checks.py).
FRAME = 2048
HOP = 512
PARENT_ACTIVE_DBFS = -40.0
PARENT_MIN_ACTIVE_SHARE = 0.08
SUB_MIN_REL_DB = -20.0
SUB_ACTIVE_FLOOR_DBFS = -50.0
SUB_ACTIVE_FLOOR_REL_DB = -25.0
SUB_MIN_ACTIVE_SHARE = 0.05
MIN_KEPT = 2
MAX_RECONSTRUCTION_DB = -30.0

LEAD_NOTES = (220.0, 246.94, 261.63, 293.66, 329.63, 293.66, 261.63, 246.94, 220.0, 196.0)
BACKING_NOTES = (0.0, 329.63, 0.0, 293.66, 0.0)  # 0 Hz = rest: backing sings every other bar
STRING_CHORDS = (196.0, 174.61, 261.63, 196.0, 220.0)
BASS_NOTES = (55.0, 43.65, 65.41, 49.0, 55.0)
SAW = (1.0, 0.5, 0.33, 0.25, 0.2, 0.17)
ODD = (1.0, 0.0, 0.33, 0.0, 0.2, 0.0, 0.14)


class DetailFixtureError(RuntimeError):
    """Raised when a fixture would violate its own invariants (for example it would clip)."""


# --- measurements (research.md D-04) -------------------------------------------------------


def mono(pcm: Pcm) -> Audio:
    """Mono mix-down (mean of the channels) of 16-bit PCM as floats in -1..1."""
    return np.asarray(pcm.astype(np.float64).mean(axis=1) / 32_768.0, dtype=np.float64)


def frame_levels_dbfs(signal: Audio) -> Audio:
    """RMS level in dBFS of every frame of ``FRAME`` samples, hop ``HOP`` (no padding)."""
    squares = np.concatenate(([0.0], np.cumsum(signal**2)))
    starts = np.arange(0, len(signal) - FRAME + 1, HOP)
    mean_square = (squares[starts + FRAME] - squares[starts]) / FRAME
    return np.asarray(10 * np.log10(np.maximum(mean_square, 1e-20)), dtype=np.float64)


def level_dbfs(signal: Audio) -> float:
    """Integrated level (mean power over the whole signal) in dBFS."""
    return float(10 * np.log10(max(float(np.mean(signal**2)), 1e-20)))


def active_share(signal: Audio, threshold_dbfs: float) -> float:
    """Share of frames whose level exceeds ``threshold_dbfs``."""
    return float(np.mean(frame_levels_dbfs(signal) > threshold_dbfs))


def rel_energy_db(sub: Audio, parent: Audio) -> float:
    """Energy of ``sub`` relative to ``parent`` in dB (-200 dB for a silent ``sub``)."""
    return float(10 * np.log10(max(float(np.sum(sub**2)) / float(np.sum(parent**2)), 1e-20)))


def sub_floor_dbfs(parent: Audio) -> float:
    """Active-frame floor of a sub-stem: max(-50 dBFS, parent level - 25 dB)."""
    return max(SUB_ACTIVE_FLOOR_DBFS, level_dbfs(parent) + SUB_ACTIVE_FLOOR_REL_DB)


def reconstruction_error_db(parent: Pcm, parts: Sequence[Pcm]) -> float:
    """Energy of ``parent - sum(parts)`` relative to the parent; the floor when exact."""
    residual = mono(parent) - np.sum([mono(p) for p in parts], axis=0)
    energy = float(np.sum(residual**2))
    if energy == 0.0:
        return RECONSTRUCTION_FLOOR_DB
    return float(10 * np.log10(energy / np.sum(mono(parent) ** 2)))


def _audible(sub: Audio, parent: Audio, floor_dbfs: float) -> bool:
    loud_enough = rel_energy_db(sub, parent) >= SUB_MIN_REL_DB
    return loud_enough and active_share(sub, floor_dbfs) >= SUB_MIN_ACTIVE_SHARE


def expected_outcome(parent: Pcm, components: Sequence[tuple[str, Pcm]]) -> tuple[str, list[str]]:
    """Outcome and detail stems the D-04 gates must produce when the model returns ``components``.

    Args:
        parent: The standard stem.
        components: Ideal model outputs (name, PCM), in display order.

    Returns:
        ``("skipped-silent", [])``, ``("nothing-further", [])`` or ``("split", [*kept, "rest"])``.
    """
    signal = mono(parent)
    if active_share(signal, PARENT_ACTIVE_DBFS) < PARENT_MIN_ACTIVE_SHARE:
        return "skipped-silent", []
    floor = sub_floor_dbfs(signal)
    kept = [name for name, pcm in components if _audible(mono(pcm), signal, floor)]
    if len(kept) < MIN_KEPT:
        return "nothing-further", []
    return "split", [*kept, "rest"]


# --- PCM helpers ---------------------------------------------------------------------------


def quantise(audio: Audio) -> Pcm:
    """Round float audio to 16-bit PCM; refuses audio that would clip."""
    if np.max(np.abs(audio)) >= 1.0:
        raise DetailFixtureError("fixture audio would clip at 16 bit")
    return np.round(audio * PCM_MAX).astype(np.int16)


def exact_sum(parts: Sequence[Pcm]) -> Pcm:
    """Sample-exact sum of 16-bit PCM signals; refuses a sum that would clip."""
    total = np.sum([p.astype(np.int32) for p in parts], axis=0)
    if np.max(np.abs(total)) > PCM_MAX:
        raise DetailFixtureError("sum of the components would clip at 16 bit")
    return total.astype(np.int16)


def stereo(signal: Audio, left: float = 1.0, right: float = 1.0) -> Audio:
    """Place a mono signal in the stereo field with per-channel gains."""
    return np.column_stack((left * signal, right * signal))


# --- synthesis -----------------------------------------------------------------------------


def _time(frames: int) -> Audio:
    return np.arange(frames, dtype=np.float64) / SAMPLE_RATE


def _decay(seconds: float, tau_s: float) -> Audio:
    return np.exp(-_time(round(seconds * SAMPLE_RATE)) / tau_s)


def _fade(frames: int) -> Audio:
    t = _time(frames)
    return np.minimum(1.0, np.minimum(t, t[::-1]) / 0.02)


def _partials(phase: Audio, weights: Sequence[float]) -> Audio:
    return np.asarray(sum(w * np.sin((k + 1) * phase) for k, w in enumerate(weights)))


def _hits(sound: Audio, period_s: float, offset_s: float) -> Audio:
    """``sound`` repeated every ``period_s`` from ``offset_s`` (cut at the clip end)."""
    out = np.zeros(FRAMES)
    for start_s in np.arange(offset_s, DURATION_S, period_s):
        begin = round(float(start_s) * SAMPLE_RATE)
        end = min(begin + len(sound), FRAMES)
        out[begin:end] += sound[: end - begin]
    return out


def _sequence(
    notes: Sequence[float], note_s: float, voice: Callable[[float, Audio], Audio]
) -> Audio:
    """One note per ``note_s`` cycling through ``notes``; ``voice(freq, t)`` renders a note."""
    out = np.zeros(FRAMES)
    for index, start_s in enumerate(np.arange(0.0, DURATION_S, note_s)):
        begin = round(float(start_s) * SAMPLE_RATE)
        frames = min(round(note_s * SAMPLE_RATE), FRAMES - begin)
        out[begin : begin + frames] = voice(notes[index % len(notes)], _time(frames)) * _fade(
            frames
        )
    return out


def kick(_rng: np.random.Generator) -> Audio:
    """Pitch-swept sine kick on beats 1 and 3."""
    envelope = _decay(0.15, 0.035)
    sweep = np.cumsum(2 * np.pi * np.linspace(110.0, 45.0, len(envelope)) / SAMPLE_RATE)
    return _hits(0.9 * np.sin(sweep) * envelope, 2 * BEAT_S, 0.0)


def snare(rng: np.random.Generator) -> Audio:
    """Noise plus a 185 Hz body on beats 2 and 4."""
    envelope = _decay(0.18, 0.045)
    body = 0.3 * np.sin(2 * np.pi * 185.0 * _time(len(envelope)))
    return _hits((0.5 * rng.standard_normal(len(envelope)) + body) * envelope, 2 * BEAT_S, BEAT_S)


def hihat(rng: np.random.Generator) -> Audio:
    """Short high-passed noise on every eighth note."""
    envelope = _decay(0.05, 0.008)
    noise = np.diff(rng.standard_normal(len(envelope) + 1))
    return _hits(0.35 * noise * envelope, BEAT_S / 2, 0.0)


def _lead_voice(freq: float, t: Audio) -> Audio:
    vibrato = 0.3 * np.sin(2 * np.pi * 5.0 * t)
    return 0.3 * _partials(2 * np.pi * freq * t + vibrato, (1.0, 0.5, 0.3, 0.2))


def lead(_rng: np.random.Generator) -> Audio:
    """A sung melody (one note per two beats) with vibrato."""
    return _sequence(LEAD_NOTES, 2 * BEAT_S, _lead_voice)


def _backing_voice(freq: float, t: Audio) -> Audio:
    phase = 2 * np.pi * freq * t
    return 0.15 * (_partials(phase, (1.0, 0.3)) + _partials(1.5 * phase, (1.0, 0.3)))


def backing(_rng: np.random.Generator) -> Audio:
    """Two-voice "ooh" harmony (root and fifth) in every other bar."""
    return _sequence(BACKING_NOTES, 4 * BEAT_S, _backing_voice)


def _string_voice(freq: float, t: Audio) -> Audio:
    triad = sum(_partials(2 * np.pi * ratio * freq * t, SAW) for ratio in (1.0, 1.25, 1.5))
    return np.asarray(0.08 * np.minimum(1.0, t / 0.3) * triad, dtype=np.float64)


def strings(_rng: np.random.Generator) -> Audio:
    """Sawtooth-like triads per bar with a slow bowed attack."""
    return _sequence(STRING_CHORDS, 4 * BEAT_S, _string_voice)


def brass(_rng: np.random.Generator) -> Audio:
    """Odd-harmonic stabs on the "and" of every second beat."""
    t = _time(round(0.25 * SAMPLE_RATE))
    stab = 0.25 * _partials(2 * np.pi * 349.23 * t, ODD) * np.exp(-t / 0.12) * _fade(len(t))
    return _hits(stab, 2 * BEAT_S, BEAT_S / 2)


def _bass_voice(freq: float, t: Audio) -> Audio:
    return 0.4 * _partials(2 * np.pi * freq * t, (1.0, 0.3))


def bass(_rng: np.random.Generator) -> Audio:
    """Root notes per bar (band fixture only)."""
    return _sequence(BASS_NOTES, 4 * BEAT_S, _bass_voice)


def _noise(rng: np.random.Generator) -> Audio:
    noise = rng.standard_normal(FRAMES)
    return np.asarray(SILENT_RMS * noise / np.sqrt(np.mean(noise**2)), dtype=np.float64)


def bleed(rng: np.random.Generator) -> Audio:
    """-80 dBFS noise with three short 440 Hz bursts: about 4 % active frames (bleed)."""
    t = _time(round(0.1 * SAMPLE_RATE))
    burst = 0.3 * np.sin(2 * np.pi * 440.0 * t) * _fade(len(t))
    return _noise(rng) + _hits(burst, 3.0, 2.0)


# --- catalogue -----------------------------------------------------------------------------


@dataclass(frozen=True)
class Component:
    """One ideal detail-model output.

    Attributes:
        name: Detail stem name (worker contract ``DetailStemName``).
        render: Mono renderer.
        pan: Left and right gain.
    """

    name: str
    render: Renderer
    pan: tuple[float, float] = (1.0, 1.0)


@dataclass(frozen=True)
class Group:
    """A parent stem made of components; files ``detail_<key>_<name>.wav`` and ``_sum.wav``.

    Attributes:
        key: File name part (``drums``, ``vocals``, ``instruments``).
        parent: Standard stem the sum stands for (worker contract ``ParentStemName``).
        components: Components in display order.
    """

    key: str
    parent: str
    components: tuple[Component, ...] = field(default=())

    @property
    def sum_name(self) -> str:
        """File name of the parent (sum) fixture."""
        return f"detail_{self.key}_sum.wav"

    def file_name(self, component: Component) -> str:
        """File name of one component fixture."""
        return f"detail_{self.key}_{component.name}.wav"


GROUPS: tuple[Group, ...] = (
    Group(
        "drums",
        "drums",
        (Component("kick", kick), Component("snare", snare), Component("hihat", hihat)),
    ),
    Group("vocals", "vocals", (Component("lead", lead), Component("backing", backing, (1.0, 0.4)))),
    Group("instruments", "other", (Component("strings", strings), Component("brass", brass))),
)
BAND_PARENTS = ("vocals", "drums", "bass", "other")


@dataclass(frozen=True)
class Rendered:
    """A fixture file and its manifest facts."""

    name: str
    pcm: Pcm
    facts: dict[str, Any]


def _rng(salt: int) -> np.random.Generator:
    return np.random.default_rng(SEED + salt)


def _basic_facts(kind: str, pcm: Pcm) -> dict[str, Any]:
    signal = mono(pcm)
    return {
        "kind": kind,
        "durationSeconds": len(pcm) / SAMPLE_RATE,
        "sampleRate": SAMPLE_RATE,
        "channels": 2,
        "levelDbfs": round(level_dbfs(signal), 2),
        "activeShare": round(active_share(signal, PARENT_ACTIVE_DBFS), 4),
    }


def _component_facts(parent: Audio, name: str, file: str, pcm: Pcm) -> dict[str, Any]:
    sub = mono(pcm)
    return {
        "name": name,
        "file": file,
        "relEnergyDb": round(rel_energy_db(sub, parent), 2),
        "activeShare": round(active_share(sub, sub_floor_dbfs(parent)), 4),
    }


def _reconstruction_fact(pcm: Pcm, parts: Sequence[Pcm]) -> float | None:
    """Rounded reconstruction error, or None for a parent without components."""
    if not parts:
        return None
    return round(reconstruction_error_db(pcm, parts), 2)


def parent_facts(
    parent: str, pcm: Pcm, components: Sequence[tuple[str, str, Pcm]]
) -> dict[str, Any]:
    """Manifest facts of a parent: gating metrics, components and the expected outcome.

    Args:
        parent: Standard stem name the fixture stands for.
        pcm: The parent audio.
        components: (detail name, file name, PCM) of its ideal model outputs.
    """
    outcome, stems = expected_outcome(pcm, [(name, part) for name, _, part in components])
    signal = mono(pcm)
    return {
        **_basic_facts("parent", pcm),
        "parent": parent,
        "expectedOutcome": outcome,
        "expectedStems": stems,
        "components": [_component_facts(signal, *c) for c in components],
        "reconstructionErrorDb": _reconstruction_fact(pcm, [part for *_, part in components]),
    }


def _group_floats(group: Group, salt: int) -> list[Audio]:
    return [stereo(c.render(_rng(salt + i)), *c.pan) for i, c in enumerate(group.components)]


def render_group(group: Group, salt: int) -> list[Rendered]:
    """Component files scaled to a common peak, then their exact sum as the parent file."""
    floats = _group_floats(group, salt)
    scale = GROUP_PEAK / float(np.max(np.abs(np.sum(floats, axis=0))))
    parts = [(c, quantise(scale * f)) for c, f in zip(group.components, floats, strict=True)]
    named = [(c.name, group.file_name(c), pcm) for c, pcm in parts]
    files = [
        Rendered(
            file, pcm, {**_basic_facts("component", pcm), "parent": group.parent, "name": name}
        )
        for name, file, pcm in named
    ]
    total = exact_sum([pcm for *_, pcm in named])
    return [*files, Rendered(group.sum_name, total, parent_facts(group.parent, total, named))]


def render_band() -> Rendered:
    """The 10 s band: every group plus a bass line, peak-normalised to ``BAND_PEAK``."""
    floats = [f for i, g in enumerate(GROUPS) for f in _group_floats(g, 10 * i)]
    mix = np.sum([*floats, stereo(bass(_rng(99)))], axis=0)
    pcm = quantise(BAND_PEAK * mix / np.max(np.abs(mix)))
    facts = {**_basic_facts("band", pcm), "bpm": BPM, "parents": list(BAND_PARENTS)}
    return Rendered("detail_10s_band.wav", pcm, facts)


def render_silent_parents() -> list[Rendered]:
    """A silent parent (-80 dBFS noise) and a bleed-only parent (about 4 % active frames)."""
    silent = quantise(stereo(_noise(_rng(50))))
    leaky = quantise(stereo(bleed(_rng(60))))
    return [
        Rendered("detail_silent_parent.wav", silent, parent_facts("piano", silent, [])),
        Rendered("detail_bleed_parent.wav", leaky, parent_facts("other", leaky, [])),
    ]


def render_all() -> list[Rendered]:
    """Every detail fixture in catalogue order."""
    groups = [r for i, g in enumerate(GROUPS) for r in render_group(g, 10 * i)]
    return [*render_silent_parents(), *groups, render_band()]


def gating_constants() -> dict[str, float | int]:
    """The D-04 constants the expected outcomes were derived with (camelCase)."""
    return {
        "frame": FRAME,
        "hop": HOP,
        "parentActiveDbfs": PARENT_ACTIVE_DBFS,
        "parentMinActiveShare": PARENT_MIN_ACTIVE_SHARE,
        "subMinRelDb": SUB_MIN_REL_DB,
        "subActiveFloorDbfs": SUB_ACTIVE_FLOOR_DBFS,
        "subActiveFloorRelDb": SUB_ACTIVE_FLOOR_REL_DB,
        "subMinActiveShare": SUB_MIN_ACTIVE_SHARE,
        "minKept": MIN_KEPT,
        "maxReconstructionDb": MAX_RECONSTRUCTION_DB,
    }


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write every detail fixture into ``out_dir`` and the manifest to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    rendered = render_all()
    for item in rendered:
        sf.write(str(out_dir / item.name), item.pcm, SAMPLE_RATE, subtype="PCM_16", format="WAV")
    manifest = {
        "version": MANIFEST_VERSION,
        "sampleRate": SAMPLE_RATE,
        "gating": gating_constants(),
        "fixtures": {item.name: item.facts for item in rendered},
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    return manifest


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Command line: ``--out-dir`` (default ``audio/``) and ``--manifest`` (default next to it)."""
    parser = argparse.ArgumentParser(description="Generate the detail-separation fixtures.")
    parser.add_argument("--out-dir", type=Path, default=FIXTURE_DIR / "audio")
    parser.add_argument("--manifest", type=Path, default=FIXTURE_DIR / "detail-manifest.json")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Entry point; returns the process exit code."""
    args = parse_args(argv)
    count = len(generate(args.out_dir, args.manifest)["fixtures"])
    sys.stdout.write(f"wrote {count} detail fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
