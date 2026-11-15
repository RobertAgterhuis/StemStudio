"""Tests of the block-wise multi-input reader (data-model.md 2.2, research.md E-04)."""

from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.ports import RenderError
from stemstudio_worker.render.reader import FrameRange, InputFile, frame_range, open_inputs
from stemstudio_worker.schemas import RenderRange

Samples = np.ndarray[Any, np.dtype[np.float32]]


def _ints(frames: int, channels: int = 2, seed: int = 0) -> np.ndarray[Any, np.dtype[np.int16]]:
    rng = np.random.default_rng(seed)
    return rng.integers(-32768, 32768, size=(frames, channels), dtype=np.int16)


def _flac(
    path: Path, data: np.ndarray[Any, Any], rate: int = 44100, subtype: str = "PCM_16"
) -> InputFile:
    sf.write(path, data, rate, format="FLAC", subtype=subtype)
    return InputFile(name=f"tracks/x/{path.name}", file=path)


def _read_all(inputs: list[InputFile], span: FrameRange, block: int) -> list[Samples]:
    with open_inputs(inputs) as opened:
        parts = list(opened.blocks(span, block))
    return [np.concatenate([part[i] for part in parts]) for i in range(len(inputs))]


def test_frames_is_the_longest_input(tmp_path: Path) -> None:
    inputs = [_flac(tmp_path / "a.flac", _ints(100)), _flac(tmp_path / "b.flac", _ints(250, seed=1))]
    with open_inputs(inputs) as opened:
        assert opened.frames == 250


def test_shorter_inputs_are_padded_with_silence(tmp_path: Path) -> None:
    short, long = _ints(100), _ints(250, seed=1)
    inputs = [_flac(tmp_path / "a.flac", short), _flac(tmp_path / "b.flac", long)]
    first, second = _read_all(inputs, FrameRange(0, 250), 64)
    assert first.shape == second.shape == (250, 2)
    np.testing.assert_array_equal(first[:100], short / np.float32(32768))
    np.testing.assert_array_equal(first[100:], np.zeros((150, 2), dtype=np.float32))
    np.testing.assert_array_equal(second, long / np.float32(32768))


def test_concatenated_blocks_equal_a_whole_file_read(tmp_path: Path) -> None:
    data = _ints(1000)
    inputs = [_flac(tmp_path / "a.flac", data, subtype="PCM_24")]
    whole, _ = sf.read(tmp_path / "a.flac", dtype="float32", always_2d=True)
    (blocked,) = _read_all(inputs, FrameRange(0, 1000), 77)
    assert blocked.dtype == np.dtype(np.float32)
    assert np.array_equal(blocked, whole)


def test_every_block_has_the_block_length_except_the_last(tmp_path: Path) -> None:
    inputs = [_flac(tmp_path / "a.flac", _ints(250))]
    with open_inputs(inputs) as opened:
        lengths = [blocks[0].shape[0] for blocks in opened.blocks(FrameRange(0, 250), 100)]
    assert lengths == [100, 100, 50]


def test_a_range_reads_only_its_frames(tmp_path: Path) -> None:
    data = _ints(500)
    inputs = [_flac(tmp_path / "a.flac", data), _flac(tmp_path / "b.flac", _ints(150, seed=1))]
    first, second = _read_all(inputs, FrameRange(200, 400), 64)
    np.testing.assert_array_equal(first, data[200:400] / np.float32(32768))
    np.testing.assert_array_equal(second, np.zeros((200, 2), dtype=np.float32))


def test_mono_inputs_are_duplicated_to_stereo(tmp_path: Path) -> None:
    mono = _ints(80, channels=1)
    (read,) = _read_all([_flac(tmp_path / "m.flac", mono)], FrameRange(0, 80), 32)
    expected = np.repeat(mono / np.float32(32768), 2, axis=1)
    assert read.shape == (80, 2)
    np.testing.assert_array_equal(read, expected)


def test_other_sample_rates_are_rejected(tmp_path: Path) -> None:
    item = _flac(tmp_path / "a.flac", _ints(48), rate=48000)
    with pytest.raises(RenderError) as caught, open_inputs([item]):
        pass
    assert str(caught.value) == f"Unsupported sample rate 48000 Hz in {item.name}"


def test_more_than_two_channels_are_rejected(tmp_path: Path) -> None:
    item = _flac(tmp_path / "a.flac", _ints(48, channels=3))
    with pytest.raises(RenderError, match=r"Unsupported channel count 3 in tracks/x/a\.flac"):
        open_inputs([item]).__enter__()


def test_a_file_that_is_not_audio_is_a_readable_error(tmp_path: Path) -> None:
    bad = tmp_path / "bad.flac"
    bad.write_bytes(b"not audio at all")
    with pytest.raises(RenderError, match=r"^Cannot read tracks/x/bad\.flac$"):
        open_inputs([InputFile(name="tracks/x/bad.flac", file=bad)]).__enter__()


def test_files_opened_before_a_failure_are_closed(tmp_path: Path) -> None:
    good = _flac(tmp_path / "good.flac", _ints(10))
    bad = _flac(tmp_path / "bad.flac", _ints(10), rate=48000)
    with pytest.raises(RenderError), open_inputs([good, bad]):
        pass
    good.file.unlink()
    assert not good.file.exists()


def test_files_are_closed_after_the_block(tmp_path: Path) -> None:
    item = _flac(tmp_path / "a.flac", _ints(10))
    _read_all([item], FrameRange(0, 10), 4)
    item.file.unlink()
    assert not item.file.exists()


def test_frame_range_without_a_range_is_everything() -> None:
    assert frame_range(None, 1234) == FrameRange(0, 1234)


def test_frame_range_converts_seconds_and_clamps_the_end() -> None:
    clamped = frame_range(RenderRange(start_seconds=1.0, end_seconds=99.0), 100_000)
    exact = frame_range(RenderRange(start_seconds=0.5, end_seconds=1.25), 100_000)
    assert (clamped, exact) == (FrameRange(44100, 100_000), FrameRange(22050, 55125))


def test_frame_range_rounds_to_the_nearest_sample() -> None:
    span = frame_range(RenderRange(start_seconds=0.00001, end_seconds=0.50002), 100_000)
    assert span == FrameRange(0, 22051)


def test_a_range_that_starts_after_the_inputs_fails() -> None:
    with pytest.raises(RenderError, match="The range starts after the end of the inputs"):
        frame_range(RenderRange(start_seconds=3.0, end_seconds=4.0), 44100)


def test_frame_range_count() -> None:
    assert FrameRange(10, 25).count == 15
