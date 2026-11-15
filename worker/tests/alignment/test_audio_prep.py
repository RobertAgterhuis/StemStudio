"""Tests of the vocal stem preparation: mono float32 at 16 kHz via soxr (006 T040)."""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.alignment.audio_prep import VocalAudioLoader
from stemstudio_worker.ports import ALIGN_SAMPLE_RATE, AlignmentError
from stemstudio_worker.schemas import VOCAL_STEM_MISSING_TEXT, VOCAL_STEM_SILENT_TEXT


def _write(path: Path, left: np.ndarray, right: np.ndarray, rate: int = 44_100) -> Path:
    sf.write(path, np.stack([left, right], axis=1), rate, format="FLAC", subtype="PCM_16")
    return path


def _tone(seconds: float = 1.0, rate: int = 44_100, amplitude: float = 0.5) -> np.ndarray:
    t = np.arange(int(seconds * rate)) / rate
    return amplitude * np.sin(2 * np.pi * 440 * t)


def test_a_stereo_stem_becomes_mono_float32_at_16_khz(tmp_path: Path) -> None:
    tone = _tone()
    path = _write(tmp_path / "vocals.flac", tone, tone)

    audio = VocalAudioLoader().load(path)

    assert audio.dtype == np.float32
    assert audio.ndim == 1
    assert abs(audio.size - ALIGN_SAMPLE_RATE) <= 1
    assert 0.45 < float(np.max(np.abs(audio))) < 0.55


def test_the_channels_are_averaged(tmp_path: Path) -> None:
    tone = _tone()
    path = _write(tmp_path / "vocals.flac", tone, np.zeros_like(tone))

    audio = VocalAudioLoader().load(path)

    assert 0.2 < float(np.max(np.abs(audio))) < 0.3


def test_16_khz_audio_is_not_resampled(tmp_path: Path) -> None:
    tone = _tone(rate=ALIGN_SAMPLE_RATE)
    path = _write(tmp_path / "vocals.flac", tone, tone, ALIGN_SAMPLE_RATE)

    assert VocalAudioLoader().load(path).size == tone.size


def test_a_silent_stem_is_refused(tmp_path: Path) -> None:
    quiet = np.full(44_100, 1e-4)
    path = _write(tmp_path / "vocals.flac", quiet, quiet)

    with pytest.raises(AlignmentError, match=VOCAL_STEM_SILENT_TEXT):
        VocalAudioLoader().load(path)


def test_an_unreadable_stem_counts_as_missing(tmp_path: Path) -> None:
    broken = tmp_path / "vocals.flac"
    broken.write_bytes(b"not audio")

    for path in (broken, tmp_path / "absent.flac"):
        with pytest.raises(AlignmentError, match="no longer available"):
            VocalAudioLoader().load(path)
    assert "no longer available" in VOCAL_STEM_MISSING_TEXT
