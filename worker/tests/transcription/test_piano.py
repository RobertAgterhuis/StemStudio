"""Tests of the piano CRNN transcriber with a fake torch runtime (008 T024, FR-008, FR-011).

The model runs through the injected ``DeviceRunner`` with ``PIANO_GPU_NEED`` on 10 s segments
(half-overlapping, as the upstream inference) with progress per segment; the regression
post-processor turns the outputs into notes; weights come from the model store.
"""

import ast
import importlib
import sys
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.ports import (
    PIANO_GPU_NEED,
    PIANO_WEIGHTS,
    DeviceUse,
    GpuNeed,
    TranscriptionCancelledError,
)
from stemstudio_worker.schemas import PIANO_ATTRIBUTION, RenderRange
from stemstudio_worker.transcription import piano
from stemstudio_worker.transcription.piano import SEGMENT_SAMPLES, PianoTranscriber, TorchRuntime

from .helpers import RecordingModels, ramp, write_audio

FPS = 100
KEYS = ("reg_onset_output", "reg_offset_output", "frame_output", "velocity_output")
PEDAL = ("reg_pedal_onset_output", "reg_pedal_offset_output", "pedal_frame_output")


def _segment_outputs(note: tuple[int, int, int] | None) -> dict[str, npt.NDArray[np.float32]]:
    """Outputs of one 10 s segment (1001 frames); ``note`` = (pitch, first, last frame)."""
    frames = SEGMENT_SAMPLES // 160 + 1
    out = {key: np.zeros((frames, 88), dtype=np.float32) for key in KEYS}
    out |= {key: np.zeros((frames, 1), dtype=np.float32) for key in PEDAL}
    if note is not None:
        pitch, first, last = note
        index = pitch - 21
        out["frame_output"][first:last, index] = 0.9
        out["reg_onset_output"][first - 2 : first + 3, index] = [0.2, 0.6, 1.0, 0.6, 0.2]
        out["reg_offset_output"][last - 2 : last + 3, index] = [0.2, 0.6, 1.0, 0.6, 0.2]
        out["velocity_output"][first:last, index] = 0.5
    return out


class FakeRuntime:
    """Stands in for torch: records loads and devices, returns scripted segment outputs."""

    def __init__(self, notes: dict[int, tuple[int, int, int]] | None = None) -> None:
        self.notes = notes if notes is not None else {0: (60, 100, 200)}
        self.loads: list[tuple[Path, str]] = []
        self.segments: list[tuple[npt.NDArray[np.float32], str]] = []
        self.error: Exception | None = None

    def load(self, checkpoint: Path, device: str) -> Any:
        self.loads.append((checkpoint, device))
        return "model"

    def infer(self, model: Any, segment: npt.NDArray[np.float32], device: str) -> dict[str, Any]:
        if self.error is not None:
            raise self.error
        index = len(self.segments)
        self.segments.append((segment, device))
        return _segment_outputs(self.notes.get(index))


class OomOnceRunner(FakeDeviceRunner):
    """A fake GPU whose first step fails over to the CPU with a reason."""

    def run[T](self, work: Any, need: GpuNeed) -> tuple[T, DeviceUse]:
        self.need = need
        return super().run(work, need)


def _transcriber(
    tmp_path: Path, runtime: FakeRuntime, mode: str = "cpu"
) -> tuple[PianoTranscriber, OomOnceRunner]:
    devices = OomOnceRunner(mode)  # type: ignore[arg-type]
    return PianoTranscriber(RecordingModels(tmp_path), devices, runtime), devices


def test_a_note_comes_out_of_the_regression_post_processor(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(4.0))
    runtime = FakeRuntime()
    transcriber, devices = _transcriber(tmp_path, runtime)
    reports: list[float] = []

    result = transcriber.transcribe(audio, None, reports.append)

    assert (transcriber.engine, transcriber.attribution) == ("piano-crnn", PIANO_ATTRIBUTION)
    (note,) = result.notes
    assert (note.pitch, round(note.start, 1), round(note.end, 1), round(note.velocity, 1)) == (
        60,
        1.0,
        2.0,
        0.5,
    )
    assert (devices.need, result.device and result.device.kind) == (PIANO_GPU_NEED, "cpu")
    assert runtime.loads == [(tmp_path / f"{PIANO_WEIGHTS.model_id}.bin", "cpu")]
    assert reports == [1.0]


