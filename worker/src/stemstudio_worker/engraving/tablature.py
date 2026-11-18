"""Checks of Verovio's TAB staves (ADR-033 D6: native Verovio, no fallback drawing).

Verovio 6.4 engraves the MusicXML TAB staff of the notator (``notation/tab_staff.py``): a staff
whose layer holds ``tabGrp`` groups, its lines drawn as horizontal paths that are interrupted
behind every fret number, and the fret numbers as ``<text>``. These helpers read our own SVG
pages back (never user input) so the pipeline and the tests can confirm that every TAB staff
has one line per string; a capo is printed as the notator's "Capo n" direction (``g.dir``).
"""

import re
import xml.etree.ElementTree as ET
from collections.abc import Iterator, Sequence
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.ports import TranscriptionError

TAB_ENGRAVING_FAILED_TEXT: Final = "The tablature could not be engraved."
"""Failure text when an engraved page lacks the TAB staff or has the wrong line count."""

_SVG: Final = "{http://www.w3.org/2000/svg}"
_HORIZONTAL: Final = re.compile(r"^M\s*-?[\d.]+\s+(-?[\d.]+)\s+L\s*-?[\d.]+\s+(-?[\d.]+)\s*$")


@dataclass(frozen=True, slots=True)
class EngravedStaff:
    """One staff of an engraved system.

    Attributes:
        lines: Staff lines (distinct heights of its horizontal line segments).
        frets: Fret numbers of a TAB staff in drawing order; empty for a notation staff.
    """

    lines: int
    frets: tuple[str, ...]


def _groups(element: ET.Element, css_class: str) -> Iterator[ET.Element]:
    return (g for g in element.iter(f"{_SVG}g") if g.get("class", "").split(" ")[0] == css_class)


def _line_count(staff: ET.Element) -> int:
    heights = set()
    for path in staff.findall(f"{_SVG}path"):
        match = _HORIZONTAL.match(path.get("d", ""))
        if match and match.group(1) == match.group(2):
            heights.add(match.group(1))
    return len(heights)


def _frets(staff: ET.Element) -> tuple[str, ...]:
    texts = (t for group in _groups(staff, "tabGrp") for t in group.iter(f"{_SVG}text"))
    return tuple(text for t in texts if (text := "".join(t.itertext()).strip()))


def tab_staves(svg: str, *, tab_only: bool = True) -> tuple[EngravedStaff, ...]:
    """The TAB staves of one SVG page (``tab_only=False``: every staff) in drawing order.

    Verovio draws one staff group per measure, so a staff appears once per measure.
    """
    root = ET.fromstring(svg)  # noqa: S314 - Verovio output of our own score
    staves = []
    for staff in _groups(root, "staff"):
        is_tab = next(_groups(staff, "tabGrp"), None) is not None
        if is_tab or not tab_only:
            staves.append(EngravedStaff(_line_count(staff), _frets(staff) if is_tab else ()))
    return tuple(staves)


def direction_texts(svg: str) -> list[str]:
    """The texts of the directions (``g.dir``) of one SVG page, such as "Capo 3"."""
    root = ET.fromstring(svg)  # noqa: S314 - Verovio output of our own score
    return [" ".join("".join(g.itertext()).split()) for g in _groups(root, "dir")]


def check_tab_pages(pages: Sequence[str], strings: int) -> None:
    """Confirm the pages hold TAB staves and each has ``strings`` lines.

    Raises:
        TranscriptionError: ``TAB_ENGRAVING_FAILED_TEXT`` otherwise.
    """
    staves = [staff for page in pages for staff in tab_staves(page)]
    if not staves or any(staff.lines != strings for staff in staves):
        raise TranscriptionError(TAB_ENGRAVING_FAILED_TEXT)
