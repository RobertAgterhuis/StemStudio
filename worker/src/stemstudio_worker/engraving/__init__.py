"""Score engraving of feature 008: MusicXML -> Verovio SVG pages -> CairoSVG -> pypdf (ADR-017).

The composition root (``main.create_app``, stem-separation) builds the engraver with
``create_score_engraver`` and reports ``Health.transcription.font.leipzig`` from
``fonts.leipzig_available()``. Dependencies: ``ports``, ``schemas`` (fixed texts), Verovio,
CairoSVG (lazy) and pypdf only.
"""

from stemstudio_worker.engraving import fonts, pdf, verovio_svg
from stemstudio_worker.engraving.engraver import VerovioScoreEngraver
from stemstudio_worker.engraving.verovio_svg import ENGRAVING_FAILED_TEXT
from stemstudio_worker.ports import ScoreEngraver

__all__ = [
    "ENGRAVING_FAILED_TEXT",
    "VerovioScoreEngraver",
    "create_score_engraver",
    "fonts",
    "pdf",
    "verovio_svg",
]


def create_score_engraver() -> ScoreEngraver:
    """Build the production engraver (fontconfig check, CairoSVG pages)."""
    return VerovioScoreEngraver()
