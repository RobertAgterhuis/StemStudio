"""Tests of the fake transcriber (008 T026, data-model 2.6; E2E bar counts depend on it).

One note per beat of the grid from the input start to its end: pitched profiles walk the C-major
scale inside the profile range (from the C nearest its middle); piano adds a C-major triad in the bass staff on every bar; drums
play kick on beats 1 and 3, snare on 2 and 4 and hi-hat on every eighth.
"""

from pathlib import Path

import numpy as np
import pytest
from stemstudio_worker.transcription.fake import (
    FakeDrumTranscriber,
    FakePitchTranscriber,
    grid_beats,
)

from stemstudio_worker.schemas import BeatGridTempo, ManualTempo, RenderRange

from .helpers import write_audio

C_MAJOR = {0, 2, 4, 5, 7, 9, 11}
GRID = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[-0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0,
                     3.5, 4.0, 4.5], downbeats=[0.5, 2.5, 4.5])  # fmt: skip


def _audio(tmp_path: Path, seconds: float = 4.2) -> Path:
    return write_audio(tmp_path / "in.flac", np.zeros(int(seconds * 44_100)))


def test_grid_beats_cover_the_input_with_bar_positions() -> None:
    beats = grid_beats(GRID, 4.2)

    assert [b.time for b in beats] == [0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0]
    assert [b.position for b in beats] == [3, 0, 1, 2, 3, 0, 1, 2, 3]
    assert beats[-1].end == pytest.approx(4.2)
    assert beats[0].end == 0.5


def test_a_manual_grid_generates_beats_from_its_first_downbeat() -> None:
    manual = ManualTempo(bpm=60, beats_per_bar=3, first_downbeat_seconds=1.5)

    beats = grid_beats(manual, 5.0)

    assert [b.time for b in beats] == [0.5, 1.5, 2.5, 3.5, 4.5]
    assert [b.position for b in beats] == [2, 0, 1, 2, 0]


def test_a_pitched_profile_plays_the_c_major_scale_one_note_per_beat(tmp_path: Path) -> None:
    fake = FakePitchTranscriber("bass", (28, 67), GRID)
    reports: list[float] = []

    result = fake.transcribe(_audio(tmp_path), None, reports.append)

    assert (fake.engine, fake.attribution, result.device) == ("fake", None, None)
    assert [n.start for n in result.notes] == [0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0]
    assert [n.pitch for n in result.notes] == [48, 50, 52, 53, 55, 57, 59, 60, 62]
    assert all(n.pitch % 12 in C_MAJOR and 28 <= n.pitch <= 67 for n in result.notes)
    assert [n.end for n in result.notes][:2] == [0.5, 1.0]
    assert result.notes[-1].end == pytest.approx(4.2)
    assert reports == [1.0]


def test_the_scale_wraps_inside_a_narrow_range(tmp_path: Path) -> None:
    result = FakePitchTranscriber("vocals", (60, 64), GRID).transcribe(
        _audio(tmp_path), None, print
    )

    assert [n.pitch for n in result.notes] == [60, 62, 64, 60, 62, 64, 60, 62, 64]


def test_piano_adds_a_c_major_triad_in_the_bass_staff_on_every_bar(tmp_path: Path) -> None:
    result = FakePitchTranscriber("piano", (21, 108), GRID).transcribe(
        _audio(tmp_path), None, print
    )

    triads = [n for n in result.notes if n.pitch < 60]
    melody = [n for n in result.notes if n.pitch >= 60]
    assert [(n.start, n.pitch) for n in triads] == [
        (0.5, 48), (0.5, 52), (0.5, 55), (2.5, 48), (2.5, 52), (2.5, 55),
    ]  # fmt: skip
    assert [n.end for n in triads][::3] == [2.5, pytest.approx(4.2)]
    assert [n.pitch for n in melody][:3] == [60, 62, 64]
    assert len(melody) == 9
    assert list(result.notes) == sorted(result.notes, key=lambda n: (n.start, n.pitch))


def test_a_window_limits_the_beats_to_its_length(tmp_path: Path) -> None:
    window = RenderRange(start_seconds=10.0, end_seconds=11.2)

    result = FakePitchTranscriber("bass", (28, 67), GRID).transcribe(
        _audio(tmp_path, 20.0), window, print
    )

    assert [n.start for n in result.notes] == [0.0, 0.5, 1.0]


def test_drums_play_kick_snare_and_eighth_hi_hats(tmp_path: Path) -> None:
    inputs = {piece: _audio(tmp_path) for piece in ("kick", "snare", "hihat")}
    reports: list[float] = []

    hits = FakeDrumTranscriber(GRID).transcribe(inputs, None, reports.append)  # type: ignore[arg-type]

    kicks = [h.time for h in hits if h.piece == "kick"]
    snares = [h.time for h in hits if h.piece == "snare"]
    hats = [h.time for h in hits if h.piece == "hihat"]
    assert kicks == [0.5, 1.5, 2.5, 3.5]
    assert snares == [0.0, 1.0, 2.0, 3.0, 4.0]
    assert hats == [i * 0.25 for i in range(17)]
    assert [h.time for h in hits] == sorted(h.time for h in hits)
    assert reports == [pytest.approx(1 / 3), pytest.approx(2 / 3), 1.0]


def test_missing_drum_pieces_give_no_hits(tmp_path: Path) -> None:
    hits = FakeDrumTranscriber(GRID).transcribe({"snare": _audio(tmp_path)}, None, print)

    assert {h.piece for h in hits} == {"snare"}
