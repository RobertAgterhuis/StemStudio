"""Beat-grid time base (008 T014; data-model 2.4; FR-004, FR-006, FR-007, SC-003)."""

from fractions import Fraction

import pytest

from stemstudio_worker.notation.timebase import (
    Bar,
    TimeBase,
    bar_layout,
    grid_step,
    manual_beats,
    round_bpm,
    shift_grid,
)
from stemstudio_worker.schemas import BeatGridTempo, ManualTempo

# Accelerating then dragging beats: a drifting live tempo around 120 BPM.
DRIFT = [1.0, 1.5, 2.0, 2.6, 3.1, 3.55, 4.0, 4.5]


def drifting(downbeat: float = 1.0, beats_per_bar: int = 4) -> TimeBase:
    grid = BeatGridTempo(bpm=120, beats_per_bar=beats_per_bar, beats=DRIFT, downbeats=[downbeat])
    return TimeBase.from_grid(grid, duration_seconds=6.0)


# ------------------------------------------------------------------ interpolation


@pytest.mark.parametrize(("index", "seconds"), list(enumerate(DRIFT)))
def test_a_note_on_a_beat_lands_exactly_on_it(index: int, seconds: float) -> None:
    assert drifting().quarters(seconds) == index
    assert drifting().quantiser("sixteenth").snap(seconds) == index


def test_between_uneven_beats_the_position_is_interpolated() -> None:
    timebase = drifting()
    assert timebase.quarters(2.3) == pytest.approx(2.5)  # half way 2.0 .. 2.6
    assert timebase.quarters(3.775) == pytest.approx(5.5)  # half way 3.55 .. 4.0


def test_the_period_is_the_median_beat_difference() -> None:
    assert drifting().period == pytest.approx(0.5)


def test_before_and_after_the_grid_positions_are_extrapolated_with_the_period() -> None:
    timebase = drifting()
    assert timebase.quarters(0.0) == pytest.approx(-2.0)
    assert timebase.quarters(0.25) == pytest.approx(-1.5)
    assert timebase.quarters(5.5) == pytest.approx(9.0)


def test_negative_beats_from_a_shift_are_used_for_interpolation() -> None:
    grid = BeatGridTempo(bpm=60, beats_per_bar=4, beats=[-2.0, -1.0, 0.0, 1.0], downbeats=[-2.0])
    timebase = TimeBase.from_grid(grid, duration_seconds=2.0)
    assert timebase.quarters(0.0) == 2
    assert timebase.quarters(-1.5) == pytest.approx(0.5)


# ------------------------------------------------------------------ bar 1 = first downbeat


def test_bar_one_starts_at_the_first_downbeat() -> None:
    timebase = drifting(downbeat=2.0)
    assert timebase.downbeat_index == 2
    assert timebase.quarters(2.0) == 0
    assert timebase.quarters(1.0) == -2


def test_the_downbeat_index_is_the_nearest_beat() -> None:
    assert drifting(downbeat=2.55).downbeat_index == 3
    assert drifting(downbeat=0.2).downbeat_index == 0


# ------------------------------------------------------------------ snapping


@pytest.mark.parametrize(
    ("seconds", "sixteenth", "eighth"),
    [
        (1.0, Fraction(0), Fraction(0)),
        (1.185, Fraction(1, 4), Fraction(1, 2)),  # 0.37 quarters
        (1.0625, Fraction(1, 4), Fraction(0)),  # 0.125 quarters: half up to the next 1/16
        (1.3, Fraction(1, 2), Fraction(1, 2)),
        (1.4, Fraction(3, 4), Fraction(1)),
        (0.9, Fraction(-1, 4), Fraction(0)),
    ],
)
def test_snapping_to_sixteenths_and_eighths(
    seconds: float, sixteenth: Fraction, eighth: Fraction
) -> None:
    timebase = drifting()
    assert timebase.quantiser("sixteenth").snap(seconds) == sixteenth
    assert timebase.quantiser("eighth").snap(seconds) == eighth


def test_grid_steps() -> None:
    assert grid_step("sixteenth") == Fraction(1, 4)
    assert grid_step("eighth") == Fraction(1, 2)
    quantiser = drifting(beats_per_bar=3).quantiser("eighth")
    assert (quantiser.step, quantiser.beats_per_bar) == (Fraction(1, 2), 3)


# ------------------------------------------------------------------ bars and pickup


def test_layout_without_pickup() -> None:
    assert bar_layout(Fraction(0), Fraction(8), 4) == (
        Bar(1, Fraction(0), Fraction(4)),
        Bar(2, Fraction(4), Fraction(4)),
    )


