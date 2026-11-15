"""Drum onsets from drum detail stems (008 T057; data-model 2.3; research.md S-05; FR-009).

Every stem is a generated click track with exactly known hit times.
"""

from collections.abc import Mapping
from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.analysis import LibrosaDrumTranscriber
from stemstudio_worker.analysis.drum_onsets import BLEED_GATE, SILENCE_PEAK
from stemstudio_worker.ports import DrumHit, DrumTranscriber, TranscriptionCancelledError
from stemstudio_worker.schemas import DrumPiece, RenderRange

from .conftest import SR, MakeWav, Signal, click, place

SECONDS = 4.0
TOLERANCE = 0.020

TIMES: dict[DrumPiece, list[float]] = {
    "kick": [0.50, 1.25, 2.00, 3.10],
    "snare": [0.90, 1.70, 2.60, 3.40],
    "toms": [1.05, 2.30],
    "hihat": [0.30, 0.75, 1.50, 2.20, 2.85, 3.60],
    "cymbals": [0.40, 2.45],
}

# (frequency Hz, decay s) per piece: low thump, mid crack, toms, short and long high bursts.
TIMBRE: dict[DrumPiece, tuple[float, float]] = {
    "kick": (70.0, 0.04),
    "snare": (900.0, 0.02),
    "toms": (180.0, 0.05),
    "hihat": (7_000.0, 0.004),
    "cymbals": (5_000.0, 0.08),
}


def stroke(piece: DrumPiece, gain: float = 0.9) -> Signal:
    frequency, decay = TIMBRE[piece]
    return click(gain, frequency, decay)


def clicks(piece: DrumPiece, times: list[float], gain: float = 0.9) -> Signal:
    signal = np.zeros(int(SECONDS * SR))
    for at in times:
        place(signal, stroke(piece, gain), at)
    return signal


def no_progress(fraction: float) -> None:
    del fraction


def transcribe(
    inputs: Mapping[DrumPiece, Path], window: RenderRange | None = None
) -> tuple[DrumHit, ...]:
    return LibrosaDrumTranscriber().transcribe(inputs, window, no_progress)


def times_of(hits: tuple[DrumHit, ...], piece: DrumPiece) -> list[float]:
    return [h.time for h in hits if h.piece == piece]


def assert_close(found: list[float], expected: list[float]) -> None:
    assert len(found) == len(expected), (found, expected)
    assert np.max(np.abs(np.subtract(found, expected))) <= TOLERANCE, (found, expected)


@pytest.fixture
def all_pieces(make_wav: MakeWav) -> dict[DrumPiece, Path]:
    return {piece: make_wav(f"{piece}.wav", clicks(piece, times)) for piece, times in TIMES.items()}


# ------------------------------------------------------------------ timing per piece


@pytest.mark.parametrize("piece", list(TIMES))
def test_hits_of_every_piece_are_found_within_20_ms(
    all_pieces: dict[DrumPiece, Path], piece: DrumPiece
) -> None:
    assert_close(times_of(transcribe(all_pieces), piece), TIMES[piece])


def test_hits_come_in_time_order(all_pieces: dict[DrumPiece, Path]) -> None:
    hits = transcribe(all_pieces)
    assert [h.time for h in hits] == sorted(h.time for h in hits)
    assert len(hits) == sum(len(times) for times in TIMES.values())


def test_strengths_are_relative_and_within_0_and_1(make_wav: MakeWav) -> None:
    signal = clicks("snare", [0.5, 1.5]) + clicks("snare", [2.5, 3.0], gain=0.3)
    hits = transcribe({"snare": make_wav("snare.wav", signal)})
    strengths = [h.strength for h in hits]
    assert all(0.0 <= s <= 1.0 for s in strengths)
    assert strengths[0] == pytest.approx(1.0, abs=0.05)
    assert strengths[2] == pytest.approx(0.3 / 0.9, abs=0.1)


# ------------------------------------------------------------------ bleed gate


def test_the_bleed_gate_removes_a_quieter_copy_of_another_piece(make_wav: MakeWav) -> None:
    bleed = clicks("kick", TIMES["kick"], gain=0.9 * BLEED_GATE * 0.5)
    snare = make_wav("snare.wav", clicks("snare", TIMES["snare"]) + bleed)
    assert_close(times_of(transcribe({"snare": snare}), "snare"), TIMES["snare"])


def test_hits_just_above_the_gate_are_kept(make_wav: MakeWav) -> None:
    ghost = clicks("snare", [2.2], gain=0.9 * BLEED_GATE * 2.5)
    snare = make_wav("snare.wav", clicks("snare", TIMES["snare"]) + ghost)
    assert_close(times_of(transcribe({"snare": snare}), "snare"), sorted([*TIMES["snare"], 2.2]))


# ------------------------------------------------------------------ missing and silent pieces


def test_missing_pieces_produce_no_hits(all_pieces: dict[DrumPiece, Path]) -> None:
    hits = transcribe({"kick": all_pieces["kick"], "hihat": all_pieces["hihat"]})
    assert {h.piece for h in hits} == {"kick", "hihat"}


def test_no_inputs_produce_no_hits() -> None:
    assert transcribe({}) == ()


def test_a_silent_or_near_silent_stem_produces_no_hits(make_wav: MakeWav) -> None:
    rng = np.random.default_rng(3)
    hiss = rng.normal(0.0, SILENCE_PEAK / 10, int(SECONDS * SR))
    silent = make_wav("toms.wav", np.zeros(int(SECONDS * SR)))
    quiet = make_wav("cymbals.wav", hiss)
    assert transcribe({"toms": silent}) == ()
    assert transcribe({"cymbals": quiet}) == ()


def test_a_click_below_the_silence_floor_is_ignored(make_wav: MakeWav) -> None:
    # write_wav only scales signals above full scale down, so the stem stays this quiet.
    quiet = make_wav("quiet.wav", clicks("kick", [1.0], gain=SILENCE_PEAK / 4))
    assert transcribe({"kick": quiet}) == ()


# ------------------------------------------------------------------ window and progress


def test_a_window_gives_times_relative_to_its_start(all_pieces: dict[DrumPiece, Path]) -> None:
    window = RenderRange(start_seconds=1.0, end_seconds=3.0)
    hits = transcribe({"kick": all_pieces["kick"]}, window)
    assert_close([h.time for h in hits], [0.25, 1.0])


def test_progress_is_reported_once_per_piece(all_pieces: dict[DrumPiece, Path]) -> None:
    seen: list[float] = []
    inputs = {piece: all_pieces[piece] for piece in ("kick", "snare", "toms", "hihat")}
    LibrosaDrumTranscriber().transcribe(inputs, None, seen.append)
    assert seen == [0.25, 0.5, 0.75, 1.0]


def test_cancellation_from_progress_propagates(all_pieces: dict[DrumPiece, Path]) -> None:
    def cancel(fraction: float) -> None:
        raise TranscriptionCancelledError("cancelled")

    with pytest.raises(TranscriptionCancelledError):
        LibrosaDrumTranscriber().transcribe(all_pieces, None, cancel)


def test_it_satisfies_the_port() -> None:
    transcriber: DrumTranscriber = LibrosaDrumTranscriber()
    assert transcriber is not None
