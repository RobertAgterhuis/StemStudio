"""Tests of quantisation, TPDF dither and the FLAC/WAV/MP3 encoders (FR-004, SC-004)."""

from pathlib import Path
from typing import Any, Literal

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.render.dither import TpdfDither, dither_seed, full_scale, quantize
from stemstudio_worker.render.encoders import open_encoder
from stemstudio_worker.render.reader import FrameRange, InputFile, open_inputs
from stemstudio_worker.schemas import RenderMetadata, RenderOutput

Ints = np.ndarray[Any, np.dtype[np.int32]]
_MP3_FRAME = 1152


def _sine(frames: int, amplitude: float = 0.5) -> np.ndarray[Any, np.dtype[np.float32]]:
    t = np.arange(frames) / 44100
    mono = (np.sin(2 * np.pi * 440 * t) * amplitude).astype(np.float32)
    return np.stack([mono, mono * np.float32(0.5)], axis=1)


def _encode(path: Path, output: RenderOutput, ints: Ints, metadata: RenderMetadata) -> None:
    encoder = open_encoder(output, metadata, path)
    half = ints.shape[0] // 2
    encoder.write(ints[:half])
    encoder.write(ints[half:])
    encoder.close()


# ------------------------------------------------------------------ quantisation and dither


def test_full_scale_per_bit_depth() -> None:
    assert (full_scale(16), full_scale(24)) == (32768, 8388608)


def test_quantize_without_dither_rounds_and_clamps() -> None:
    block = np.array([[1.0, -1.0], [0.5, -0.5], [3 / 32768, -2.6 / 32768]], dtype=np.float32)
    assert quantize(block, 16, None).tolist() == [[32767, -32768], [16384, -16384], [3, -3]]


def test_quantize_returns_int32_at_24_bits() -> None:
    ints = quantize(np.array([[0.25, -1.0]], dtype=np.float32), 24, None)
    assert ints.dtype == np.dtype(np.int32)
    assert ints.tolist() == [[2097152, -8388608]]


def test_quantize_is_exact_for_16_bit_input_samples() -> None:
    samples = np.arange(-32768, 32768, 7, dtype=np.int32)
    block = np.stack([samples, samples[::-1]], axis=1).astype(np.float32) / np.float32(32768)
    np.testing.assert_array_equal(quantize(block, 16, None), np.stack([samples, samples[::-1]], 1))


def test_tpdf_dither_is_deterministic_per_seed() -> None:
    block = _sine(1000)
    first = quantize(block, 16, TpdfDither(7))
    second = quantize(block, 16, TpdfDither(7))
    other = quantize(block, 16, TpdfDither(8))
    assert np.array_equal(first, second)
    assert not np.array_equal(first, other)


def test_tpdf_dither_changes_samples_by_at_most_one_lsb() -> None:
    block = _sine(5000)
    difference = quantize(block, 16, TpdfDither(1)) - quantize(block, 16, None)
    assert int(np.abs(difference).max()) == 1
    assert np.count_nonzero(difference) > 0


def test_tpdf_noise_is_triangular_within_one_lsb() -> None:
    noise = TpdfDither(3).noise((100_000, 2))
    assert noise.shape == (100_000, 2)
    assert -1.0 < float(noise.min()) < -0.9
    assert 0.9 < float(noise.max()) < 1.0
    assert abs(float(noise.mean())) < 0.01
    assert abs(float(np.mean(np.abs(noise) < 0.5)) - 0.75) < 0.01


def test_dither_seed_is_stable_and_depends_on_the_key() -> None:
    assert dither_seed("a" * 32) == dither_seed("a" * 32)
    assert dither_seed("a" * 32) != dither_seed("b" * 32)
    assert 0 <= dither_seed("job") < 2**64


# ------------------------------------------------------------------ FLAC / WAV


@pytest.mark.parametrize(
    ("fmt", "bits", "subtype"),
    [
        ("flac", 16, "PCM_16"),
        ("flac", 24, "PCM_24"),
        ("wav", 16, "PCM_16"),
        ("wav", 24, "PCM_24"),
    ],
)
def test_pcm_outputs_use_the_requested_subtype_and_keep_every_sample(
    tmp_path: Path, fmt: Literal["flac", "wav"], bits: Literal[16, 24], subtype: str
) -> None:
    path = tmp_path / f"output.{fmt}"
    ints = quantize(_sine(3000, 0.9), bits, None)
    output = RenderOutput(format=fmt, bit_depth=bits)
    _encode(path, output, ints, RenderMetadata())
    info = sf.info(path)
    read, _ = sf.read(path, dtype="int32", always_2d=True)
    assert (info.format, info.subtype, info.samplerate, info.channels, info.frames) == (
        fmt.upper(),
        subtype,
        44100,
        2,
        3000,
    )
    np.testing.assert_array_equal(read >> (32 - bits), ints)


