"""Notation PDFs (008/016 scores) merged into the handout's frame pages with pypdf (R-07).

A notation PDF is untrusted input: it is read with size and page limits and parsed under the
stricter pypdf limits of ``PARSE_LIMITS``; an encrypted, damaged, oddly sized or too heavy file
is "unreadable" (its pages become a note and a ``notation-unreadable`` warning, the job still
completes), and only page content is merged. Annotations (links, JavaScript actions, form
fields) are dropped and the source document's catalogue (open actions, embedded files, document
JavaScript) is never copied. Each page is scaled to fit its frame, centred, never cropped; a page
that is not the handout's paper size gives a ``notation-scaled`` warning.
"""

from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from io import BytesIO
from pathlib import Path
from typing import Final

from pypdf import PageObject, PdfReader, PdfWriter, Transformation, apply_configuration
from pypdf.errors import PyPdfError
from pypdf.generic import NameObject

from stemstudio_worker.ports import HANDOUT_FAILED_TEXT, HandoutRenderError, NotationUnreadableError

MAX_NOTATION_BYTES: Final = 64 * 1024 * 1024
"""Largest notation PDF read (a 15-minute score is a few MiB)."""

MAX_NOTATION_PAGES: Final = 60
"""Most pages of one notation PDF."""

PAGE_LIMITS_PT: Final = (72.0, 3000.0)
"""Smallest and largest accepted page side in points (1 in to about 1 m)."""

PAPER_SIZES_PT: Final[Mapping[str, tuple[float, float]]] = {
    "a4": (595.28, 841.89),
    "letter": (612.0, 792.0),
}
"""Handout paper sizes in points."""

_MIB: Final = 1024 * 1024

PARSE_LIMITS: Final[Mapping[str, int]] = {
    "stream_parsing_work_maximum_count": 200_000,
    "zlib_maximum_output_length": 16 * _MIB,
    "lzw_maximum_output_length": 16 * _MIB,
    "run_length_maximum_output_length": 16 * _MIB,
    "brotli_maximum_output_length": 16 * _MIB,
    "stream_decoding_work_maximum_length": 64 * _MIB,
}
"""pypdf limits while a notation PDF is checked and merged (pypdf's defaults are 2 M tokens and
75 MB per stream). A dense engraved page (Verovio + CairoSVG) has about 72 k tokens and checks and
merges in about 0.25 s; 200 k tokens bound one page to about 0.6 s."""

_SIZE_TOLERANCE_PT: Final = 2.0
_DROPPED_KEYS: Final = ("/Annots", "/AA")


def _unreadable(path: Path) -> NotationUnreadableError:
    return NotationUnreadableError(f"Not a readable notation PDF: {path.name}")


def _open(path: Path) -> PdfReader:
    """A reader over the checked file (size, encryption, page count)."""
    try:
        if path.stat().st_size > MAX_NOTATION_BYTES:
            raise _unreadable(path)
        reader = PdfReader(BytesIO(path.read_bytes()), strict=False)
        pages = len(reader.pages)
    except NotationUnreadableError:
        raise
    except Exception as exc:  # any parser failure of untrusted input
        raise _unreadable(path) from exc
    if reader.is_encrypted or not 1 <= pages <= MAX_NOTATION_PAGES:
        raise _unreadable(path)
    return reader


def _size(page: PageObject) -> tuple[float, float]:
    box = page.mediabox
    return float(box.width), float(box.height)


def _checked_sizes(path: Path, reader: PdfReader) -> tuple[tuple[float, float], ...]:
    """Page sizes of ``reader``, each page's content stream decoded and parsed once."""
    low, high = PAGE_LIMITS_PT
    sizes: list[tuple[float, float]] = []
    try:
        for page in reader.pages:
            contents = page.get_contents()
            if contents is not None:
                _ = contents.operations  # parse now: a merge never meets a parser limit
            sizes.append(_size(page))
    except Exception as exc:  # any parser failure of untrusted input
        raise _unreadable(path) from exc
    if any(not (low <= side <= high) for size in sizes for side in size):
        raise _unreadable(path)
    return tuple(sizes)


