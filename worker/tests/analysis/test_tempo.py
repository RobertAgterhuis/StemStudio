"""Unit tests of the tempo, downbeat and confidence helpers on hand-made envelopes."""

import numpy as np
import pytest

from stemstudio_worker.analysis import tempo
from stemstudio_worker.analysis.downbeats import Meter, detect_meter, downbeat_times
from stemstudio_worker.analysis.reliability import pulse_confidence
from stemstudio_worker.analysis.tempo import OnsetEnvelope, estimate_bpm


@pytest.mark.parametrize("beats", [[], [1.0]])
def test_bpm_needs_two_beats(beats: list[float]) -> None:
    assert estimate_bpm(np.asarray(beats, dtype=np.float64)) == 0.0


def test_bpm_is_capped_at_the_contract_maximum() -> None:
    assert estimate_bpm(np.asarray([0.0, 0.01, 0.02])) == tempo.MAX_BPM


def test_refine_interpolates_between_frames() -> None:
    values = np.asarray([0.0, 1.0, 3.0, 3.0, 1.0, 0.0])

    assert tempo._refine(values, 2) == pytest.approx(2.5)


def test_refine_keeps_a_peak_on_the_envelope_edge() -> None:
    assert tempo._refine(np.asarray([5.0, 1.0, 0.0]), 1) == 0.0


def test_refine_keeps_a_flat_plateau() -> None:
    assert tempo._refine(np.full(10, 2.0), 5) == 1.0


def test_trim_drops_weak_edges_only() -> None:
    times = np.asarray([0.0, 1.0, 2.0, 3.0, 4.0, 5.0])
    strengths = np.asarray([0.01, 1.0, 0.01, 1.0, 1.0, 0.01])

    assert tempo._trim_weak_edges(times, strengths).tolist() == [1.0, 2.0, 3.0, 4.0]


def test_trim_handles_empty_and_all_zero_input() -> None:
    empty = np.asarray([], dtype=np.float64)

    assert len(tempo._trim_weak_edges(empty, empty)) == 0
    assert len(tempo._trim_weak_edges(np.asarray([1.0, 2.0]), np.zeros(2))) == 2


def test_strength_at_clamps_to_the_envelope() -> None:
    envelope = OnsetEnvelope(np.asarray([1.0, 2.0, 3.0]), frame_rate=10.0)

    assert envelope.strength_at(np.asarray([-1.0, 0.1, 9.0])).tolist() == [1.0, 2.0, 3.0]


def test_meter_with_too_few_beats_defaults_to_four() -> None:
    assert detect_meter(np.asarray([1.0, 0.2, 0.2])) == Meter(4, 0)


def test_meter_finds_a_late_first_downbeat() -> None:
    strengths = np.asarray([0.2, 0.2, 1.0] * 4)

    meter = detect_meter(strengths)

    assert meter == Meter(3, 2)
    assert downbeat_times(np.arange(12, dtype=np.float64), meter) == [2.0, 5.0, 8.0, 11.0]


def test_pulse_confidence_is_zero_without_a_tempo_or_signal() -> None:
    pulse = OnsetEnvelope(np.tile([1.0, 0.0, 0.0, 0.0], 8), frame_rate=4.0)
    flat = OnsetEnvelope(np.ones(32), frame_rate=4.0)

    assert pulse_confidence(pulse, 0.0) == 0.0
    assert pulse_confidence(flat, 60.0) == 0.0
    assert pulse_confidence(pulse, 60.0) == pytest.approx(1.0, abs=0.2)
