"""Tests of the practice-flow fixture (014:T018, FR-033): format, manifest, grid and sections."""

from __future__ import annotations

import hashlib
import importlib.util
import itertools
import json
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

import numpy as np
import pytest
import soundfile as sf  # type: ignore[import-untyped]
from stemstudio_worker.analysis import LibrosaBeatAnalyzer
from stemstudio_worker.analysis.structure import StemsNoveltyAnalyzer
from stemstudio_worker.schemas import BeatGrid, SectionJobRequest

FOLDER = Path(__file__).resolve().parents[1] / "practice-flow"


def _load() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "generate_practice_flow_fixture", FOLDER / "generate_practice_flow_fixture.py"
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module  # dataclasses look their module up while it executes
    spec.loader.exec_module(module)
    return module


GENERATOR = _load()
_TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"


def test_the_committed_manifest_is_the_generated_one(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    _, manifest = generated

    assert json.loads((FOLDER / "manifest.json").read_text(encoding="utf-8")) == manifest
    assert manifest == GENERATOR.build_manifest()


def test_files_are_44k1_pcm16_with_the_manifest_lengths(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    song, free = manifest["song"], manifest["free"]
    checks = [(song["file"], 2, song["seconds"]), (free["file"], 2, free["seconds"])]
    checks += [(path, 1, song["seconds"]) for path in song["stems"].values()]

    for name, channels, seconds in checks:
        info = sf.info(str(out_dir / name))
        assert (info.samplerate, info.subtype, info.channels) == (44_100, "PCM_16", channels)
        assert info.frames == round(seconds * 44_100)
    assert 60 <= song["seconds"] <= 90


def test_sections_cover_the_song_and_start_on_downbeats(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    song = generated[1]["song"]
    sections = song["sections"]

    assert sections[0]["startSeconds"] == 0
    assert sections[-1]["endSeconds"] == song["seconds"]
    for current, following in itertools.pairwise(sections):
        assert current["endSeconds"] == following["startSeconds"]
        assert following["startSeconds"] in song["downbeats"]
    assert sum(s["bars"] for s in sections) == len(song["downbeats"]) == 40
    assert [s["label"] for s in sections] == ["Intro", "Verse", "Chorus 1", "Guitar solo",
                                              "Chorus 2", "Outro"]  # fmt: skip


def _read(path: Path) -> np.ndarray[Any, np.dtype[np.float64]]:
    data, _ = sf.read(str(path), dtype="float64", always_2d=True)
    return np.asarray(data.mean(axis=1))


def test_the_stems_sum_to_the_mix(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    stems = [_read(out_dir / path) for path in manifest["song"]["stems"].values()]

    mix = _read(out_dir / manifest["song"]["file"])

    assert np.max(np.abs(np.sum(stems, axis=0) - mix)) < 6 / 32_767


def test_vocals_sing_only_in_verse_and_choruses(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    vocals = _read(out_dir / "stems" / "vocals.flac")

    for section in manifest["song"]["sections"]:
        start, end = (round(section[key] * 44_100) for key in ("startSeconds", "endSeconds"))
        level = float(np.sqrt(np.mean(vocals[start:end] ** 2)))
        assert (level > 0.05) is (section["kind"] in ("verse", "chorus")), section["label"]


def test_the_beat_analyser_finds_the_grid_of_the_song(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    song = manifest["song"]

    grid = LibrosaBeatAnalyzer().analyze(out_dir / song["file"])

    assert grid.reliable
    assert abs(grid.bpm - song["bpm"]) <= 1
    expected = np.arange(song["downbeats"][0], song["seconds"] - 1, 0.5)
    found = np.array(grid.beats)
    assert np.median(np.min(np.abs(found[:, None] - expected[None, :]), axis=1)) < 0.03


def test_the_free_song_has_no_reliable_grid(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated

    grid = LibrosaBeatAnalyzer().analyze(out_dir / manifest["free"]["file"])

    assert grid.reliable is manifest["free"]["expectReliableGrid"] is False


def _section_request(song: dict[str, Any]) -> SectionJobRequest:
    """A section job over the fixture stems with the song's known grid."""
    downbeats = song["downbeats"]
    beats = [round(downbeats[0] + 0.5 * k, 3) for k in range(4 * len(downbeats))]
    grid = BeatGrid(bpm=song["bpm"], beats=beats, downbeats=downbeats, beats_per_bar=4,
                    confidence=0.9, reliable=True)  # fmt: skip
    paths = {name: f"tracks/{_TRACK}/stems/{name}.flac" for name in song["stems"]}
    body = {"stems": paths, "durationSeconds": song["seconds"],
            "grid": grid.model_dump(by_alias=True)}  # fmt: skip
    return SectionJobRequest.model_validate(body)


def _recall(found: list[float], truth: list[float]) -> float:
    """Share of the true boundaries with a found one within one bar (SC-001)."""
    return sum(min(abs(f - t) for f in found) <= 2.0 for t in truth) / len(truth)


def test_the_section_analyser_finds_the_form_on_the_stems(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    song = manifest["song"]
    files = {name: out_dir / path for name, path in song["stems"].items()}

    result = StemsNoveltyAnalyzer().analyze(_section_request(song), files, lambda _f: None)

    found = [s.start_seconds for s in result.sections[1:]]
    assert _recall(found, [s["startSeconds"] for s in song["sections"][1:]]) >= 0.8
    solo = next(s for s in result.sections if s.start_seconds <= 50 < s.end_seconds)
    assert (solo.kind, solo.instrument) == ("solo", "guitar")


def test_a_rerun_is_byte_identical(generated: tuple[Path, dict[str, Any]], tmp_path: Path) -> None:
    out_dir, _ = generated
    GENERATOR.generate(tmp_path, tmp_path / "manifest.json")

    for name in ("practice-song.wav", "practice-song-free.wav", "stems/guitar.flac"):
        first = hashlib.sha256((out_dir / name).read_bytes()).hexdigest()
        assert hashlib.sha256((tmp_path / name).read_bytes()).hexdigest() == first


def test_main_writes_next_to_the_given_paths(tmp_path: Path) -> None:
    assert GENERATOR.main(["--out-dir", str(tmp_path), "--manifest", str(tmp_path / "m.json")]) == 0
    assert (tmp_path / "practice-song.wav").is_file()


@pytest.mark.parametrize("seconds", [0.0, 0.5])
def test_the_lead_in_belongs_to_the_intro(seconds: float) -> None:
    assert (
        GENERATOR.sections()[0]["startSeconds"] <= seconds < GENERATOR.sections()[0]["endSeconds"]
    )
