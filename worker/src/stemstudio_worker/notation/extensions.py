"""The 016 notation steps after the 008 score is built (data-model 4.5-4.7).

Order: chord symbols are inserted into a copy of the concert score, the part transposes that copy
(symbols included), the MusicXML gets ``<transpose>``, then the TAB staff. MIDI always comes from
the concert score without symbols. Without any 016 setting the document stays the 008 one.
"""

from collections.abc import Sequence
from dataclasses import dataclass, replace

from music21 import stream as m21_stream

from stemstudio_worker.notation.cleaning import reduce_monophonic
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.parts import (
    out_of_range,
    plan_part,
    summary,
    with_transpose,
    written_score,
)
from stemstudio_worker.notation.profiles import SECTION_PROFILES, Profile
from stemstudio_worker.notation.tab_staff import tablature_document
from stemstudio_worker.notation.tablature import FitResult, ViterbiTabFingerer
from stemstudio_worker.notation.writer import musicxml_bytes
from stemstudio_worker.ports import NotationSettings, NoteEvent, ScoreDocument
from stemstudio_worker.schemas import KeyInfo, ScoreWarning


@dataclass(frozen=True, slots=True)
class Extensions:
    """What the 016 steps need besides the document.

    Attributes:
        settings: The notation request.
        profile: The profile constants (range already widened for a tuning).
        key: Concert key of the score.
        fit: The cleaned notes after the tablature range fit.
    """

    settings: NotationSettings
    profile: Profile
    key: KeyInfo
    fit: FitResult


def section_warnings(notes: Sequence[NoteEvent], profile: Profile) -> tuple[ScoreWarning, ...]:
    """``section-reduced`` when a section profile kept one line of overlapping notes."""
    if profile.name not in SECTION_PROFILES:
        return ()
    in_range = [n for n in notes if profile.contains(n.pitch)]
    reduced = len(reduce_monophonic(in_range)) < len(in_range)
    return (score_warning("section-reduced"),) if reduced else ()


def _name(extensions: Extensions) -> str:
    return extensions.settings.titles.instrument_name or extensions.profile.name.capitalize()


def _with_part(
    document: ScoreDocument, score: m21_stream.Score, extensions: Extensions
) -> ScoreDocument:
    """Transpose the score for the written instrument (``concert`` + ``auto``: summary only)."""
    part = extensions.settings.part
    if part is None:
        return document
    plan = plan_part(part, extensions.profile, extensions.key, _name(extensions))
    outside = out_of_range(extensions.fit.notes, plan.info)
    warnings = (score_warning("part-out-of-range", n=outside),) if outside else ()
    musicxml = document.musicxml
    if part.instrument != "concert" or part.clef != "auto":
        written = written_score(score, plan, len(extensions.profile.clefs) > 1)
        musicxml = with_transpose(musicxml_bytes(written), plan.info)
    return replace(
        document,
        musicxml=musicxml,
        part=summary(plan, outside),
        warnings=document.warnings + warnings,
    )


def extend(
    document: ScoreDocument, score: m21_stream.Score, extensions: Extensions
) -> ScoreDocument:
    """Apply part and tablature to the 008 document (unchanged when neither is set)."""
    extended = _with_part(document, score, extensions)
    tab = extensions.settings.tablature
    if tab is None:
        return extended
    return tablature_document(extended, tab, extensions.fit, ViterbiTabFingerer())
