"""From a resolved ``HandoutDocument`` to the view the templates print (data-model 6.4-6.6).

Pure functions, no WeasyPrint: the notice check, the ingredient filter, the text of the facts
row, the form chart, the chord chart (``%`` for a whole-bar repeat, research.md R-08), chords
over lyric words (segments at each chord's character index) and the setlist cover. The templates
only print what this module prepared; every text stays text (Jinja2 autoescape, FR-018).
"""

from collections.abc import Callable, Iterable, Sequence
from dataclasses import dataclass
from typing import Any, Final

from stemstudio_worker.ports import NOTICE_FOOTER, NOTICE_FULL, HandoutRenderError
from stemstudio_worker.schemas import (
    HandoutDocument,
    HandoutJobRequest,
    HandoutLyrics,
    HandoutSection,
    HandoutSong,
    IngredientCode,
    LyricLine,
    SetlistBlock,
    SongFacts,
)

NOTICE_MISMATCH_TEXT: Final = "The handout notice is not the fixed notice text."
REPEAT: Final = "%"
"""Chord chart cell of a bar that repeats the bar before it."""

PAPERS: Final = {"a4": "A4", "letter": "letter"}
"""CSS ``@page size`` of each paper."""

FRAME_HEIGHT_MM: Final = {"a4": 232.0, "letter": 214.0}
"""Height of the notation frame of each paper (content height minus the frame heading)."""


def check_notice(document: HandoutDocument) -> None:
    """Refuse a document whose notice texts are not the fixed texts (data-model 6.6).

    Raises:
        HandoutRenderError: The texts differ (the schema already refuses this with 422).
    """
    if (document.notice.footer, document.notice.full) != (NOTICE_FOOTER, NOTICE_FULL):
        raise HandoutRenderError(NOTICE_MISMATCH_TEXT)


@dataclass(frozen=True, slots=True)
class Segment:
    """Part of a lyric line that starts at a chord (``chord`` empty for the line's start)."""

    chord: str
    text: str


@dataclass(frozen=True, slots=True)
class LyricRow:
    """One printed lyric line: segments when chords are shown, else the plain text."""

    segments: tuple[Segment, ...]
    text: str


@dataclass(frozen=True, slots=True)
class FormRow:
    """One row of the form chart."""

    name: str
    bars: str
    count: str
    time: str
    tempo: str


@dataclass(frozen=True, slots=True)
class ChordRow:
    """The chord chart of one section: one cell per bar."""

    name: str
    cells: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class NotationBlock:
    """A notation item: frame anchors (one per PDF page) or a note when it cannot be read."""

    heading: str
    title: str
    frames: tuple[str, ...]
    note: str | None


@dataclass(frozen=True, slots=True)
class SongView:
    """Everything the song template prints."""

    anchor: str
    title: str
    artist: str | None
    family: str
    running: str
    artwork: str | None
    facts: tuple[tuple[str, str], ...]
    form: tuple[FormRow, ...]
    show_tempo: bool
    chord_chart: tuple[ChordRow, ...]
    lyrics_heading: str
    lyrics: tuple[LyricRow, ...]
    legend: str
    notation: tuple[NotationBlock, ...]
    practice_notes: str | None
    entry_notes: str | None


@dataclass(frozen=True, slots=True)
class SetlistView:
    """The cover of a setlist handout: notes, running order and contents."""

    name: str
    notes: str | None
    entries: tuple[tuple[str, ...], ...]


@dataclass(frozen=True, slots=True)
class HandoutView:
    """The whole handout as the templates print it."""

    title: str
    subtitle: str | None
    family_label: str
    generated_on: str
    paper: str
    frame_height_mm: float
    setlist: SetlistView | None
    songs: tuple[SongView, ...]
    notice: str
    attributions: tuple[str, ...]
    app_version: str


@dataclass(frozen=True, slots=True)
class SongInputs:
    """Per song: the artwork URI and the notation blocks the renderer prepared."""

    artwork: str | None
    notation: tuple[NotationBlock, ...]


def build_view(request: HandoutJobRequest, inputs: Sequence[SongInputs]) -> HandoutView:
    """The view of ``request``; ``inputs`` holds the prepared artwork and notation per song."""
    document = request.document
    songs = tuple(
        song_view(document, index, song, prepared)
        for index, (song, prepared) in enumerate(zip(document.songs, inputs, strict=True), 1)
    )
    return HandoutView(
        title=document.title, subtitle=document.subtitle, family_label=document.family_label,
        generated_on=document.generated_on.isoformat(), paper=PAPERS[request.paper],
        frame_height_mm=FRAME_HEIGHT_MM[request.paper],
        setlist=None if document.setlist is None else setlist_view(document.setlist),
        songs=songs, notice=document.notice.full, attributions=tuple(document.attributions),
        app_version=document.app_version,
    )  # fmt: skip


def song_view(document: HandoutDocument, index: int, song: HandoutSong,
              prepared: SongInputs) -> SongView:  # fmt: skip
    """The view of one song (ingredients not in ``song.include`` stay empty)."""
    has = _Includes(song.include)
    heading, lyrics = lyric_rows(song.lyrics, has)
    return SongView(
        anchor=f"song-{index}", title=song.title, artist=song.artist,
        family=document.family_label, running=f"{song.title} - {document.family_label}",
        lyrics_heading=heading, lyrics=lyrics, show_tempo=has("practice-progress"),
        entry_notes=song.entry_notes if document.kind == "setlist" else None,
        **_header(song, prepared, has), **_charts(song, has), **_extras(song, prepared, has),
    )  # fmt: skip


