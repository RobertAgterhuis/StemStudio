"""Tests of the song handout layout (020 T068, FR-012, data-model 6.4-6.6, research R-07/R-09/R-10).

The view and HTML tests run everywhere; the PDF tests (text, footer, outline, metadata, chord
positions) need WeasyPrint and run in the Linux worker image and CI.
"""

import io
import re
from dataclasses import astuple
from pathlib import Path
from typing import Any

import pypdf

from stemstudio_worker.handouts.document import (
    REPEAT,
    SongInputs,
    bar_cells,
    build_view,
    clock,
    fact_rows,
    form_rows,
    segments,
)
from stemstudio_worker.handouts.markup import STYLE, css_string, render_html
from stemstudio_worker.handouts.renderer import WeasyHandoutRenderer
from stemstudio_worker.schemas import HandoutSection, LyricChord, LyricLine, SongFacts

from .documents import full_song, library_files, page_texts, request
from .support import needs_weasyprint

FONT = "file:///fonts/archivo.woff2"


def _line(text: str, *chords: tuple[str, int]) -> LyricLine:
    return LyricLine(text=text, section=None,
                     chords=[LyricChord(symbol=s, char_index=i) for s, i in chords])  # fmt: skip


def test_chords_split_the_line_at_their_character_index() -> None:
    parts = segments(_line("Dvořák wrote Łódź songs", ("Am", 0), ("F#m7", 13)))

    assert [(p.chord, p.text) for p in parts] == [("Am", "Dvořák wrote "), ("F#m7", "Łódź songs")]


def test_text_before_the_first_chord_and_chords_on_one_word() -> None:
    parts = segments(_line("so we go", ("C", 3), ("G", 3)))

    assert [(p.chord, p.text) for p in parts] == [("", "so "), ("C G", "we go")]


def test_chords_past_the_text_and_instrumental_rows_pad_with_spaces() -> None:
    assert [(p.chord, p.text) for p in segments(_line("la", ("D", 5)))] == [
        ("", "la   "), ("D", "")]  # fmt: skip
    assert [(p.chord, p.text) for p in segments(_line("", ("C", 0), ("G", 4)))] == [
        ("C", "    "), ("G", "")]  # fmt: skip
    assert [(p.chord, p.text) for p in segments(_line("no chords"))] == [("", "no chords")]


def test_whole_bar_repeats_print_as_a_percent_sign() -> None:
    assert bar_cells([["Am"], ["Am"], ["F", "G"], ["F", "G"], [], [], ["Am"]]) == (
        "Am", REPEAT, "F G", REPEAT, "", "", "Am")  # fmt: skip


def test_facts_and_form_rows_are_readable_text() -> None:
    facts = SongFacts(tempo_bpm=120, time_signature="4/4", key="A minor", key_source="chords",
                      duration_seconds=262.4, transposition_note="Part in B-flat")  # fmt: skip
    section = HandoutSection(name="Verse 1", start_seconds=8, end_seconds=40.2, start_bar=5,
                             bars=16, chord_bars=None, practice_tempo_percent=85)  # fmt: skip
    bare = section.model_copy(update={"start_bar": None, "bars": None,
                                      "practice_tempo_percent": None})  # fmt: skip

    assert fact_rows(facts) == (("Tempo", "120 BPM"), ("Time signature", "4/4"), ("Key", "A minor"),
                                ("Length", "4:22"), ("Part", "Part in B-flat"))  # fmt: skip
    assert astuple(form_rows([section], tempo=True)[0]) == (
        "Verse 1",
        "5-20",
        "16",
        "0:08-0:40",
        "85 %",
    )
    assert astuple(form_rows([bare], tempo=True)[0]) == ("Verse 1", "", "", "0:08-0:40", "")
    assert form_rows([section], tempo=False)[0].tempo == ""
    assert clock(59.6) == "1:00"


def _view(**song: Any) -> Any:
    return build_view(request(**song), [SongInputs("file:///cover.jpg", ())])


def test_ingredients_that_are_not_included_stay_empty() -> None:
    view = _view(include=["facts"], lyrics=full_song()["lyrics"], practiceNotes="notes")
    (song,) = view.songs

    assert song.facts
    assert (song.form, song.chord_chart, song.lyrics, song.legend) == ((), (), (), "")
    assert (song.notation, song.practice_notes, song.entry_notes) == ((), None, None)


def test_lyrics_without_chords_print_plain_lines() -> None:
    plain = _view(include=["lyrics"], lyrics=full_song()["lyrics"]).songs[0]
    unplaced = {**full_song()["lyrics"], "chordPlacement": "none"}
    placed_none = _view(**{**full_song(), "lyrics": unplaced}).songs[0]

    assert plain.lyrics_heading == "Lyrics"
    assert [row.text for row in plain.lyrics] == ["Dvořák wrote Łódź songs"]
    assert placed_none.lyrics_heading == "Lyrics"
    assert _view(**full_song()).songs[0].lyrics_heading == "Lyrics and chords"


