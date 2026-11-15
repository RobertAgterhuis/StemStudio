"""Deterministic aligner of the fake stack (``STEMSTUDIO_FAKE_LYRICS``, data-model.md 1).

Words are spread evenly from 2 s to the end of the vocal stem (at most 3 s, at least 100 ms per
word; 500 ms when the stem is too short); ``failed`` fails every alignment. No model is loaded.
"""

from stemstudio_worker.alignment.mapping import AlignedWord, build_document, line_tokens
from stemstudio_worker.config import FakeLyricsMode
from stemstudio_worker.ports import (
    ALIGN_SAMPLE_RATE,
    AlignmentError,
    AlignRequest,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_ALIGNING,
    MAX_WORD_MS,
    TimedLyrics,
)

START_MS = 2000
"""The first fake word starts here."""

_MIN_SLOT_MS = 100
_SHORT_SLOT_MS = 500


class FakeAligner:
    """``Aligner`` without a model, for E2E tests and the fake stack."""

    def __init__(self, mode: FakeLyricsMode) -> None:
        """Behave as ``STEMSTUDIO_FAKE_LYRICS`` says."""
        self._mode = mode

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:  # noqa: ARG002 - port signature
        """Spread the words of ``request.text`` evenly after 2 s.

        Raises:
            AlignmentError: In ``failed`` mode.
        """
        if self._mode == "failed":
            raise AlignmentError(ALIGNMENT_FAILED_TEXT)
        progress(0.0, LYRICS_STAGE_ALIGNING)
        words = _spread(request)
        document = build_document(request.text, words, request.language or "en", request.refine)
        progress(1.0, LYRICS_STAGE_ALIGNING)
        return document

    def release(self) -> None:
        """Nothing is loaded."""


def _spread(request: AlignRequest) -> list[AlignedWord]:
    """The words of the text in consecutive slots from ``START_MS``."""
    tokens = [token for line in line_tokens(request.text) for token in line]
    slot = _slot(request.audio.size * 1000 // ALIGN_SAMPLE_RATE, len(tokens))
    starts = [START_MS + index * slot for index in range(len(tokens))]
    return [
        AlignedWord(t, s / 1000, (s + slot) / 1000, 0.9)
        for t, s in zip(tokens, starts, strict=True)
    ]


def _slot(duration_ms: int, count: int) -> int:
    """Milliseconds per word."""
    slot = (duration_ms - START_MS) // max(count, 1)
    return _SHORT_SLOT_MS if slot < _MIN_SLOT_MS else min(slot, MAX_WORD_MS)
