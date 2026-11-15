"""Instrument profiles of the sheet-music feature (data-model.md 2.1, ADR-017 D3/D5)."""

from collections.abc import Mapping
from dataclasses import dataclass
from types import MappingProxyType
from typing import Final, Literal

from stemstudio_worker.schemas import ScoreEngine, ScoreProfile, ScoreQuality, ScoreResolution

Clef = Literal["treble", "bass", "bass-8vb", "treble-8vb", "percussion"]
"""Clef of one staff (``-8vb``: written an octave above the sounding pitch)."""

GRAND_STAFF_SPLIT: Final = 60
"""Notes at or above this MIDI pitch go to the treble staff of a grand staff."""

MAX_NOTES_PER_BAR: Final = 32
"""Cap of note onsets per voice per bar (cleaning step 5)."""


@dataclass(frozen=True, slots=True)
class Profile:
    """Constants of one instrument profile.

    Attributes:
        name: Profile name.
        clefs: One clef per staff, top to bottom.
        low: Lowest kept MIDI pitch.
        high: Highest kept MIDI pitch.
        single_voice: Monophonic reduction and next-onset ends apply.
        default_resolution: Resolution when the request gives none.
        max_notes_per_bar: Cap per voice per bar.
    """

    name: ScoreProfile
    clefs: tuple[Clef, ...]
    low: int
    high: int
    single_voice: bool
    default_resolution: ScoreResolution
    max_notes_per_bar: int = MAX_NOTES_PER_BAR

    def contains(self, pitch: int) -> bool:
        """Return True when ``pitch`` lies inside the profile range."""
        return self.low <= pitch <= self.high


_GRAND: Final[tuple[Clef, ...]] = ("treble", "bass")

PROFILES: Final[Mapping[ScoreProfile, Profile]] = MappingProxyType(
    {
        "vocals": Profile("vocals", ("treble",), 40, 84, True, "eighth"),
        "bass": Profile("bass", ("bass-8vb",), 28, 67, True, "sixteenth"),
        "guitar": Profile("guitar", ("treble-8vb",), 40, 88, False, "sixteenth"),
        "keys": Profile("keys", _GRAND, 21, 108, False, "sixteenth"),
        "piano": Profile("piano", _GRAND, 21, 108, False, "sixteenth"),
        "drums": Profile("drums", ("percussion",), 0, 127, False, "sixteenth"),
    }
)
"""Every profile by name."""

_EXACT: Final[Mapping[str, ScoreProfile]] = MappingProxyType(
    {"bass": "bass", "piano": "piano", "drums": "drums"}
)
_FAMILIES: Final[Mapping[str, ScoreProfile]] = MappingProxyType(
    {"vocals": "vocals", "guitar": "guitar", "piano": "keys", "other": "keys"}
)


def get_profile(name: ScoreProfile) -> Profile:
    """Return the constants of profile ``name``."""
    return PROFILES[name]


def suggest_profile(channel: str) -> ScoreProfile | None:
    """Return the suggested profile of a stem channel, or None when none fits.

    ``vocals*`` -> vocals, ``bass`` -> bass, ``guitar*`` -> guitar, ``piano`` -> piano,
    ``piano.*`` and ``other*`` -> keys, ``drums`` -> drums; single drum pieces get none (008).
    """
    exact = _EXACT.get(channel)
    return exact if exact is not None else _FAMILIES.get(channel.split(".", 1)[0])


def resolve_resolution(profile: ScoreProfile, requested: ScoreResolution | None) -> ScoreResolution:
    """Return ``requested`` or, when None, the profile's default resolution."""
    return requested if requested is not None else PROFILES[profile].default_resolution


def engine_for(profile: ScoreProfile, quality: ScoreQuality) -> ScoreEngine:
    """Return the transcription engine of a profile (ADR-017 D1)."""
    if profile == "drums":
        return "drum-onsets"
    if profile == "piano" and quality == "accurate":
        return "piano-crnn"
    return "basic-pitch"


def staff_of(profile: Profile, pitch: int) -> int:
    """Return the staff index of ``pitch`` (grand staff: 0 = treble at >= 60, 1 = bass)."""
    if len(profile.clefs) == 1:
        return 0
    return 0 if pitch >= GRAND_STAFF_SPLIT else 1
