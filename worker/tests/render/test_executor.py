"""Tests of the two-pass block renderer (data-model.md 3, FR-005, SC-004, SC-005)."""

import hashlib
import math
from pathlib import Path
from typing import Any, Literal

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.config import RenderSettings, WorkerSettings
from stemstudio_worker.ports import (
    InputNotFoundError,
    InputPathError,
    LibraryReader,
    RenderCancelledError,
    RenderError,
    RenderExecutor,
)
from stemstudio_worker.render import create_library_reader, create_render_executor
from stemstudio_worker.render.executor import BlockRenderExecutor
from stemstudio_worker.render.paths import MountedLibrary
from stemstudio_worker.schemas import (
    LIBRARY_UNAVAILABLE_TEXT,
    SILENT_RENDER_TEXT,
    LibraryHealth,
    RenderJobRequest,
    RenderResult,
)

_ID = "3f2b8c1d-9e4a-4f6b-8a7c-5d3e2f1a0b9c"
_BLOCK_SECONDS = 400 / 44100
_TARGET_DBFS = -0.1

Ints16 = np.ndarray[Any, np.dtype[np.int16]]


def _stem(name: str) -> str:
    return f"tracks/{_ID}/stems/{name}.flac"


def _write_stem(root: Path, name: str, data: Ints16) -> Ints16:
    file = root / _stem(name)
    file.parent.mkdir(parents=True, exist_ok=True)
    sf.write(file, data, 44100, format="FLAC", subtype="PCM_16")
    return data


def _noise(frames: int, amplitude: int, seed: int = 0) -> Ints16:
    rng = np.random.default_rng(seed)
    return rng.integers(-amplitude, amplitude + 1, size=(frames, 2), dtype=np.int16)


def _request(*inputs: tuple[str, float, float], **extra: Any) -> RenderJobRequest:
    body: dict[str, Any] = {
        "inputs": [{"path": _stem(n), "gain": g, "pan": p} for n, g, p in inputs],
        "masterGain": extra.pop("master", 1.0),
        "output": {"format": "flac", "bitDepth": 24, "dither": False} | extra.pop("output", {}),
    }
    return RenderJobRequest.model_validate(body | extra)


def _executor(root: Path) -> BlockRenderExecutor:
    return BlockRenderExecutor(MountedLibrary(root), _BLOCK_SECONDS)


def _out(tmp_path: Path, name: str = "0" * 32) -> Path:
    out_dir = tmp_path / "render-jobs" / name
    out_dir.mkdir(parents=True)
    return out_dir


def _render(
    root: Path, request: RenderJobRequest, out_dir: Path
) -> tuple[RenderResult, list[float]]:
    seen: list[float] = []
    return _executor(root).render(request, out_dir, seen.append), seen


def _read_ints(path: Path) -> np.ndarray[Any, np.dtype[np.int32]]:
    data, _ = sf.read(path, dtype="int32", always_2d=True)
    return data


# ------------------------------------------------------------------ protocol and factories


def test_factories_build_the_mounted_library_and_the_executor(tmp_path: Path) -> None:
    settings = WorkerSettings(render=RenderSettings(library_dir=tmp_path, block_seconds=2.0))
    library: LibraryReader = create_library_reader(settings)
    executor: RenderExecutor = create_render_executor(settings, library)
    assert library.status() == LibraryHealth(readable=True, reason=None)
    assert isinstance(executor, BlockRenderExecutor)
    assert (executor.library, executor.block_frames) == (library, 88200)


def test_the_factory_builds_its_own_library_when_none_is_given() -> None:
    executor = create_render_executor(WorkerSettings())
    assert isinstance(executor, BlockRenderExecutor)
    assert executor.library.status().reason == LIBRARY_UNAVAILABLE_TEXT
    assert executor.block_frames == 441000


def test_a_tiny_block_length_is_at_least_one_frame(tmp_path: Path) -> None:
    assert BlockRenderExecutor(MountedLibrary(tmp_path), 1e-9).block_frames == 1


# ------------------------------------------------------------------ validate


def test_validate_resolves_every_input(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(10, 100))
    _write_stem(tmp_path, "drums", _noise(10, 100))
    _executor(tmp_path).validate(_request(("bass", 1, 0), ("drums", 1, 0)))


def test_validate_rejects_a_path_off_the_allow_list(tmp_path: Path) -> None:
    request = _request(("bass", 1, 0))
    request.inputs[0].path = "tracks/../stemstudio.db"
    with pytest.raises(InputPathError):
        _executor(tmp_path).validate(request)


def test_validate_names_a_missing_input(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(10, 100))
    with pytest.raises(InputNotFoundError) as caught:
        _executor(tmp_path).validate(_request(("bass", 1, 0), ("piano", 1, 0)))
    assert caught.value.path == _stem("piano")


