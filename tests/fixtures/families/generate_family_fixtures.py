"""Per-family handout fixtures (qa, feature 020, T028/T029, SC-006).

Run from the repository root with the worker environment (numpy, soundfile)::

    uv run --project worker python tests/fixtures/families/generate_family_fixtures.py [--check]

The fixture song is 014's ``practice-song.wav`` (``tests/fixtures/practice-flow/``: 120 BPM, 4/4,
six sections). On the fake stack it gets sections (014 fake analyser), chords (016 fake engine),
word-timed lyrics (006 fake LRCLIB ``word``), a detail split (002 fake) and, through the seeding
helper ``e2e/support/handout-seed.ts``, the notation items and practice notes this plan lists per
family. ``manifest.json`` (committed) is that plan: per family the score requests, the practice
note and the ingredients a one-click handout must include (data-model 6.2 defaults), the fake
analyzer's ratings (data-model 2.6) and a three-song setlist.

Two short setlist songs are written next to this script (git-ignored, 44.1 kHz stereo PCM16):
``setlist-b.wav`` (100 BPM, 4/4) and ``setlist-c.wav`` (90 BPM, 3/4), 24 s each: a chord pad and
an accented click on every beat. ``--check`` writes nothing and exits 1 when the committed
manifest differs from the plan. Fixed seeds: reruns are byte-identical; the music is qa's own work,
dedicated to the public domain (CC0 1.0).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections.abc import Sequence
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import soundfile as sf  # type: ignore[import-untyped]

HERE = Path(__file__).resolve().parent
SAMPLE_RATE = 44_100
LICENCE = "CC0-1.0"
PEAK = 0.7
SECONDS = 24.0
LEAD_IN_S = 0.5
SONG_SECTIONS = ("Intro", "Verse", "Chorus 1", "Guitar solo", "Chorus 2", "Outro")
Audio = npt.NDArray[np.float64]

_CORE = ["facts", "form-chart"]
_LYRICS = [*_CORE, "chords-over-lyrics"]
_FULL = [*_LYRICS, "chord-chart", "notation", "practice-notes"]
_TAB = {"guitar": "guitar-standard", "bass": "bass-4-standard"}
FAMILIES: dict[str, dict[str, Any]] = {
    "overview": {"label": "Band overview", "scores": [],
                 "expectedIncluded": [*_LYRICS, "chord-chart", "practice-notes"]},
    "vocals": {"label": "Vocals", "scores": [{"channel": "vocals", "profile": "vocals"}],
               "expectedIncluded": [*_LYRICS, "notation", "practice-notes"]},
    "guitar": {"label": "Guitar", "scores": [
        {"channel": "guitar", "profile": "guitar"},
        {"channel": "guitar", "profile": "guitar", "tablature": {"tuning": _TAB["guitar"]}}],
        "expectedIncluded": _FULL},
    "bass": {"label": "Bass", "scores": [
        {"channel": "bass", "profile": "bass"},
        {"channel": "bass", "profile": "bass", "tablature": {"tuning": _TAB["bass"]}}],
        "expectedIncluded": [*_CORE, "chord-chart", "notation", "practice-notes"]},
    "keys": {"label": "Keys", "scores": [{"channel": "piano", "profile": "piano"}],
             "expectedIncluded": _FULL},
    "drums": {"label": "Drums", "scores": [{"channel": "drums", "profile": "drums"}],
              "expectedIncluded": [*_CORE, "notation", "practice-notes"]},
    "melodic": {"label": "Melodic instruments", "transposition": "b-flat", "needsDetail": True,
                "transpositionNote": "Part in B-flat (sounds a major second lower)",
                "scores": [{"channel": "other.woodwinds", "profile": "woodwinds",
                            "part": {"instrument": "clarinet-bb", "clef": "auto"}}],
                "expectedIncluded": [*_CORE, "chord-chart", "notation", "practice-notes"]},
}  # fmt: skip
"""The seeding plan per family (data-model 6.1/6.2, 016 score profiles and tunings)."""

FAKE_QUALITY: dict[str, Any] = {
    "analyzerVersion": "fake-1",
    "stems": {
        "vocals": {"rating": "fair", "bleedSource": "drums", "artefactKinds": []},
        "drums": {"rating": "good", "bleedSource": None, "artefactKinds": []},
        "bass": {"rating": "good", "bleedSource": None, "artefactKinds": []},
        "guitar": {"rating": "poor", "bleedSource": None, "artefactKinds": ["flicker"]},
        "piano": {"rating": "not-present", "bleedSource": None, "artefactKinds": []},
        "other": {"rating": "fair", "bleedSource": "guitar", "artefactKinds": []},
    },
    "detail": {"rating": "good", "rest": "fair", "restKind": "rest-share"},
}  # fmt: skip
"""The fake analyzer of the E2E stack (data-model 2.6), as the specs expect it."""

SONGS: tuple[tuple[str, int, int, tuple[float, ...]], ...] = (
    ("setlist-b.wav", 100, 4, (130.81, 174.61, 196.0, 130.81)),
    ("setlist-c.wav", 90, 3, (146.83, 110.0, 164.81)),
)
"""Setlist songs: file, BPM, beats per bar, chord roots (one per bar, repeated)."""


def _time(seconds: float) -> Audio:
    return np.arange(round(seconds * SAMPLE_RATE)) / SAMPLE_RATE


def _pad(root: float, seconds: float) -> Audio:
    t = _time(seconds)
    wave = sum(np.sin(2 * np.pi * root * ratio * t) for ratio in (1.0, 1.26, 1.5))
    fade = np.minimum(1.0, np.minimum(t, t[::-1]) / 0.02)
    return np.asarray(0.08 * wave * fade)


def _click(accent: bool) -> Audio:
    t = _time(0.05)
    freq = 1500.0 if accent else 1000.0
    return np.asarray((0.9 if accent else 0.5) * np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.01))


def render_song(bpm: int, beats: int, roots: Sequence[float]) -> Audio:
    """A chord pad (one chord per bar) and a click on every beat, accented on the downbeat."""
    beat_s = 60 / bpm
    audio = np.zeros(round(SECONDS * SAMPLE_RATE))
    count = int((SECONDS - LEAD_IN_S - 0.5) / beat_s)
    for k in range(count):
        start = round((LEAD_IN_S + k * beat_s) * SAMPLE_RATE)
        click = _click(k % beats == 0)
        audio[start : start + len(click)] += click
        if k % beats == 0:
            pad = _pad(roots[(k // beats) % len(roots)], beat_s * beats)[: len(audio) - start]
            audio[start : start + len(pad)] += pad
    return np.asarray(audio * (PEAK / float(np.max(np.abs(audio)))))


def _write(path: Path, audio: Audio) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(np.clip(audio, -1, 1) * 32_767) / 32_767
    sf.write(path, np.column_stack([pcm, pcm]), SAMPLE_RATE, subtype="PCM_16", format="WAV")
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _families() -> dict[str, dict[str, Any]]:
    """Every family with the fields the seeding helper reads, defaults filled in."""
    plans = {}
    for index, (family, plan) in enumerate(FAMILIES.items()):
        note = f"{plan['label']}: practise the chorus at 80 % first (note {index + 1})."
        plans[family] = {"transposition": None, "needsDetail": False, "transpositionNote": None,
                         **plan, "practiceNote": note}  # fmt: skip
    return plans


def _setlist(hashes: dict[str, str]) -> dict[str, Any]:
    entries: list[dict[str, Any]] = [
        {"file": "practice-song.wav", "folder": "practice-flow", "note": "Open the set"},
    ]
    for (file, bpm, beats, _), note in zip(SONGS, ("Capo 2", ""), strict=True):
        entries.append({"file": file, "folder": "families", "note": note, "bpm": bpm,
                        "beatsPerBar": beats, "seconds": SECONDS, "firstDownbeatSeconds": LEAD_IN_S,
                        "sha256": hashes.get(file, "")})  # fmt: skip
    return {"name": "E2E 020 setlist", "note": "Three songs for the setlist handout.",
            "entries": entries}  # fmt: skip


def build_manifest(hashes: dict[str, str]) -> dict[str, Any]:
    """The plan (committed as ``manifest.json``); ``hashes`` are the SHA-256 of the songs."""
    return {
        "manifestVersion": 1, "licence": LICENCE, "sampleRate": SAMPLE_RATE,
        "song": {"file": "practice-song.wav", "folder": "practice-flow", "bpm": 120,
                 "beatsPerBar": 4, "sections": list(SONG_SECTIONS)},
        "families": _families(), "fakeQuality": FAKE_QUALITY, "setlist": _setlist(hashes),
    }  # fmt: skip


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write the setlist songs below ``out_dir`` and the manifest; return the manifest."""
    hashes = {file: _write(out_dir / file, render_song(bpm, beats, roots))
              for file, bpm, beats, roots in SONGS}  # fmt: skip
    manifest = build_manifest(hashes)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    return manifest


def check(manifest_path: Path) -> int:
    """0 when the committed manifest is the plan of this script (audio rendered in memory)."""
    hashes = {}
    for file, bpm, beats, roots in SONGS:
        scratch = manifest_path.parent / f".check-{file}"
        hashes[file] = _write(scratch, render_song(bpm, beats, roots))
        scratch.unlink()
    committed = json.loads(manifest_path.read_text(encoding="utf-8"))
    return 0 if committed == build_manifest(hashes) else 1


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point: ``--out-dir``, ``--manifest`` (defaults next to the script)."""
    parser = argparse.ArgumentParser(description=(__doc__ or "").splitlines()[0])
    parser.add_argument("--out-dir", type=Path, default=HERE)
    parser.add_argument("--manifest", type=Path, default=HERE / "manifest.json")
    parser.add_argument("--check", action="store_true", help="compare the manifest, write nothing")
    args = parser.parse_args(argv)
    if args.check:
        return check(args.manifest)
    generate(args.out_dir, args.manifest)
    return 0


if __name__ == "__main__":
    sys.exit(main())
