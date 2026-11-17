"""Regression: render jobs convert recorded takes losslessly (015 T011, research R-08).

A take is a 24-bit WAV asset that starts with zero padding (the latency offset). A render job
with gain 1, pan 0, master 1, FLAC 24-bit and no dither must write exactly the input integers
(a mono take on both channels), keep the leading zeros, and refuse an all-zero take.
"""

from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.ports import RenderError
from stemstudio_worker.render.executor import BlockRenderExecutor
from stemstudio_worker.render.paths import MountedLibrary
from stemstudio_worker.schemas import SILENT_RENDER_TEXT, RenderJobRequest

_ASSET = "7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f"
_PATH = f"assets/{_ASSET}/take.wav"
_PADDING = 4410  # 100 ms of leading zeros
_RATE = 44_100

Ints = np.ndarray[Any, np.dtype[np.int32]]


def _take(channels: int, frames: int = 3 * _RATE, seed: int = 5) -> Ints:
    """24-bit integers (as int32 << 8 like libsndfile) with zero padding at the start."""
    rng = np.random.default_rng(seed)
    body = rng.integers(-(2**22), 2**22, size=(frames, channels), dtype=np.int32)
    body[:_PADDING] = 0
    return body << 8


def _write(root: Path, data: Ints) -> None:
    file = root / _PATH
    file.parent.mkdir(parents=True, exist_ok=True)
    sf.write(file, data if data.shape[1] > 1 else data[:, 0], _RATE, format="WAV",
             subtype="PCM_24")  # fmt: skip


def _render(root: Path, out_dir: Path) -> Ints:
    request = RenderJobRequest.model_validate({
        "inputs": [{"path": _PATH, "gain": 1, "pan": 0}], "masterGain": 1,
        "output": {"format": "flac", "bitDepth": 24, "dither": False},
    })  # fmt: skip
    out_dir.mkdir(parents=True, exist_ok=True)
    result = BlockRenderExecutor(MountedLibrary(root), 0.25).render(
        request, out_dir, lambda _: None
    )
    decoded, rate = sf.read(out_dir / result.file_name, dtype="int32", always_2d=True)
    assert rate == _RATE
    return np.asarray(decoded)


@pytest.mark.parametrize("channels", [1, 2])
def test_a_padded_24_bit_take_renders_to_identical_flac_integers(
    tmp_path: Path, channels: int
) -> None:
    take = _take(channels)
    _write(tmp_path / "library", take)

    decoded = _render(tmp_path / "library", tmp_path / "out")

    expected = np.repeat(take, 2, axis=1) if channels == 1 else take
    assert decoded.shape == expected.shape
    assert np.array_equal(decoded, expected)
    assert not decoded[:_PADDING].any()


def test_an_all_zero_take_has_nothing_to_export(tmp_path: Path) -> None:
    _write(tmp_path / "library", np.zeros((_RATE, 1), dtype=np.int32))

    with pytest.raises(RenderError, match=SILENT_RENDER_TEXT):
        _render(tmp_path / "library", tmp_path / "out")
