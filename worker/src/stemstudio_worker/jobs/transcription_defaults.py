"""Stand-ins for the feature 008 adapters of a runtime built without them (unit tests).

Every transcription job fails with ``TRANSCRIPTION_UNAVAILABLE_TEXT``; the font is reported
missing and the models not downloaded.
"""

from collections.abc import Sequence
from typing import Any, Final

from stemstudio_worker.jobs.transcription_pipeline import ScoreRules, TranscriptionParts
from stemstudio_worker.ports import (
    LibraryReader,
    NoteEvent,
    ScoreDocument,
    Transcription,
    TranscriptionError,
)
from stemstudio_worker.schemas import (
    SCORE_WARNING_TEXTS,
    ScoreEngine,
    ScoreProfile,
    ScoreResolution,
    ScoreWarning,
    ScoreWarningCode,
    TranscriptionJobRequest,
    TranscriptionModels,
)

TRANSCRIPTION_UNAVAILABLE_TEXT: Final = "Sheet music is not available in this worker."
"""Error of every job of a worker built without the transcription parts."""


class UnavailableTranscription:
    """Engines, transcriber, notator and engraver in one: every call fails."""

    engine: ScoreEngine = "fake"
    attribution: str | None = None

    def pitched(self, request: TranscriptionJobRequest, pitch_range: tuple[int, int]) -> Any:  # noqa: ARG002
        """This object (its ``transcribe`` fails)."""
        return self

    def drums(self, request: TranscriptionJobRequest) -> Any:  # noqa: ARG002
        """This object (its ``transcribe`` fails)."""
        return self

    def transcribe(self, *args: object) -> Transcription:  # noqa: ARG002
        """Fail with ``TRANSCRIPTION_UNAVAILABLE_TEXT``."""
        raise TranscriptionError(TRANSCRIPTION_UNAVAILABLE_TEXT)

    def notate(self, notes: Sequence[NoteEvent], settings: object) -> ScoreDocument:  # noqa: ARG002
        """Fail with ``TRANSCRIPTION_UNAVAILABLE_TEXT``."""
        raise TranscriptionError(TRANSCRIPTION_UNAVAILABLE_TEXT)

    notate_drums = notate

    def engrave(self, musicxml: bytes, title: str, subject: str | None) -> Any:  # noqa: ARG002
        """Fail with ``TRANSCRIPTION_UNAVAILABLE_TEXT``."""
        raise TranscriptionError(TRANSCRIPTION_UNAVAILABLE_TEXT)

    def models_health(self) -> TranscriptionModels:
        """Nothing is downloaded."""
        return TranscriptionModels()

    def release(self) -> None:
        """Nothing is loaded."""


def _range(profile: ScoreProfile) -> tuple[int, int]:  # noqa: ARG001
    return 0, 127


def _resolution(profile: ScoreProfile, requested: ScoreResolution | None) -> ScoreResolution:  # noqa: ARG001
    return requested or "sixteenth"


def _warning(code: ScoreWarningCode, **values: object) -> ScoreWarning:
    return ScoreWarning(code=code, text=SCORE_WARNING_TEXTS[code].format(**values))


def _no_font() -> bool:
    return False


def default_transcription_parts(library: LibraryReader) -> TranscriptionParts:
    """The stand-ins over ``library``."""
    unavailable: Any = UnavailableTranscription()
    rules = ScoreRules(pitch_range=_range, resolution=_resolution, warning=_warning)
    return TranscriptionParts(engines=unavailable, notator=unavailable, engraver=unavailable,
                              rules=rules, library=library, font=_no_font)  # fmt: skip
