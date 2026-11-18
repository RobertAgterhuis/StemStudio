"""The ``stems-novelty`` analyser on the 6-form synthetic set (014 T050, SC-001, SC-002, FR-033).

Forms as in spike T010 (research R-10), generated in-test (``songs.py``): recall >= 0.80 and
precision >= 0.75 of the interior boundaries within one bar over the set; the special section of
each song (the solo with its instrument, the bridge, the drums-only break) right in >= 5 of 6;
under 10 s for a 5-minute song.
"""

import time
from functools import cache
from pathlib import Path

import pytest

from stemstudio_worker.analysis.structure import StemsNoveltyAnalyzer
from stemstudio_worker.ports import StructureAnalyzer, StructureError
from stemstudio_worker.schemas import SectionJobRequest, SectionResult, StemPaths

from .songs import BAR, GUITAR_SOLO, SIX_FORMS, Form, Song, make_song

_TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"
_EXPECTED = {
    "guitar": ("solo", "guitar"), "piano": ("solo", "piano"), "bass": ("solo", "bass"),
    "woodwinds": ("solo", "other.woodwinds"), "bridge": ("bridge", None),
    "break": ("instrumental", None),
}  # fmt: skip
_SPECIAL = ("S", "B", "D")


def _request(song: Song, *, reliable: bool = True, max_sections: int = 64) -> SectionJobRequest:
    names = ("vocals", "drums", "bass", "guitar", "piano", "other")
    stems = StemPaths(**{name: f"tracks/{_TRACK}/stems/{name}.flac" for name in names})
    return SectionJobRequest(stems=stems, grid=song.grid(reliable=reliable),
                             duration_seconds=song.duration, max_sections=max_sections)  # fmt: skip


@cache
def _analysed(name: str, folder: Path) -> tuple[Song, SectionResult]:
    song = make_song(SIX_FORMS[name])
    files = song.write(folder / name)
    return song, StemsNoveltyAnalyzer().analyze(_request(song), files, lambda _f: None)


def _special(song: Song, result: SectionResult) -> tuple[str, str | None]:
    start = 0.0
    for kind, bars in song.form:
        if kind[0] in _SPECIAL:
            middle = start + bars * BAR / 2
            found = next(s for s in result.sections if s.start_seconds <= middle < s.end_seconds)
            return found.kind, found.instrument
        start += bars * BAR
    raise AssertionError("form without a special section")


@pytest.fixture(scope="module")
def results(tmp_path_factory: pytest.TempPathFactory) -> dict[str, tuple[Song, SectionResult]]:
    folder = tmp_path_factory.mktemp("songs")
    return {name: _analysed(name, folder) for name in SIX_FORMS}


def _hits(found: list[float], truth: list[float]) -> int:
    return sum(any(abs(f - t) <= BAR + 1e-6 for t in truth) for f in found)


def test_boundary_recall_and_precision_over_the_set(
    results: dict[str, tuple[Song, SectionResult]],
) -> None:
    found_total = truth_total = recall_hits = precision_hits = 0
    for song, result in results.values():
        found = [s.start_seconds for s in result.sections[1:]]
        recall_hits += _hits(song.boundaries, found)
        precision_hits += _hits(found, song.boundaries)
        found_total += len(found)
        truth_total += len(song.boundaries)

    assert recall_hits / truth_total >= 0.80
    assert precision_hits / found_total >= 0.75


def test_the_special_section_is_right_in_five_of_six(
    results: dict[str, tuple[Song, SectionResult]],
) -> None:
    right = [name for name, (song, result) in results.items()
             if _special(song, result) == _EXPECTED[name]]  # fmt: skip

    assert len(right) >= 5, {name: _special(*results[name]) for name in results}


def test_results_cover_the_song_and_snap_to_downbeats(
    results: dict[str, tuple[Song, SectionResult]],
) -> None:
    for song, result in results.values():
        assert result.snap == "downbeat"
        assert result.sections[0].start_seconds == 0
        assert result.sections[-1].end_seconds == song.duration
        assert all(s.start_seconds % BAR == 0 for s in result.sections)
        assert all(0 <= s.confidence <= 1 for s in result.sections)
        assert (result.analyzer.name, result.analyzer.version) == ("stems-novelty", "1.0.0")


def test_a_five_minute_song_takes_under_ten_seconds(tmp_path: Path) -> None:
    form: Form = list(GUITAR_SOLO) * 3  # 168 bars = 336 s
    song = make_song(form)
    files = song.write(tmp_path)
    seen: list[float] = []

    started = time.perf_counter()
    result = StemsNoveltyAnalyzer().analyze(_request(song), files, seen.append)
    elapsed = time.perf_counter() - started

    assert elapsed < 10.0
    assert seen == sorted(seen) and seen[-1] == 1.0
    assert len(result.sections) > 8


def test_an_unreliable_grid_snaps_to_beats_and_warns(tmp_path: Path) -> None:
    song = make_song(SIX_FORMS["break"])

    result = StemsNoveltyAnalyzer().analyze(_request(song, reliable=False), song.write(tmp_path),
                                            lambda _f: None)  # fmt: skip

    assert result.snap == "beat"
    assert [w.code for w in result.warnings] == ["grid-unreliable"]


def test_no_grid_gives_two_second_frames_and_a_warning(tmp_path: Path) -> None:
    song = make_song(SIX_FORMS["break"])
    request = _request(song).model_copy(update={"grid": None})

    result = StemsNoveltyAnalyzer().analyze(request, song.write(tmp_path), lambda _f: None)

    assert result.snap == "time"
    assert "no-grid" in [w.code for w in result.warnings]


def test_the_section_limit_merges_and_warns(tmp_path: Path) -> None:
    song = make_song(SIX_FORMS["guitar"])

    result = StemsNoveltyAnalyzer().analyze(_request(song, max_sections=3), song.write(tmp_path),
                                            lambda _f: None)  # fmt: skip

    assert len(result.sections) <= 3
    assert "merged-to-limit" in [w.code for w in result.warnings]


def test_a_song_without_vocals_warns(tmp_path: Path) -> None:
    song = make_song(SIX_FORMS["break"])
    files = song.write(tmp_path)
    song.stems["vocals"][:] = 0
    files = {**files, **Song({"vocals": song.stems["vocals"]}, song.form, song.duration).write(
        tmp_path / "silent")}  # fmt: skip

    result = StemsNoveltyAnalyzer().analyze(_request(song), files, lambda _f: None)

    assert "no-vocals" in [w.code for w in result.warnings]
    assert {s.kind for s in result.sections} <= {"intro", "outro", "instrumental", "solo", "other"}


def test_a_broken_stem_fails_readably(tmp_path: Path) -> None:
    (tmp_path / "vocals.flac").write_bytes(b"broken")
    song = make_song(SIX_FORMS["break"])

    with pytest.raises(StructureError, match="could not be read"):
        StemsNoveltyAnalyzer().analyze(_request(song), {"vocals": tmp_path / "vocals.flac"},
                                       lambda _f: None)  # fmt: skip


def test_it_is_a_structure_analyzer() -> None:
    assert isinstance(StemsNoveltyAnalyzer(), StructureAnalyzer)
