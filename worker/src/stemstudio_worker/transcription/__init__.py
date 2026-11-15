"""Pitched transcription engines of feature 008 (ADR-017 D1): basic-pitch ONNX, the piano CRNN.

Only the composition root (``main.py``) imports this package. ``create_transcription_engines``
gives per-job transcribers: the shared real engines (models downloaded on first use into
``STEMSTUDIO_MODEL_DIR/transcription``, CPU threads capped by ``STEMSTUDIO_TRANSCRIBE_THREADS``,
the piano CRNN through the device runner) or, with ``STEMSTUDIO_TRANSCRIBER=fake``, the
deterministic fakes of data-model 2.6 on the request's grid. The real drum transcriber comes
from ``analysis`` and is passed in.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import (
    BASIC_PITCH_WEIGHTS,
    PIANO_WEIGHTS,
    DeviceRunner,
    DrumTranscriber,
    PitchTranscriber,
)
from stemstudio_worker.schemas import TranscriptionJobRequest, TranscriptionModels
from stemstudio_worker.transcription.basic_pitch import BasicPitchTranscriber
from stemstudio_worker.transcription.fake import FakeDrumTranscriber, FakePitchTranscriber
from stemstudio_worker.transcription.models import ModelStore
from stemstudio_worker.transcription.piano import PianoTranscriber, TorchRuntime

__all__ = ["TranscriptionEngines", "create_transcription_engines"]


class TranscriptionEngines:
    """Hands out the transcriber of each job (real engines are shared and keep their models)."""

    def __init__(
        self, settings: WorkerSettings, devices: DeviceRunner, drums: DrumTranscriber
    ) -> None:
        """Build the real engines for ``settings``; ``drums`` is the real drum transcriber."""
        threads = settings.transcription.threads
        self._fake = settings.transcription.transcriber == "fake"
        self._models = ModelStore(settings.model_dir)
        self._basic_pitch = BasicPitchTranscriber(self._models, threads)
        self._piano = PianoTranscriber(self._models, devices, TorchRuntime(threads))
        self._drums = drums

    def pitched(
        self, request: TranscriptionJobRequest, pitch_range: tuple[int, int]
    ) -> PitchTranscriber:
        """The fake (on the request grid), the piano CRNN (piano, accurate) or basic-pitch."""
        if self._fake:
            return FakePitchTranscriber(request.profile, pitch_range, request.grid)
        if request.profile == "piano" and request.quality == "accurate":
            return self._piano
        return self._basic_pitch

    def drums(self, request: TranscriptionJobRequest) -> DrumTranscriber:
        """The fake groove on the request grid, or the real onset transcriber."""
        return FakeDrumTranscriber(request.grid) if self._fake else self._drums

    def models_health(self) -> TranscriptionModels:
        """``Health.transcription.models``: which weights are in the cache."""
        state = {w.model_id: "ready" if self._models.ready(w) else "not-downloaded"
                 for w in (BASIC_PITCH_WEIGHTS, PIANO_WEIGHTS)}  # fmt: skip
        return TranscriptionModels.model_validate(state)

    def release(self) -> None:
        """Drop the loaded piano models (idle hook)."""
        self._piano.release()


def create_transcription_engines(
    settings: WorkerSettings, devices: DeviceRunner, drums: DrumTranscriber
) -> TranscriptionEngines:
    """The engines of the composition root."""
    return TranscriptionEngines(settings, devices, drums)
