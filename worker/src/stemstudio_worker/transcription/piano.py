"""The ByteDance high-resolution piano CRNN behind the ``PitchTranscriber`` port (ADR-017 D1/D7).

Runs through the 003 ``DeviceRunner`` with ``PIANO_GPU_NEED`` (one GPU step at a time, CPU when
the GPU lacks memory, one CPU retry on GPU out of memory). Audio is read at 16 kHz mono, padded
to whole 10 s segments and cut into half-overlapping segments as the upstream inference does;
progress is reported per segment. The vendored regression post-processor turns the stitched
outputs into notes (upstream thresholds: onset 0.3, offset 0.3, frame 0.1). The weights come
from the model store (SHA-256 checked), never through a shell download.
"""

import importlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final, Protocol

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import (
    PIANO_GPU_NEED,
    PIANO_WEIGHTS,
    DeviceRunner,
    NoteEvent,
    Transcription,
    TranscriptionProgress,
    WeightsFile,
)
from stemstudio_worker.schemas import PIANO_ATTRIBUTION, RenderRange, ScoreEngine
from stemstudio_worker.transcription.audio import load_mono
from stemstudio_worker.transcription.vendor.piano_transcription import config

_VENDOR = "stemstudio_worker.transcription.vendor.piano_transcription"
_POST: Any = importlib.import_module(f"{_VENDOR}.post_processing")  # untyped vendored code
_SEGMENTS: Any = importlib.import_module(f"{_VENDOR}.segments")

SAMPLE_RATE: Final = config.sample_rate
"""Input rate of the model (16 kHz)."""

SEGMENT_SAMPLES: Final = SAMPLE_RATE * 10
"""Samples per model segment (10 s)."""

_THRESHOLDS: Final = {"onset_threshold": 0.3, "offset_threshold": 0.3, "frame_threshold": 0.1,
                      "pedal_offset_threshold": 0.2}  # fmt: skip


class PianoRuntime(Protocol):
    """The torch operations of the transcriber (``TorchRuntime``; a fake in unit tests)."""

    def load(self, checkpoint: Path, device: str) -> Any:
        """Build the model from ``checkpoint`` on ``device``."""
        ...

    def infer(self, model: Any, segment: npt.NDArray[np.float32], device: str) -> dict[str, Any]:
        """Outputs (frames x classes numpy arrays) of one segment."""
        ...


class WeightsSource(Protocol):
    """Hands out verified weight files (``models.ModelStore``)."""

    def ensure(self, weights: WeightsFile) -> Path:
        """The verified file of ``weights``."""
        ...


def _torch() -> Any:
    return importlib.import_module("torch")


class TorchRuntime:
    """``PianoRuntime`` over torch and the vendored ``Note_pedal`` model."""

    def __init__(self, threads: int) -> None:
        """Cap torch's intra-op threads at ``threads`` (``STEMSTUDIO_TRANSCRIBE_THREADS``)."""
        self._threads = threads

    def load(self, checkpoint: Path, device: str) -> Any:
        """The vendored model with the checkpoint's weights, in evaluation mode on ``device``."""
        torch = _torch()
        torch.set_num_threads(self._threads)
        models = importlib.import_module(f"{_VENDOR}.models")
        model = models.Note_pedal(frames_per_second=config.frames_per_second,
                                  classes_num=config.classes_num)  # fmt: skip
        state = torch.load(checkpoint, map_location=device, weights_only=True)
        model.load_state_dict(state["model"], strict=False)
        return model.to(device).eval()

    def infer(self, model: Any, segment: npt.NDArray[np.float32], device: str) -> dict[str, Any]:
        """Run one segment without gradients; outputs as numpy arrays (batch dimension dropped)."""
        torch = _torch()
        with torch.no_grad():
            batch = torch.from_numpy(segment[None, :]).float().to(device)
            outputs = model(batch)
        return {key: value.detach().cpu().numpy()[0] for key, value in outputs.items()}


