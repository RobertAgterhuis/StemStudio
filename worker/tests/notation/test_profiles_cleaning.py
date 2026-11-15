"""Instrument profiles and note cleaning (008 T012; data-model 2.1, 2.3, 2.7; FR-002, FR-010)."""

from fractions import Fraction

import pytest

from stemstudio_worker.notation.cleaning import (
    PlacedHit,
    PlacedNote,
    cap_per_bar,
    clean_hits,
    clean_notes,
    cleaning_warnings,
    drop_short,
    end_at_next_onset,
    keep_in_range,
    merge_duplicates,
    reduce_monophonic,
)
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.profiles import (
    GRAND_STAFF_SPLIT,
    PROFILES,
    engine_for,
    get_profile,
    resolve_resolution,
    staff_of,
    suggest_profile,
)
from stemstudio_worker.ports import DrumHit
from stemstudio_worker.schemas import SCORE_WARNING_TEXTS

from .conftest import ConstantQuantiser, at_quarters, note, rows

Q = ConstantQuantiser()
SIXTEENTH = Fraction(1, 4)


# ------------------------------------------------------------------ profiles (data-model 2.1)


PROFILE_TABLE = [
    ("vocals", ("treble",), 40, 84, True, "eighth"),
    ("bass", ("bass-8vb",), 28, 67, True, "sixteenth"),
    ("guitar", ("treble-8vb",), 40, 88, False, "sixteenth"),
    ("keys", ("treble", "bass"), 21, 108, False, "sixteenth"),
    ("piano", ("treble", "bass"), 21, 108, False, "sixteenth"),
    ("drums", ("percussion",), 0, 127, False, "sixteenth"),
]


@pytest.mark.parametrize("row", PROFILE_TABLE, ids=[row[0] for row in PROFILE_TABLE])
def test_profile_table(row: tuple[str, tuple[str, ...], int, int, bool, str]) -> None:
    name, clefs, low, high, single, resolution = row
    profile = get_profile(name)  # type: ignore[arg-type]
    assert profile.name == name
    assert profile.clefs == clefs
    assert (profile.low, profile.high) == (low, high)
    assert profile.single_voice is single
    assert profile.default_resolution == resolution
    assert profile.max_notes_per_bar == 32


def test_every_profile_is_in_the_table() -> None:
    assert set(PROFILES) == {"vocals", "bass", "guitar", "keys", "piano", "drums"}


@pytest.mark.parametrize(
    ("channel", "expected"),
    [
        ("vocals", "vocals"),
        ("vocals.lead", "vocals"),
        ("vocals.backing", "vocals"),
        ("bass", "bass"),
        ("guitar", "guitar"),
        ("guitar.lead", "guitar"),
        ("piano", "piano"),
        ("piano.left", "keys"),
        ("other", "keys"),
        ("other.synth", "keys"),
        ("drums", "drums"),
        ("drums.kick", None),
        ("bassoon", None),
        ("", None),
    ],
)
def test_suggested_profile_per_channel(channel: str, expected: str | None) -> None:
    assert suggest_profile(channel) == expected


def test_resolution_defaults_to_the_profile() -> None:
    assert resolve_resolution("vocals", None) == "eighth"
    assert resolve_resolution("bass", None) == "sixteenth"
    assert resolve_resolution("vocals", "sixteenth") == "sixteenth"


@pytest.mark.parametrize(
    ("profile", "quality", "engine"),
    [
        ("piano", "accurate", "piano-crnn"),
        ("piano", "fast", "basic-pitch"),
        ("bass", "accurate", "basic-pitch"),
        ("keys", "fast", "basic-pitch"),
        ("drums", "accurate", "drum-onsets"),
    ],
)
def test_engine_per_profile(profile: str, quality: str, engine: str) -> None:
    assert engine_for(profile, quality) == engine  # type: ignore[arg-type]


def test_grand_staff_split_at_middle_c() -> None:
    keys = get_profile("keys")
    assert GRAND_STAFF_SPLIT == 60
    assert staff_of(keys, 60) == 0
    assert staff_of(keys, 59) == 1
    assert staff_of(get_profile("bass"), 30) == 0


def test_profile_contains_its_range() -> None:
    bass = get_profile("bass")
    assert bass.contains(28)
    assert bass.contains(67)
    assert not bass.contains(27)
    assert not bass.contains(68)


# ------------------------------------------------------------------ cleaning steps, data-model 2.3


def test_step1_drops_pitches_outside_the_range() -> None:
    notes = [note(0, 0.5, 27), note(0.5, 1, 28), note(1, 1.5, 67), note(1.5, 2, 68)]
    assert [n.pitch for n in keep_in_range(notes, get_profile("bass"))] == [28, 67]


