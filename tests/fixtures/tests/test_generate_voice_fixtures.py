"""Tests of the synthetic own-voice samples (010:T009): lengths, voicing and the check outcomes."""

from __future__ import annotations

from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_voice_fixtures as gen
from generate_lyrics_fixtures import Syllable

GENERATOR = gen

FRAME = 2048
HOP = 1024
SILENCE_RMS = 0.01
PERIODICITY = 0.6
MIN_LAG = 44_100 // 800
MAX_LAG = 44_100 // 70


def _read(out_dir: Path, name: str) -> tuple[npt.NDArray[np.float64], int]:
    data, rate = sf.read(str(out_dir / name), dtype="float64")
    return np.asarray(data, dtype=np.float64), int(rate)


def _is_voiced(frame: npt.NDArray[np.float64]) -> bool:
    """Reference pitch check: normalised autocorrelation peak in the 70-800 Hz lag range."""
    if np.sqrt(np.mean(frame**2)) < SILENCE_RMS:
        return False
    centred = frame - frame.mean()
    corr = np.correlate(centred, centred, mode="full")[FRAME - 1 :]
    return bool(np.max(corr[MIN_LAG:MAX_LAG]) / corr[0] > PERIODICITY)


def measured_voiced_ratio(signal: npt.NDArray[np.float64]) -> float:
    """Share of 46 ms frames that are voiced (silence counts as unvoiced, like RMVPE's ratio)."""
    starts = range(0, len(signal) - FRAME, HOP)
    return float(np.mean([_is_voiced(signal[s : s + FRAME]) for s in starts]))


def _expected_check(seconds: float, ratio: float, limits: dict[str, float]) -> str:
    if seconds < limits["minSeconds"]:
        return "too-short"
    if seconds > limits["maxSeconds"]:
        return "too-long"
    return "ok" if ratio >= limits["minVoicedRatio"] else "unvoiced"


def test_every_file_is_mono_pcm16_of_its_length(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    for name, facts in manifest["fixtures"].items():
        info = sf.info(str(out_dir / name))
        assert (info.channels, info.samplerate, info.subtype) == (1, 44_100, "PCM_16")
        assert info.frames == round(facts["durationSeconds"] * 44_100)


@pytest.mark.parametrize("name", [f.name for f in gen.CATALOGUE])
def test_expected_check_follows_from_length_and_measured_voicing(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    signal, _ = _read(out_dir, name)
    facts = manifest["fixtures"][name]
    ratio = measured_voiced_ratio(signal)
    assert (
        _expected_check(facts["durationSeconds"], ratio, manifest["limits"])
        == facts["expectedCheck"]
    )


def test_own_voice_is_clearly_voiced_and_noise_is_not(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, _ = generated
    assert measured_voiced_ratio(_read(out_dir, "own-voice-20s.wav")[0]) >= 0.7
    assert measured_voiced_ratio(_read(out_dir, "unvoiced-20s.wav")[0]) <= 0.05


def test_designed_voiced_share_is_close_to_the_measured_one(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    signal, _ = _read(out_dir, "own-voice-20s.wav")
    design = manifest["fixtures"]["own-voice-20s.wav"]["voicedShare"]
    assert abs(measured_voiced_ratio(signal) - design) < 0.15


def test_samples_peak_at_the_target_level(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    for name in manifest["fixtures"]:
        signal, _ = _read(out_dir, name)
        assert np.max(np.abs(signal)) == pytest.approx(gen.PEAK, abs=1e-3)


def test_melody_never_runs_past_the_tail() -> None:
    segments = gen.melody_segments(gen.PHRASE, 6.0)
    assert segments[0].start_s == pytest.approx(gen.LEAD_IN_S)
    assert segments[-1].end_s <= 6.0 - gen.TAIL_S


def test_a_phrase_longer_than_the_file_gives_no_segments() -> None:
    long_note = (Syllable("AA", 40, 60),)
    assert gen.melody_segments(long_note, 10.0) == []


def test_voiced_share_counts_only_voiced_phones() -> None:
    segments = gen.melody_segments((Syllable("S AA", 2, 60),), 4.0)
    unvoiced = sum(s.end_s - s.start_s for s in segments if s.phone == "S")
    vowel = sum(s.end_s - s.start_s for s in segments if s.phone == "AA")
    assert unvoiced > 0
    assert gen.voiced_share(segments, 4.0) == pytest.approx(round(vowel / 4.0, 3))


def test_manifest_records_licence_and_limits(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert manifest["licence"] == "CC0-1.0"
    assert manifest["limits"] == {"minSeconds": 10.0, "maxSeconds": 60.0, "minVoicedRatio": 0.5}
    assert {f["expectedCheck"] for f in manifest["fixtures"].values()} == {
        "ok",
        "too-short",
        "too-long",
        "unvoiced",
    }
