"""Tests for stem summing, range cut and levels (009 T018, FR-008)."""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.audio.levels import limit_peak, peak_dbfs
from stemstudio_generator.audio.mix import (
    AudioClip,
    cut_range,
    read_audio,
    sum_stems,
    write_wav,
)
from stemstudio_generator.ports import InvalidInputError

from ..signals import noise, stereo, tone, write


def _clip(samples: np.ndarray, rate: int = 48_000) -> AudioClip:
    return AudioClip(samples.astype(np.float32), rate)


def test_read_audio_returns_frames_by_channels(tmp_path: Path) -> None:
    path = write(tmp_path / "a.flac", stereo(tone(1.0)), 48_000)

    clip = read_audio(path)

    assert clip.samples.shape == (48_000, 2)
    assert clip.samples.dtype == np.float32
    assert (clip.sample_rate, clip.frames, clip.channels) == (48_000, 48_000, 2)
    assert clip.duration_seconds == pytest.approx(1.0)


def test_read_audio_keeps_mono_as_one_channel(tmp_path: Path) -> None:
    clip = read_audio(write(tmp_path / "m.wav", tone(0.5, 44_100), 44_100, "PCM_16"))

    assert clip.samples.shape == (22_050, 1)


def test_read_audio_refuses_unreadable_files(tmp_path: Path) -> None:
    bad = tmp_path / "bad.flac"
    bad.write_bytes(b"not audio at all")

    with pytest.raises(InvalidInputError, match="could not be read"):
        read_audio(bad)


def test_one_stem_sums_to_itself() -> None:
    stem = _clip(stereo(tone(1.0, amplitude=0.25)))

    result = sum_stems([stem])

    np.testing.assert_allclose(result.clip.samples, stem.samples, atol=1e-7)
    assert result.clipped is False


def test_six_stems_sum_at_unity_gain() -> None:
    stems = [_clip(stereo(tone(1.0, freq=110.0 * (i + 1), amplitude=0.1))) for i in range(6)]

    result = sum_stems(stems)

    expected = np.sum([s.samples for s in stems], axis=0)
    np.testing.assert_allclose(result.clip.samples, expected, atol=1e-6)
    assert result.clip.sample_rate == 48_000


def test_mono_stems_are_spread_to_stereo() -> None:
    result = sum_stems([_clip(tone(1.0)[:, None]), _clip(stereo(tone(1.0)))])

    assert result.clip.channels == 2
    np.testing.assert_allclose(result.clip.samples[:, 0], 2 * tone(1.0), atol=1e-6)


def test_lengths_within_one_frame_are_trimmed_to_the_shortest() -> None:
    long = _clip(stereo(tone(1.0))[:48_001])
    short = _clip(stereo(tone(1.0))[:48_000])

    assert sum_stems([long, short]).clip.frames == 48_000


def test_lengths_differing_by_more_than_one_frame_are_refused() -> None:
    with pytest.raises(InvalidInputError, match="same length"):
        sum_stems([_clip(stereo(tone(1.0))), _clip(stereo(tone(1.0))[:47_998])])


def test_sample_rate_mismatch_is_refused() -> None:
    with pytest.raises(InvalidInputError, match="same sample rate"):
        sum_stems([_clip(stereo(tone(1.0))), _clip(stereo(tone(1.0, 44_100)), 44_100)])


@pytest.mark.parametrize("count", [0, 7])
def test_stem_count_outside_one_to_six_is_refused(count: int) -> None:
    stems = [_clip(stereo(tone(0.1)))] * count

    with pytest.raises(InvalidInputError, match="1 to 6 stems"):
        sum_stems(stems)


def test_more_than_two_channels_are_refused() -> None:
    with pytest.raises(InvalidInputError, match="mono or stereo"):
        sum_stems([_clip(np.zeros((100, 3)))])


def test_clipping_sum_is_detected_and_limited_to_full_scale() -> None:
    loud = _clip(stereo(tone(1.0, amplitude=0.8)))

    result = sum_stems([loud, loud])

    assert result.clipped is True
    assert float(np.max(np.abs(result.clip.samples))) == pytest.approx(1.0, abs=1e-6)


def test_cut_range_returns_the_frames_of_the_range() -> None:
    samples = np.arange(48_000 * 20, dtype=np.float32).reshape(-1, 1) / 1e7
    clip = _clip(samples)

    cut = cut_range(clip, 5.0, 15.5)

    assert cut.frames == round(10.5 * 48_000)
    assert cut.samples[0, 0] == samples[5 * 48_000, 0]


def test_cut_range_tolerates_an_end_one_frame_past_the_source() -> None:
    clip = _clip(stereo(tone(10.0)))

    assert cut_range(clip, 0.0, 10.0 + 1 / 48_000).frames == clip.frames


def test_cut_range_after_the_source_end_is_refused() -> None:
    with pytest.raises(InvalidInputError, match="after the end"):
        cut_range(_clip(stereo(tone(10.0))), 0.0, 10.5)


def test_write_wav_round_trips_float_samples_without_clipping(tmp_path: Path) -> None:
    clip = _clip(stereo(noise(0.5, amplitude=1.0)) * 1.5)

    path = write_wav(tmp_path / "sum.wav", clip)

    data, rate = sf.read(path, dtype="float32", always_2d=True)
    assert rate == 48_000
    np.testing.assert_array_equal(data, clip.samples)


def test_peak_dbfs_of_half_scale_is_minus_six() -> None:
    assert peak_dbfs(tone(1.0, amplitude=0.5)) == pytest.approx(-6.0206, abs=1e-3)


def test_peak_dbfs_of_silence_is_the_floor_and_never_above_zero() -> None:
    assert peak_dbfs(np.zeros(10, dtype=np.float32)) == -120.0
    assert peak_dbfs(np.full(10, 2.0, dtype=np.float32)) == 0.0


def test_limit_peak_scales_down_to_the_ceiling_only_when_needed() -> None:
    quiet = tone(0.1, amplitude=0.5)
    loud = tone(0.1, amplitude=2.0)

    same, quiet_limited = limit_peak(quiet, -1.0)
    scaled, loud_limited = limit_peak(loud, -1.0)

    assert quiet_limited is False
    np.testing.assert_array_equal(same, quiet)
    assert loud_limited is True
    assert peak_dbfs(scaled) == pytest.approx(-1.0, abs=1e-4)
    assert scaled.dtype == np.float32
