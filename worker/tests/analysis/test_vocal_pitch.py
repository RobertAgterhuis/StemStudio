"""Reference melody of the vocal stem: pYIN / YIN, packing, warnings (016 T021, data-model 3.1)."""

import time

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.analysis.vocal_pitch import LibrosaPitchAnalyzer
from stemstudio_worker.ports import PITCH_SAMPLE_RATE, AnalysisCancelledError, NoMelodyError
from stemstudio_worker.schemas import NO_MELODY_TEXT, VocalPitch

SR = PITCH_SAMPLE_RATE
HOP_S = 0.01
STEPS_HZ = (80.0, 130.0, 220.0, 350.0, 520.0, 700.0, 1000.0)
NOTE_S = 0.6
GAP_S = 0.2


def _sung(hz: float, seconds: float) -> npt.NDArray[np.float64]:
    """A sine with two quieter harmonics and 10 ms fades."""
    t = np.arange(round(seconds * SR)) / SR
    tone = sum(a * np.sin(2 * np.pi * k * hz * t) for k, a in ((1, 1.0), (2, 0.4), (3, 0.2)))
    ramp = np.minimum(1.0, np.minimum(np.arange(len(t)), np.arange(len(t))[::-1]) / 160)
    return np.asarray(0.3 * tone * ramp, dtype=np.float64)


def _melody(steps: tuple[float, ...] = STEPS_HZ) -> npt.NDArray[np.float32]:
    gap = np.zeros(round(GAP_S * SR))
    parts = [gap]
    for hz in steps:
        parts += [_sung(hz, NOTE_S), gap]
    return np.concatenate(parts).astype(np.float32)


def _note_frames(index: int) -> slice:
    """Frames of the middle 60 % of note ``index`` of ``_melody``."""
    start = GAP_S + index * (NOTE_S + GAP_S)
    return slice(round((start + 0.2 * NOTE_S) / HOP_S), round((start + 0.8 * NOTE_S) / HOP_S))


def _gap_frames(index: int) -> slice:
    """Frames of the middle of gap ``index`` (the gap before note ``index``)."""
    start = index * (NOTE_S + GAP_S)
    return slice(round((start + 0.06) / HOP_S), round((start + GAP_S - 0.06) / HOP_S))


def _analyze(
    audio: npt.NDArray[np.float32], engine: str = "pyin", source: str = "vocals"
) -> VocalPitch:
    analyzer = LibrosaPitchAnalyzer(engine)  # type: ignore[arg-type]
    return analyzer.analyze(audio, source, lambda _p: None)  # type: ignore[arg-type]


def _cents(hz: float) -> float:
    return 6900 + 1200 * float(np.log2(hz / 440.0))


@pytest.mark.parametrize("engine", ["pyin", "yin"])
def test_sung_steps_from_80_to_1000_hz_are_within_20_cents(engine: str) -> None:
    result = _analyze(_melody(), engine)
    frames = np.asarray(result.frames)
    for index, hz in enumerate(STEPS_HZ):
        voiced = frames[_note_frames(index)]
        assert np.all(voiced > 0), hz
        assert np.all(np.abs(voiced - _cents(hz)) <= 20), hz


@pytest.mark.parametrize("engine", ["pyin", "yin"])
def test_unvoiced_gaps_are_zero(engine: str) -> None:
    frames = np.asarray(_analyze(_melody(), engine).frames)
    for index in range(len(STEPS_HZ) + 1):
        assert not np.any(frames[_gap_frames(index)]), index


