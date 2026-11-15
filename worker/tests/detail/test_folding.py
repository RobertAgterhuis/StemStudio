"""Tests of the folding of raw model outputs into detail stem names (T014, FR-002)."""

import numpy as np

from stemstudio_worker.detail.folding import (
    DRUMSEP_TABLE,
    MEGA_OUTPUTS,
    MEGA_TABLE,
    VOCAL_TABLE,
    fold,
    output_sum,
)
from stemstudio_worker.detail.names import (
    DRUM_STEMS,
    INSTRUMENT_STEMS,
    VOCAL_STEMS,
    detail_names,
    display_order,
    is_splittable,
)

from .signals import tone

DRUMSEP_OUTPUTS = ("kick", "snare", "toms", "hh", "ride", "crash")
MEGA_ALL = (
    "accordion", "acoustic-guitar", "back-vocal", "banjo", "bass", "bassoon", "bells",
    "bowed_strings", "brass", "cello", "clarinet", "congas", "digital-piano", "dobro",
    "double-bass", "drums", "electric-guitar", "flute", "french-horn", "glockenspiel", "guitar",
    "harmonica", "harp", "harpsichord", "hh", "keys", "kick", "lead-vocal", "mandolin", "marimba",
    "oboe", "organ", "percussion", "piano", "saxophone", "sitar", "snare", "strings", "synth",
    "tambourine", "timpani", "toms", "triangle", "trombone", "trumpet", "tuba", "ukulele", "viola",
    "violin", "vocal", "wind", "wind-chimes", "woodwind",
)  # fmt: skip


def _outputs(names: tuple[str, ...]) -> dict[str, np.ndarray]:
    return {name: tone(0.1, 0.01 * (i + 1)) for i, name in enumerate(names)}


def test_drumsep_six_outputs_fold_into_five_stems() -> None:
    outputs = _outputs(DRUMSEP_OUTPUTS)
    folded = fold(outputs, DRUMSEP_TABLE)
    assert tuple(folded) == DRUM_STEMS
    assert np.array_equal(folded["hihat"], outputs["hh"])
    assert np.allclose(folded["cymbals"], outputs["ride"] + outputs["crash"])
    assert np.array_equal(folded["kick"], outputs["kick"])


def test_mega_53_outputs_fold_into_eight_families_and_ignore_the_rest() -> None:
    assert len(MEGA_ALL) == 53
    outputs = _outputs(MEGA_ALL)
    folded = fold(outputs, MEGA_TABLE)
    assert tuple(folded) == INSTRUMENT_STEMS
    assert np.array_equal(folded["strings"], outputs["bowed_strings"])
    assert np.array_equal(folded["woodwinds"], outputs["woodwind"])
    assert np.array_equal(folded["keys"], outputs["keys"])
    assert set(MEGA_OUTPUTS) == {"bowed_strings", "brass", "woodwind", "keys", "synth",
                                 "acoustic-guitar", "electric-guitar", "percussion"}  # fmt: skip
    assert set(MEGA_OUTPUTS) <= set(MEGA_ALL)


def test_vocals_keep_lead_and_backing() -> None:
    outputs = _outputs(("lead", "backing"))
    assert tuple(fold(outputs, VOCAL_TABLE)) == VOCAL_STEMS


def test_missing_outputs_leave_their_family_out() -> None:
    outputs = _outputs(("kick", "ride"))
    folded = fold(outputs, DRUMSEP_TABLE)
    assert tuple(folded) == ("kick", "cymbals")
    assert np.array_equal(folded["cymbals"], outputs["ride"])


def test_output_sum_adds_every_output() -> None:
    outputs = _outputs(("a", "b", "c"))
    assert np.allclose(output_sum(outputs), outputs["a"] + outputs["b"] + outputs["c"])


def test_names_per_parent_and_display_order() -> None:
    assert detail_names("drums") == DRUM_STEMS
    assert detail_names("vocals") == VOCAL_STEMS
    assert detail_names("piano") == INSTRUMENT_STEMS
    assert display_order("drums", {"rest", "snare", "kick"}) == ["kick", "snare", "rest"]
    assert display_order("guitar", {"synth", "brass"}) == ["brass", "synth"]


def test_bass_is_not_splittable() -> None:
    assert not is_splittable("bass")
    assert all(is_splittable(s) for s in ("vocals", "drums", "guitar", "piano", "other"))