def _notes(output: dict[str, Any]) -> list[NoteEvent]:
    processor = _POST.RegressionPostProcessor(
        config.frames_per_second, classes_num=config.classes_num, **_THRESHOLDS
    )
    events, _pedals = processor.output_dict_to_midi_events(output)
    notes = [NoteEvent(start=float(e["onset_time"]), end=float(e["offset_time"]),
                       pitch=int(e["midi_note"]),
                       velocity=min(float(e["velocity"]) / config.velocity_scale, 1.0))
             for e in events]  # fmt: skip
    return sorted(notes, key=lambda n: (n.start, n.pitch))


class PianoTranscriber:
    """``PitchTranscriber`` over the piano CRNN (``accurate`` quality of the piano profile)."""

    def __init__(self, models: WeightsSource, devices: DeviceRunner, runtime: PianoRuntime) -> None:
        """Fetch the checkpoint from ``models``; run it with ``runtime`` on ``devices``."""
        self._models = models
        self._devices = devices
        self._runtime = runtime
        self._loaded: dict[str, Any] = {}

    @property
    def engine(self) -> ScoreEngine:
        """``piano-crnn``."""
        return "piano-crnn"

    @property
    def attribution(self) -> str | None:
        """The CC BY 4.0 credit of the weights."""
        return PIANO_ATTRIBUTION

    def transcribe(
        self, audio_path: Path, window: RenderRange | None, progress: TranscriptionProgress
    ) -> Transcription:
        """Transcribe ``audio_path`` (or ``window``) on the device the runner picks.

        Raises:
            TranscriptionError: The audio or the weights are unavailable.
            TranscriptionCancelledError: Propagated from ``progress``.
        """
        audio = load_mono(audio_path, window, SAMPLE_RATE)
        checkpoint = self._models.ensure(PIANO_WEIGHTS)

        def work(device: str) -> list[NoteEvent]:
            model = self._model(checkpoint, device)
            return _notes(self._stitched(_Segments(audio, model, device), progress))

        notes, use = self._devices.run(work, PIANO_GPU_NEED)
        return Transcription(notes=tuple(notes), device=use)

    def release(self) -> None:
        """Drop the loaded models (idle hook)."""
        self._loaded.clear()

    def _stitched(self, job: "_Segments", progress: TranscriptionProgress) -> dict[str, Any]:
        """Run every segment (progress per segment) and stitch the outputs to the audio length."""
        segments = job.cut()
        outputs: list[dict[str, Any]] = []
        for index, segment in enumerate(segments):
            outputs.append(self._runtime.infer(job.model, segment, job.device))
            progress((index + 1) / len(segments))
        return job.stitch(outputs)

    def _model(self, checkpoint: Path, device: str) -> Any:
        if device not in self._loaded:
            self._loaded[device] = self._runtime.load(checkpoint, device)
        return self._loaded[device]


@dataclass(frozen=True, slots=True)
class _Segments:
    """The 10 s segments of one transcription on one device."""

    audio: npt.NDArray[np.float32]
    model: Any
    device: str

    def cut(self) -> list[npt.NDArray[np.float32]]:
        """Pad to whole segments and cut into half-overlapping segments (upstream ``enframe``)."""
        padded = np.concatenate([self.audio, np.zeros(-self.audio.size % SEGMENT_SAMPLES)])
        return [
            row.astype(np.float32) for row in _SEGMENTS.enframe(padded[None, :], SEGMENT_SAMPLES)
        ]

    def stitch(self, outputs: list[dict[str, Any]]) -> dict[str, Any]:
        """Join the segment outputs (upstream ``deframe``), cut to the audio's frames."""
        frames = int(self.audio.size * config.frames_per_second / SAMPLE_RATE)
        return {key: _SEGMENTS.deframe(np.stack([o[key] for o in outputs]))[:frames]
                for key in outputs[0]}  # fmt: skip
