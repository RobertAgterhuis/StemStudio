"""Display names of section kinds and channels (data-model 2.3, the 004 instrument labels)."""

from collections import Counter
from collections.abc import Mapping, Sequence
from typing import Final

from stemstudio_worker.schemas import SectionKind

KIND_NAMES: Final[Mapping[SectionKind, str]] = {
    "intro": "Intro", "verse": "Verse", "pre-chorus": "Pre-chorus", "chorus": "Chorus",
    "bridge": "Bridge", "solo": "Solo", "instrumental": "Instrumental", "outro": "Outro",
    "other": "Part",
}  # fmt: skip
"""Display name of every kind; ``other`` sections are named "Part <cluster>"."""

_STEM_NAMES: Final[Mapping[str, str]] = {
    "vocals": "Vocals", "drums": "Drums", "bass": "Bass", "guitar": "Guitar", "piano": "Piano",
    "other": "Other",
}  # fmt: skip

_DETAIL_NAMES: Final[Mapping[str, str]] = {
    "kick": "Kick", "snare": "Snare", "toms": "Toms", "hihat": "Hi-hat", "cymbals": "Cymbals",
    "lead": "Lead vocals", "backing": "Backing vocals", "strings": "Strings", "brass": "Brass",
    "woodwinds": "Woodwinds", "keys": "Keys", "synth": "Synth",
    "acoustic-guitar": "Acoustic guitar", "electric-guitar": "Electric guitar",
    "percussion": "Percussion",
}  # fmt: skip


def channel_name(channel: str) -> str:
    """The 004 display name of a stem (``guitar``) or a detail channel (``guitar.keys``)."""
    parent, _, detail = channel.partition(".")
    if detail:
        return _DETAIL_NAMES.get(detail, detail.replace("-", " ").capitalize())
    return _STEM_NAMES.get(parent, parent.capitalize())


def solo_label(instrument: str | None) -> str:
    """The solo label "<instrument> solo", or "Solo" without an instrument."""
    return "Solo" if instrument is None else f"{channel_name(instrument)} solo"


def numbered_labels(
    kinds: Sequence[SectionKind], clusters: Sequence[str], instruments: Sequence[str | None]
) -> list[str]:
    """Section labels: the kind name, numbered per kind in time order when it occurs twice or more.

    ``other`` sections become "Part <cluster>" (never numbered), solos "<instrument> solo".
    """
    totals = Counter(kinds)
    seen: Counter[SectionKind] = Counter()
    labels = []
    for kind, cluster, instrument in zip(kinds, clusters, instruments, strict=True):
        seen[kind] += 1
        number = seen[kind] if totals[kind] > 1 else None
        labels.append(_label(kind, cluster, instrument, number))
    return labels


def _label(kind: SectionKind, cluster: str, instrument: str | None, number: int | None) -> str:
    if kind == "other":
        return f"Part {cluster}"
    name = solo_label(instrument) if kind == "solo" else KIND_NAMES[kind]
    return name if number is None else f"{name} {number}"
