"""Tests of the fake section analyser of the E2E stack (014 T032, data-model 2.4)."""

from pathlib import Path

import pytest

from stemstudio_worker.analysis.structure import create_structure_analyzer
from stemstudio_worker.analysis.structure.fake import FakeStructureAnalyzer
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import StructureAnalyzer
from stemstudio_worker.schemas import (
    SECTION_WARNING_TEXTS,
    BeatGrid,
    SectionJobRequest,
    SectionResult,
    StemPaths,
)

_TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"
_STEMS = StemPaths(**{name: f"tracks/{_TRACK}/stems/{name}.flac"
                      for name in ("vocals", "drums", "bass", "other")})  # fmt: skip
_CYCLE = ["Intro", "Verse 1", "Chorus 1", "Verse 2", "Chorus 2", "Guitar solo", "Chorus 3",
          "Outro"]  # fmt: skip


def _grid(bpm: float, duration: float, *, reliable: bool = True, lead_in: float = 0.5) -> BeatGrid:
    """A constant grid of 4/4 bars starting at ``lead_in`` that stops before ``duration``."""
    period = 60 / bpm
    beats = [round(lead_in + i * period, 3) for i in range(int((duration - lead_in) / period))]
    return BeatGrid(bpm=bpm, beats=beats, downbeats=beats[::4], beats_per_bar=4,
                    confidence=0.9 if reliable else 0.2, reliable=reliable)  # fmt: skip


def _analyze(grid: BeatGrid | None, duration: float, max_sections: int = 64) -> SectionResult:
    request = SectionJobRequest(stems=_STEMS, grid=grid, duration_seconds=duration,
                                max_sections=max_sections)  # fmt: skip
    return FakeStructureAnalyzer().analyze(request, {}, lambda _fraction: None)


def _spans(result: SectionResult) -> list[tuple[float, float]]:
    return [(s.start_seconds, s.end_seconds) for s in result.sections]


def test_a_reliable_grid_gives_the_cycle_in_8_bar_sections() -> None:
    # 120 BPM: a bar is 2 s; intro = 4 bars after the 0.5 s lead-in, then 8 bars each.
    result = _analyze(_grid(120, 200), 200)

    assert [s.label for s in result.sections] == _CYCLE
    assert _spans(result)[:3] == [(0, 8.5), (8.5, 24.5), (24.5, 40.5)]
    assert result.sections[-1].start_seconds == 104.5
    assert result.sections[-1].end_seconds == 200
    assert (result.snap, result.warnings) == ("downbeat", [])
    assert (result.analyzer.name, result.analyzer.version) == ("fake", "1.0.0")


def test_kinds_clusters_instrument_and_confidence() -> None:
    result = _analyze(_grid(120, 200), 200)

    kinds = [s.kind for s in result.sections]
    assert kinds == ["intro", "verse", "chorus", "verse", "chorus", "solo", "chorus", "outro"]
    assert [s.cluster for s in result.sections] == ["A", "B", "C", "B", "C", "D", "C", "E"]
    assert [s.instrument for s in result.sections] == [None] * 5 + ["guitar", None, None]
    assert {s.confidence for s in result.sections} == {0.9}


def test_bars_continue_past_the_last_downbeat_with_the_grid_tempo() -> None:
    # The grid stops after 40 s, the song lasts 200 s: bar starts go on every 2 s.
    result = _analyze(_grid(120, 40), 200)

    assert _spans(result)[3] == (40.5, 56.5)
    assert result.sections[-1].start_seconds == 104.5


def test_an_unreliable_grid_snaps_to_beats_and_warns() -> None:
    result = _analyze(_grid(120, 200, reliable=False), 200)

    assert result.snap == "beat"
    assert [(w.code, w.message) for w in result.warnings] == [
        ("grid-unreliable", SECTION_WARNING_TEXTS["grid-unreliable"])
    ]
    assert _spans(result)[0] == (0, 8.5)


def test_without_a_grid_sections_are_8_times_2_seconds() -> None:
    result = _analyze(None, 100)

    assert result.snap == "time"
    assert result.warnings == []
    assert _spans(result) == [(0, 8), (8, 24), (24, 40), (40, 56), (56, 72), (72, 88), (88, 100)]
    assert [s.label for s in result.sections][-2:] == ["Guitar solo", "Chorus 3"]


def test_a_grid_without_downbeats_counts_bars_from_zero() -> None:
    grid = BeatGrid(bpm=120, beats=[], downbeats=[], confidence=0, reliable=False)

    assert _spans(_analyze(grid, 30)) == [(0, 8), (8, 24), (24, 30)]


@pytest.mark.parametrize(("duration", "labels"), [(5, ["Intro"]), (12, ["Intro", "Verse"])])
def test_short_songs_end_with_the_section_that_reaches_the_end(
    duration: float, labels: list[str]
) -> None:
    result = _analyze(None, duration)

    assert [s.label for s in result.sections] == labels
    assert result.sections[-1].end_seconds == duration


def test_long_songs_keep_the_outro_to_the_end() -> None:
    result = _analyze(None, 400)

    assert len(result.sections) == 8
    assert _spans(result)[-1] == (104, 400)


def test_max_sections_stops_early_and_the_last_section_reaches_the_end() -> None:
    result = _analyze(None, 400, max_sections=3)

    assert [s.label for s in result.sections] == ["Intro", "Verse", "Chorus"]
    assert _spans(result)[-1] == (24, 400)


def test_progress_ends_at_one_and_no_file_is_read(tmp_path: Path) -> None:
    seen: list[float] = []
    request = SectionJobRequest(stems=_STEMS, grid=None, duration_seconds=30)

    FakeStructureAnalyzer().analyze(request, {"vocals": tmp_path / "missing.flac"}, seen.append)

    assert seen == [1.0]


@pytest.mark.parametrize(("kind", "fake"), [("fake", True), ("real", False)])
def test_factory_follows_the_setting(tmp_path: Path, kind: str, *, fake: bool) -> None:
    analyzer = create_structure_analyzer(WorkerSettings(data_dir=tmp_path,
                                                        structure_analyzer=kind))  # type: ignore[arg-type]  # fmt: skip

    assert isinstance(analyzer, StructureAnalyzer)
    assert isinstance(analyzer, FakeStructureAnalyzer) is fake
    assert analyzer.name == ("fake" if fake else "stems-novelty")
    assert analyzer.version == "1.0.0"
