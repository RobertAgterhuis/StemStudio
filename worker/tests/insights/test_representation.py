"""Analysis representation (T014, data-model 4.1): block-wise mono 22,050 Hz mel power."""

import tracemalloc
from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.insights import representation as rep
from stemstudio_worker.ports import QUALITY_SAMPLE_RATE, QualityAnalysisError

from .synthetic import SR, song, write_stem


def _sine(freq: float, seconds: float, amplitude: float = 0.5, rate: int = SR) -> np.ndarray:
    t = np.arange(round(seconds * rate)) / rate
    return amplitude * np.sin(2 * np.pi * freq * t)


def test_constants_match_the_data_model() -> None:
    assert rep.N_FFT == 2048
    assert rep.HOP == 512
    assert rep.BANDS == 64
    assert rep.FRAME_SECONDS == pytest.approx(512 / QUALITY_SAMPLE_RATE)


def test_filterbank_has_64_bands_from_30_hz_to_11_khz() -> None:
    bank = rep.mel_filterbank()
    freqs = np.fft.rfftfreq(rep.N_FFT, 1 / QUALITY_SAMPLE_RATE)
    assert bank.shape == (rep.BANDS, rep.N_FFT // 2 + 1)
    used = freqs[bank.sum(axis=0) > 0]
    assert used.min() >= 30.0
    assert used.max() <= 11_000.0
    assert np.all(bank.sum(axis=1) > 0)  # no empty band


def test_shape_and_frame_count_follow_the_hop(tmp_path: Path) -> None:
    path = write_stem(tmp_path / "a.flac", _sine(440.0, 3.0))
    spec = rep.read_spectrogram(path)
    assert spec.power.shape[1] == rep.BANDS
    assert spec.power.dtype == np.float32
    expected = 3.0 * QUALITY_SAMPLE_RATE / rep.HOP
    assert abs(spec.frames - expected) <= 5
    assert spec.seconds == pytest.approx(spec.frames * rep.FRAME_SECONDS)
    assert spec.frame_db.shape == (spec.frames,)


def test_resamples_to_22050_hz(tmp_path: Path) -> None:
    low = rep.read_spectrogram(write_stem(tmp_path / "low.flac", _sine(5_000.0, 2.0)))
    high = rep.read_spectrogram(write_stem(tmp_path / "high.flac", _sine(15_000.0, 2.0)))
    centres = rep.band_centres()
    peak_band = int(np.argmax(low.power.sum(axis=0)))
    assert abs(centres[peak_band] - 5_000.0) < 400.0
    # 15 kHz is above the 11.025 kHz Nyquist of the analysis rate: removed by the resampler.
    assert high.power.sum() < 1e-6 * low.power.sum()


def test_other_input_rates_are_resampled_too(tmp_path: Path) -> None:
    native = rep.read_spectrogram(write_stem(tmp_path / "n.flac", _sine(1_000.0, 2.0, rate=48_000),
                                             rate=48_000))  # fmt: skip
    reference = rep.read_spectrogram(write_stem(tmp_path / "r.flac", _sine(1_000.0, 2.0)))
    assert abs(native.frames - reference.frames) <= 3
    assert np.argmax(native.power.sum(axis=0)) == np.argmax(reference.power.sum(axis=0))


def test_channels_are_mixed_to_mono(tmp_path: Path) -> None:
    tone = _sine(1_000.0, 2.0)
    opposite = tmp_path / "opposite.flac"
    sf.write(opposite, np.stack([tone, -tone], axis=1), SR, format="FLAC", subtype="PCM_24")
    left_only = tmp_path / "left.flac"
    sf.write(left_only, np.stack([tone, 0 * tone], axis=1), SR, format="FLAC", subtype="PCM_24")
    both = rep.read_spectrogram(write_stem(tmp_path / "both.flac", tone))
    assert rep.read_spectrogram(opposite).power.sum() < 1e-6 * both.power.sum()
    ratio = rep.read_spectrogram(left_only).power.sum() / both.power.sum()
    assert ratio == pytest.approx(0.25, rel=0.01)
    mono = rep.read_spectrogram(write_stem(tmp_path / "mono.flac", tone, channels=1))
    np.testing.assert_allclose(mono.power, both.power, rtol=1e-3, atol=1e-9)


def test_reads_in_blocks_and_matches_a_single_block_read(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    path = write_stem(tmp_path / "a.flac", song(seconds=6.0)["guitar"])
    whole = rep.read_spectrogram(path)
    monkeypatch.setattr(rep, "BLOCK_FRAMES", 4_000)
    blocky = rep.read_spectrogram(path)
    assert blocky.frames == whole.frames
    np.testing.assert_allclose(blocky.power, whole.power, rtol=1e-3, atol=1e-7)


def test_mix_is_the_sum_of_the_six_standard_stems(tmp_path: Path) -> None:
    stems = song(seconds=6.0)
    paths = {name: write_stem(tmp_path / f"{name}.flac", sound) for name, sound in stems.items()}
    total = write_stem(tmp_path / "total.flac", sum(stems.values()))
    separate, mix = rep.read_standard(paths)
    assert set(separate) == set(stems)
    expected = rep.read_spectrogram(total)
    assert mix.frames == expected.frames
    np.testing.assert_allclose(mix.power, expected.power, rtol=2e-3, atol=1e-7)
    np.testing.assert_allclose(separate["drums"].power,
                               rep.read_spectrogram(paths["drums"]).power, rtol=1e-4, atol=1e-9)  # fmt: skip


def test_mix_pads_a_shorter_stem_with_silence(tmp_path: Path) -> None:
    stems = song(seconds=4.0)
    stems["piano"] = stems["piano"][: SR * 2]
    paths = {name: write_stem(tmp_path / f"{name}.flac", sound) for name, sound in stems.items()}
    separate, mix = rep.read_standard(paths)
    assert separate["piano"].frames < mix.frames
    assert mix.frames == separate["vocals"].frames


def test_mixed_input_rates_are_refused(tmp_path: Path) -> None:
    stems = song(seconds=2.0)
    paths = {name: write_stem(tmp_path / f"{name}.flac", sound) for name, sound in stems.items()}
    paths["bass"] = write_stem(tmp_path / "bass48.flac", _sine(60.0, 2.0, rate=48_000), rate=48_000)
    with pytest.raises(QualityAnalysisError, match="sample rate"):
        rep.read_standard(paths)


def test_level_and_activity_floor(tmp_path: Path) -> None:
    # -10, -45 and -55 dBFS segments (sine RMS = amplitude - 3 dB): peak -10 -> floor -50.
    parts = [_sine(500.0, 1.0, amplitude=10 ** (level / 20) * np.sqrt(2)) for level in (-10, -45, -55)]
    spec = rep.read_spectrogram(write_stem(tmp_path / "a.flac", np.concatenate(parts)))
    per_second = round(1 / rep.FRAME_SECONDS)
    middle = slice(per_second // 4, per_second * 3 // 4)
    for index, level in enumerate((-10, -45, -55)):
        frames = spec.frame_db[index * per_second :][middle]
        assert np.median(frames) == pytest.approx(level, abs=0.5)
    active = spec.active()
    assert active[: per_second - 3].all()
    assert active[per_second + 3 : 2 * per_second - 3].all()
    assert not active[2 * per_second + 3 :].any()


def test_activity_floor_is_40_db_below_a_loud_peak(tmp_path: Path) -> None:
    parts = [_sine(500.0, 1.0, amplitude=10 ** (level / 20) * np.sqrt(2)) for level in (-3, -38, -46)]
    spec = rep.read_spectrogram(write_stem(tmp_path / "a.flac", np.concatenate(parts), channels=1))
    per_second = round(1 / rep.FRAME_SECONDS)
    active = spec.active()
    assert active[per_second + 3 : 2 * per_second - 3].all()  # -38 > -43
    assert not active[2 * per_second + 3 :].any()  # -46 < -43


def test_silence_has_no_active_frames(tmp_path: Path) -> None:
    spec = rep.read_spectrogram(write_stem(tmp_path / "s.flac", np.zeros(SR * 2)))
    assert spec.frames > 0
    assert not spec.active().any()
    assert spec.band_db().min() == pytest.approx(-120.0)


def test_empty_file_gives_zero_frames(tmp_path: Path) -> None:
    spec = rep.read_spectrogram(write_stem(tmp_path / "e.flac", np.zeros(0)))
    assert spec.frames == 0
    assert spec.power.shape == (0, rep.BANDS)
    assert not spec.active().any()


def test_a_file_shorter_than_one_window_gives_one_frame(tmp_path: Path) -> None:
    spec = rep.read_spectrogram(write_stem(tmp_path / "short.flac", _sine(440.0, 0.03)))
    assert spec.frames >= 1


def test_unreadable_file_raises_quality_error(tmp_path: Path) -> None:
    bad = tmp_path / "bad.flac"
    bad.write_bytes(b"not a flac file")
    with pytest.raises(QualityAnalysisError, match=r"bad\.flac"):
        rep.read_spectrogram(bad)


def test_memory_stays_bounded_for_a_15_minute_stem(tmp_path: Path) -> None:
    seconds = 15 * 60
    one = _sine(220.0, 10.0, amplitude=0.3)
    path = tmp_path / "long.flac"
    with sf.SoundFile(path, "w", SR, 2, format="FLAC", subtype="PCM_16") as out:
        block = np.stack([one, one], axis=1)
        for _ in range(seconds // 10):
            out.write(block)
    tracemalloc.start()
    try:
        spec = rep.read_spectrogram(path)
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    assert spec.seconds == pytest.approx(seconds, abs=1.0)
    # The decoded stereo float32 audio alone would be about 318 MB; the mel power is about 10 MB.
    assert peak < 48 * 1024 * 1024
