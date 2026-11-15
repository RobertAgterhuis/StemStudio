"""Shared lyrics normalisation: input limits, word splitting and document building (R-03).

Every parser ends in ``finish``, which builds the contract ``TimedLyrics`` and turns any broken
invariant (data-model.md 2.1) or exceeded limit into ``LyricsUnusableError``. Limits: at most
1 MB input, 400 lines, 40 words and 500 characters per line, 100 characters per word.
"""

import re
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

from pydantic import ValidationError

from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import (
    MAX_LYRICS_LINES,
    LyricsLine,
    LyricsOrigin,
    LyricsSource,
    LyricsWord,
    SyncLevel,
    TimedLyrics,
    WordOrigin,
)

MAX_INPUT_BYTES: Final = 1_000_000
"""Largest accepted lyrics input (UTF-8 bytes)."""

MAX_LINE_CHARS: Final = 500
"""Longest line (characters)."""

MAX_WORDS_PER_LINE: Final = 40
"""Most words in one line."""

MAX_WORD_CHARS: Final = 100
"""Longest word (characters)."""

WordSpan = tuple[str, int, int]
"""``(text, start_ms, end_ms)`` of one timed word."""

_CJK: Final = "[぀-ヿ㐀-䶿一-鿿가-힯豈-﫿\U00020000-\U0002ebef]"
_PIECE: Final = re.compile(f"{_CJK}|(?:(?!{_CJK}).)+")
_LANGUAGE: Final = re.compile(r"[a-z]{2,3}")
_SUBTAG: Final = re.compile(r"[-_]")


@dataclass(frozen=True, slots=True)
class DocumentMeta:
    """Everything of a ``TimedLyrics`` document except its timing.

    Attributes:
        source: ``lrclib`` or ``user``.
        source_id: LRCLIB record id.
        source_duration_seconds: Duration of the LRCLIB record.
        language: Language tag (reduced to its primary subtag; unknown values are dropped).
        plain_text: The text as received; None = the line texts joined by line breaks.
    """

    source: LyricsSource
    source_id: str | None = None
    source_duration_seconds: float | None = None
    language: str | None = None
    plain_text: str | None = None


def check_input(text: str) -> str:
    """Reject input over ``MAX_INPUT_BYTES`` and normalise line breaks to LF.

    Raises:
        LyricsUnusableError: The input is larger than 1 MB.
    """
    if len(text) > MAX_INPUT_BYTES or len(text.encode("utf-8")) > MAX_INPUT_BYTES:
        raise LyricsUnusableError
    return text.replace("\r\n", "\n").replace("\r", "\n")


def split_words(text: str) -> list[str]:
    """Split a line into words: on whitespace, CJK characters one by one.

    Punctuation next to a CJK character joins the word before it (``"ラ、"``).
    """
    return [word for token in text.split() for word in _token_words(token)]


def _token_words(token: str) -> list[str]:
    """The words of one whitespace-free token."""
    pieces: list[str] = _PIECE.findall(token)
    words = pieces[:1]
    for piece in pieces[1:]:
        if any(char.isalnum() for char in piece):
            words.append(piece)
        else:
            words[-1] += piece
    return words


def check_line(text: str, words: Sequence[str]) -> None:
    """Reject a line without words or over the line, word-count or word-length limits.

    Raises:
        LyricsUnusableError: The line cannot be used.
    """
    too_long = len(text) > MAX_LINE_CHARS or any(len(word) > MAX_WORD_CHARS for word in words)
    if too_long or not 0 < len(words) <= MAX_WORDS_PER_LINE:
        raise LyricsUnusableError


def check_line_count(count: int) -> None:
    """Reject more than ``MAX_LYRICS_LINES`` lines.

    Raises:
        LyricsUnusableError: Too many lines.
    """
    if count > MAX_LYRICS_LINES:
        raise LyricsUnusableError


def timed_line(
    text: str, bounds: tuple[int, int], spans: Sequence[WordSpan], origin: WordOrigin
) -> LyricsLine:
    """Build one timed line.

    Args:
        text: The line text.
        bounds: ``(start_ms, end_ms)`` of the line.
        spans: The timed words.
        origin: Origin of every word time.

    Raises:
        LyricsUnusableError: Over a limit, or a time is negative or not after its start.
    """
    check_line(text, [word for word, _, _ in spans])
    words = [_timed_word(span, origin) for span in spans]
    try:
        return LyricsLine(start_ms=bounds[0], end_ms=bounds[1], text=text, words=words)
    except ValidationError:
        raise LyricsUnusableError from None


def _timed_word(span: WordSpan, origin: WordOrigin) -> LyricsWord:
    """Build one timed word."""
    try:
        return LyricsWord(text=span[0], start_ms=span[1], end_ms=span[2], origin=origin)
    except ValidationError:
        raise LyricsUnusableError from None


def plain_lines(text: str) -> list[LyricsLine]:
    """Build untimed lines from text: one per non-empty line, stripped.

    Raises:
        LyricsUnusableError: Over a limit.
    """
    rows = [row.strip() for row in text.split("\n")]
    rows = [row for row in rows if row]
    check_line_count(len(rows))
    return [_plain_line(row) for row in rows]


def _plain_line(text: str) -> LyricsLine:
    """Build one untimed line."""
    words = split_words(text)
    check_line(text, words)
    return LyricsLine(text=text, words=[LyricsWord(text=word) for word in words])


def plain_document(text: str, meta: DocumentMeta) -> TimedLyrics:
    """Build a ``plain`` document (no times; to be aligned) from text.

    Raises:
        LyricsUnusableError: The text is empty or over a limit.
    """
    text = check_input(text)
    plain = text if meta.plain_text is None else meta.plain_text
    return finish(plain_lines(text), "plain", "none", _with_text(meta, plain))


def _with_text(meta: DocumentMeta, plain_text: str) -> DocumentMeta:
    """``meta`` with ``plain_text`` set."""
    return DocumentMeta(
        meta.source, meta.source_id, meta.source_duration_seconds, meta.language, plain_text
    )


def finish(
    lines: list[LyricsLine], level: SyncLevel, origin: LyricsOrigin, meta: DocumentMeta
) -> TimedLyrics:
    """Build the contract document and check its invariants.

    Args:
        lines: At least one line.
        level: ``word``, ``line`` or ``plain``.
        origin: Dominant word origin (``none`` for plain documents).
        meta: Source, language and plain text.

    Raises:
        LyricsUnusableError: No lines, too many lines or a broken invariant.
    """
    check_line_count(len(lines))
    if not lines:
        raise LyricsUnusableError
    fields = {"sync_level": level, "origin": origin, "lines": lines, **_meta_fields(meta, lines)}
    try:
        return TimedLyrics.model_validate(fields)
    except ValidationError:
        raise LyricsUnusableError from None


def _meta_fields(meta: DocumentMeta, lines: list[LyricsLine]) -> dict[str, object]:
    """The ``TimedLyrics`` fields that come from ``meta`` (plain text defaults to the lines)."""
    plain = "\n".join(line.text for line in lines) if meta.plain_text is None else meta.plain_text
    return {
        "language": _language(meta.language),
        "source": meta.source,
        "source_id": meta.source_id,
        "source_duration_seconds": meta.source_duration_seconds,
        "plain_text": plain,
    }


def _language(value: str | None) -> str | None:
    """The primary subtag of a language tag, or None when it is not a 2-3 letter code."""
    primary = _SUBTAG.split(value or "", maxsplit=1)[0].lower()
    return primary if _LANGUAGE.fullmatch(primary) else None
