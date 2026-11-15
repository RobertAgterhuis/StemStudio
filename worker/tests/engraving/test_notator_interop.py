"""The real notator's MusicXML engraves (008 T072): pitched, grand-staff and percussion scores.

The notator (beat-analysis, ``notation/``) is the engraver's only producer, so its output is the
input that matters: every profile shape must lay out into full A4 pages with only the music font
and the text fonts. CairoSVG is replaced by a blank page (``test_engraver.py`` runs the real one).
"""

import re

import pytest

from stemstudio_worker.engraving import VerovioScoreEngraver, verovio_svg
from stemstudio_worker.notation import create_notator
from stemstudio_worker.ports import DrumHit, NotationSettings, NoteEvent, ScoreDocument
from stemstudio_worker.schemas import ManualTempo, ScoreProfile, ScoreTitles

from .scores import TEXT_FONTS, blank_pdf, font_families, read_pdf

BPM = 120
BEAT = 60 / BPM
A4_SVG = re.compile(r'<svg width="840px" height="1188px"')


def _settings(profile: ScoreProfile, bars: int) -> NotationSettings:
    return NotationSettings(
        profile=profile,
        grid=ManualTempo(bpm=BPM, beats_per_bar=4, first_downbeat_seconds=0.0),
        resolution="eighth",
        key_override=None,
        titles=ScoreTitles(title=f"{profile.title()} - Song"),
        duration_seconds=bars * 4 * BEAT,
    )


def _line(bars: int, low: int, step: int = 1) -> list[NoteEvent]:
    """Eighth notes climbing two octaves from ``low`` (``step`` > 1 skips through a wider range)."""
    eighths = range(bars * 8)
    return [
        NoteEvent(
            start=i * BEAT / 2, end=(i + 1) * BEAT / 2, pitch=low + (i * step) % 24, velocity=0.8
        )
        for i in eighths
    ]


def _groove(bars: int) -> list[DrumHit]:
    hits = [DrumHit(time=i * BEAT / 2, piece="hihat", strength=0.7) for i in range(bars * 8)]
    hits += [
        DrumHit(time=b * BEAT, piece="kick" if b % 2 == 0 else "snare", strength=0.9)
        for b in range(bars * 4)
    ]
    return sorted(hits, key=lambda hit: hit.time)


def _documents() -> dict[str, ScoreDocument]:
    notator = create_notator()
    return {
        "bass": notator.notate(_line(64, 28), _settings("bass", 64)),
        "piano": notator.notate(_line(16, 36, step=5), _settings("piano", 16)),
        "drums": notator.notate_drums(_groove(16), _settings("drums", 16)),
    }


DOCUMENTS = _documents()


@pytest.mark.parametrize("profile", sorted(DOCUMENTS))
def test_the_notator_output_engraves_into_full_a4_pages(profile: str) -> None:
    musicxml = DOCUMENTS[profile].musicxml
    svg_pages = verovio_svg.render_svg_pages(musicxml)
    engraver = VerovioScoreEngraver(font_check=lambda: True, svg_to_pdf=lambda svg: blank_pdf())

    result = engraver.engrave(musicxml, f"{profile} - Song", None)

    assert result.pages == len(svg_pages) == len(read_pdf(result.pdf).pages)
    assert all(A4_SVG.search(page) for page in svg_pages)
    families = set().union(*(font_families(page) for page in svg_pages))
    assert families <= TEXT_FONTS | {"Leipzig"}, families


def test_a_long_bass_line_needs_more_than_one_page() -> None:
    assert len(verovio_svg.render_svg_pages(DOCUMENTS["bass"].musicxml)) > 1