class PypdfNotationReader:
    """``NotationReader`` with pypdf (``ports.NotationReader``)."""

    def page_sizes(self, path: Path) -> tuple[tuple[float, float], ...]:
        """Width and height in points of every page.

        Raises:
            NotationUnreadableError: Too large, encrypted, damaged, too many or odd pages.
        """
        with apply_configuration(**PARSE_LIMITS):
            return _checked_sizes(path, _open(path))


def fits_paper(size: tuple[float, float], paper: str) -> bool:
    """Whether a page of ``size`` is the handout's paper (portrait or landscape)."""
    width, height = PAPER_SIZES_PT[paper]
    return any(
        abs(size[0] - w) <= _SIZE_TOLERANCE_PT and abs(size[1] - h) <= _SIZE_TOLERANCE_PT
        for w, h in ((width, height), (height, width))
    )


@dataclass(frozen=True, slots=True)
class Frame:
    """Where one notation page goes: a page of the laid-out handout and a box on it (points).

    Attributes:
        page: 0-based page of the handout.
        box: ``(left, bottom, width, height)`` in PDF points.
        source: The notation PDF.
        source_page: 0-based page of ``source``.
    """

    page: int
    box: tuple[float, float, float, float]
    source: Path
    source_page: int


def fit(box: tuple[float, float, float, float], size: tuple[float, float]) -> Transformation:
    """Scale a page of ``size`` into ``box`` keeping its aspect, centred, at the top."""
    left, bottom, width, height = box
    scale = min(width / size[0], height / size[1])
    offset_x = left + (width - size[0] * scale) / 2
    offset_y = bottom + height - size[1] * scale
    return Transformation().scale(scale, scale).translate(offset_x, offset_y)


def merge_frames(pdf: bytes, frames: Sequence[Frame], progress: Callable[[float], None]) -> bytes:
    """``pdf`` with every frame's notation page merged into it; ``progress`` gets 0..1.

    Raises:
        HandoutRenderError: A notation file changed and cannot be merged any more.
    """
    with apply_configuration(**PARSE_LIMITS):
        return _merged(pdf, frames, progress)


def _merged(pdf: bytes, frames: Sequence[Frame], progress: Callable[[float], None]) -> bytes:
    writer = PdfWriter(clone_from=BytesIO(pdf))
    readers: dict[Path, PdfReader] = {}
    try:
        for done, frame in enumerate(frames, 1):
            reader = readers.get(frame.source) or readers.setdefault(
                frame.source, _open(frame.source)
            )
            _merge(writer.pages[frame.page], reader.pages[frame.source_page], frame.box)
            progress(done / len(frames))
    except (NotationUnreadableError, PyPdfError, IndexError, ValueError, KeyError) as exc:
        raise HandoutRenderError(HANDOUT_FAILED_TEXT) from exc
    out = BytesIO()
    writer.write(out)
    return out.getvalue()


def _merge(target: PageObject, source: PageObject, box: tuple[float, float, float, float]) -> None:
    """Merge the content of ``source`` (without annotations or actions) into ``box``."""
    for key in _DROPPED_KEYS:
        if key in source:
            del source[NameObject(key)]
    lower_x, lower_y = float(source.mediabox.left), float(source.mediabox.bottom)
    placed = Transformation().translate(-lower_x, -lower_y)
    target.merge_transformed_page(source, placed.transform(fit(box, _size(source))))


def pdf_facts(pdf: bytes) -> tuple[int, tuple[str, ...]]:
    """Page count and the bookmark titles in document order of a finished handout."""
    reader = PdfReader(BytesIO(pdf))
    return len(reader.pages), tuple(_titles(reader.outline))


def _titles(outline: Sequence[object]) -> list[str]:
    titles: list[str] = []
    for item in outline:
        if isinstance(item, list):
            titles.extend(_titles(item))
        else:
            titles.append(str(getattr(item, "title", "")))
    return titles
