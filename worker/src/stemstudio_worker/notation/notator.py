"""The :class:`~stemstudio_worker.ports.Notator`: clean -> quantise -> key -> build -> write."""

import dataclasses
from collections.abc import Sequence
from dataclasses import dataclass

from stemstudio_worker.notation.builders import (
    BuiltScore,
    ScoreSpec,
    build_drum_score,
    build_pitched_score,
)
from stemstudio_worker.notation.cleaning import (
    PlacedNote,
    clean_hits,
    clean_notes,
    cleaning_warnings,
)
from stemstudio_worker.notation.extensions import Extensions, extend, section_warnings
from stemstudio_worker.notation.keys import MIN_KEY_NOTES, KeyResult, resolve_key
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.profiles import Profile, get_profile
from stemstudio_worker.notation.tablature import FitResult, fit_notes
from stemstudio_worker.notation.timebase import TimeBase
from stemstudio_worker.notation.writer import midi_bytes, musicxml_bytes
from stemstudio_worker.ports import (
    TAB_MAX_FRET,
    TUNINGS,
    DrumHit,
    NoNotesError,
    NotationSettings,
    NoteEvent,
    ScoreDocument,
)
from stemstudio_worker.schemas import KeyInfo, ManualTempo, ScoreWarning

MAX_WARNINGS = 8
"""``ScoreSummary.warnings`` holds at most this many entries."""


class MusicNotator:
    """music21-based notator for every profile (data-model.md 2.3-2.7)."""

    def notate(self, notes: Sequence[NoteEvent], settings: NotationSettings) -> ScoreDocument:
        """Build the score of a pitched profile.

        Raises:
            NoNotesError: Nothing is left after cleaning.
        """
        profile = _with_tuning_range(get_profile(settings.profile), settings)
        timebase = _timebase(settings)
        cleaned = clean_notes(notes, profile, timebase.quantiser(settings.resolution))
        if not cleaned.notes:
            raise NoNotesError
        fit = _fitted(cleaned.notes, settings)
        key, built = _built(fit, profile, timebase, settings)
        warnings = (*cleaning_warnings(cleaned), *key.warnings, *section_warnings(notes, profile))
        facts = _Facts(key.info, len(fit.notes), (*_tempo_warnings(settings), *warnings))
        extensions = Extensions(
            settings, profile, key.info, fit, timebase.quantiser(settings.resolution)
        )
        return extend(_document(built, timebase, facts), built.score, extensions)

    def notate_drums(self, hits: Sequence[DrumHit], settings: NotationSettings) -> ScoreDocument:
        """Build the two-voice percussion score of the ``drums`` profile.

        Raises:
            NoNotesError: No hits.
        """
        timebase = _timebase(settings)
        cleaned = clean_hits(hits, timebase.quantiser(settings.resolution))
        if not cleaned.hits:
            raise NoNotesError
        spec = ScoreSpec(get_profile("drums"), timebase, None, settings.titles)
        built = build_drum_score(cleaned.hits, spec)
        few = (score_warning("few-notes"),) if len(cleaned.hits) < MIN_KEY_NOTES else ()
        warnings = (*_tempo_warnings(settings), *cleaning_warnings(cleaned), *few)
        return _document(built, timebase, _Facts(None, len(cleaned.hits), warnings))


def _built(
    fit: FitResult, profile: Profile, timebase: TimeBase, settings: NotationSettings
) -> tuple[KeyResult, BuiltScore]:
    """The key of the fitted notes and the pitched score spelled in it."""
    key = resolve_key(fit.notes, settings.key_override)
    spec = ScoreSpec(profile, timebase, key.info, settings.titles)
    return key, build_pitched_score(fit.notes, spec)


def _with_tuning_range(profile: Profile, settings: NotationSettings) -> Profile:
    """The profile range widened to the tablature tuning (drop D, 5-string bass)."""
    if settings.tablature is None:
        return profile
    pitches = TUNINGS[settings.tablature.tuning].pitches
    low, high = min(profile.low, pitches[0]), max(profile.high, pitches[-1] + TAB_MAX_FRET)
    return dataclasses.replace(profile, low=low, high=high)


def _fitted(notes: Sequence[PlacedNote], settings: NotationSettings) -> FitResult:
    """The notes fitted into the tablature tuning (unchanged without tablature)."""
    if settings.tablature is None:
        return FitResult(tuple(notes), 0, 0)
    tab = settings.tablature
    return fit_notes(notes, TUNINGS[tab.tuning], tab.capo)


def _timebase(settings: NotationSettings) -> TimeBase:
    return TimeBase.from_grid(settings.grid, settings.duration_seconds)


def _tempo_warnings(settings: NotationSettings) -> tuple[ScoreWarning, ...]:
    return (score_warning("manual-tempo"),) if isinstance(settings.grid, ManualTempo) else ()


@dataclass(frozen=True, slots=True)
class _Facts:
    """Summary facts that come from the cleaning and the key."""

    key: KeyInfo | None
    notes: int
    warnings: tuple[ScoreWarning, ...]


def _document(built: BuiltScore, timebase: TimeBase, facts: _Facts) -> ScoreDocument:
    """Write the files and collect the facts of the summary."""
    return ScoreDocument(
        musicxml=musicxml_bytes(built.score),
        midi=midi_bytes(built.score),
        key=facts.key,
        bpm=timebase.bpm,
        time_signature=timebase.time_signature,
        bars=built.bars,
        notes=facts.notes,
        warnings=facts.warnings[:MAX_WARNINGS],
    )