def _header(song: HandoutSong, prepared: SongInputs, has: "_Includes") -> dict[str, Any]:
    """Artwork and facts row (ingredient ``facts``)."""
    facts = has("facts")
    return {"artwork": prepared.artwork if facts else None,
            "facts": fact_rows(song.facts) if facts else ()}  # fmt: skip


def _charts(song: HandoutSong, has: "_Includes") -> dict[str, Any]:
    """Form chart (or practice progress alone), chord chart and chord legend."""
    form = has("form-chart", "practice-progress")
    return {
        "form": form_rows(song.sections, has("practice-progress")) if form else (),
        "chord_chart": chord_rows(song.sections) if has("chord-chart") else (),
        "legend": " ".join(song.chord_symbols) if has("chord-chart", "chords-over-lyrics") else "",
    }


def _extras(song: HandoutSong, prepared: SongInputs, has: "_Includes") -> dict[str, Any]:
    """Notation blocks and practice notes."""
    return {"notation": prepared.notation if has("notation") else (),
            "practice_notes": song.practice_notes if has("practice-notes") else None}  # fmt: skip


class _Includes:
    """``has("a", "b")``: whether any of the ingredients is included."""

    def __init__(self, include: Iterable[IngredientCode]) -> None:
        self._include = frozenset(include)

    def __call__(self, *codes: IngredientCode) -> bool:
        return not self._include.isdisjoint(codes)


def clock(seconds: float) -> str:
    """``262.0`` -> ``"4:22"``."""
    whole = round(seconds)
    return f"{whole // 60}:{whole % 60:02d}"


def fact_rows(facts: SongFacts) -> tuple[tuple[str, str], ...]:
    """Label and value of every known fact (tempo, time signature, key, length, part)."""
    rows = (
        ("Tempo", None if facts.tempo_bpm is None else f"{facts.tempo_bpm} BPM"),
        ("Time signature", facts.time_signature),
        ("Key", facts.key),
        ("Length", clock(facts.duration_seconds)),
        ("Part", facts.transposition_note),
    )
    return tuple((label, value) for label, value in rows if value)


def form_rows(sections: Sequence[HandoutSection], tempo: bool) -> tuple[FormRow, ...]:
    """One form chart row per section (bars ``5-20``, time ``0:08-0:40``, practice tempo)."""
    return tuple(
        FormRow(
            name=s.name,
            bars=_bar_span(s),
            count="" if s.bars is None else str(s.bars),
            time=f"{clock(s.start_seconds)}-{clock(s.end_seconds)}",
            tempo=_tempo(s) if tempo else "",
        )
        for s in sections
    )


def _bar_span(section: HandoutSection) -> str:
    if section.start_bar is None or not section.bars:
        return ""
    return f"{section.start_bar}-{section.start_bar + section.bars - 1}"


def _tempo(section: HandoutSection) -> str:
    percent = section.practice_tempo_percent
    return "" if percent is None else f"{percent} %"


def chord_rows(sections: Sequence[HandoutSection]) -> tuple[ChordRow, ...]:
    """The chord chart: per section with chords one cell per bar, whole-bar repeats as ``%``."""
    return tuple(ChordRow(s.name, bar_cells(s.chord_bars)) for s in sections if s.chord_bars)


def bar_cells(bars: Sequence[Sequence[str]]) -> tuple[str, ...]:
    """Cell texts of the bars; a bar equal to the bar before it (and not empty) is ``%``."""
    cells: list[str] = []
    previous: Sequence[str] = ()
    for bar in bars:
        cells.append(REPEAT if bar and list(bar) == list(previous) else " ".join(bar))
        previous = bar
    return tuple(cells)


def lyric_rows(lyrics: HandoutLyrics | None, has: Callable[..., bool]
               ) -> tuple[str, tuple[LyricRow, ...]]:  # fmt: skip
    """Heading and rows of the lyrics block: with chords, plain, or none."""
    if lyrics is None or not has("lyrics", "chords-over-lyrics"):
        return "", ()
    if has("chords-over-lyrics") and lyrics.chord_placement != "none":
        return "Lyrics and chords", tuple(_chord_row(line) for line in lyrics.lines)
    return "Lyrics", tuple(LyricRow((), line.text) for line in lyrics.lines if line.text)


def _chord_row(line: LyricLine) -> LyricRow:
    return LyricRow(segments(line), line.text)


def segments(line: LyricLine) -> tuple[Segment, ...]:
    """Split ``line.text`` at its chords' character indices (chords at one index joined).

    A chord past the end of the text pads the text with spaces; an instrumental row (empty
    text) gives one segment per chord position.
    """
    starts: dict[int, list[str]] = {}
    for chord in line.chords:
        starts.setdefault(chord.char_index, []).append(chord.symbol)
    if not starts:
        return (Segment("", line.text),)
    text = line.text.ljust(max(starts))
    cuts = sorted(starts)
    pieces = [Segment("", text[: cuts[0]])] if cuts[0] > 0 else []
    for begin, end in zip(cuts, [*cuts[1:], len(text)], strict=True):
        pieces.append(Segment(" ".join(starts[begin]), text[begin:end]))
    return tuple(pieces)


def setlist_view(setlist: SetlistBlock) -> SetlistView:
    """Cover of a setlist handout: running order rows ``position, title, artist, key, ...``."""
    entries = tuple(
        (str(e.position), e.title, e.artist or "", e.key or "",
         "" if e.tempo_bpm is None else f"{e.tempo_bpm} BPM",
         "" if e.duration_seconds is None else clock(e.duration_seconds), e.notes or "")
        for e in setlist.entries
    )  # fmt: skip
    return SetlistView(name=setlist.name, notes=setlist.notes, entries=entries)
