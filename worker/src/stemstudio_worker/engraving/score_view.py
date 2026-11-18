"""Score view: MusicXML -> SVG pages and a timed bar map (014, data-model 2.5; audio-ingest).

Two Verovio passes with the 008 options: plain SVG pages (stored and served as images) and the
same layout with ``svgBoundingBoxes`` (parsed for the bar boxes, then dropped). Bar *k* is the
*k*-th ``g.measure`` in document order; its box is the union of its staff boxes plus the page
margin, as fractions of the 21000 x 29700 page. Bar times follow the 008 time base: full bar *j*
starts at beat ``j x beatsPerBar`` after the first downbeat (beats interpolated, extrapolated with
the median period), a pickup starts ``pickupBeats`` periods before the first downbeat and the last
bar ends at the duration. Verovio embeds the Leipzig ``@font-face`` in every page that uses
Leipzig text; the check refuses a page that would need it without it (research R-11).

Imports: ``ports``, ``schemas``, ``engraving`` itself, Verovio and the standard library only.
The MusicXML is the score asset the API stored from an 008 job (``SCORE_MUSICXML_PATTERN``);
Python's expat does not expand external entities.
"""

import itertools
import re
import statistics
import xml.etree.ElementTree as ET
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

import verovio

from stemstudio_worker.engraving.verovio_svg import new_toolkit
from stemstudio_worker.ports import ScoreView, ScoreViewError
from stemstudio_worker.schemas import (
    MAX_SCORE_BARS,
    MAX_SCORE_PAGES,
    BarBox,
    BeatGridTempo,
    ManualTempo,
    TempoGrid,
)

PAGE_UNITS: Final = (21000.0, 29700.0)
"""Width and height of a page in Verovio units (the inner ``viewBox``)."""

_SVG: Final = "{http://www.w3.org/2000/svg}"
_TRANSLATE: Final = re.compile(r"translate\(\s*([-\d.]+)[ ,]+([-\d.]+)\s*\)")
_EXTERNAL_HREF: Final = re.compile(r"href\s*=\s*[\"'](?!#)")
_MIN_FRACTION: Final = 0.0001

Rect = tuple[float, float, float, float]
"""``(left, top, right, bottom)`` in page units."""


class VerovioScoreViewRenderer:
    """:class:`~stemstudio_worker.ports.ScoreViewRenderer` with the Verovio toolkit."""

    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView:
        """Lay out ``musicxml`` and time its bars on ``grid``.

        Raises:
            ScoreViewError: Unreadable MusicXML, a Verovio failure, too many pages or bars, or an
                unsafe page.
        """
        text = _decode(musicxml)
        pickup = _pickup_beats(_score_root(text))
        plain, boxed, version = _layout(text)
        boxes = _all_boxes(boxed)
        for page in plain:
            check_page(page)
        times = bar_times(len(boxes), pickup, grid, duration_seconds)
        bars = [_bar(k, box, times[k], pickup) for k, box in enumerate(boxes)]
        return ScoreView(pages=plain, bars=tuple(bars), engine_version=version)


@dataclass(frozen=True, slots=True)
class Box:
    """A bar box in page fractions on page ``page`` (1-based)."""

    page: int
    x: float
    y: float
    width: float
    height: float


def _decode(musicxml: bytes) -> str:
    try:
        return musicxml.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ScoreViewError from exc


def _score_root(text: str) -> ET.Element:
    """The parsed MusicXML; anything but ``score-partwise`` (MEI, ABC, Humdrum, HTML) fails."""
    try:
        root = ET.fromstring(text)  # noqa: S314 - own score asset (see the module docstring)
    except ET.ParseError as exc:
        raise ScoreViewError from exc
    if root.tag != "score-partwise":
        raise ScoreViewError
    return root


def _all_boxes(boxed: Sequence[str]) -> list[Box]:
    boxes = [box for page, svg in enumerate(boxed, 1) for box in measure_boxes(svg, page)]
    if not boxes or len(boxes) > MAX_SCORE_BARS:
        raise ScoreViewError
    return boxes


def _layout(text: str) -> tuple[tuple[str, ...], tuple[str, ...], str]:
    """Plain pages, bounding-box pages and the Verovio version (same layout in both passes)."""
    plain = _pass(text, boxes=False)
    boxed = _pass(text, boxes=True)
    if not plain or len(plain) != len(boxed) or len(plain) > MAX_SCORE_PAGES:
        raise ScoreViewError
    return plain, boxed, verovio_version()


def verovio_version() -> str:
    """The Verovio toolkit version without the build suffix (``6.4.0``)."""
    version: str = verovio.toolkit().getVersion()
    return version.split("[", 1)[0][:40]


def _pass(text: str, *, boxes: bool) -> tuple[str, ...]:
    toolkit = new_toolkit()
    if boxes:
        toolkit.setOptions({"svgBoundingBoxes": True})
    if not toolkit.loadData(text):
        raise ScoreViewError
    count = min(toolkit.getPageCount(), MAX_SCORE_PAGES + 1)
    return tuple(toolkit.renderToSVG(page) for page in range(1, count + 1))


def check_page(svg: str) -> None:
    """Refuse a page with script, ``foreignObject``, an external href or Leipzig text without font.

    Raises:
        ScoreViewError: The page is not safe or complete as an image.
    """
    if _unsafe(svg) or _missing_font(svg):
        raise ScoreViewError


def _unsafe(svg: str) -> bool:
    return "<script" in svg or "foreignObject" in svg or bool(_EXTERNAL_HREF.search(svg))


def _missing_font(svg: str) -> bool:
    return 'font-family="Leipzig"' in svg and "@font-face" not in svg


