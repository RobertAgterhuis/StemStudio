"""The fake section analyser of the E2E stack (data-model 2.4; owned by stem-separation).

Deterministic from ``durationSeconds`` and the grid; it reads no audio. Bars start at the grid's
downbeats and go on with the grid tempo after the last one (2 s bars without a usable grid). The
song is cut into ``intro`` (4 bars) and then 8-bar sections of the cycle ``verse, chorus, verse,
chorus, solo (guitar), chorus, outro``; the last section runs to the end of the song.
"""

from collections.abc import Sequence
from typing import Final

from stemstudio_worker.analysis.structure.names import numbered_labels
from stemstudio_worker.analysis.structure.times import contiguous_spans, section_time
from stemstudio_worker.ports import SectionProgress, StemFiles
from stemstudio_worker.schemas import (
    SECTION_WARNING_TEXTS,
    AnalyzerInfo,
    BeatGrid,
    DetectedSection,
    SectionJobRequest,
    SectionKind,
    SectionResult,
    SectionSnap,
    SectionWarning,
    SectionWarningCode,
    StructureAnalyzerName,
)

FAKE_VERSION: Final = "1.0.0"
_CYCLE: Final[tuple[tuple[SectionKind, str, str | None], ...]] = (
    ("intro", "A", None), ("verse", "B", None), ("chorus", "C", None), ("verse", "B", None),
    ("chorus", "C", None), ("solo", "D", "guitar"), ("chorus", "C", None), ("outro", "E", None),
)  # fmt: skip
_INTRO_BARS: Final = 4
_SECTION_BARS: Final = 8
_FALLBACK_BAR_SECONDS: Final = 2.0
_CONFIDENCE: Final = 0.9


class FakeStructureAnalyzer:
    """:class:`~stemstudio_worker.ports.StructureAnalyzer` ``fake`` 1.0.0."""

    name: StructureAnalyzerName = "fake"
    version: str = FAKE_VERSION

    def analyze(
        self,
        request: SectionJobRequest,
        files: StemFiles,  # noqa: ARG002 - the fake reads no audio
        progress: SectionProgress,
    ) -> SectionResult:
        """Cut the song into the fixed cycle; reports progress 1.0 once."""
        duration = request.duration_seconds
        starts = _section_starts(_bar_clock(request.grid), duration, request.max_sections)
        result = SectionResult(analyzer=AnalyzerInfo(name="fake", version=FAKE_VERSION),
                               snap=_snap(request.grid), sections=_sections(starts, duration),
                               warnings=_warnings(request.grid))  # fmt: skip
        progress(1.0)
        return result


class _BarClock:
    """Start time of bar *k*: the grid's downbeats, then steps of one bar period."""

    def __init__(self, downbeats: Sequence[float], period: float) -> None:
        self._downbeats = list(downbeats) or [0.0]
        self._period = period

    def start(self, bar: int) -> float:
        if bar < len(self._downbeats):
            return self._downbeats[bar]
        return round(self._downbeats[-1] + (bar - len(self._downbeats) + 1) * self._period, 3)


def _bar_clock(grid: BeatGrid | None) -> _BarClock:
    if grid is None or grid.bpm <= 0:
        return _BarClock([0.0], _FALLBACK_BAR_SECONDS)
    return _BarClock(grid.downbeats, grid.beats_per_bar * 60 / grid.bpm)


def _section_starts(clock: _BarClock, duration: float, max_sections: int) -> list[float]:
    """Start of every section; the first is 0 (the lead-in belongs to the intro)."""
    starts = [0.0]
    bar = _INTRO_BARS
    while (
        len(starts) < min(len(_CYCLE), max_sections) and section_time(clock.start(bar)) < duration
    ):
        starts.append(clock.start(bar))
        bar += _SECTION_BARS
    return starts


def _sections(starts: list[float], duration: float) -> list[DetectedSection]:
    cycle = _CYCLE[: len(starts)]
    labels = numbered_labels([c[0] for c in cycle], [c[1] for c in cycle], [c[2] for c in cycle])
    spans = contiguous_spans(starts[1:], duration)
    return [
        DetectedSection(kind=kind, label=label, start_seconds=start, end_seconds=end,
                        cluster=cluster, instrument=instrument, confidence=_CONFIDENCE)
        for (kind, cluster, instrument), label, (start, end) in zip(cycle, labels, spans,
                                                                    strict=True)
    ]  # fmt: skip


def _snap(grid: BeatGrid | None) -> SectionSnap:
    if grid is None:
        return "time"
    return "downbeat" if grid.reliable else "beat"


def _warnings(grid: BeatGrid | None) -> list[SectionWarning]:
    if grid is None or grid.reliable:
        return []
    code: SectionWarningCode = "grid-unreliable"
    return [SectionWarning(code=code, message=SECTION_WARNING_TEXTS[code])]
