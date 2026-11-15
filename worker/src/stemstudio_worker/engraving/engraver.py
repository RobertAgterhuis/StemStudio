"""The ``ScoreEngraver``: font check, Verovio pages, CairoSVG per page, pypdf merge (ADR-017)."""

from collections.abc import Callable
from dataclasses import dataclass

from pypdf.errors import PyPdfError

from stemstudio_worker.engraving.fonts import leipzig_available
from stemstudio_worker.engraving.pdf import SvgToPdf, cairo_svg_to_pdf, merge_pages
from stemstudio_worker.engraving.verovio_svg import ENGRAVING_FAILED_TEXT, render_svg_pages
from stemstudio_worker.ports import EngravedScore, TranscriptionError
from stemstudio_worker.schemas import MUSIC_FONT_MISSING_TEXT

_CONVERSION_ERRORS = (OSError, ValueError, PyPdfError)
"""libcairo missing (OSError), unparsable SVG (ValueError, incl. XML parse errors' subclasses),
unreadable page PDF (pypdf)."""


@dataclass(frozen=True, slots=True)
class VerovioScoreEngraver:
    """``ScoreEngraver`` with the data-model.md 3 options.

    Attributes:
        font_check: Whether the Leipzig font is installed; checked before every score, so a
            missing font fails the job instead of printing boxes (SC-004).
        svg_to_pdf: Converts one SVG page to a one-page PDF (CairoSVG in production).
    """

    font_check: Callable[[], bool] = leipzig_available
    svg_to_pdf: SvgToPdf = cairo_svg_to_pdf

    def engrave(self, musicxml: bytes, title: str, subject: str | None) -> EngravedScore:
        """Render the score to an A4 PDF (see ``ports.ScoreEngraver.engrave``).

        Raises:
            TranscriptionError: ``MUSIC_FONT_MISSING_TEXT`` without the font, else
                ``ENGRAVING_FAILED_TEXT`` when the score cannot be laid out or converted.
        """
        if not self.font_check():
            raise TranscriptionError(MUSIC_FONT_MISSING_TEXT)
        svg_pages = render_svg_pages(musicxml)
        try:
            pdf = merge_pages([self.svg_to_pdf(svg) for svg in svg_pages], title, subject)
        except _CONVERSION_ERRORS as exc:
            raise TranscriptionError(ENGRAVING_FAILED_TEXT) from exc
        return EngravedScore(pdf=pdf, pages=len(svg_pages))