def measure_boxes(svg: str, page: int) -> list[Box]:
    """The box of every ``g.measure`` of a bounding-box page, in document order."""
    try:
        root = ET.fromstring(svg)  # noqa: S314 - Verovio output of our own score asset
    except ET.ParseError as exc:
        raise ScoreViewError from exc
    dx, dy = _margin(root)
    return [_box(rects, page, dx, dy) for rects in _measure_rects(root)]


def _margin(root: ET.Element) -> tuple[float, float]:
    for group in root.iter(f"{_SVG}g"):
        if group.get("class") == "page-margin":
            match = _TRANSLATE.search(group.get("transform", ""))
            if match:
                return float(match.group(1)), float(match.group(2))
    return 0.0, 0.0


def _measure_rects(root: ET.Element) -> list[list[Rect]]:
    return [_staff_rects(m) for m in _groups(root, "measure")]


def _staff_rects(measure: ET.Element) -> list[Rect]:
    return [_rect(r) for g in _groups(measure, "staff bounding-box") for r in g.iter(f"{_SVG}rect")]


def _groups(root: ET.Element, css_class: str) -> list[ET.Element]:
    return [g for g in root.iter(f"{_SVG}g") if g.get("class") == css_class]


def _rect(rect: ET.Element) -> Rect:
    x, y = float(rect.get("x", 0)), float(rect.get("y", 0))
    return x, y, x + float(rect.get("width", 0)), y + float(rect.get("height", 0))


def _box(rects: Sequence[Rect], page: int, dx: float, dy: float) -> Box:
    left, top, right, bottom = _union(rects)
    left, right, top, bottom = left + dx, right + dx, top + dy, bottom + dy
    width, height = PAGE_UNITS
    x, y = _fraction(left / width), _fraction(top / height)
    return Box(page, x, y, _extent(right / width, x), _extent(bottom / height, y))


def _union(rects: Sequence[Rect]) -> Rect:
    if not rects:
        raise ScoreViewError
    lefts, tops, rights, bottoms = zip(*rects, strict=True)
    return min(lefts), min(tops), max(rights), max(bottoms)


def _fraction(value: float) -> float:
    return round(min(max(value, 0.0), 1.0), 4)


def _extent(end: float, start: float) -> float:
    return max(_fraction(end) - start, _MIN_FRACTION) if start < 1 else _MIN_FRACTION


def _pickup_beats(root: ET.Element) -> float:
    """Length in quarters of a pickup (first measure ``implicit="yes"`` or numbered 0), else 0."""
    first = root.find("./part/measure")
    if first is None or not _is_pickup(first):
        return 0.0
    divisions = root.findtext(".//attributes/divisions", default="1")
    return _measure_length(first) / float(divisions)


def _is_pickup(measure: ET.Element) -> bool:
    return measure.get("implicit") == "yes" or measure.get("number") == "0"


def _measure_length(measure: ET.Element) -> float:
    cursor = longest = 0.0
    for child in measure:
        step = float(child.findtext("duration", default="0"))
        if child.tag == "backup":
            cursor -= step
        elif child.tag in ("note", "forward") and child.find("chord") is None:
            cursor += step
        longest = max(longest, cursor)
    return longest


def bar_times(
    count: int, pickup_beats: float, grid: TempoGrid, duration: float
) -> list[tuple[float, float]]:
    """``(start, end)`` of ``count`` bars (the first one a pickup when ``pickup_beats`` > 0)."""
    beats, first = _beats(grid, duration)
    clock = _BeatClock(beats)
    per_bar = grid.beats_per_bar
    pickup = [clock.time(first) - pickup_beats * clock.period] if pickup_beats else []
    starts = pickup + [clock.time(first + j * per_bar) for j in range(count - len(pickup))]
    last = duration if duration > starts[-1] else starts[-1] + per_bar * clock.period
    return list(zip(_rounded(starts), _rounded([*starts[1:], last]), strict=True))


def _rounded(times: Sequence[float]) -> list[float]:
    return [round(t, 3) for t in times]


def _beats(grid: TempoGrid, duration: float) -> tuple[list[float], int]:
    """Beat times and the index of the first downbeat (a manual tempo generates its beats)."""
    if isinstance(grid, ManualTempo):
        period = 60.0 / grid.bpm
        before = int(grid.first_downbeat_seconds // period)
        count = before + int(duration // period) + 2
        start = grid.first_downbeat_seconds - before * period
        return [start + k * period for k in range(count)], before
    return list(grid.beats), _nearest(grid)


def _nearest(grid: BeatGridTempo) -> int:
    first = grid.downbeats[0]
    return min(range(len(grid.beats)), key=lambda i: abs(grid.beats[i] - first))


class _BeatClock:
    """Time of a (possibly fractional or outside) beat index; median period outside the beats."""

    def __init__(self, beats: Sequence[float]) -> None:
        self._beats = list(beats)
        self.period = statistics.median(b - a for a, b in itertools.pairwise(self._beats))

    def time(self, index: float) -> float:
        last = len(self._beats) - 1
        if index <= 0:
            return self._beats[0] + index * self.period
        if index >= last:
            return self._beats[last] + (index - last) * self.period
        whole = int(index)
        left, right = self._beats[whole], self._beats[whole + 1]
        return left + (index - whole) * (right - left)


def _bar(index: int, box: Box, span: tuple[float, float], pickup: float) -> BarBox:
    number = index if pickup else index + 1
    return BarBox(index=index, number=number, page=box.page, x=box.x, y=box.y, width=box.width,
                  height=box.height, start_seconds=span[0], end_seconds=span[1])  # fmt: skip