def test_step2_keeps_the_louder_note_within_60_ms() -> None:
    quiet_first = [note(1.0, 1.4, 40, 0.3), note(1.03, 1.4, 52, 0.9)]
    loud_first = [note(2.0, 2.4, 40, 0.9), note(2.05, 2.4, 52, 0.3)]
    kept = reduce_monophonic([*quiet_first, *loud_first])
    assert [(n.start, n.pitch) for n in kept] == [(1.03, 52), (2.0, 40)]


def test_step2_same_onset_keeps_the_louder_and_far_notes_stay() -> None:
    notes = [note(0.0, 0.4, 40, 0.2), note(0.0, 0.4, 47, 0.7), note(0.07, 0.4, 45, 0.1)]
    assert [n.pitch for n in reduce_monophonic(notes)] == [47, 45]


def test_step2_window_is_measured_from_the_kept_onset() -> None:
    # 0.00 kept, 0.05 quieter (dropped), 0.10 is 100 ms after the kept onset -> kept.
    notes = [note(0.0, 1, 40, 0.9), note(0.05, 1, 41, 0.1), note(0.10, 1, 42, 0.5)]
    assert [n.pitch for n in reduce_monophonic(notes)] == [40, 42]


def test_step3_single_voice_notes_end_at_the_next_onset() -> None:
    placed = [
        PlacedNote(Fraction(0), Fraction(2), 40, 0.5),
        PlacedNote(Fraction(1), Fraction(3, 2), 42, 0.5),
        PlacedNote(Fraction(3), Fraction(4), 43, 0.5),
    ]
    assert rows((n.onset, n.end) for n in end_at_next_onset(placed)) == [
        (0, 1),
        (1, Fraction(3, 2)),
        (3, 4),
    ]


def test_step3_single_voice_keeps_the_loudest_note_per_slot() -> None:
    placed = [
        PlacedNote(Fraction(0), Fraction(1), 40, 0.3),
        PlacedNote(Fraction(0), Fraction(1), 45, 0.9),
        PlacedNote(Fraction(1), Fraction(2), 47, 0.5),
    ]
    assert rows((n.onset, n.pitch, n.end) for n in end_at_next_onset(placed)) == [
        (0, 45, 1),
        (1, 47, 2),
    ]


def test_step4_drops_notes_shorter_than_one_grid_step() -> None:
    placed = [
        PlacedNote(Fraction(0), Fraction(0), 40, 0.5),
        PlacedNote(Fraction(1), Fraction(5, 4), 41, 0.5),
    ]
    assert [n.pitch for n in drop_short(placed, SIXTEENTH)] == [41]


def test_step4_merges_duplicates_of_the_same_pitch_and_slot() -> None:
    placed = [
        PlacedNote(Fraction(0), Fraction(1), 60, 0.4),
        PlacedNote(Fraction(0), Fraction(2), 60, 0.2),
        PlacedNote(Fraction(0), Fraction(1), 64, 0.9),
    ]
    merged = merge_duplicates(placed)
    assert rows((n.pitch, n.end, n.velocity) for n in merged) == [(60, 2, 0.4), (64, 1, 0.9)]