def test_render_without_a_library_fails_readably(tmp_path: Path) -> None:
    executor = BlockRenderExecutor(MountedLibrary(None), _BLOCK_SECONDS)
    with pytest.raises(RenderError, match=LIBRARY_UNAVAILABLE_TEXT):
        executor.render(_request(("bass", 1, 0)), _out(tmp_path), lambda fraction: None)


# ------------------------------------------------------------------ passes and progress


def test_two_passes_report_progress_from_0_to_half_and_half_to_1(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(2000, 1000))
    _, seen = _render(tmp_path, _request(("bass", 1, 0)), _out(tmp_path))
    np.testing.assert_allclose(seen, [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0])
    assert (seen[4], seen[-1]) == (0.5, 1.0)


def test_a_mix_is_written_per_the_mixing_law(tmp_path: Path) -> None:
    bass = _write_stem(tmp_path, "bass", _noise(1500, 8000))
    drums = _write_stem(tmp_path, "drums", _noise(1000, 8000, seed=1))
    request = _request(("bass", 0.8, -0.4), ("drums", 0.5, 0.5), master=0.7)
    out_dir = _out(tmp_path)
    result, _ = _render(tmp_path, request, out_dir)
    written = _read_ints(out_dir / "output.flac") >> 8
    b, d = bass / 32768.0, np.concatenate([drums, np.zeros((500, 2))]) / 32768.0
    m1 = np.array([[1.0, math.cos(0.3 * math.pi)], [0.0, math.sin(0.3 * math.pi)]])
    m2 = np.array([[math.cos(0.25 * math.pi), 0.0], [math.sin(0.25 * math.pi), 1.0]])
    expected = ((b * 0.8) @ m1.T + (d * 0.5) @ m2.T) * 0.7 * 8388608
    assert result.applied_gain_db == 0.0
    assert np.abs(written - expected).max() <= 2


# ------------------------------------------------------------------ clip protection


def test_a_peak_above_full_scale_is_scaled_to_minus_0_1_dbfs(tmp_path: Path) -> None:
    loud = np.full((1200, 2), 30000, dtype=np.int16)
    loud[::2] *= -1
    _write_stem(tmp_path, "bass", loud)
    _write_stem(tmp_path, "drums", loud)
    out_dir = _out(tmp_path)
    result, _ = _render(tmp_path, _request(("bass", 1, 0), ("drums", 1, 0)), out_dir)
    peak = 60000 / 32768
    written = _read_ints(out_dir / "output.flac") >> 8
    assert result.applied_gain_db == pytest.approx(_TARGET_DBFS - 20 * math.log10(peak), abs=1e-6)
    assert result.peak_dbfs == pytest.approx(_TARGET_DBFS, abs=1e-4)
    assert int(np.abs(written).max()) < 8388607


def test_a_peak_at_or_below_full_scale_is_not_changed(tmp_path: Path) -> None:
    edge = np.full((1000, 2), -32768, dtype=np.int16)
    _write_stem(tmp_path, "bass", edge)
    out_dir = _out(tmp_path)
    result, _ = _render(tmp_path, _request(("bass", 1, 0), output={"bitDepth": 16}), out_dir)
    assert (result.applied_gain_db, result.peak_dbfs) == (0.0, 0.0)
    assert int(_read_ints(out_dir / "output.flac").min() >> 16) == -32768


@pytest.mark.parametrize("gain", [0.0, 1.0])
def test_an_all_silent_render_fails_and_writes_nothing(tmp_path: Path, gain: float) -> None:
    _write_stem(tmp_path, "bass", np.zeros((900, 2), dtype=np.int16) if gain else _noise(9, 9))
    out_dir = _out(tmp_path)
    with pytest.raises(RenderError) as caught:
        _render(tmp_path, _request(("bass", gain, 0)), out_dir)
    assert str(caught.value) == SILENT_RENDER_TEXT
    assert list(out_dir.iterdir()) == []


def test_a_peak_that_quantises_to_zero_reports_the_floor(tmp_path: Path) -> None:
    tiny = np.ones((500, 2), dtype=np.int16)
    _write_stem(tmp_path, "bass", tiny)
    request = _request(("bass", 0.1, 0), output={"bitDepth": 16})
    result, _ = _render(tmp_path, request, _out(tmp_path))
    assert result.peak_dbfs == -150.0


# ------------------------------------------------------------------ cancellation and failures


