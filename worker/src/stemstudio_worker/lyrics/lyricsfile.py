"""LRCLIB ``lyricsfile`` parser: word-timed lyrics as YAML (research F-03, R-03).

Read with ``yaml.safe_load`` only, after a scan of the parse events that rejects every anchor and
alias (no "billion laughs" amplification past the 1 MB input cap). Words carry only
``start_ms``: a word ends where the next word of its line starts, the last word at the line's
``end_ms``; every word lasts at most 3000 ms and at least 1 ms, and a line grows to hold its
words. ``metadata.language`` gives the language.
"""

from typing import Final

import yaml
from pydantic import ConfigDict, Field, ValidationError

from stemstudio_worker.lyrics.normalize import (
    DocumentMeta,
    WordSpan,
    check_input,
    check_line_count,
    finish,
    timed_line,
)
from stemstudio_worker.metadata.http import LenientModel
from stemstudio_worker.ports import LyricsUnusableError
from stemstudio_worker.schemas import MAX_WORD_MS, LyricsLine, TimedLyrics

_TEXT_CONFIG: Final = ConfigDict(extra="ignore", frozen=True, coerce_numbers_to_str=True)


class _Word(LenientModel):
    model_config = _TEXT_CONFIG

    text: str = ""
    start_ms: int


class _Line(LenientModel):
    model_config = _TEXT_CONFIG

    text: str = ""
    start_ms: int
    end_ms: int
    words: list[_Word] = Field(default_factory=list)


class _Metadata(LenientModel):
    model_config = _TEXT_CONFIG

    language: str | None = None


class _Lyricsfile(LenientModel):
    metadata: _Metadata | None = None
    lines: list[_Line] = Field(default_factory=list)


def parse_lyricsfile(text: str, meta: DocumentMeta) -> TimedLyrics:
    """Parse a ``lyricsfile`` into a word-synced document (origin ``source``).

    Args:
        text: The YAML text.
        meta: Source fields; its language is used when the file names none.

    Raises:
        LyricsUnusableError: Not readable, no lines, over a limit or a broken invariant.
    """
    record = _read(check_input(text))
    check_line_count(len(record.lines))
    lines = [_line(line) for line in record.lines if line.text.strip()]
    language = (record.metadata.language if record.metadata else None) or meta.language
    meta = DocumentMeta(
        meta.source, meta.source_id, meta.source_duration_seconds, language, meta.plain_text
    )
    return finish(lines, "word", "source", meta)


def _read(text: str) -> _Lyricsfile:
    """Load and validate the YAML structure (anchors and aliases are refused first)."""
    try:
        _refuse_aliases(text)
        return _Lyricsfile.model_validate(yaml.safe_load(text))
    except (yaml.YAMLError, ValidationError):
        raise LyricsUnusableError from None


def _refuse_aliases(text: str) -> None:
    """Raise ``LyricsUnusableError`` when the YAML uses an anchor or an alias."""
    for event in yaml.parse(text, Loader=yaml.SafeLoader):
        if isinstance(event, yaml.AliasEvent) or getattr(event, "anchor", None) is not None:
            raise LyricsUnusableError


def _line(line: _Line) -> LyricsLine:
    """One timed line from its word starts."""
    words = [word for word in line.words if word.text.strip()]
    spans = _spans(words, line.end_ms)
    start = min([line.start_ms, *(span[1] for span in spans)])
    end = max([line.end_ms, *(span[2] for span in spans)])
    return timed_line(line.text.strip(), (start, end), spans, "source")


def _spans(words: list[_Word], line_end: int) -> list[WordSpan]:
    """Word ends: the next start (or the line end), capped at 3000 ms, at least 1 ms."""
    starts = [word.start_ms for word in words]
    limits = [*starts[1:], line_end]
    return [
        (word.text.strip(), start, max(min(limit, start + MAX_WORD_MS), start + 1))
        for word, start, limit in zip(words, starts, limits, strict=True)
    ]
