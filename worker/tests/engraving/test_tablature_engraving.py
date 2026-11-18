"""Verovio engraving of TAB staves (016 T037, data-model 4.5, ADR-033 D6 native Verovio, SC-002)."""

import pytest

from stemstudio_worker.engraving import create_score_engraver
from stemstudio_worker.engraving.tablature import (
    TAB_ENGRAVING_FAILED_TEXT,
    check_tab_pages,
    direction_texts,
    tab_staves,
)
from stemstudio_worker.engraving.verovio_svg import render_svg_pages
from stemstudio_worker.notation.notator import MusicNotator
from stemstudio_worker.ports import NotationSettings, NoteEvent, TranscriptionError
from stemstudio_worker.schemas import (
    BeatGridTempo,
    ScoreProfile,
    ScoreTitles,
    TablatureSettings,
    TabLayout,
    TuningId,
)

from .scores import is_leipzig, music_glyph_fonts, read_pdf
from .test_engraver import needs_leipzig

GRID = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[i * 0.5 for i in range(80)], downbeats=[0.0])


def _musicxml(
    profile: ScoreProfile, tuning: TuningId, pitches: list[int], layout: TabLayout, capo: int = 0
) -> bytes:
    settings = NotationSettings(
        profile=profile,
        grid=GRID,
        resolution="sixteenth",
        key_override=None,
        titles=ScoreTitles(title="Tab"),
        duration_seconds=40.0,
        tablature=TablatureSettings(tuning=tuning, capo=capo, layout=layout),
    )
    notes = [NoteEvent(i * 0.5, (i + 1) * 0.5, p, 0.8) for i, p in enumerate(pitches)]
    return MusicNotator().notate(notes, settings).musicxml


CASES = [
    (
        "guitar",
        "guitar-standard",
        [40, 45, 50, 55, 59, 64, 67, 52],
        6,
        ["0", "0", "0", "0", "0", "0", "3", "2"],
    ),
    ("bass", "bass-5-standard", [23, 30, 35, 40], 5, ["0", "2", "2", "2"]),
    ("bass", "bass-4-standard", [28, 33, 38, 43, 45], 4, ["0", "0", "0", "0", "2"]),
]


@pytest.mark.parametrize("layout", ["notation-and-tab", "tab-only"])
@pytest.mark.parametrize(("profile", "tuning", "pitches", "lines", "frets"), CASES)
def test_tab_staff_has_the_string_count_and_the_fret_numbers(
    profile: ScoreProfile,
    tuning: TuningId,
    pitches: list[int],
    lines: int,
    frets: list[str],
    layout: TabLayout,
) -> None:
    pages = render_svg_pages(_musicxml(profile, tuning, pitches, layout))
    staves = [staff for page in pages for staff in tab_staves(page)]
    assert [s.lines for s in staves] == [lines]
    assert list(staves[0].frets) == frets


def test_notation_and_tab_keeps_a_five_line_notation_staff() -> None:
    (page,) = render_svg_pages(
        _musicxml("guitar", "guitar-drop-d", [38, 45, 50], "notation-and-tab")
    )
    assert [s.lines for s in tab_staves(page, tab_only=False)] == [5, 6]


def test_capo_is_printed_as_a_direction_and_frets_count_from_it() -> None:
    (page,) = render_svg_pages(
        _musicxml("guitar", "guitar-standard", [43, 48, 53], "tab-only", capo=3)
    )
    assert "Capo 3" in direction_texts(page)
    assert list(tab_staves(page)[0].frets) == ["0", "0", "0"]


def test_check_accepts_the_expected_string_count() -> None:
    pages = render_svg_pages(_musicxml("bass", "bass-4-standard", [28, 33], "tab-only"))
    check_tab_pages(pages, strings=4)


@pytest.mark.parametrize("strings", [5, 6])
def test_check_refuses_a_wrong_string_count(strings: int) -> None:
    pages = render_svg_pages(_musicxml("bass", "bass-4-standard", [28, 33], "tab-only"))
    with pytest.raises(TranscriptionError, match=TAB_ENGRAVING_FAILED_TEXT):
        check_tab_pages(pages, strings=strings)


def test_check_refuses_pages_without_a_tab_staff() -> None:
    settings = NotationSettings(
        profile="guitar",
        grid=GRID,
        resolution="sixteenth",
        key_override=None,
        titles=ScoreTitles(title="No tab"),
        duration_seconds=40.0,
    )
    plain = MusicNotator().notate([NoteEvent(0.0, 0.5, 52, 0.8)], settings).musicxml
    with pytest.raises(TranscriptionError, match=TAB_ENGRAVING_FAILED_TEXT):
        check_tab_pages(render_svg_pages(plain), strings=6)


def test_svg_without_staves_has_no_tab_staff() -> None:
    assert tab_staves('<svg xmlns="http://www.w3.org/2000/svg"><g class="page"/></svg>') == ()
    assert direction_texts('<svg xmlns="http://www.w3.org/2000/svg"/>') == []


@needs_leipzig
def test_tab_score_engraves_to_a_pdf_without_missing_glyphs() -> None:
    musicxml = _musicxml("guitar", "guitar-drop-d", [38, 45, 50, 55, 59, 64], "notation-and-tab", 2)
    result = create_score_engraver().engrave(musicxml, "Tab", None)
    pages = read_pdf(result.pdf).pages
    assert len(pages) == result.pages == 1
    glyph_fonts = music_glyph_fonts(pages[0])
    assert glyph_fonts
    assert all(is_leipzig(name) for name in glyph_fonts), glyph_fonts
