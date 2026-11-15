"""Fakes and builders for the lyrics job tests: scriptable lookup, aligner and vocal loader.

All lyrics text here is self-written (no third-party lyrics, research.md R-07).
"""

import threading
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import AlignRequest, LyricsQuery, ProgressCallback
from stemstudio_worker.schemas import (
    LYRICS_STAGE_ALIGNING,
    LYRICS_STAGE_SEARCHING,
    AlignLyricsRequest,
    LookupLyricsRequest,
    LyricsLine,
    LyricsResult,
    LyricsWord,
    TimedLyrics,
)

WAIT = 5.0
TEXT = "Fake line one\nLa la la"


def _word(text: str, start: int, end: int, origin: str) -> LyricsWord:
    return LyricsWord.model_validate(
        {"text": text, "startMs": start, "endMs": end, "origin": origin}
    )


def timed(origin: str = "aligned", source: str = "user", level: str = "word") -> TimedLyrics:
    """A two-line document with self-written text."""
    first = [_word("Fake", 1000, 1400, origin), _word("line", 1400, 1800, origin),
             _word("one", 1800, 2200, origin)]  # fmt: skip
    second = [_word("La", 3000, 3300, origin), _word("la", 3300, 3600, origin),
              _word("la", 3600, 4000, origin)]  # fmt: skip
    lines = [
        LyricsLine(start_ms=1000, end_ms=2200, text="Fake line one", words=first),
        LyricsLine(start_ms=3000, end_ms=4000, text="La la la", words=second),
    ]
    return TimedLyrics.model_validate(
        {"syncLevel": level, "origin": origin, "source": source, "lines": lines,
         "plainText": TEXT, "language": "en"}
    )  # fmt: skip


ALIGNED = timed()
FOUND = LyricsResult(outcome="found", lyrics=timed("source", "lrclib"))
LOOKUP = LookupLyricsRequest(title="Fake Song", artist="Fake Band", duration_seconds=180.0)


def align_request(standard_job_id: str, **changes: object) -> AlignLyricsRequest:
    """An align request for ``standard_job_id`` with the self-written text."""
    body = {"mode": "align", "standardJobId": standard_job_id, "text": TEXT, **changes}
    return AlignLyricsRequest.model_validate(body)


class _Gate:
    """Lets a fake block until released and tells when it was entered."""

    def __init__(self) -> None:
        self.entered = threading.Event()
        self.release: threading.Event | None = None

    def block(self) -> threading.Event:
        self.release = threading.Event()
        return self.release

    def enter(self) -> None:
        self.entered.set()
        if self.release is not None:
            assert self.release.wait(WAIT), "fake was never released"


@dataclass
class FakeLyricsLookup:
    """Returns ``result`` (or raises ``error``) after reporting the searching stage."""

    result: LyricsResult = FOUND
    error: Exception | None = None
    queries: list[LyricsQuery] = field(default_factory=list)
    gate: _Gate = field(default_factory=_Gate)

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        self.queries.append(query)
        self.gate.enter()
        progress(0.0, LYRICS_STAGE_SEARCHING)
        if self.error is not None:
            raise self.error
        return self.result


@dataclass
class FakeAligner:
    """Records requests and devices; reports ``steps`` progress, then returns ``document``."""

    document: TimedLyrics = ALIGNED
    error: Exception | None = None
    steps: tuple[tuple[float, str], ...] = ((0.4, LYRICS_STAGE_ALIGNING),)
    calls: list[tuple[AlignRequest, str]] = field(default_factory=list)
    gate: _Gate = field(default_factory=_Gate)

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:
        self.calls.append((request, device))
        self.gate.enter()
        for fraction, stage in self.steps:
            progress(fraction, stage)
        if self.error is not None:
            raise self.error
        return self.document


@dataclass
class FakeVocals:
    """Returns one second of quiet audio at 16 kHz (or raises ``error``)."""

    error: Exception | None = None
    paths: list[Path] = field(default_factory=list)

    def load(self, path: Path) -> npt.NDArray[np.float32]:
        self.paths.append(path)
        if self.error is not None:
            raise self.error
        return np.full(16_000, 0.1, dtype=np.float32)
