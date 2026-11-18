"""Setlist PDF with ReportLab Platypus (feature 014, data-model 2.6; audio-ingest).

A4 portrait, 18 mm margins, DejaVu Sans 10/13 pt, headings in DejaVu Sans Bold. The overview
(title, note, a song table and the total length) is followed by one block per song (a new page
each when ``pageBreakPerSong``): heading, facts line, note, sections in two columns and the lyrics
by stanza. Every page carries "<title> - page n of m - made with StemStudio on <date>". Text the
font cannot show becomes U+FFFD and the overview says so. All content comes in the request; the
only files read are the fonts.
"""

import io
from collections.abc import Callable, Sequence
from typing import Any, Final
from xml.sax.saxutils import escape

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfgen.canvas import Canvas
from reportlab.platypus import (
    Flowable,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

from stemstudio_worker.documents.fonts import (
    BOLD,
    REGULAR,
    FontLocator,
    Glyphs,
    find_dejavu,
    register,
)
from stemstudio_worker.ports import DocumentError, SetlistDocument
from stemstudio_worker.schemas import SetlistDocumentRequest, SetlistSongContent

MARGIN: Final = 18 * mm
NOTE_PREVIEW: Final = 80
"""Characters of a song note shown in the overview table."""
REPLACED_NOTE: Final = "Some characters could not be shown"
_BODY: Final = ParagraphStyle("body", fontName=REGULAR, fontSize=10, leading=13)
_TITLE: Final = ParagraphStyle("title", fontName=BOLD, fontSize=18, leading=22, spaceAfter=6)
_HEADING: Final = ParagraphStyle("heading", fontName=BOLD, fontSize=14, leading=18, spaceAfter=4)
_CELL: Final = ParagraphStyle("cell", fontName=REGULAR, fontSize=9, leading=11)
_HEAD_CELL: Final = ParagraphStyle("head", fontName=BOLD, fontSize=9, leading=11)
_COLUMNS: Final = ("#", "Title", "Artist", "Tempo", "Length", "Note")
_WIDTHS: Final = (8 * mm, 44 * mm, 34 * mm, 21 * mm, 18 * mm, 49 * mm)


class ReportLabSetlistRenderer:
    """:class:`~stemstudio_worker.ports.SetlistDocumentRenderer` with ReportLab."""

    def __init__(self, fonts: FontLocator = find_dejavu) -> None:
        """Use the fonts ``fonts`` finds (DejaVu Sans by default)."""
        self._fonts = fonts

    def render(self, request: SetlistDocumentRequest) -> SetlistDocument:
        """Lay out the overview, the songs and the footers.

        Raises:
            DocumentError: The fonts are missing or ReportLab fails.
        """
        text = _Text(register(self._fonts()))
        story = _story(request, text)
        try:
            return _build(story, text.clean(request.title), request.generated_at.date().isoformat())
        except Exception as exc:  # any ReportLab failure is a failed document
            raise DocumentError from exc


class _Text:
    """Cleans and escapes text for Platypus and remembers whether a character was replaced."""

    def __init__(self, glyphs: Glyphs) -> None:
        self._glyphs = glyphs
        self.replaced = False

    def clean(self, text: str) -> str:
        kept, replaced = self._glyphs.clean(text)
        self.replaced = self.replaced or replaced
        return kept

    def markup(self, text: str) -> str:
        return escape(self.clean(text))

    def paragraph(self, text: str, style: ParagraphStyle = _BODY) -> Paragraph:
        return Paragraph(self.markup(text), style)


def _story(request: SetlistDocumentRequest, text: _Text) -> list[Flowable]:
    songs = [part for song in request.songs for part in _song(song, request, text)]
    return [*_overview(request, text), PageBreak(), *songs]


def _overview(request: SetlistDocumentRequest, text: _Text) -> list[Flowable]:
    total = sum(song.duration_seconds for song in request.songs)
    parts: list[Flowable] = [text.paragraph(request.title, _TITLE)]
    parts += [text.paragraph(request.note)] if request.note else []
    parts += [Spacer(1, 4 * mm), _song_table(request, text), Spacer(1, 3 * mm),
              text.paragraph(f"Total {length(total)}")]  # fmt: skip
    return parts + ([Paragraph(REPLACED_NOTE, _BODY)] if text.replaced else [])


def _song_table(request: SetlistDocumentRequest, text: _Text) -> Table:
    rows = [[Paragraph(name, _HEAD_CELL) for name in _COLUMNS]]
    rows += [[Paragraph(text.markup(cell), _CELL) for cell in _row(song)] for song in request.songs]
    table = Table(rows, colWidths=_WIDTHS, repeatRows=1)
    table.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"),
                               ("LINEBELOW", (0, 0), (-1, 0), 0.5, "black")]))  # fmt: skip
    return table


def _row(song: SetlistSongContent) -> list[str]:
    note = (song.note or "")[:NOTE_PREVIEW]
    return [str(song.position), song.title, song.artist or "", _tempo(song),
            length(song.duration_seconds), note]  # fmt: skip


def _tempo(song: SetlistSongContent) -> str:
    return f"{round(song.bpm)} BPM" if song.bpm else ""


