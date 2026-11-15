"""Tests of the basic-pitch ONNX transcriber with a fake ONNX session (008 T022, FR-001).

Audio is resampled to 22,050 Hz mono and windowed as basic-pitch 0.4.0 does (2 s windows, 30
overlapping frames, half an overlap of zeros in front); window outputs are stitched and decoded
with onset 0.6, frame 0.4 and a minimum note length of 127.7 ms (data-model 2.1).
"""

import sys
import types
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest

from stemstudio_worker.ports import (
    BASIC_PITCH_WEIGHTS,
    NoteEvent,
    TranscriptionCancelledError,
    TranscriptionError,
)
from stemstudio_worker.schemas import RenderRange
from stemstudio_worker.transcription.basic_pitch import (
    SAMPLE_RATE,
    WINDOW_SAMPLES,
    BasicPitchTranscriber,
    onnx_session,
)

from .helpers import RecordingModels, ramp, write_audio

FRAMES = 172
"""Output frames of one 2 s window (86 per second)."""

HOP_SECONDS = 256 / SAMPLE_RATE
OUTPUTS = ["StatefulPartitionedCall:1", "StatefulPartitionedCall:2", "StatefulPartitionedCall:0"]


class FakeSession:
    """Returns ``note``/``onset``/``contour`` activations; one held MIDI 60 note in window 0."""

    def __init__(self, notes: dict[int, tuple[int, int, int, float]] | None = None) -> None:
        self.feeds: list[npt.NDArray[np.float32]] = []
        self.notes = notes if notes is not None else {0: (60, 45, 75, 0.8)}

    def run(self, outputs: list[str], feeds: dict[str, Any]) -> list[npt.NDArray[np.float32]]:
        assert outputs == OUTPUTS
        window = len(self.feeds)
        self.feeds.append(feeds["serving_default_input_2:0"])
        frames = np.zeros((1, FRAMES, 88), dtype=np.float32)
        onsets = np.zeros_like(frames)
        if window in self.notes:
            pitch, first, last, level = self.notes[window]
            frames[0, first:last, pitch - 21] = level
            onsets[0, first, pitch - 21] = 0.9
        return [frames, onsets, np.zeros((1, FRAMES, 264), dtype=np.float32)]


class Factory:
    """Session factory: records ``(path, threads)`` and returns the fake session."""

    def __init__(self, session: FakeSession) -> None:
        self.session = session
        self.calls: list[tuple[Path, int]] = []

    def __call__(self, path: Path, threads: int) -> FakeSession:
        self.calls.append((path, threads))
        return self.session


def _transcriber(tmp_path: Path, session: FakeSession) -> tuple[BasicPitchTranscriber, Factory]:
    factory = Factory(session)
    models = RecordingModels(tmp_path)
    return BasicPitchTranscriber(models, threads=3, session_factory=factory), factory


def _progress(reports: list[float]) -> Any:
    return reports.append


