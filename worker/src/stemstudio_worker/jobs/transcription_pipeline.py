"""One transcription job: transcribe -> notate -> engrave (feature 008, ADR-017, data-model 2.9).

Stages and progress: ``loading-model`` 0-0.05, ``transcribing`` 0.05-0.80 (the engine reports
per window, segment or drum piece), ``notating`` 0.80-0.90, ``engraving`` 0.90-1.0. The adapters
come in through :class:`TranscriptionParts` (composed in ``main.py``; ``jobs`` never imports
``transcription``, ``notation`` or ``engraving``). Job-level warnings are added to the notator's:
``detail-bleed`` (a pitched profile on a detail stem), ``cpu-piano`` (the piano CRNN on the CPU)
and ``missing-drum-piece``; at most 8 are kept.
"""

from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final, Protocol

import soundfile as sf

from stemstudio_worker.ports import (
    DeviceUse,
    DrumTranscriber,
    LibraryReader,
    NotationSettings,
    Notator,
    PitchTranscriber,
    ScoreDocument,
    ScoreEngraver,
    TranscriptionError,
    TranscriptionProgress,
    merge_device,
)
from stemstudio_worker.schemas import (
    DrumPiece,
    JobDevice,
    RenderRange,
    ScoreEngine,
    ScoreFileRole,
    ScoreProfile,
    ScoreResolution,
    ScoreSummary,
    ScoreWarning,
    TranscriptionJobRequest,
    TranscriptionModels,
    TranscriptionStage,
)

STAGES: Final[dict[TranscriptionStage, tuple[float, float]]] = {
    "loading-model": (0.0, 0.05),
    "transcribing": (0.05, 0.80),
    "notating": (0.80, 0.90),
    "engraving": (0.90, 1.0),
}
"""Progress range of every stage (data-model 2.9)."""

MAX_WARNINGS: Final = 8
_PIECES: Final[tuple[DrumPiece, ...]] = ("kick", "snare", "toms", "hihat", "cymbals")
_CPU: Final = JobDevice(kind="cpu")


class TranscriptionEnginesPort(Protocol):
    """Per-job transcribers (``transcription.TranscriptionEngines``)."""

    def pitched(
        self, request: TranscriptionJobRequest, pitch_range: tuple[int, int]
    ) -> PitchTranscriber:
        """The pitched transcriber of a job."""
        ...

    def drums(self, request: TranscriptionJobRequest) -> DrumTranscriber:
        """The drum transcriber of a job."""
        ...

    def models_health(self) -> TranscriptionModels:
        """``Health.transcription.models``."""
        ...

    def release(self) -> None:
        """Drop loaded models."""
        ...


@dataclass(frozen=True, slots=True)
class ScoreRules:
    """Profile facts from ``notation`` (injected by the composition root).

    Attributes:
        pitch_range: Lowest and highest MIDI pitch of a profile.
        resolution: The requested resolution, or the profile default.
        warning: Builds a warning with its fixed text (``warning(code, **values)``).
    """

    pitch_range: Callable[[ScoreProfile], tuple[int, int]]
    resolution: Callable[[ScoreProfile, ScoreResolution | None], ScoreResolution]
    warning: Callable[..., ScoreWarning]


@dataclass(frozen=True, slots=True)
class TranscriptionParts:
    """The adapters of feature 008.

    Attributes:
        engines: Per-job transcribers.
        notator: Notes -> MusicXML/MIDI.
        engraver: MusicXML -> PDF.
        rules: Profile facts.
        library: Resolves library input paths.
        font: Whether the Leipzig font is installed (``Health.transcription.font``).
    """

    engines: TranscriptionEnginesPort
    notator: Notator
    engraver: ScoreEngraver
    rules: ScoreRules
    library: LibraryReader
    font: Callable[[], bool]


@dataclass(frozen=True, slots=True)
class Outcome:
    """What a finished pipeline produced."""

    files: dict[ScoreFileRole, bytes]
    summary: ScoreSummary
    device: JobDevice


class StageReporter(Protocol):
    """Reports a stage and returns the progress callback of that stage."""

    def __call__(self, stage: TranscriptionStage) -> TranscriptionProgress:
        """Enter ``stage``; the returned callback maps 0..1 into the stage's range."""
        ...


