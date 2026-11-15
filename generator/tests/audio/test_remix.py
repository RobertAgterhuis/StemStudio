"""Tests for resampling and the voice-conversion remix (010 T012, FR-008)."""

import numpy as np
import pytest

from stemstudio_generator.audio.levels import db_to_gain, peak_dbfs
from stemstudio_generator.audio.mix import AudioClip
from stemstudio_generator.audio.remix import (
    MIX_CEILING_DBFS,
    align_length,
    remix,
    upsample_vocal,
)
from stemstudio_generator.audio.resample import resample
from stemstudio_generator.ports import InvalidInputError

from ..signals import stereo, tone

INNER = slice(2_000, -2_000)


def _vocal(seconds: float = 2.0, amplitude: float = 0.5) -> AudioClip:
    return AudioClip(tone(seconds, 24_000, 300.0, amplitude)[:, None], 24_000)


def test_resample_24k_to_48k_doubles_the_frames_and_keeps_the_tone() -> None:
    out = resample(tone(1.0, 24_000, 440.0)[:, None], 24_000, 48_000)

    assert out.shape == (48_000, 1)
    assert out.dtype == np.float32
    np.testing.assert_allclose(out[INNER, 0], tone(1.0, 48_000, 440.0)[INNER], atol=2e-3)


def test_resample_44k1_to_48k_rounds_the_frame_count() -> None:
    out = resample(stereo(tone(1.5, 44_100, 1_000.0)), 44_100, 48_000)

    assert out.shape == (72_000, 2)
    np.testing.assert_allclose(out[INNER, 1], tone(1.5, 48_000, 1_000.0)[INNER], atol=5e-3)


def test_resample_to_the_same_rate_and_of_nothing_returns_the_input() -> None:
    samples = stereo(tone(0.1))
    empty = np.zeros((0, 2), dtype=np.float32)

    assert resample(samples, 48_000, 48_000) is samples
    assert resample(empty, 24_000, 48_000).shape == (0, 2)


def test_upsample_vocal_is_48k_stereo() -> None:
    vocal = upsample_vocal(_vocal(1.0))

    assert (vocal.sample_rate, vocal.frames, vocal.channels) == (48_000, 48_000, 2)
    np.testing.assert_array_equal(vocal.samples[:, 0], vocal.samples[:, 1])


def test_align_length_pads_with_silence_or_trims() -> None:
    samples = stereo(tone(0.01))

    padded = align_length(samples, 600)
    trimmed = align_length(samples, 100)

    assert padded.shape == (600, 2)
    assert np.all(padded[480:] == 0)
    np.testing.assert_array_equal(trimmed, samples[:100])


def test_remix_without_accompaniment_is_the_upsampled_vocal() -> None:
    result = remix(_vocal(), [], 0.0)

    assert (result.mix.sample_rate, result.mix.frames, result.mix.channels) == (48_000, 96_000, 2)
    np.testing.assert_allclose(result.mix.samples, result.vocal.samples, atol=1e-6)
    assert result.limited is False


def test_remix_sums_accompaniment_at_unity_and_the_vocal_with_its_gain() -> None:
    drums = AudioClip(stereo(tone(2.0, 44_100, 100.0, 0.1)), 44_100)
    bass = AudioClip(stereo(tone(2.0, 44_100, 60.0, 0.1)), 44_100)

    result = remix(_vocal(), [drums, bass], -6.0)

    accompaniment = resample(drums.samples + bass.samples, 44_100, 48_000)
    expected = accompaniment + result.vocal.samples * db_to_gain(-6.0)
    np.testing.assert_allclose(result.mix.samples[INNER], expected[INNER], atol=1e-5)


def test_remix_aligns_the_vocal_to_the_accompaniment_length() -> None:
    accompaniment = AudioClip(stereo(tone(3.0, 48_000, 80.0, 0.1)), 48_000)

    result = remix(_vocal(2.0), [accompaniment], 0.0)

    assert result.mix.frames == result.vocal.frames == 144_000
    assert np.all(result.vocal.samples[96_000:] == 0)


def test_remix_limits_peaks_to_minus_one_dbfs() -> None:
    loud = AudioClip(stereo(tone(2.0, 48_000, 300.0, 0.9)), 48_000)

    result = remix(_vocal(2.0, 0.9), [loud] * 5, 6.0)

    assert result.limited is True
    assert peak_dbfs(result.mix.samples) == pytest.approx(MIX_CEILING_DBFS, abs=1e-3)
    assert MIX_CEILING_DBFS == -1.0
    assert peak_dbfs(result.vocal.samples) <= MIX_CEILING_DBFS + 1e-6


def test_remix_refuses_more_than_five_accompaniment_stems() -> None:
    stem = AudioClip(stereo(tone(2.0, 48_000)), 48_000)

    with pytest.raises(InvalidInputError, match="0 to 5"):
        remix(_vocal(), [stem] * 6, 0.0)
