"""Fake LRCLIB provider of the fake stack (``STEMSTUDIO_FAKE_LYRICS``, separator ``fake``).

Deterministic and network-free, with self-written text (``FAKE_LINES``) spread evenly over the
track (line *i* of *n* starts at ``duration * (i + 1) / (n + 1)``):

* ``word``: word timing from the "source" (origin ``source``, language ``en``);
* ``line``: line timing, words interpolated (origin ``interpolated``);
* ``plain``: text only (to be aligned);
* ``instrumental`` / ``not-found``: those outcomes;
* ``failed``: the job fails with the "could not be retrieved" text.
"""

from collections.abc import Callable
from typing import Final

from stemstudio_worker.config import FakeLyricsMode
from stemstudio_worker.lyrics.interpolate import interpolate_lrc
from stemstudio_worker.lyrics.lrc import LrcLine
from stemstudio_worker.lyrics.normalize import (
    DocumentMeta,
    finish,
    instrumental_document,
    plain_document,
)
from stemstudio_worker.ports import LyricsQuery, LyricsUnavailableError, ProgressCallback
from stemstudio_worker.schemas import (
    LYRICS_STAGE_SEARCHING,
    LyricsLine,
    LyricsResult,
    TimedLyrics,
)

FAKE_LINES: Final = ("Fake line one", "La la la", "Fixture words here", "Sing along now")
"""The self-written lines of every fake document."""

FAKE_SOURCE_ID: Final = "fake-1"
"""``sourceId`` of the fake LRCLIB record."""


class FakeLyricsLookup:
    """``LyricsLookup`` of the fake stack."""

    def __init__(self, mode: FakeLyricsMode) -> None:
        """Create the fake for one ``STEMSTUDIO_FAKE_LYRICS`` mode."""
        self._mode = mode

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        """Return the mode's result (``failed`` raises ``LyricsUnavailableError``)."""
        progress(0.0, LYRICS_STAGE_SEARCHING)
        result = self._result(query)
        progress(1.0, LYRICS_STAGE_SEARCHING)
        return result

    def _result(self, query: LyricsQuery) -> LyricsResult:
        """The result of the configured mode."""
        if self._mode == "failed":
            raise LyricsUnavailableError
        if self._mode == "not-found":
            return LyricsResult(outcome="not-found")
        meta = DocumentMeta(
            "lrclib", FAKE_SOURCE_ID, query.duration_seconds, "en", "\n".join(FAKE_LINES)
        )
        if self._mode == "instrumental":
            return LyricsResult(outcome="instrumental", lyrics=instrumental_document(meta))
        return LyricsResult(outcome="found", lyrics=_BUILDERS[self._mode](query, meta))


def _timed_lines(query: LyricsQuery) -> list[LyricsLine]:
    """``FAKE_LINES`` spread evenly over the track, words interpolated."""
    step = round(query.duration_seconds * 1000) // (len(FAKE_LINES) + 1)
    return interpolate_lrc([LrcLine(step * (i + 1), text) for i, text in enumerate(FAKE_LINES)])


def _word_document(query: LyricsQuery, meta: DocumentMeta) -> TimedLyrics:
    """Word-synced document: the interpolated times labelled as coming from the source."""
    lines = [
        line.model_copy(
            update={"words": [w.model_copy(update={"origin": "source"}) for w in line.words]}
        )
        for line in _timed_lines(query)
    ]
    return finish(lines, "word", "source", meta)


def _line_document(query: LyricsQuery, meta: DocumentMeta) -> TimedLyrics:
    """Line-synced document with interpolated words."""
    return finish(_timed_lines(query), "line", "interpolated", meta)


def _plain_document(query: LyricsQuery, meta: DocumentMeta) -> TimedLyrics:  # noqa: ARG001 (builder signature)
    """Plain document of the fake text."""
    return plain_document("\n".join(FAKE_LINES), meta)


_BUILDERS: Final[dict[str, Callable[[LyricsQuery, DocumentMeta], TimedLyrics]]] = {
    "word": _word_document,
    "line": _line_document,
    "plain": _plain_document,
}