def test_layout_with_a_pickup_shortened_to_the_used_length() -> None:
    layout = bar_layout(Fraction(-3, 2), Fraction(6), 4)
    assert layout == (
        Bar(0, Fraction(-3, 2), Fraction(3, 2)),
        Bar(1, Fraction(0), Fraction(4)),
        Bar(2, Fraction(4), Fraction(4)),
    )
    assert layout[0].end == 0


def test_layout_with_a_lead_longer_than_a_bar_keeps_whole_bars() -> None:
    assert bar_layout(Fraction(-6), Fraction(2), 4) == (
        Bar(0, Fraction(-6), Fraction(2)),
        Bar(1, Fraction(-4), Fraction(4)),
        Bar(2, Fraction(0), Fraction(4)),
    )


def test_layout_with_a_whole_bar_lead_has_no_pickup() -> None:
    assert bar_layout(Fraction(-3), Fraction(1), 3) == (
        Bar(1, Fraction(-3), Fraction(3)),
        Bar(2, Fraction(0), Fraction(3)),
    )


def test_layout_starts_at_bar_one_when_the_first_note_is_later() -> None:
    assert [bar.number for bar in bar_layout(Fraction(5), Fraction(6), 4)] == [1, 2]


def test_layout_always_has_bar_one() -> None:
    assert bar_layout(Fraction(-1), Fraction(0), 4) == (
        Bar(0, Fraction(-1), Fraction(1)),
        Bar(1, Fraction(0), Fraction(4)),
    )


# ------------------------------------------------------------------ manual grid


def test_manual_beats_cover_the_input_at_a_constant_period() -> None:
    grid = ManualTempo(bpm=120, beats_per_bar=3, first_downbeat_seconds=0.75)
    beats, downbeat_index = manual_beats(grid, duration_seconds=2.0)
    assert beats == pytest.approx([-0.25, 0.25, 0.75, 1.25, 1.75, 2.25])
    assert beats[downbeat_index] == pytest.approx(0.75)


def test_manual_beats_of_a_very_short_input_still_form_a_grid() -> None:
    grid = ManualTempo(bpm=60, beats_per_bar=4, first_downbeat_seconds=0.0)
    beats, downbeat_index = manual_beats(grid, duration_seconds=0.1)
    assert (beats, downbeat_index) == ([0.0, 1.0], 0)


def test_manual_time_base() -> None:
    grid = ManualTempo(bpm=90, beats_per_bar=3, first_downbeat_seconds=2.0)
    timebase = TimeBase.from_grid(grid, duration_seconds=10.0)
    assert timebase.period == pytest.approx(60 / 90)
    assert timebase.quarters(2.0) == pytest.approx(0)
    assert timebase.quarters(0.0) == pytest.approx(-3)
    assert timebase.quarters(12.0) == pytest.approx(15)
    assert timebase.quantiser("sixteenth").snap(0.0) == -3


# ------------------------------------------------------------------ shift for a range / asset


def test_shift_moves_beats_and_downbeats_and_keeps_earlier_beats() -> None:
    grid = BeatGridTempo(bpm=120, beats_per_bar=4, beats=DRIFT, downbeats=[1.0, 3.1])
    shifted = shift_grid(grid, 2.0)
    assert isinstance(shifted, BeatGridTempo)
    assert shifted.beats == pytest.approx([b - 2.0 for b in DRIFT])
    assert shifted.downbeats == pytest.approx([-1.0, 1.1])
    assert TimeBase.from_grid(shifted, 4.0).quarters(0.0) == TimeBase.from_grid(grid, 6.0).quarters(
        2.0
    )


def test_shift_of_a_manual_grid_moves_the_downbeat_by_whole_bars() -> None:
    grid = ManualTempo(bpm=120, beats_per_bar=4, first_downbeat_seconds=0.5)
    shifted = shift_grid(grid, 5.0)
    assert isinstance(shifted, ManualTempo)
    assert shifted.first_downbeat_seconds == pytest.approx(1.5)  # 0.5 - 5 + 3 bars of 2 s
    assert shift_grid(grid, 0.0) == grid


# ------------------------------------------------------------------ meter and tempo mark


def test_time_signature_and_rounded_tempo() -> None:
    grid = BeatGridTempo(bpm=119.5, beats_per_bar=3, beats=DRIFT, downbeats=[1.0])
    timebase = TimeBase.from_grid(grid, duration_seconds=6.0)
    assert timebase.time_signature == "3/4"
    assert timebase.beats_per_bar == 3
    assert timebase.bpm == 120


@pytest.mark.parametrize(("bpm", "rounded"), [(119.5, 120), (120.49, 120), (87.5, 88), (30, 30)])
def test_round_bpm_rounds_half_up(bpm: float, rounded: int) -> None:
    assert round_bpm(bpm) == rounded
