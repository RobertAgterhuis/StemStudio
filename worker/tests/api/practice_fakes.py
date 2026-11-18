"""Scriptable fakes of the practice ports and request bodies for the practice route tests."""

import threading
from pathlib import Path
from typing import Any

from stemstudio_worker.ports import (
    ScoreView,
    SectionProgress,
    SetlistDocument,
    StemFiles,
    StructureError,
)
from stemstudio_worker.schemas import (
    AnalyzerInfo,
    BarBox,
    DetectedSection,
    SectionJobRequest,
    SectionResult,
    SetlistDocumentRequest,
    StructureAnalyzerName,
    TempoGrid,
)

WAIT = 5.0
TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"
ASSET = "9a8b7c6d-1111-4222-8333-444455556666"
STEMS = ("vocals", "drums", "bass", "guitar", "piano", "other")
MUSICXML = f"assets/{ASSET}/score.musicxml"
PDF = b"%PDF-1.7 fake"


def stem_path(name: str) -> str:
    """Library path of a standard stem of the test track."""
    return f"tracks/{TRACK}/stems/{name}.flac"


def write_library(root: Path) -> None:
    """Every stem, one detail stem and the score of the test track below ``root``."""
    for name in STEMS:
        _touch(root / stem_path(name), b"fLaC")
    _touch(root / f"tracks/{TRACK}/detail/other/woodwinds.flac", b"fLaC")
    _touch(root / MUSICXML, b"<score-partwise/>")


def _touch(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def section_body(**changes: Any) -> dict[str, Any]:
    """A valid ``SectionJobRequest`` (six stems, one detail) with ``changes``."""
    body: dict[str, Any] = {
        "stems": {name: stem_path(name) for name in STEMS},
        "details": [{"channel": "other.woodwinds",
                     "path": f"tracks/{TRACK}/detail/other/woodwinds.flac"}],
        "grid": None, "durationSeconds": 60, "maxSections": 64,
    }  # fmt: skip
    body.update(changes)
    return body


class _Gated:
    """``gate`` holds the work after its first progress report; ``entered`` says it started."""

    def __init__(self) -> None:
        self.gate = threading.Event()
        self.gate.set()
        self.entered = threading.Event()
        self.error: Exception | None = None

    def _hold(self, progress: SectionProgress | None = None) -> None:
        if progress is not None:
            progress(0.5)
        self.entered.set()
        assert self.gate.wait(WAIT)
        if self.error is not None:
            raise self.error


class FakeAnalyzer(_Gated):
    """Records the resolved files and answers one ``other`` section."""

    name: StructureAnalyzerName = "fake"
    version: str = "1.0.0"

    def __init__(self) -> None:
        super().__init__()
        self.files: list[StemFiles] = []

    def analyze(
        self, request: SectionJobRequest, files: StemFiles, progress: SectionProgress
    ) -> SectionResult:
        self.files.append(files)
        self._hold(progress)
        section = DetectedSection(kind="other", label="Song", start_seconds=0,
                                  end_seconds=request.duration_seconds, cluster="A",
                                  instrument=None, confidence=0.5)  # fmt: skip
        return SectionResult(analyzer=AnalyzerInfo(name="fake", version="1.0.0"), snap="time",
                             sections=[section], warnings=[])  # fmt: skip


class FakeScoreViews(_Gated):
    """Two pages and two bars; records the MusicXML it got."""

    def __init__(self) -> None:
        super().__init__()
        self.inputs: list[bytes] = []

    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView:
        self.inputs.append(musicxml)
        self._hold()
        bars = (_bar(0, 1, 0.0, 2.0), _bar(1, 2, 2.0, duration_seconds))
        return ScoreView(pages=("<svg>1</svg>", "<svg>2</svg>"), bars=bars, engine_version="6.4.0")


def _bar(index: int, page: int, start: float, end: float) -> BarBox:
    return BarBox(index=index, number=index + 1, page=page, x=0.1, y=0.1, width=0.2, height=0.1,
                  start_seconds=start, end_seconds=end)  # fmt: skip


class FakeDocuments(_Gated):
    """A fixed PDF with one page per song."""

    def render(self, request: SetlistDocumentRequest) -> SetlistDocument:
        self._hold()
        return SetlistDocument(pdf=PDF, pages=len(request.songs))


__all__ = ["StructureError"]
