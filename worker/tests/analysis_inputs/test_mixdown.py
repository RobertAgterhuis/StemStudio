"""Harmonic mixdown of library stems (016 T035, research R-04, ports.HarmonicMixer)."""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf

from stemstudio_worker.analysis_inputs.mixdown import SoundfileHarmonicMixer
from stemstudio_worker.ports import (
    CHORD_SAMPLE_RATE,
    PITCH_SAMPLE_RATE,
    AnalysisError,
    InputNotFoundError,
    InputPathError,
)

SR = 44_100


def _tone(hz: float, seconds: float, amplitude: float) -> npt.NDArray[np.float64]:
    t = np.arange(round(seconds * SR)) / SR
    return amplitude * np.sin(2 * np.pi * hz * t)


def _flac(path: Path, data: npt.NDArray[np.float64]) -> Path:
    sf.write(str(path), data, SR, format="FLAC", subtype="PCM_24")
    return path


def _stereo(mono: npt.NDArray[np.float64]) -> npt.NDArray[np.float64]:
    return np.column_stack((mono, mono))


@pytest.mark.parametrize("rate", [CHORD_SAMPLE_RATE, PITCH_SAMPLE_RATE])
def test_one_stereo_stem_becomes_mono_at_the_requested_rate(tmp_path: Path, rate: int) -> None:
    stem = _flac(tmp_path / "bass.flac", _stereo(_tone(110.0, 2.0, 0.5)))
    audio = SoundfileHarmonicMixer().mix([stem], rate)
    assert audio.dtype == np.float32
    assert audio.ndim == 1
    assert len(audio) == pytest.approx(2.0 * rate, abs=2)
    assert float(np.max(np.abs(audio))) == pytest.approx(0.5, abs=0.02)


def test_stems_of_different_lengths_are_summed_to_the_longest(tmp_path: Path) -> None:
    short = _flac(tmp_path / "guitar.flac", _stereo(_tone(220.0, 1.0, 0.2)))
    long = _flac(tmp_path / "piano.flac", _tone(330.0, 3.0, 0.2))
    audio = SoundfileHarmonicMixer().mix([short, long], CHORD_SAMPLE_RATE)
    assert len(audio) == pytest.approx(3.0 * CHORD_SAMPLE_RATE, abs=2)
    head = audio[: CHORD_SAMPLE_RATE // 2]
    tail = audio[2 * CHORD_SAMPLE_RATE :]
    assert float(np.max(np.abs(head))) > float(np.max(np.abs(tail))) + 0.1


def test_four_stems_are_summed(tmp_path: Path) -> None:
    stems = [_flac(tmp_path / f"s{k}.flac", _stereo(np.full(SR, 0.1))) for k in range(4)]
    audio = SoundfileHarmonicMixer().mix(stems, CHORD_SAMPLE_RATE)
    middle = audio[CHORD_SAMPLE_RATE // 4 : CHORD_SAMPLE_RATE // 2]
    assert np.allclose(middle, 0.4, atol=0.01)


def test_a_loud_sum_is_scaled_below_full_scale(tmp_path: Path) -> None:
    stems = [_flac(tmp_path / f"s{k}.flac", _tone(220.0, 1.0, 0.9)) for k in range(3)]
    audio = SoundfileHarmonicMixer().mix(stems, CHORD_SAMPLE_RATE)
    assert float(np.max(np.abs(audio))) <= 1.0
    assert float(np.max(np.abs(audio))) == pytest.approx(1.0, abs=0.01)


def test_a_quiet_sum_is_not_amplified(tmp_path: Path) -> None:
    stem = _flac(tmp_path / "other.flac", _tone(220.0, 1.0, 0.05))
    audio = SoundfileHarmonicMixer().mix([stem], CHORD_SAMPLE_RATE)
    assert float(np.max(np.abs(audio))) == pytest.approx(0.05, abs=0.005)


def test_a_missing_file_raises_input_not_found(tmp_path: Path) -> None:
    present = _flac(tmp_path / "bass.flac", _tone(110.0, 1.0, 0.5))
    with pytest.raises(InputNotFoundError, match=r"guitar\.flac"):
        SoundfileHarmonicMixer().mix([present, tmp_path / "guitar.flac"], CHORD_SAMPLE_RATE)


def test_an_unreadable_file_raises_an_analysis_error(tmp_path: Path) -> None:
    broken = tmp_path / "bass.flac"
    broken.write_bytes(b"fLaC\x00\x00\x00\x22not really flac")
    with pytest.raises(AnalysisError, match="could not be read"):
        SoundfileHarmonicMixer().mix([broken], CHORD_SAMPLE_RATE)


def test_paths_inside_the_allowed_root_are_read(tmp_path: Path) -> None:
    stem = _flac(tmp_path / "bass.flac", _tone(110.0, 1.0, 0.5))
    audio = SoundfileHarmonicMixer(allowed_root=tmp_path).mix([stem], CHORD_SAMPLE_RATE)
    assert len(audio) > 0


@pytest.mark.parametrize("escape", ["../outside.flac", "sub/../../outside.flac"])
def test_paths_outside_the_allowed_root_are_refused(tmp_path: Path, escape: str) -> None:
    root = tmp_path / "library"
    root.mkdir()
    (root / "sub").mkdir()
    _flac(tmp_path / "outside.flac", _tone(110.0, 1.0, 0.5))
    with pytest.raises(InputPathError):
        SoundfileHarmonicMixer(allowed_root=root).mix([root / escape], CHORD_SAMPLE_RATE)


def test_an_absolute_path_elsewhere_is_refused(tmp_path: Path) -> None:
    root = tmp_path / "library"
    root.mkdir()
    other = _flac(tmp_path / "other.flac", _tone(110.0, 1.0, 0.5))
    with pytest.raises(InputPathError):
        SoundfileHarmonicMixer(allowed_root=root).mix([other], CHORD_SAMPLE_RATE)


@pytest.mark.parametrize("count", [0, 5])
def test_the_stem_count_must_be_one_to_four(tmp_path: Path, count: int) -> None:
    stems = [_flac(tmp_path / f"s{k}.flac", _tone(110.0, 0.5, 0.1)) for k in range(count)]
    with pytest.raises(ValueError, match="1-4 stems"):
        SoundfileHarmonicMixer().mix(stems, CHORD_SAMPLE_RATE)


def test_an_empty_stem_gives_silence_of_the_other_stems_length(tmp_path: Path) -> None:
    empty = tmp_path / "empty.flac"
    sf.write(str(empty), np.zeros((0, 2)), SR, format="FLAC")
    other = _flac(tmp_path / "other.flac", _tone(110.0, 1.0, 0.5))
    audio = SoundfileHarmonicMixer().mix([empty, other], CHORD_SAMPLE_RATE)
    assert len(audio) == pytest.approx(CHORD_SAMPLE_RATE, abs=2)


def test_a_stem_already_at_the_target_rate_is_not_resampled(tmp_path: Path) -> None:
    path = tmp_path / "pitch.flac"
    data = np.linspace(-0.5, 0.5, PITCH_SAMPLE_RATE)
    sf.write(str(path), data, PITCH_SAMPLE_RATE, format="FLAC", subtype="PCM_24")
    audio = SoundfileHarmonicMixer().mix([path], PITCH_SAMPLE_RATE)
    assert np.allclose(audio, data, atol=1e-6)
