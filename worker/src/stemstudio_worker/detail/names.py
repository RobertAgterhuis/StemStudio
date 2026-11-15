"""Parent and detail stem names, their display order and the families per parent (data-model 1).

Every name is a closed ``Literal`` value from ``schemas``, so names are safe path components.
"""

from typing import Final, TypeGuard, get_args

from stemstudio_worker.schemas import DetailStemName, ParentStemName, StemName

STANDARD_ORDER: Final[tuple[StemName, ...]] = get_args(StemName)
"""The standard stems in display order (vocals, drums, bass, guitar, piano, other)."""

SPLITTABLE: Final[frozenset[str]] = frozenset(get_args(ParentStemName))
"""Standard stems that have a detail model (every stem except bass)."""

INSTRUMENT_PARENTS: Final[tuple[ParentStemName, ...]] = ("guitar", "piano", "other")
"""Parents split by the instrument-family model (MVSep Mega 53), in standard display order."""

DRUM_STEMS: Final[tuple[DetailStemName, ...]] = ("kick", "snare", "toms", "hihat", "cymbals")
"""Detail stems of ``drums`` in display order."""

VOCAL_STEMS: Final[tuple[DetailStemName, ...]] = ("lead", "backing")
"""Detail stems of ``vocals`` in display order."""

INSTRUMENT_STEMS: Final[tuple[DetailStemName, ...]] = (
    "strings", "brass", "woodwinds", "keys", "synth", "acoustic-guitar", "electric-guitar",
    "percussion",
)  # fmt: skip
"""Instrument families of ``guitar``, ``piano`` and ``other`` in display order."""

REST: Final[DetailStemName] = "rest"
"""The remainder stem of every split parent (parent minus the kept sub-stems), listed last."""

_BY_PARENT: Final[dict[ParentStemName, tuple[DetailStemName, ...]]] = {
    "drums": DRUM_STEMS,
    "vocals": VOCAL_STEMS,
    **dict.fromkeys(INSTRUMENT_PARENTS, INSTRUMENT_STEMS),
}


def detail_names(parent: ParentStemName) -> tuple[DetailStemName, ...]:
    """Return the possible detail stems of ``parent`` in display order (without ``rest``)."""
    return _BY_PARENT[parent]


def is_splittable(stem: StemName) -> TypeGuard[ParentStemName]:
    """Return True when ``stem`` can be split further (every standard stem except bass)."""
    return stem in SPLITTABLE


def display_order(parent: ParentStemName, names: set[DetailStemName]) -> list[DetailStemName]:
    """Sort kept detail stems of ``parent`` into display order, ``rest`` last when present."""
    ordered = [name for name in detail_names(parent) if name in names]
    return [*ordered, REST] if REST in names else ordered