def test_frames_are_10_ms_centi_semitones_of_the_stem_length() -> None:
    audio = _melody((440.0,))
    result = _analyze(audio)
    assert result.hop_ms == 10
    assert len(result.frames) == -(-len(audio) // 160)
    assert all(isinstance(f, int) for f in result.frames)
    assert int(np.median(np.asarray(result.frames)[_note_frames(0)])) == pytest.approx(6900, abs=5)


def test_voiced_ratio_and_range_percentiles() -> None:
    result = _analyze(_melody())
    voiced = np.asarray([f for f in result.frames if f > 0])
    assert result.voiced_ratio == pytest.approx(len(voiced) / len(result.frames))
    assert result.range is not None
    assert result.range.low_cents == round(float(np.percentile(voiced, 5)))
    assert result.range.high_cents == round(float(np.percentile(voiced, 95)))
    assert result.range.low_cents < _cents(130.0) < result.range.high_cents


def test_engine_switch_is_recorded() -> None:
    assert _analyze(_melody((220.0,)), "yin").engine == "yin"
    assert _analyze(_melody((220.0,)), "pyin").engine == "pyin"
    assert LibrosaPitchAnalyzer().engine == "pyin"
    assert LibrosaPitchAnalyzer("yin").engine == "yin"


def test_an_unknown_engine_is_refused() -> None:
    with pytest.raises(ValueError, match="pitch engine"):
        LibrosaPitchAnalyzer("crepe")  # type: ignore[arg-type]


@pytest.mark.parametrize("engine", ["pyin", "yin"])
@pytest.mark.parametrize("seconds", [0.0, 2.0])
def test_a_silent_stem_raises_no_melody(engine: str, seconds: float) -> None:
    with pytest.raises(NoMelodyError, match=NO_MELODY_TEXT):
        _analyze(np.zeros(round(seconds * SR), np.float32), engine)


def test_a_stem_without_any_voiced_frame_raises_no_melody() -> None:
    noise = np.random.default_rng(5).uniform(-0.003, 0.003, 2 * SR).astype(np.float32)
    with pytest.raises(NoMelodyError):
        _analyze(noise, "yin")


def test_yin_leaves_loud_breath_noise_unvoiced() -> None:
    """R-06 uses the R-05 voicing rule: RMS >= -45 dBFS *and* minimum difference <= 0.25."""
    breath = np.random.default_rng(7).normal(0.0, 0.05, 2 * SR).astype(np.float32)  # ~ -26 dBFS
    with pytest.raises(NoMelodyError):
        _analyze(breath, "yin")


def test_yin_voices_a_tone_but_not_the_breath_noise_after_it() -> None:
    noise = np.random.default_rng(8).normal(0.0, 0.05, SR)
    audio = np.concatenate([_sung(220.0, 1.0), noise]).astype(np.float32)
    frames = np.asarray(_analyze(audio, "yin").frames)
    assert np.all(frames[20:80] > 0)
    assert np.count_nonzero(frames[120:]) <= 4


def test_little_melody_warns_few_voiced() -> None:
    audio = np.concatenate([np.zeros(10 * SR), _sung(220.0, 0.4)]).astype(np.float32)
    result = _analyze(audio)
    assert [w.code for w in result.warnings] == ["few-voiced"]
    assert result.warnings[0].text == "Very little sung melody was found."


def test_the_lead_detail_stem_warns_detail_bleed() -> None:
    result = _analyze(_melody(), source="vocals.lead")
    assert result.source == "vocals.lead"
    assert [w.code for w in result.warnings] == ["detail-bleed"]


def test_the_vocals_stem_has_no_warning() -> None:
    result = _analyze(_melody())
    assert (result.source, result.warnings) == ("vocals", [])


def test_long_stems_are_analysed_in_chunks_without_seams() -> None:
    audio = np.tile(_sung(196.0, 5.0), 26).astype(np.float32)  # 130 s, more than two chunks
    seen: list[float] = []
    result = LibrosaPitchAnalyzer("yin").analyze(audio, "vocals", seen.append)
    frames = np.asarray(result.frames)
    assert len(frames) == -(-len(audio) // 160)
    assert np.mean(np.abs(frames[frames > 0] - _cents(196.0)) <= 20) >= 0.99
    assert seen == sorted(seen)
    assert (seen[0], seen[-1]) == (0.0, 1.0)
    assert len(seen) >= 5


def test_cancellation_from_progress_propagates() -> None:
    calls: list[float] = []

    def cancel(value: float) -> None:
        calls.append(value)
        if value > 0.05:
            raise AnalysisCancelledError

    with pytest.raises(AnalysisCancelledError):
        LibrosaPitchAnalyzer().analyze(_melody(), "vocals", cancel)


def test_frames_are_capped_at_15_minutes() -> None:
    result = _analyze(np.tile(_sung(220.0, 1.0), 901).astype(np.float32), "yin")
    assert len(result.frames) == 90_000


@pytest.mark.integration
def test_real_pyin_five_minutes() -> None:
    audio = np.tile(_melody(), int(300 / (len(_melody()) / SR)) + 1)[: 300 * SR].astype(np.float32)
    started = time.perf_counter()
    result = _analyze(audio)
    seconds = time.perf_counter() - started
    frames = np.asarray(result.frames)
    assert len(frames) == 30_000
    assert result.voiced_ratio > 0.5
    print(f"pyin: {seconds / 300:.4f} s per audio second")  # noqa: T201 - recorded in the hand-off
