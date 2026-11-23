"""The ``HandoutRenderer`` of feature 020: document -> HTML -> WeasyPrint -> pypdf (ADR-029 D7).

Steps and progress (``ports.HandoutRenderer``): check the fixed notice, read the notation page
sizes and lay out the HTML (``layout`` 0-0.6), merge the notation pages into their frames
(``notation`` 0.6-0.9), read the page count and the outline and check the glyphs (``writing``
0.9-1). The renderer reads only the files of ``files`` (artwork through the allow-list fetcher,
notation PDFs through pypdf) and its packaged assets; it never uses the network (FR-018).
WeasyPrint is imported when the first handout is rendered, so the worker starts without Pango.
"""

import importlib
import logging
from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Final

from stemstudio_worker.handouts.document import (
    NotationBlock,
    SongInputs,
    build_view,
    check_notice,
)
from stemstudio_worker.handouts.glyphs import glyph_warning
from stemstudio_worker.handouts.markup import render_html
from stemstudio_worker.handouts.notation import (
    Frame,
    PypdfNotationReader,
    fits_paper,
    merge_frames,
    pdf_facts,
)
from stemstudio_worker.handouts.resources import ARCHIVO_FONT, AllowList
from stemstudio_worker.ports import (
    HANDOUT_FAILED_TEXT,
    HandoutRenderError,
    InsightsProgress,
    NotationReader,
    NotationUnreadableError,
    RenderedHandout,
    notation_unreadable_text,
)
from stemstudio_worker.schemas import HandoutJobRequest, HandoutNotation, HandoutWarning

_LOG = logging.getLogger(__name__)
MAX_WARNINGS: Final = 50
"""``HandoutResult.warnings`` holds at most 50."""

Layout = Callable[[str, AllowList], Any]
"""HTML + allow-list -> ``layout.LaidOut`` (``layout.lay_out``; replaced in tests)."""


def scaled_text(title: str) -> str:
    """Warning text of a notation file whose pages are not the handout's paper size."""
    return f"{title} is not on this paper size; its pages were scaled to fit."


def _default_layout() -> Layout:
    """``layout.lay_out``, imported now (it loads Pango)."""
    try:
        module = importlib.import_module("stemstudio_worker.handouts.layout")
    except (OSError, ImportError) as exc:
        _LOG.warning("WeasyPrint cannot be loaded: %s", exc)
        raise HandoutRenderError(HANDOUT_FAILED_TEXT) from exc
    layout: Layout = module.lay_out
    return layout


@dataclass(slots=True)
class _Job:
    """One render: the request, its files and allow-list, the frame plan and the warnings."""

    request: HandoutJobRequest
    files: Mapping[str, Path]
    reader: NotationReader
    allowed: AllowList
    sources: list[tuple[str, Path, int]] = field(default_factory=list)
    warnings: list[HandoutWarning] = field(default_factory=list)

    def warn(self, code: Any, text: str) -> None:
        """Add a warning (each text once, at most ``MAX_WARNINGS``)."""
        warning = HandoutWarning(code=code, text=text[:300])
        if warning not in self.warnings and len(self.warnings) < MAX_WARNINGS:
            self.warnings.append(warning)

    def song_inputs(self, index: int) -> SongInputs:
        """Artwork URI and notation blocks of song ``index`` (notation only when included)."""
        song = self.request.document.songs[index]
        artwork = self.files.get(song.artwork_path) if song.artwork_path else None
        uri = self.allowed.uri(artwork) if artwork in self.allowed.paths else None
        if "notation" not in song.include:
            return SongInputs(uri, ())
        blocks = tuple(
            self.block(f"frame-{index}-{k}", item) for k, item in enumerate(song.notation)
        )
        return SongInputs(uri, blocks)

    def block(self, anchor: str, item: HandoutNotation) -> NotationBlock:
        """One frame per page of ``item``, or a note when the file cannot be read."""
        heading = f"Notation - {item.title}"
        source = self.files.get(item.pdf_path)
        sizes = self.sizes(source, item.title)
        if source is None or sizes is None:
            text = notation_unreadable_text(item.title)
            self.warn("notation-unreadable", text)
            return NotationBlock(heading, item.title, (), text)
        frames = tuple(f"{anchor}-{page}" for page in range(len(sizes)))
        self.sources.extend((frame, source, page) for page, frame in enumerate(frames))
        return NotationBlock(heading, item.title, frames, None)

    def sizes(self, source: Path | None, title: str) -> tuple[tuple[float, float], ...] | None:
        """Page sizes of ``source`` (None when it cannot be read); warns when scaled."""
        try:
            sizes = self.reader.page_sizes(source) if source is not None else None
        except NotationUnreadableError:
            return None
        if sizes is not None and not all(fits_paper(s, self.request.paper) for s in sizes):
            self.warn("notation-scaled", scaled_text(title))
        return sizes


def _frame(placed: tuple[int, tuple[float, float, float, float]], source: Path, page: int) -> Frame:
    """The frame of a laid-out anchor (handout page and box) for page ``page`` of ``source``."""
    return Frame(page=placed[0], box=placed[1], source=source, source_page=page)


class WeasyHandoutRenderer:
    """``HandoutRenderer`` with Jinja2 templates, WeasyPrint and pypdf."""

    def __init__(self, reader: NotationReader | None = None, layout: Layout | None = None) -> None:
        """Read notation with ``reader`` (pypdf) and lay out with ``layout`` (WeasyPrint)."""
        self._reader = reader or PypdfNotationReader()
        self._layout = layout

    def render(
        self, request: HandoutJobRequest, files: Mapping[str, Path], progress: InsightsProgress
    ) -> RenderedHandout:
        """Lay out ``request`` on its paper and merge the notation (``ports.HandoutRenderer``).

        Raises:
            HandoutRenderError: The notice is not the fixed text or the PDF cannot be written.
            HandoutCancelledError: Propagated from ``progress``.
        """
        check_notice(request.document)
        layout = self._layout or _default_layout()
        job = _Job(request, files, self._reader, AllowList.for_job(files))
        try:
            return _render(job, progress, layout)
        except HandoutRenderError:
            raise
        except Exception as exc:
            _LOG.exception("A handout could not be written")
            raise HandoutRenderError(HANDOUT_FAILED_TEXT) from exc


def _render(job: _Job, progress: InsightsProgress, layout: Layout) -> RenderedHandout:
    inputs = [job.song_inputs(index) for index in range(len(job.request.document.songs))]
    progress(0.1)
    html = render_html(build_view(job.request, inputs), job.allowed.uri(ARCHIVO_FONT))
    progress(0.2)
    laid = layout(html, job.allowed)
    progress(0.6)
    frames = [_frame(laid.frames[anchor], source, page) for anchor, source, page in job.sources]
    pdf = merge_frames(laid.pdf, frames, lambda done: progress(0.6 + 0.3 * done))
    progress(0.9)
    pages, outline = pdf_facts(pdf)
    warning = glyph_warning(job.request.document)
    if warning is not None:
        job.warn(warning.code, warning.text)
    progress(1.0)
    return RenderedHandout(pdf=pdf, pages=pages, outline=outline, warnings=tuple(job.warnings))
