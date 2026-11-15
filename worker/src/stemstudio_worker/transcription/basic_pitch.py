"""basic-pitch (ICASSP 2022 ONNX model) behind the ``PitchTranscriber`` port (ADR-017 D1/D3).

CPU only: an onnxruntime session on the CPU provider with ``transcribe_threads`` intra-op threads.
Audio is resampled to 22,050 Hz mono and windowed like basic-pitch 0.4.0 (2 s windows, 30
overlapping frames); the stitched outputs are decoded by the vendored post-processing with onset
threshold 0.6, frame threshold 0.4 and a minimum note length of 127.7 ms (research.md S-03).
"""

from collections.abc import Callable
from pathlib import Path
from typing import Any, Final, Protocol, cast

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import (
    BASIC_PITCH_WEIGHTS,
    NoteEvent,
    Transcription,
    TranscriptionError,
    TranscriptionProgress,
    WeightsFile,
)
from stemstudio_worker.schemas import RenderRange, ScoreEngine
from stemstudio_worker.transcription.audio import load_mono
from stemstudio_worker.transcription.vendor.basic_pitch import constants as bp
from stemstudio_worker.transcription.vendor.basic_pitch.note_creation import (
    model_frames_to_time,
    output_to_notes_polyphonic,
    unwrap_output,
    window_audio_file,
)

SAMPLE_RATE: Final = bp.AUDIO_SAMPLE_RATE
"""Input rate of the model (22,050 Hz)."""

WINDOW_SAMPLES: Final = bp.AUDIO_N_SAMPLES
"""Samples per model window (2 s minus one hop)."""

ONSET_THRESHOLD: Final = 0.6
FRAME_THRESHOLD: Final = 0.4
MIN_NOTE_MS: Final = 127.7
MODEL_FAILED_TEXT: Final = "The transcription model could not be run."
"""Error when the ONNX model cannot be loaded or run."""

_OVERLAP_FRAMES = 30
_OVERLAP = _OVERLAP_FRAMES * bp.FFT_HOP
_HOP = WINDOW_SAMPLES - _OVERLAP
_INPUT = "serving_default_input_2:0"
_OUTPUTS = ["StatefulPartitionedCall:1", "StatefulPartitionedCall:2", "StatefulPartitionedCall:0"]
_MIN_FRAMES = int(np.round(MIN_NOTE_MS / 1000 * (SAMPLE_RATE / bp.FFT_HOP)))


class OnnxSession(Protocol):
    """The part of ``onnxruntime.InferenceSession`` the transcriber uses."""

    def run(self, outputs: list[str], feeds: dict[str, Any]) -> list[npt.NDArray[np.float32]]:
        """Run the model."""
        ...


class WeightsSource(Protocol):
    """Hands out verified weight files (``models.ModelStore``)."""

    def ensure(self, weights: WeightsFile) -> Path:
        """The verified file of ``weights`` (downloaded on first use)."""
        ...


SessionFactory = Callable[[Path, int], OnnxSession]
"""Creates a session for a model file with a number of intra-op threads."""


def onnx_session(path: Path, threads: int) -> OnnxSession:
    """An onnxruntime session on the CPU provider with ``threads`` intra-op threads."""
    import onnxruntime as ort  # noqa: PLC0415 - imported lazily (only when transcribing)

    options = ort.SessionOptions()
    options.intra_op_num_threads = threads
    options.inter_op_num_threads = 1
    session: OnnxSession = ort.InferenceSession(
        str(path), sess_options=options, providers=["CPUExecutionProvider"]
    )
    return session


def _to_notes(frames: npt.NDArray[np.float32], onsets: npt.NDArray[np.float32]) -> list[NoteEvent]:
    """Decode stitched activations into notes in onset order."""
    if not np.any(frames > 0):
        return []
    events = output_to_notes_polyphonic(frames, onsets, onset_thresh=ONSET_THRESHOLD,
                                        frame_thresh=FRAME_THRESHOLD, min_note_len=_MIN_FRAMES,
                                        infer_onsets=True, melodia_trick=True)  # fmt: skip
    times = model_frames_to_time(frames.shape[0])
    notes = [NoteEvent(start=float(times[s]), end=float(times[e]), pitch=int(p),
                       velocity=float(min(max(a, 0.0), 1.0))) for s, e, p, a in events]  # fmt: skip
    return sorted(notes, key=lambda n: (n.start, n.pitch))


def _stitched(parts: list[npt.NDArray[np.float32]], samples: int) -> npt.NDArray[np.float32]:
    """One activation matrix from the window outputs (overlaps removed, trimmed)."""
    matrix = unwrap_output(np.concatenate(parts), samples, _OVERLAP_FRAMES)
    return cast(npt.NDArray[np.float32], matrix)  # three-dimensional input: never None


class BasicPitchTranscriber:
    """``PitchTranscriber`` over the basic-pitch ONNX model."""

    def __init__(
        self, models: WeightsSource, threads: int, session_factory: SessionFactory = onnx_session
    ) -> None:
        """Fetch the model from ``models``; run it with ``threads`` CPU threads."""
        self._models = models
        self._threads = threads
        self._factory = session_factory
        self._session: OnnxSession | None = None

    @property
    def engine(self) -> ScoreEngine:
        """``basic-pitch``."""
        return "basic-pitch"

    @property
    def attribution(self) -> str | None:
        """No attribution is needed (Apache-2.0)."""
        return None

    def transcribe(
        self, audio_path: Path, window: RenderRange | None, progress: TranscriptionProgress
    ) -> Transcription:
        """Transcribe ``audio_path`` (or ``window``); progress is reported per model window.

        Raises:
            TranscriptionError: The audio cannot be read or the model cannot be run.
            TranscriptionCancelledError: Propagated from ``progress``.
        """
        audio = load_mono(audio_path, window, SAMPLE_RATE)
        session = self._loaded()
        padded = np.concatenate([np.zeros(_OVERLAP // 2, dtype=np.float32), audio])
        windows = list(window_audio_file(padded, _HOP))
        outputs: dict[str, list[npt.NDArray[np.float32]]] = {"note": [], "onset": []}
        for index, chunk in enumerate(windows):
            note, onset, _contour = self._run(session, chunk)
            outputs["note"].append(note)
            outputs["onset"].append(onset)
            progress((index + 1) / len(windows))
        frames, onsets = (_stitched(outputs[k], audio.size) for k in ("note", "onset"))
        return Transcription(notes=tuple(_to_notes(frames, onsets)))

    def _loaded(self) -> OnnxSession:
        if self._session is None:
            path = self._models.ensure(BASIC_PITCH_WEIGHTS)
            try:
                self._session = self._factory(path, self._threads)
            except Exception as exc:
                raise TranscriptionError(MODEL_FAILED_TEXT) from exc
        return self._session

    @staticmethod
    def _run(session: OnnxSession, chunk: npt.NDArray[np.float32]) -> list[npt.NDArray[np.float32]]:
        try:
            return session.run(_OUTPUTS, {_INPUT: np.expand_dims(chunk, axis=0).astype(np.float32)})
        except Exception as exc:
            raise TranscriptionError(MODEL_FAILED_TEXT) from exc