@pytest.mark.parametrize("cancel_at", [0.2, 0.7])
def test_cancellation_between_blocks_stops_and_removes_the_partial_file(
    tmp_path: Path, cancel_at: float
) -> None:
    _write_stem(tmp_path, "bass", _noise(2000, 1000))
    out_dir = _out(tmp_path)
    seen: list[float] = []

    def progress(fraction: float, /) -> None:
        seen.append(fraction)
        if fraction >= cancel_at:
            raise RenderCancelledError

    with pytest.raises(RenderCancelledError):
        _executor(tmp_path).render(_request(("bass", 1, 0)), out_dir, progress)
    assert seen[-1] == pytest.approx(cancel_at)
    assert list(out_dir.iterdir()) == []


def test_an_output_that_cannot_be_written_is_a_readable_error(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(100, 1000))
    with pytest.raises(RenderError, match="The export file could not be written"):
        _render(tmp_path, _request(("bass", 1, 0)), tmp_path / "missing-dir")


# ------------------------------------------------------------------ results and formats


@pytest.mark.parametrize(
    ("fmt", "content_type"), [("flac", "audio/flac"), ("wav", "audio/wav"), ("mp3", "audio/mpeg")]
)
def test_the_result_matches_the_written_file(
    tmp_path: Path, fmt: Literal["flac", "wav", "mp3"], content_type: str
) -> None:
    _write_stem(tmp_path, "vocals", _noise(44100, 12000))
    out_dir = _out(tmp_path)
    request = _request(("vocals", 0.9, 0.25), output={"format": fmt, "dither": True})
    result, _ = _render(tmp_path, request, out_dir)
    file = out_dir / f"output.{fmt}"
    assert (result.file_name, result.content_type) == (f"output.{fmt}", content_type)
    assert result.size_bytes == file.stat().st_size
    assert result.sha256 == hashlib.sha256(file.read_bytes()).hexdigest()
    assert result.duration_seconds == pytest.approx(1.0)
    assert (result.sample_rate, result.channels) == (44100, 2)
    assert sorted(p.name for p in out_dir.iterdir()) == [f"output.{fmt}"]


@pytest.mark.parametrize("fmt", ["flac", "wav"])
def test_the_reported_peak_is_the_peak_of_the_written_samples(
    tmp_path: Path, fmt: Literal["flac", "wav"]
) -> None:
    _write_stem(tmp_path, "vocals", _noise(3000, 20000))
    out_dir = _out(tmp_path)
    result, _ = _render(tmp_path, _request(("vocals", 0.7, -0.5), output={"format": fmt}), out_dir)
    peak = int(np.abs(_read_ints(out_dir / f"output.{fmt}")).max()) / 2**31
    assert result.peak_dbfs == pytest.approx(20 * math.log10(peak), abs=1e-9)


def test_a_raw_16_bit_single_stem_is_bit_identical(tmp_path: Path) -> None:
    source = _write_stem(tmp_path, "drums", _noise(5000, 32767))
    source[7] = (-32768, 32767)
    _write_stem(tmp_path, "drums", source)
    out_dir = _out(tmp_path)
    request = _request(("drums", 1, 0), output={"bitDepth": 16, "dither": False})
    _render(tmp_path, request, out_dir)
    written, _ = sf.read(out_dir / "output.flac", dtype="int16", always_2d=True)
    np.testing.assert_array_equal(written, source)


def test_dither_is_reproducible_per_job_and_differs_between_jobs(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(3000, 9000))
    request = _request(("bass", 0.77, 0), output={"bitDepth": 16, "dither": True})
    first, _ = _render(tmp_path, request, _out(tmp_path / "a", "a" * 32))
    again, _ = _render(tmp_path, request, _out(tmp_path / "b", "a" * 32))
    other, _ = _render(tmp_path, request, _out(tmp_path / "c", "c" * 32))
    assert first.sha256 == again.sha256 != other.sha256


def test_a_range_renders_only_its_part_and_clamps_the_end(tmp_path: Path) -> None:
    source = _write_stem(tmp_path, "bass", _noise(44100, 5000))
    out_dir = _out(tmp_path)
    request = _request(("bass", 1, 0), range={"startSeconds": 0.25, "endSeconds": 5.0})
    result, _ = _render(tmp_path, request, out_dir)
    written = _read_ints(out_dir / "output.flac") >> 8
    assert result.duration_seconds == pytest.approx(0.75)
    np.testing.assert_array_equal(written, source[11025:].astype(np.int32) * 256)


def test_tags_are_written(tmp_path: Path) -> None:
    _write_stem(tmp_path, "bass", _noise(500, 5000))
    out_dir = _out(tmp_path)
    request = _request(("bass", 1, 0), metadata={"title": "Song - bass", "artist": "Band"})
    _render(tmp_path, request, out_dir)
    with sf.SoundFile(out_dir / "output.flac") as file:
        assert (file.title, file.artist) == ("Song - bass", "Band")