def _song(song: SetlistSongContent, request: SetlistDocumentRequest, text: _Text) -> list[Flowable]:
    options = request.options
    new_page = options.page_break_per_song and song.position > 1
    parts: list[Flowable] = [PageBreak()] if new_page else []
    parts += [text.paragraph(f"{song.position}. {song.title}", _HEADING),
              text.paragraph(facts(song))]  # fmt: skip
    parts += _details(song, request, text)
    return [*parts, Spacer(1, 8 * mm)]


def _details(
    song: SetlistSongContent, request: SetlistDocumentRequest, text: _Text
) -> list[Flowable]:
    """Note, sections and lyrics as far as the options include them."""
    options = request.options
    wanted = ((options.include_notes, _note), (options.include_sections, _section_list),
              (options.include_lyrics, _lyrics))  # fmt: skip
    return [part for include, make in wanted if include for part in make(song, text)]


def _note(song: SetlistSongContent, text: _Text) -> list[Flowable]:
    return [text.paragraph(song.note)] if song.note else []


def _section_list(song: SetlistSongContent, text: _Text) -> list[Flowable]:
    return [_sections(song, text)] if song.sections else []


def facts(song: SetlistSongContent) -> str:
    """The facts line "Artist - 120 BPM, 4/4, A minor - 3:42 - Mix: name" (parts it has)."""
    metre = f"{song.beats_per_bar}/4" if song.beats_per_bar else ""
    tempo = _joined(", ", (_tempo(song), metre, song.key or ""))
    mix = f"Mix: {song.mix_name}" if song.mix_name else ""
    return _joined(" - ", (song.artist or "", tempo, length(song.duration_seconds), mix))


def _joined(separator: str, parts: Sequence[str]) -> str:
    return separator.join(part for part in parts if part)


def _sections(song: SetlistSongContent, text: _Text) -> Table:
    lines = [Paragraph(text.markup(f"{length(s.start_seconds)} {s.label}"), _BODY)
             for s in song.sections]  # fmt: skip
    half = (len(lines) + 1) // 2
    right: list[Paragraph | str] = [*lines[half:], ""]
    rows = [[left, right[k]] for k, left in enumerate(lines[:half])]
    table = Table(rows, colWidths=(85 * mm, 85 * mm))
    table.setStyle(TableStyle([("LEFTPADDING", (0, 0), (-1, -1), 0)]))
    return table


def _lyrics(song: SetlistSongContent, text: _Text) -> list[Flowable]:
    stanzas = _stanzas(song.lyrics.lines) if song.lyrics else []
    if not stanzas:
        return [Spacer(1, 2 * mm), Paragraph("No lyrics", _BODY)]
    return [part for stanza in stanzas for part in _stanza(stanza, text)] + _shortened(song)


def _shortened(song: SetlistSongContent) -> list[Flowable]:
    shortened = song.lyrics is not None and song.lyrics.shortened
    return [Paragraph("Lyrics shortened", _BODY)] if shortened else []


def _stanza(lines: Sequence[str], text: _Text) -> tuple[Flowable, Flowable]:
    return Spacer(1, 3 * mm), Paragraph("<br/>".join(text.markup(line) for line in lines), _BODY)


def _stanzas(lines: Sequence[str]) -> list[list[str]]:
    """Groups of non-empty lines; an empty line ends a stanza."""
    stanzas: list[list[str]] = [[]]
    for line in lines:
        stanzas[-1].append(line) if line.strip() else stanzas.append([])
    return [stanza for stanza in stanzas if stanza]


def length(seconds: float) -> str:
    """``m:ss``, or ``h:mm:ss`` from one hour."""
    total = round(seconds)
    hours, rest = divmod(total, 3600)
    minutes, secs = divmod(rest, 60)
    return f"{hours}:{minutes:02d}:{secs:02d}" if hours else f"{minutes}:{secs:02d}"


def _build(story: list[Flowable], title: str, date: str) -> SetlistDocument:
    out, pages = io.BytesIO(), [0]

    def footer(number: int, total: int) -> str:
        pages[0] = total
        return f"{title} - page {number} of {total} - made with StemStudio on {date}"

    document = SimpleDocTemplate(out, pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN,
                                 topMargin=MARGIN, bottomMargin=MARGIN, title=title,
                                 author="StemStudio")  # fmt: skip
    document.build(story, canvasmaker=_footer_canvas(footer))
    return SetlistDocument(pdf=out.getvalue(), pages=pages[0])


def _footer_canvas(footer: Callable[[int, int], str]) -> type[Canvas]:
    """A canvas that draws ``footer(n, m)`` on every page once the page count is known."""

    class _FooterCanvas(Canvas):  # type: ignore[misc]  # ReportLab has no type information
        def __init__(self, *args: Any, **kwargs: Any) -> None:
            super().__init__(*args, **kwargs)
            self._pages: list[dict[str, Any]] = []

        def showPage(self) -> None:  # noqa: N802 - ReportLab API
            self._pages.append(dict(self.__dict__))
            self._startPage()

        def save(self) -> None:
            total = len(self._pages)
            for number, state in enumerate(self._pages, 1):
                self.__dict__.update(state)
                self.setFont(REGULAR, 8)
                self.drawCentredString(A4[0] / 2, MARGIN / 2, footer(number, total))
                super().showPage()
            super().save()

    return _FooterCanvas