def test_step5_caps_each_voice_per_bar_keeping_the_loudest() -> None:
    placed = [PlacedNote(Fraction(i, 4), Fraction(i + 1, 4), 60, i / 10) for i in range(10)]
    placed.append(PlacedNote(Fraction(4), Fraction(5), 60, 0.1))  # next bar: untouched
    kept, capped = cap_per_bar(placed, lambda n: (int(n.onset // 4), 0), lambda n: n.velocity, 3)
    assert capped == 7
    assert [n.onset for n in kept] == [Fraction(7, 4), 2, Fraction(9, 4), 4]


def test_step5_counts_voices_separately() -> None:
    placed = [PlacedNote(Fraction(i, 4), Fraction(i + 1, 4), 60 + i, 0.5) for i in range(4)]
    kept, capped = cap_per_bar(placed, lambda n: (0, n.pitch % 2), lambda n: n.velocity, 2)
    assert (len(kept), capped) == (4, 0)


# ------------------------------------------------------------------ clean_notes (order 1-6)


def test_clean_notes_runs_the_steps_in_order() -> None:
    # Out-of-range loud note at 0 must not suppress the in-range quiet one (range first).
    notes = [
        note(0.0, 0.5, 20, 1.0),
        note(0.01, 0.5, 40, 0.2),
        note(0.5, 2.0, 43, 0.6),  # ends at the next onset (1.0 s = quarter 2): step 3 first
        note(1.0, 1.02, 45, 0.6),  # then too short after quantisation (step 4)
        note(1.5, 2.0, 47, 0.6),
    ]
    cleaned = clean_notes(notes, get_profile("bass"), Q)
    assert rows((n.onset, n.end, n.pitch) for n in cleaned.notes) == [
        (0, 1, 40),
        (1, 2, 43),
        (3, 4, 47),
    ]
    assert (cleaned.total, cleaned.removed, cleaned.capped) == (5, 2, 0)


def test_clean_notes_polyphonic_keeps_chords_and_merges_duplicates() -> None:
    notes = [note(0, 0.5, 60), note(0, 0.5, 64), note(0.02, 0.5, 64), note(0, 1.0, 67)]
    cleaned = clean_notes(notes, get_profile("guitar"), Q)
    assert rows((n.pitch, n.end) for n in cleaned.notes) == [(60, 1), (64, 1), (67, 2)]


def test_clean_notes_caps_busy_bars() -> None:
    chords = [
        n for pitch in (60, 64, 67) for n in at_quarters([i / 4 for i in range(16)], pitch, 0.25)
    ]
    cleaned = clean_notes(chords, get_profile("guitar"), Q)
    assert (len(cleaned.notes), cleaned.capped) == (32, 16)


def test_clean_notes_caps_each_grand_staff_separately() -> None:
    upper = at_quarters([i / 4 for i in range(16)], 72, 0.25)
    lower = at_quarters([i / 4 for i in range(16)], 48, 0.25)
    cleaned = clean_notes([*upper, *lower, *at_quarters([0], 76, 0.25)], get_profile("keys"), Q)
    assert (len(cleaned.notes), cleaned.capped) == (33, 0)


def test_clean_notes_of_nothing_is_empty() -> None:
    cleaned = clean_notes([], get_profile("vocals"), Q)
    assert (cleaned.notes, cleaned.total, cleaned.removed, cleaned.capped) == ((), 0, 0, 0)


# ------------------------------------------------------------------ warnings, data-model 2.7


def test_no_warnings_for_a_clean_line() -> None:
    cleaned = clean_notes(at_quarters([0, 1, 2, 3], 40), get_profile("bass"), Q)
    assert cleaning_warnings(cleaned) == ()


def test_notes_cleaned_warning_above_ten_percent() -> None:
    line = at_quarters([float(i) for i in range(9)], 40)
    over = clean_notes([*line, note(5, 5.5, 10), note(6, 6.5, 11)], get_profile("bass"), Q)
    exactly = clean_notes([*line, note(5, 5.5, 10)], get_profile("bass"), Q)
    assert cleaning_warnings(over) == (score_warning("notes-cleaned", n=2),)
    assert cleaning_warnings(over)[0].text == "2 very short or out-of-range notes were removed."
    assert cleaning_warnings(exactly) == ()


def test_dense_capped_warning() -> None:
    chords = [
        n for pitch in (60, 64, 67) for n in at_quarters([i / 4 for i in range(16)], pitch, 0.25)
    ]
    warnings = cleaning_warnings(clean_notes(chords, get_profile("guitar"), Q))
    assert [w.code for w in warnings] == ["dense-capped"]
    assert warnings[0].text == "16 notes were left out in very busy bars."


def test_score_warning_formats_every_text() -> None:
    assert score_warning("few-notes").text == SCORE_WARNING_TEXTS["few-notes"]
    assert score_warning("missing-drum-piece", piece="toms").text == (
        "No toms stem was available; that part is empty."
    )


# ------------------------------------------------------------------ drums, data-model 2.3


def hit(time: float, piece: str = "snare", strength: float = 0.8) -> DrumHit:
    return DrumHit(time=time, piece=piece, strength=strength)  # type: ignore[arg-type]


def test_clean_hits_quantises_and_merges_per_piece() -> None:
    hits = [hit(0.0, "kick", 0.4), hit(0.02, "kick", 0.9), hit(0.01, "hihat"), hit(0.5)]
    cleaned = clean_hits(hits, Q)
    assert rows((h.onset, h.piece, h.strength) for h in cleaned.hits) == [
        (0, "kick", 0.9),
        (0, "hihat", 0.8),
        (1, "snare", 0.8),
    ]
    assert (cleaned.total, cleaned.removed, cleaned.capped) == (4, 0, 0)


def test_clean_hits_caps_the_hands_and_feet_separately() -> None:
    hands = [hit(i * 0.125, piece, 0.5) for i in range(16) for piece in ("hihat", "snare", "toms")]
    feet = [hit(i * 0.125, "kick", 0.5) for i in range(16)]
    cleaned = clean_hits([*hands, *feet], Q)
    assert cleaned.capped == 16
    assert [h.piece for h in cleaned.hits].count("kick") == 16


def test_placed_values_expose_their_voice() -> None:
    assert PlacedHit(Fraction(0), "kick", 1.0).hands is False
    assert PlacedHit(Fraction(0), "cymbals", 1.0).hands is True
    assert PlacedNote(Fraction(1), Fraction(3, 2), 60, 1.0).duration == Fraction(1, 2)
