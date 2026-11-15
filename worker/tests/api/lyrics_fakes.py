"""Fakes for the lyrics route tests; documents are the contract examples (self-written text)."""

import threading
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import AlignRequest, LyricsQuery, ProgressCallback
from stemstudio_worker.schemas import LYRICS_STAGE_ALIGNING, LyricsResult, TimedLyrics

from .contract import spec

WAIT = 5.0


def example(name: str) -> LyricsResult:
    """The ``LyricsResult`` contract example ``name`` (e.g. ``WordSyncedLyricsResult``)."""
    value = spec()["components"]["examples"][name]["value"]
    return LyricsResult.model_validate(value)


def aligned() -> TimedLyrics:
    """The document of the ``AlignedLyricsResult`` example."""
    document = example("AlignedLyricsResult").lyrics
    assert document is not None
    return document


@dataclass
class ScriptedLookup:
    """A lyrics lookup returning ``result``."""

    result: LyricsResult = field(default_factory=lambda: example("WordSyncedLyricsResult"))
    queries: list[LyricsQuery] = field(default_factory=list)

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        self.queries.append(query)
        progress(0.5, "Searching lyrics")
        return self.result


@dataclass
class GatedAligner:
    """An aligner that waits for ``gate`` (set by default), then returns the aligned example."""

    gate: threading.Event = field(default_factory=threading.Event)
    entered: threading.Event = field(default_factory=threading.Event)
    devices: list[str] = field(default_factory=list)

    def __post_init__(self) -> None:
        self.gate.set()

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:
        self.devices.append(device)
        self.entered.set()
        assert self.gate.wait(WAIT), "aligner was never released"
        progress(0.5, LYRICS_STAGE_ALIGNING)
        return aligned()


class QuietVocals:
    """Loads every stem as one second of quiet 16 kHz audio."""

    def load(self, path: Path) -> npt.NDArray[np.float32]:
        assert path.is_file()
        return np.full(16_000, 0.1, dtype=np.float32)