def test_audio_is_cut_into_half_overlapping_10_s_segments_at_16_khz(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(14.0))
    runtime = FakeRuntime({0: (60, 100, 200), 2: (64, 260, 380)})
    transcriber, _devices = _transcriber(tmp_path, runtime)
    reports: list[float] = []

    result = transcriber.transcribe(audio, None, reports.append)

    assert len(runtime.segments) == 3
    assert all(segment.shape == (SEGMENT_SAMPLES,) for segment, _device in runtime.segments)
    first, second = runtime.segments[0][0], runtime.segments[1][0]
    assert np.array_equal(first[SEGMENT_SAMPLES // 2 :], second[: SEGMENT_SAMPLES // 2])
    assert reports == pytest.approx([1 / 3, 2 / 3, 1.0])
    assert [note.pitch for note in result.notes] == [60, 64]
    assert result.notes[1].start == pytest.approx(10.0 + 2.6, abs=0.02)


def test_a_gpu_step_and_a_cpu_fallback_are_reported(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(2.0))
    for mode, kind in (("gpu", "gpu"), ("gpu-oom", "cpu")):
        runtime = FakeRuntime()
        transcriber, _devices = _transcriber(tmp_path, runtime, mode)

        result = transcriber.transcribe(audio, None, lambda f: None)

        assert result.device is not None
        assert result.device.kind == kind
        assert (result.device.fallback_reason is not None) == (mode == "gpu-oom")
        assert runtime.loads[-1][1] == ("cuda" if mode == "gpu" else "cpu")


def test_the_model_is_loaded_once_per_device(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(2.0))
    runtime = FakeRuntime({})
    transcriber, _devices = _transcriber(tmp_path, runtime)

    transcriber.transcribe(audio, None, lambda f: None)
    transcriber.transcribe(audio, None, lambda f: None)
    transcriber.release()
    transcriber.transcribe(audio, None, lambda f: None)

    assert len(runtime.loads) == 2


def test_a_window_is_transcribed_from_its_start(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(6.0))
    runtime = FakeRuntime()
    transcriber, _devices = _transcriber(tmp_path, runtime)

    result = transcriber.transcribe(
        audio, RenderRange(start_seconds=3.0, end_seconds=5.0), lambda f: None
    )

    segment = runtime.segments[0][0]
    assert segment[100] == pytest.approx(0.0, abs=0.02)
    assert np.all(segment[2 * 16_000 + 8 :] == 0)
    assert result.notes[0].start == pytest.approx(1.0, abs=0.02)


def test_a_cancel_from_the_progress_callback_propagates(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(2.0))
    transcriber, _devices = _transcriber(tmp_path, FakeRuntime())

    def cancel(fraction: float) -> None:
        raise TranscriptionCancelledError("deleted")

    with pytest.raises(TranscriptionCancelledError):
        transcriber.transcribe(audio, None, cancel)


def test_model_errors_propagate_so_the_runner_can_retry(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(2.0))
    runtime = FakeRuntime()
    runtime.error = RuntimeError("CUDA out of memory")
    transcriber, _devices = _transcriber(tmp_path, runtime)

    with pytest.raises(RuntimeError, match="out of memory"):
        transcriber.transcribe(audio, None, lambda f: None)


# ------------------------------------------------------------------ the torch runtime


class _TinyModel:
    """Replaces the vendored ``Note_pedal`` (no 20 M parameters in a unit test)."""

    def __init__(self, frames_per_second: int, classes_num: int) -> None:
        import torch  # noqa: PLC0415

        self.args = (frames_per_second, classes_num)
        self.loaded: Any = None
        self.device = "unset"
        self.torch = torch

    def load_state_dict(self, state: Any, strict: bool = False) -> None:
        self.loaded = (state, strict)

    def to(self, device: str) -> "_TinyModel":
        self.device = device
        return self

    def eval(self) -> "_TinyModel":
        return self

    def __call__(self, batch: Any) -> dict[str, Any]:
        frames = batch.shape[1] // 160 + 1
        return {key: self.torch.full((1, frames, 88), 0.25) for key in KEYS}


def _checkpoint(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    """A tiny checkpoint, with the vendored ``Note_pedal`` replaced by :class:`_TinyModel`."""
    import torch  # noqa: PLC0415

    checkpoint = tmp_path / "piano.pth"
    torch.save({"model": {"note_model": {}, "pedal_model": {}}}, checkpoint)
    vendored = "stemstudio_worker.transcription.vendor.piano_transcription.models"
    monkeypatch.setattr(importlib.import_module(vendored), "Note_pedal", _TinyModel)
    return checkpoint


def test_the_torch_runtime_builds_the_vendored_model_and_runs_a_segment(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import torch  # noqa: PLC0415

    checkpoint = _checkpoint(tmp_path, monkeypatch)
    threads = torch.get_num_threads()
    runtime = TorchRuntime(2)

    model = runtime.load(checkpoint, "cpu")
    out = runtime.infer(model, np.zeros(1600, dtype=np.float32), "cpu")
    used = torch.get_num_threads()
    torch.set_num_threads(threads)

    assert (model.args, model.device, model.loaded[1]) == ((100, 88), "cpu", False)
    assert model.loaded[0] == {"note_model": {}, "pedal_model": {}}
    assert out["frame_output"].shape == (11, 88)
    assert isinstance(out["frame_output"], np.ndarray)
    assert used == 2


def test_no_shell_download_matplotlib_audioread_or_mido_in_the_piano_code() -> None:
    folder = Path(piano.__file__).parent
    files = [folder / "piano.py", *sorted((folder / "vendor" / "piano_transcription").glob("*.py"))]
    banned = {"matplotlib", "audioread", "mido"}
    for path in files:
        tree = ast.parse(path.read_text(encoding="utf-8"))
        imported = {alias.name.split(".")[0] for node in ast.walk(tree)
                    if isinstance(node, ast.Import) for alias in node.names}  # fmt: skip
        imported |= {(node.module or "").split(".")[0] for node in ast.walk(tree)
                     if isinstance(node, ast.ImportFrom)}  # fmt: skip
        assert not imported & banned, path
        assert "os.system" not in path.read_text(encoding="utf-8"), path
    for name in banned:
        assert name not in sys.modules or name == "mido"