def test_open_encoder_reports_its_bit_depth(tmp_path: Path) -> None:
    pcm = open_encoder(RenderOutput(format="wav", bit_depth=24), RenderMetadata(), tmp_path / "a")
    mp3 = open_encoder(RenderOutput(format="mp3", bit_depth=24), RenderMetadata(), tmp_path / "b")
    pcm.close()
    mp3.close()
    assert (pcm.bits, mp3.bits) == (24, 16)


@pytest.mark.parametrize("fmt", ["flac", "wav"])
def test_pcm_outputs_carry_the_tags(tmp_path: Path, fmt: Literal["flac", "wav"]) -> None:
    path = tmp_path / f"output.{fmt}"
    metadata = RenderMetadata(title="My song - drums", artist="The Band", comment="drums, raw")
    _encode(path, RenderOutput(format=fmt), quantize(_sine(100), 24, None), metadata)
    with sf.SoundFile(path) as file:
        tags = (file.title, file.artist, file.comment)
    assert tags == ("My song - drums", "The Band", "drums, raw")


def test_control_characters_are_stripped_and_empty_tags_skipped(tmp_path: Path) -> None:
    path = tmp_path / "output.flac"
    metadata = RenderMetadata(title="Line\none\x00\t\x7f\x85", artist="\x01\x02", comment=None)
    _encode(path, RenderOutput(format="flac"), quantize(_sine(100), 24, None), metadata)
    with sf.SoundFile(path) as file:
        tags = (file.title, file.artist, file.comment)
    assert tags == ("Lineone", "", "")


def test_raw_16_bit_input_written_without_dither_is_sample_identical(tmp_path: Path) -> None:
    rng = np.random.default_rng(11)
    source = rng.integers(-32768, 32768, size=(5000, 2), dtype=np.int16)
    source[0] = (-32768, 32767)
    stem = tmp_path / "vocals.flac"
    sf.write(stem, source, 44100, format="FLAC", subtype="PCM_16")
    output = RenderOutput(format="flac", bit_depth=16, dither=False)
    encoder = open_encoder(output, RenderMetadata(), tmp_path / "output.flac")
    with open_inputs([InputFile(name="vocals.flac", file=stem)]) as opened:
        for (block,) in opened.blocks(FrameRange(0, 5000), 1024):
            encoder.write(quantize(block, 16, None))
    encoder.close()
    written, _ = sf.read(tmp_path / "output.flac", dtype="int16", always_2d=True)
    np.testing.assert_array_equal(written, source)


# ------------------------------------------------------------------ MP3


@pytest.mark.parametrize("kbps", [192, 256, 320])
def test_mp3_is_cbr_at_the_requested_bitrate_and_decodes_to_the_length(
    tmp_path: Path, kbps: Literal[192, 256, 320]
) -> None:
    frames = 44100 * 2
    path = tmp_path / "output.mp3"
    output = RenderOutput(format="mp3", mp3_bitrate_kbps=kbps)
    _encode(path, output, quantize(_sine(frames), 16, None), RenderMetadata(title="ignored"))
    decoded, rate = sf.read(path, dtype="float32", always_2d=True)
    # LAME adds its encoder delay and pads to whole frames; libsndfile does not trim them.
    assert (rate, decoded.shape[1]) == (44100, 2)
    assert frames <= decoded.shape[0] <= frames + 2 * _MP3_FRAME
    assert path.stat().st_size == pytest.approx(kbps * 1000 / 8 * 2, rel=0.05)


def test_mp3_carries_the_audio(tmp_path: Path) -> None:
    path = tmp_path / "output.mp3"
    _encode(path, RenderOutput(format="mp3"), quantize(_sine(44100), 16, None), RenderMetadata())
    decoded, _ = sf.read(path, dtype="float32", always_2d=True)
    peaks = np.abs(decoded).max(axis=0)
    assert peaks[0] == pytest.approx(0.5, abs=0.05)
    assert peaks[1] == pytest.approx(0.25, abs=0.05)
