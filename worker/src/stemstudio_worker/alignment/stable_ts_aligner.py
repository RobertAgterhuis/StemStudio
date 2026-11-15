"""The stable-ts + Whisper aligner adapter (ADR-015 D3, research.md R-04 and P0-2).

Runs inside ``DeviceRunner.run(work, WHISPER_GPU_NEED)``: the given torch device is used as is, a
GPU out-of-memory error while loading or aligning propagates so the runner retries on the CPU.
The weights are downloaded into ``STEMSTUDIO_MODEL_DIR/whisper`` on first use (stage
``LYRICS_STAGE_DOWNLOADING``). Alignment uses the default stable-ts options with
``original_split=True`` (our line breaks are kept); a collapsed run (``mapping.is_collapsed``) is
retried once with ``vad=True``, and a second collapse fails with ``ALIGNMENT_FAILED_TEXT``. With
``refine`` the words are aligned inside the given line times (``align_words``). Word starts are
then moved to the nearest vocal onset (``onsets.snap_starts``; the qa sung clip showed starts
inside held vowels). Loaded models are kept per device until :meth:`StableTsAligner.release`.
"""

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Protocol

import numpy as np
import numpy.typing as npt

from stemstudio_worker.alignment.mapping import AlignedWord, build_document, is_collapsed
from stemstudio_worker.alignment.onsets import snap_starts, vocal_onsets
from stemstudio_worker.alignment.weights import WhisperWeights
from stemstudio_worker.ports import AlignmentError, AlignRequest, ProgressCallback
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_ALIGNING,
    LYRICS_STAGE_DOWNLOADING,
    SPEECH_MODEL_DOWNLOAD_TEXT,
    TimedLyrics,
)

_LANGUAGE = re.compile(r"[a-z]{2,3}")


class WhisperRuntimePort(Protocol):
    """Downloads, loads and queries Whisper models (``whisper_runtime.WhisperRuntime``)."""

    def download(self, name: str, root: Path) -> Path:
        """Download the checkpoint of ``name`` into ``root`` (SHA-256 checked); return its path."""
        ...

    def load(self, name: str, device: str, root: Path) -> Any:
        """Load the stable-ts model ``name`` on ``device`` from ``root``."""
        ...

    def detect_language(self, model: Any, audio: npt.NDArray[np.float32]) -> str:
        """The most likely language code of ``audio``."""
        ...


class _Callback:
    """stable-ts ``progress_callback(seek, total)`` -> our progress callback."""

    def __init__(self, progress: ProgressCallback) -> None:
        self._progress = progress

    def __call__(self, seek: float, total: float) -> None:
        fraction = min(seek / total, 1.0) if total > 0 else 0.0
        self._progress(fraction, LYRICS_STAGE_ALIGNING)


def _language(found: object, hint: str | None) -> str | None:
    """The result's language when it is a valid code, else the hint."""
    for value in (found, hint):
        if isinstance(value, str) and _LANGUAGE.fullmatch(value):
            return value
    return None


def _segments(refine: TimedLyrics) -> list[dict[str, Any]]:
    """The refined lines as stable-ts segments (seconds)."""
    return [
        {"start": (line.start_ms or 0) / 1000, "end": (line.end_ms or 0) / 1000, "text": line.text}
        for line in refine.lines
    ]


@dataclass(frozen=True, slots=True)
class _Run:
    """One alignment: the model, the request, the language, progress and the vocal onsets."""

    model: Any
    request: AlignRequest
    language: str
    callback: _Callback
    onsets: npt.NDArray[np.float64]

    def attempt(self, *, vad: bool) -> TimedLyrics | None:
        """One stable-ts run, onset-refined; None when stable-ts gave up."""
        result = self._result(vad=vad)
        if result is None:
            return None
        words = [AlignedWord(w.word, w.start, w.end, w.probability) for w in result.all_words()]
        found = _language(getattr(result, "language", None), self.language)
        request = self.request
        return build_document(request.text, snap_starts(words, self.onsets), found, request.refine)

    def _result(self, *, vad: bool) -> Any:
        """``align`` with our line breaks, or ``align_words`` inside the refined lines."""
        request = self.request
        if request.refine is None:
            return self.model.align(request.audio, request.text, language=self.language,
                                    original_split=True, vad=vad,
                                    progress_callback=self.callback)  # fmt: skip
        segments = _segments(request.refine)
        return self.model.align_words(request.audio, segments, language=self.language, vad=vad)


class StableTsAligner:
    """``Aligner`` port over stable-ts and Whisper."""

    def __init__(self, weights: WhisperWeights, runtime: WhisperRuntimePort) -> None:
        """Align with the checkpoint ``weights`` through ``runtime``."""
        self._weights = weights
        self._runtime = runtime
        self._models: dict[str, Any] = {}

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:
        """Align ``request.text`` to ``request.audio`` on ``device``.

        Raises:
            AlignmentError: The download failed or both attempts collapsed.
            AlignmentCancelledError: Propagated from ``progress``.
        """
        model = self._model(device, progress)
        progress(0.0, LYRICS_STAGE_ALIGNING)
        language = request.language or self._runtime.detect_language(model, request.audio)
        run = _Run(model, request, language, _Callback(progress), vocal_onsets(request.audio))
        for vad in (False, True):
            document = run.attempt(vad=vad)
            if document is not None and not is_collapsed(document):
                return document
        raise AlignmentError(ALIGNMENT_FAILED_TEXT)

    def release(self) -> None:
        """Drop the loaded models (their GPU memory is returned by the device runner)."""
        self._models.clear()

    def _model(self, device: str, progress: ProgressCallback) -> Any:
        if device not in self._models:
            if not self._weights.cached():
                progress(0.0, LYRICS_STAGE_DOWNLOADING)
                self._download()
            self._models[device] = self._runtime.load(
                self._weights.name, device, self._weights.root
            )
        return self._models[device]

    def _download(self) -> None:
        try:
            self._runtime.download(self._weights.name, self._weights.root)
        except Exception as exc:
            raise AlignmentError(SPEECH_MODEL_DOWNLOAD_TEXT) from exc