def test_the_html_escapes_every_text_and_names_title_and_language() -> None:
    html = render_html(_view(**{**full_song(), "title": "<i>Song</i>"}), FONT)

    assert '<html lang="en">' in html
    assert "<title>Example Song</title>" in html
    assert "&lt;b&gt;not bold&lt;/b&gt; &amp; &lt;script&gt;x&lt;/script&gt;" in html
    assert "<script>" not in html
    assert "<b>not bold</b>" not in html


def test_the_page_style_carries_paper_frame_font_and_footer() -> None:
    letter = build_view(request("letter"), [SongInputs(None, ())])
    html = render_html(letter, 'file:///a"b.woff2')

    assert "size: letter;" in html
    assert 'content: "For personal practice and teaching only"' in html
    assert 'url("file:///a\\"b.woff2")' in html
    assert ".frame { height: 214.0mm; }" in html


def test_css_strings_escape_quotes_backslashes_and_line_breaks() -> None:
    assert css_string('a"b\\c\nd\re') == '"a\\"b\\\\c\\a de"'


def test_the_handout_style_uses_tokens_only() -> None:
    css = STYLE.read_text(encoding="utf-8")
    literal = re.compile(r"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(|"
                         r":\s*(white|black|red|blue|green|gray|grey)\b")  # fmt: skip

    assert literal.search(css) is None
    assert "var(--ss-color-ink)" in css
    assert "var(--ss-color-focus)" in css


# PDF tests (WeasyPrint)


def _render(tmp_path: Path, **song: Any) -> bytes:
    files = library_files(tmp_path / "library")
    result = WeasyHandoutRenderer().render(request(**song), files, lambda _fraction: None)
    return result.pdf


@needs_weasyprint
def test_a_song_handout_prints_facts_form_chords_lyrics_and_notes(tmp_path: Path) -> None:
    first, *rest = page_texts(_render(tmp_path, **full_song()))

    for expected in ("Example Song", "Example Band", "Bass handout", "120 BPM", "4/4", "A minor",
                     "4:22", "Form", "Intro", "Verse 1", "5-20", "0:08-0:40", "85 %",
                     "Chord chart", "Lyrics and chords", "Dvořák wrote", "Łódź songs", "F#m7",
                     "Practice notes", "Keep the eighths even."):  # fmt: skip
        assert expected in first, expected
    assert "<b>not bold</b> & <script>x</script>" in first
    assert "About this handout" in rest[-1]


@needs_weasyprint
def test_the_footer_and_page_numbers_are_on_every_page(tmp_path: Path) -> None:
    texts = page_texts(_render(tmp_path, **full_song()))

    assert len(texts) == 5
    for number, text in enumerate(texts, 1):
        assert "For personal practice and teaching only" in text
        assert f"Page {number} of 5" in text


@needs_weasyprint
def test_pages_after_the_first_carry_the_song_and_family_header(tmp_path: Path) -> None:
    texts = page_texts(_render(tmp_path, **full_song()))

    assert texts[0].count("Example Song - Bass") == 0
    assert all("Example Song - Bass" in text for text in texts[1:4])


@needs_weasyprint
def test_the_outline_title_and_language_are_set(tmp_path: Path) -> None:
    reader = pypdf.PdfReader(io.BytesIO(_render(tmp_path, **full_song())))

    titles = [item.title for item in reader.outline if not isinstance(item, list)]
    assert titles[0] == "Example Song"
    assert reader.metadata is not None
    assert reader.metadata.title == "Example Song"
    root: Any = reader.trailer["/Root"]
    assert root["/Lang"] == "en"


def _spans(html: str, cls: str) -> list[tuple[str, float, float]]:
    """Text, x and y of every laid-out ``<span class=cls>`` on page 1 (WeasyPrint boxes)."""
    import weasyprint  # noqa: PLC0415 - needs Pango

    found: list[tuple[str, float, float]] = []

    def walk(box: Any) -> None:
        element = getattr(box, "element", None)
        if element is not None and element.get("class") == cls and box.element_tag == "span":
            text = "".join(getattr(child, "text", "") for child in box.descendants())
            found.append((text, box.position_x, box.position_y))
            return
        for child in getattr(box, "children", []):
            walk(child)

    walk(weasyprint.HTML(string=html).render().pages[0]._page_box)
    return found


@needs_weasyprint
def test_chords_sit_above_the_start_of_their_word() -> None:
    lyrics = {
        "chordPlacement": "word",
        "source": "LRCLIB",
        "lines": [
            {
                "text": "Sing it loud and clear",
                "section": 0,
                "chords": [{"symbol": "Am", "charIndex": 0}, {"symbol": "F#m7", "charIndex": 8}],
            }
        ],
    }
    html = render_html(_view(**{**full_song(), "lyrics": lyrics}), FONT)

    chords = [(t, x, y) for t, x, y in _spans(html, "chord") if t]
    words = _spans(html, "lyric")

    assert [t for t, _, _ in chords] == ["Am", "F#m7"]
    assert [t.split()[0] for t, _, _ in words] == ["Sing", "loud"]
    for (_, chord_x, chord_y), (_, word_x, word_y) in zip(chords, words, strict=True):
        assert chord_x == word_x
        assert chord_y < word_y
