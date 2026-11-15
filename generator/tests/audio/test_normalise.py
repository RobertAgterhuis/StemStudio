"""Tests for the voice-sample normalisation and the PCM hash (010 T012, FR-008)."""

import hashlib
from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.audio.mix import AudioClip
from stemstudio_generator.audio.normalise import (
    SAMPLE_RATE,
    normalise_sample,
    pcm16,
    pcm_sha256,
    write_sample_flac,
)

from ..signals import stereo, tone


def test_stereo_44k1_becomes_48k_mono() -> None:
    clip = AudioClip(stereo(tone(2.0, 44_100, 220.0)), 44_100)

    mono = normalise_sample(clip)

    assert SAMPLE_RATE == 48_000
    assert mono.shape == (96_000,)
    assert mono.dtype == np.float32
    np.testing.assert_allclose(mono[2_000:-2_000], tone(2.0, 48_000, 220.0)[2_000:-2_000],
                               atol=5e-3)  # fmt: skip


def test_channels_are_averaged() -> None:
    left = tone(1.0, 48_000, 220.0, 0.4)
    clip = AudioClip(np.column_stack([left, np.zeros_like(left)]), 48_000)

    np.testing.assert_allclose(normalise_sample(clip), left / 2, atol=1e-7)


def test_pcm16_is_little_endian_int16_clipped_to_full_scale() -> None:
    data = pcm16(np.array([0.0, 0.5, -1.0, 2.0, -2.0], dtype=np.float32))

    assert np.frombuffer(data, "<i2").tolist() == [0, 16384, -32767, 32767, -32767]


def test_pcm_sha256_hashes_the_int16_pcm() -> None:
    mono = tone(1.0)

    assert pcm_sha256(mono) == hashlib.sha256(pcm16(mono)).hexdigest()


def test_written_sample_is_48k_mono_16_bit_flac_with_the_same_pcm_hash(tmp_path: Path) -> None:
    mono = normalise_sample(AudioClip(stereo(tone(1.5, 44_100)), 44_100))

    path = write_sample_flac(tmp_path / "checks" / "sample.flac", mono)

    info = sf.info(path)
    assert (info.format, info.subtype, info.samplerate, info.channels) == (
        "FLAC", "PCM_16", 48_000, 1,
    )  # fmt: skip
    decoded, _ = sf.read(path, dtype="int16")
    assert hashlib.sha256(decoded.astype("<i2").tobytes()).hexdigest() == pcm_sha256(mono)
    assert info.duration == pytest.approx(1.5)