def test_a_held_note_becomes_one_note_event(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    transcriber, factory = _transcriber(tmp_path, FakeSession())
    reports: list[float] = []

    result = transcriber.transcribe(audio, None, _progress(reports))

    assert (transcriber.engine, transcriber.attribution, result.device) == (
        "basic-pitch",
        None,
        None,
    )
    (note,) = result.notes
    assert isinstance(note, NoteEvent)
    assert note.pitch == 60
    assert note.start == pytest.approx(30 * HOP_SECONDS, abs=0.01)
    assert note.end == pytest.approx(60 * HOP_SECONDS, abs=0.03)
    assert note.velocity == pytest.approx(0.8, abs=1e-6)
    assert factory.calls == [(tmp_path / f"{BASIC_PITCH_WEIGHTS.model_id}.bin", 3)]
    assert reports == [1.0]


def test_audio_is_resampled_to_mono_22050_and_padded_by_half_an_overlap(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    session = FakeSession()
    transcriber, _factory = _transcriber(tmp_path, session)

    transcriber.transcribe(audio, None, lambda fraction: None)

    (feed,) = session.feeds
    assert feed.shape == (1, WINDOW_SAMPLES, 1)
    assert feed.dtype == np.float32
    signal = feed[0, :, 0]
    assert np.all(signal[:3840] == 0)
    assert signal[3840 + 100] == pytest.approx(-0.5, abs=0.02)
    assert signal[3840 + SAMPLE_RATE - 100] == pytest.approx(0.5, abs=0.02)
    assert np.all(signal[3840 + SAMPLE_RATE + 8 :] == 0)


def test_long_audio_is_windowed_with_the_original_overlap(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(3.0))
    session = FakeSession({0: (60, 45, 75, 0.8), 1: (64, 60, 100, 0.7)})
    transcriber, _factory = _transcriber(tmp_path, session)
    reports: list[float] = []

    result = transcriber.transcribe(audio, None, _progress(reports))

    hop = WINDOW_SAMPLES - 30 * 256
    assert len(session.feeds) == 2
    assert np.array_equal(session.feeds[1][0, : WINDOW_SAMPLES - hop, 0],
                          session.feeds[0][0, hop:, 0])  # fmt: skip
    assert reports == [0.5, 1.0]
    assert [note.pitch for note in result.notes] == [60, 64]
    second = result.notes[1]
    assert second.start == pytest.approx((FRAMES - 30 + 45) * HOP_SECONDS, abs=0.03)


def test_a_window_transcribes_only_that_part_with_times_from_its_start(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(4.0))
    session = FakeSession()
    transcriber, _factory = _transcriber(tmp_path, session)

    result = transcriber.transcribe(
        audio, RenderRange(start_seconds=2.0, end_seconds=3.0), lambda f: None
    )

    signal = session.feeds[0][0, :, 0]
    assert signal[3840 + 100] == pytest.approx(0.0, abs=0.02)
    assert np.all(signal[3840 + SAMPLE_RATE + 8 :] == 0)
    assert result.notes[0].start == pytest.approx(30 * HOP_SECONDS, abs=0.01)


def test_short_or_quiet_activations_give_no_notes(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    for notes in ({0: (60, 45, 52, 0.8)}, {0: (60, 45, 75, 0.3)}, {}):
        transcriber, _factory = _transcriber(tmp_path, FakeSession(notes))

        assert transcriber.transcribe(audio, None, lambda f: None).notes == ()


def test_a_cancel_from_the_progress_callback_propagates(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    transcriber, _factory = _transcriber(tmp_path, FakeSession())

    def cancel(fraction: float) -> None:
        raise TranscriptionCancelledError("deleted")

    with pytest.raises(TranscriptionCancelledError):
        transcriber.transcribe(audio, None, cancel)


def test_the_session_is_created_once(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    transcriber, factory = _transcriber(tmp_path, FakeSession())

    transcriber.transcribe(audio, None, lambda f: None)
    transcriber.transcribe(audio, None, lambda f: None)

    assert len(factory.calls) == 1


def test_unreadable_audio_and_broken_models_fail_with_a_transcription_error(tmp_path: Path) -> None:
    broken = tmp_path / "broken.flac"
    broken.write_bytes(b"no audio")
    transcriber, _factory = _transcriber(tmp_path, FakeSession())

    with pytest.raises(TranscriptionError):
        transcriber.transcribe(broken, None, lambda f: None)

    def failing(path: Path, threads: int) -> FakeSession:
        raise RuntimeError("bad model")

    models = RecordingModels(tmp_path)
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    with pytest.raises(TranscriptionError, match="could not be run"):
        BasicPitchTranscriber(models, 2, failing).transcribe(audio, None, lambda f: None)


def test_the_onnx_session_uses_the_cpu_provider_and_the_thread_cap(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    created: list[dict[str, Any]] = []

    class Options:
        intra_op_num_threads = 0
        inter_op_num_threads = 0

    marker = FakeSession()

    def session(path: str, sess_options: Options, providers: list[str]) -> FakeSession:
        threads = (sess_options.intra_op_num_threads, sess_options.inter_op_num_threads)
        created.append({"path": path, "threads": threads, "providers": providers})
        return marker

    fake: Any = types.ModuleType("onnxruntime")
    fake.SessionOptions = Options
    fake.InferenceSession = session
    monkeypatch.setitem(sys.modules, "onnxruntime", fake)

    assert onnx_session(tmp_path / "nmp.onnx", 4) is marker
    assert created == [{"path": str(tmp_path / "nmp.onnx"), "threads": (4, 1),
                        "providers": ["CPUExecutionProvider"]}]  # fmt: skip


class _BrokenSession(FakeSession):
    def run(self, outputs: list[str], feeds: dict[str, Any]) -> list[npt.NDArray[np.float32]]:
        raise RuntimeError("onnx failure")


def test_a_failing_model_run_is_a_transcription_error(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0))
    transcriber, _factory = _transcriber(tmp_path, _BrokenSession())

    with pytest.raises(TranscriptionError, match="could not be run"):
        transcriber.transcribe(audio, None, lambda f: None)


def test_audio_at_the_model_rate_is_used_as_it_is(tmp_path: Path) -> None:
    audio = write_audio(tmp_path / "in.flac", ramp(1.0, SAMPLE_RATE), SAMPLE_RATE, stereo=False)
    session = FakeSession()
    transcriber, _factory = _transcriber(tmp_path, session)

    transcriber.transcribe(audio, None, lambda f: None)

    assert session.feeds[0][0, 3840, 0] == pytest.approx(-0.5, abs=1e-3)
