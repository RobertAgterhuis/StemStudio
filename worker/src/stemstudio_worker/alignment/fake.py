"""Deterministic aligner of the fake stack (``STEMSTUDIO_FAKE_LYRICS``, data-model.md 1).

Word *k* (counted over the whole text, line breaks kept) starts at 2000 + 500 *k* ms and lasts
500 ms; a line spans its words (qa ``tests/fixtures/lyrics/fake-lyrics.json`` ``fakeAlignment``).
``failed`` fails every alignment. No model is loaded.
"""

from stemstudio_worker.alignment.mapping import AlignedWord, build_document, line_tokens
from stemstudio_worker.config import FakeLyricsMode
from stemstudio_worker.ports import (
    AlignmentError,
    AlignRequest,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_ALIGNING,
    TimedLyrics,
)

START_MS = 2000
"""The first fake word starts here."""

WORD_MS = 500
"""Every fake word lasts this long."""


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
    """The words of the text in consecutive ``WORD_MS`` slots from ``START_MS``."""
    tokens = [token for line in line_tokens(request.text) for token in line]
    starts = [START_MS + index * WORD_MS for index in range(len(tokens))]
    return [
        AlignedWord(t, s / 1000, (s + WORD_MS) / 1000, 0.9)
        for t, s in zip(tokens, starts, strict=True)
    ]
