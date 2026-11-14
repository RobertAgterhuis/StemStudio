"""Tests of the fake separator used with STEMSTUDIO_SEPARATOR=fake (T056)."""

from pathlib import Path

import numpy as np
import soundfile as sf

from stemstudio_worker.separation.fake import FakeSeparator

SIX = ["vocals", "drums", "bass", "guitar", "piano", "other"]


def test_writes_six_sample_aligned_flac_stems(mix_wav: Path, tmp_path: Path) -> None:
    out_dir = tmp_path / "stems"
    out_dir.mkdir()

    result = FakeSeparator().separate(mix_wav, out_dir, lambda f, s: None)

    assert result.model == "fake"
    assert list(result.stems) == SIX
    mix, _ = sf.read(mix_wav, dtype="int16")
    for name, path in result.stems.items():
        assert path == out_dir / f"{name}.flac"
        info = sf.info(path)
        assert (info.format, info.subtype) == ("FLAC", "PCM_16")
        assert (info.samplerate, info.channels) == (44_100, 2)
        stem, _ = sf.read(path, dtype="int16")
        np.testing.assert_array_equal(stem, mix)


def test_reports_non_decreasing_progress_up_to_one(mix_wav: Path, tmp_path: Path) -> None:
    reports: list[tuple[float, str]] = []

    FakeSeparator().separate(mix_wav, tmp_path, lambda f, s: reports.append((f, s)))

    fractions = [f for f, _ in reports]
    assert fractions == sorted(fractions)
    assert fractions[-1] == 1.0
    assert {s for _, s in reports} == {"Separating stems"}