def resolve_inputs(request: TranscriptionJobRequest, library: LibraryReader) -> dict[str, Path]:
    """Resolve every input path (``input`` or the drum pieces), keyed by piece or ``input``.

    Raises:
        InputPathError, InputNotFoundError, RenderError: From the library reader.
    """
    if request.drum_inputs is not None:
        return {
            piece: library.resolve(path) for piece, path in request.drum_inputs.pieces().items()
        }
    assert request.input is not None  # noqa: S101 - guaranteed by the request validator
    return {"input": library.resolve(request.input.path)}


def _duration(path: Path, window: RenderRange | None) -> float:
    if window is not None:
        return window.end_seconds - window.start_seconds
    try:
        return float(sf.info(path).duration)
    except (OSError, sf.LibsndfileError) as exc:
        raise TranscriptionError("The audio file could not be read.") from exc


@dataclass(frozen=True, slots=True)
class _Transcribed:
    document: ScoreDocument
    engine: ScoreEngine
    attribution: str | None
    use: DeviceUse | None


class Pipeline:
    """Runs one job's stages with the parts of the composition root."""

    def __init__(self, parts: TranscriptionParts, request: TranscriptionJobRequest) -> None:
        """Run ``request`` with ``parts``."""
        self._parts = parts
        self._request = request

    def run(self, inputs: Mapping[str, Path], report: StageReporter) -> Outcome:
        """Transcribe, notate and engrave; returns the files and the summary.

        Raises:
            TranscriptionError: No notes, engraving or model failures (fixed texts).
            TranscriptionCancelledError: The job was deleted.
        """
        report("loading-model")
        settings = self._settings(next(iter(inputs.values())))
        done = self._transcribe(inputs, settings, report)
        finished = report("engraving")
        engraved = self._parts.engraver.engrave(
            done.document.musicxml, self._request.titles.title, done.attribution
        )
        finished(1.0)
        document = done.document
        files: dict[ScoreFileRole, bytes] = {
            "pdf": engraved.pdf, "musicxml": document.musicxml, "midi": document.midi,
        }  # fmt: skip
        device = _CPU if done.use is None else merge_device(None, done.use)
        return Outcome(files, self._summary(done, engraved.pages, settings, inputs), device)

    def _settings(self, first: Path) -> NotationSettings:
        request, rules = self._request, self._parts.rules
        return NotationSettings(
            profile=request.profile, grid=request.grid,
            resolution=rules.resolution(request.profile, request.resolution),
            key_override=request.key_override, titles=request.titles,
            duration_seconds=_duration(first, request.range),
        )  # fmt: skip

    def _transcribe(
        self, inputs: Mapping[str, Path], settings: NotationSettings, report: StageReporter
    ) -> _Transcribed:
        request, parts = self._request, self._parts
        if request.profile == "drums":
            drums = parts.engines.drums(request)
            pieces: dict[Any, Path] = dict(inputs)
            hits = drums.transcribe(pieces, request.range, report("transcribing"))
            report("notating")
            engine: ScoreEngine = getattr(drums, "engine", "drum-onsets")
            return _Transcribed(parts.notator.notate_drums(hits, settings), engine, None, None)
        pitched = parts.engines.pitched(request, parts.rules.pitch_range(request.profile))
        result = pitched.transcribe(inputs["input"], request.range, report("transcribing"))
        report("notating")
        document = parts.notator.notate(result.notes, settings)
        return _Transcribed(document, pitched.engine, pitched.attribution, result.device)

    def _summary(
        self, done: _Transcribed, pages: int, settings: NotationSettings, inputs: Mapping[str, Path]
    ) -> ScoreSummary:
        request, document = self._request, done.document
        warnings = [*document.warnings, *self._job_warnings(done, inputs)][:MAX_WARNINGS]
        return ScoreSummary(
            profile=request.profile, engine=done.engine,
            quality=request.quality if request.profile == "piano" else None,
            key=document.key, bpm=document.bpm, time_signature=document.time_signature,
            bars=document.bars, notes=document.notes, pages=pages,
            resolution=settings.resolution, warnings=warnings, attribution=done.attribution,
        )  # fmt: skip

    def _job_warnings(self, done: _Transcribed, inputs: Mapping[str, Path]) -> list[ScoreWarning]:
        request, warn = self._request, self._parts.rules.warning
        found: list[ScoreWarning] = []
        if request.input is not None and "/detail/" in request.input.path:
            found.append(warn("detail-bleed"))
        if done.engine == "piano-crnn" and done.use is not None and done.use.kind == "cpu":
            found.append(warn("cpu-piano"))
        if request.profile == "drums":
            found += [warn("missing-drum-piece", piece=p) for p in _PIECES if p not in inputs]
        return found
