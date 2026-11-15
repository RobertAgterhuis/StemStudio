"""Tests of the feature 008 synthetic note material (score fixtures)."""

from __future__ import annotations

import itertools
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_score_fixtures as gen

GENERATOR = gen

BASS = "score_bass_line_120bpm.wav"
PIANO = "score_piano_texture_20s.wav"
DRUMS = [f"score_drums_{piece}.wav" for piece in gen.DRUM_PIECES]


def _mono(path: Path) -> npt.NDArray[np.float64]:
    data, _ = sf.read(str(path), dtype="float64", always_2d=True)
    return np.asarray(data.mean(axis=1), dtype=np.float64)


def _notes(facts: dict[str, Any]) -> list[gen.Note]:
    return [gen.Note(**n) for n in facts["notes"]]


@pytest.mark.parametrize("name", [BASS, PIANO, *DRUMS])
def test_every_file_is_stereo_pcm16_and_as_long_as_listed(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    data, rate = sf.read(str(out_dir / name), dtype="int16", always_2d=True)
    expected_frames = round(manifest["fixtures"][name]["durationSeconds"] * 44_100)
    assert (rate, data.shape) == (44_100, (expected_frames, 2))


def test_bass_line_follows_research_s03(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    notes = _notes(manifest["fixtures"][BASS])
    assert len(notes) == 78
    assert {n.pitch for n in notes} <= set(gen.BASS_SET)
    assert {round(n.end - n.start, 3) for n in notes} == {0.25, 0.5}
    assert all(a.end <= b.start for a, b in itertools.pairwise(notes))
    assert any(a.end < b.start for a, b in itertools.pairwise(notes))


def test_piano_texture_spans_both_staves(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    notes = _notes(manifest["fixtures"][PIANO])
    treble = [n for n in notes if n.pitch >= 60]
    bass = [n for n in notes if n.pitch < 60]
    assert len(treble) == 38
    assert len(bass) == 57
    assert max(n.end for n in notes) <= 20.0


def test_notes_and_hits_sit_on_the_grid(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    for facts in manifest["fixtures"].values():
        times = [n["start"] for n in facts.get("notes", [])] + [
            h["time"] for h in facts.get("hits", [])
        ]
        sixteenths = [(t - gen.FIRST_DOWNBEAT_S) / (gen.BEAT_S / 4) for t in times]
        assert all(abs(s - round(s)) < 1e-6 for s in sixteenths)


def test_grid_is_a_worker_beat_grid(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    grid = manifest["fixtures"][BASS]["grid"]
    assert (grid["kind"], grid["bpm"], grid["beatsPerBar"]) == ("beatGrid", 120.0, 4)
    assert grid["beats"][0] == grid["downbeats"][0] == gen.FIRST_DOWNBEAT_S
    assert np.allclose(np.diff(grid["beats"]), 0.5)
    assert grid["downbeats"] == grid["beats"][::4]


def test_drum_pattern_is_the_documented_groove(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    counts = {name: len(manifest["fixtures"][name]["hits"]) for name in DRUMS}
    assert counts == {
        "score_drums_kick.wav": 16,
        "score_drums_snare.wav": 16,
        "score_drums_toms.wav": 8,
        "score_drums_hihat.wav": 64,
        "score_drums_cymbals.wav": 2,
    }


def test_each_drum_file_is_loud_only_at_its_hits(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    signal = _mono(out_dir / "score_drums_snare.wav")
    hit = round(manifest["fixtures"]["score_drums_snare.wav"]["hits"][0]["time"] * 44_100)
    assert np.max(np.abs(signal[hit : hit + 2000])) > 0.3
    assert np.max(np.abs(signal[: round(0.5 * 44_100)])) == 0.0


def test_pitched_audio_has_energy_at_the_first_note(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    first = _notes(manifest["fixtures"][BASS])[0]
    signal = _mono(out_dir / BASS)
    begin = round(first.start * 44_100)
    window = signal[begin : begin + 8192] * np.hanning(8192)
    peak_hz = np.argmax(np.abs(np.fft.rfft(window))) * 44_100 / 8192
    assert peak_hz == pytest.approx(440.0 * 2 ** ((first.pitch - 69) / 12), rel=0.03)


def test_f_measure_of_identical_lists_is_one() -> None:
    notes = [gen.Note(0.5, 1.0, 40), gen.Note(1.0, 1.5, 43)]
    assert gen.onset_f_measure(notes, notes) == 1.0


def test_f_measure_counts_onset_tolerance_and_pitch() -> None:
    reference = [gen.Note(0.5, 1.0, 40), gen.Note(1.0, 1.5, 43)]
    estimate = [gen.Note(0.54, 1.0, 40), gen.Note(1.0, 1.5, 44), gen.Note(2.0, 2.5, 45)]
    assert gen.onset_f_measure(reference, estimate) == pytest.approx(0.4)


def test_f_measure_without_matches_or_notes_is_zero() -> None:
    assert gen.onset_f_measure([], [gen.Note(0.0, 1.0, 40)]) == 0.0
    assert gen.onset_f_measure([gen.Note(0.0, 1.0, 40)], [gen.Note(0.2, 1.0, 40)]) == 0.0
