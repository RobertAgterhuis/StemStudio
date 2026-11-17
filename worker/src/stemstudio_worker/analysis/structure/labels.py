"""Section kinds and labels (data-model 2.3, research R-03); the thresholds are binding data.

Rules in order: one segment is the "Song"; a low-vocal first / last segment is the intro /
outro; without a repeating cluster every other segment is a "Part <cluster>"; vocal segments are
chorus (the loudest repeating vocal cluster), verse, or bridge (a single vocal cluster after the
first chorus); low-vocal middle segments are a solo when one non-drum source stands out, else
instrumental; in-between vocal ratios count as verse.
"""

from collections import Counter
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.analysis.structure.names import numbered_labels
from stemstudio_worker.schemas import SectionKind, SectionWarningCode

VOCAL_RATIO: Final = 0.5
"""A segment with at least this vocal ratio is a vocal segment."""
LOW_VOCAL_RATIO: Final = 0.25
"""A segment below this vocal ratio is a low-vocal segment."""
SOLO_SHARE: Final = 0.4
"""Least share of the non-drum energy the solo source holds in the segment."""
SOLO_MEDIAN_FACTOR: Final = 1.3
"""The solo share is at least this times the source's median share over the song."""
DETAIL_SHARE: Final = 0.5
"""A kept detail stem names the solo when it holds this share of its parent's energy."""
NO_VOCALS_LEVEL: Final = 0.01
"""``no-vocals`` when the vocals' p95 RMS is below this share of the mix's p95 RMS."""
SOLO_SOURCES: Final = ("bass", "guitar", "piano", "other")
"""Sources of the solo rule: the standard stems except drums and vocals."""


@dataclass(frozen=True, slots=True)
class SegmentFacts:
    """What the rules read about one segment.

    Attributes:
        cluster: Cluster letter.
        vocal_ratio: Mean vocal activity ratio.
        mix_rms: Mean mix RMS.
        shares: Share of each ``SOLO_SOURCES`` stem in their summed energy.
        detail_shares: Share of each kept detail channel in its parent's energy.
    """

    cluster: str
    vocal_ratio: float
    mix_rms: float
    shares: Mapping[str, float]
    detail_shares: Mapping[str, float]


@dataclass(frozen=True, slots=True)
class SongFacts:
    """Song-wide facts: median solo-source shares over the bars and the ``no-vocals`` check."""

    median_shares: Mapping[str, float]
    no_vocals: bool


@dataclass(frozen=True, slots=True)
class Labelled:
    """Kinds, solo instruments, labels and warning codes per segment."""

    kinds: list[SectionKind]
    instruments: list[str | None]
    labels: list[str]
    warnings: list[SectionWarningCode]


def label_segments(segments: Sequence[SegmentFacts], song: SongFacts) -> Labelled:
    """Apply the labelling rules of data-model 2.3 to the segments in time order."""
    if len(segments) == 1:
        return Labelled(["other"], [None], ["Song"], [])
    rules = _Rules(segments, song)
    kinds, instruments = rules.all_kinds()
    labels = numbered_labels(kinds, [s.cluster for s in segments], instruments)
    return Labelled(kinds, instruments, labels, rules.warnings())


class _Rules:
    """The rule set over one song's segments."""

    def __init__(self, segments: Sequence[SegmentFacts], song: SongFacts) -> None:
        self._segments = segments
        self._song = song
        self._counts = Counter(s.cluster for s in segments)
        self._few = max(self._counts.values()) < 2  # noqa: PLR2004 - "occurs twice"
        self._chorus = self._chorus_cluster()
        self._first_chorus = next(
            (k for k, s in enumerate(segments) if s.cluster == self._chorus and self._vocal(k)),
            None,
        )

    def warnings(self) -> list[SectionWarningCode]:
        found: list[SectionWarningCode] = []
        if self._few:
            found.append("few-repetitions")
        if self._song.no_vocals:
            found.append("no-vocals")
        return found

    def all_kinds(self) -> tuple[list[SectionKind], list[str | None]]:
        picks = [self.kind(k) for k in range(len(self._segments))]
        return [kind for kind, _ in picks], [instrument for _, instrument in picks]

    def kind(self, k: int) -> tuple[SectionKind, str | None]:
        edge = k in (0, len(self._segments) - 1)
        if edge and self._low(k):
            return ("intro" if k == 0 else "outro"), None
        if self._few:
            return "other", None
        return self._middle_kind(k)

    def _middle_kind(self, k: int) -> tuple[SectionKind, str | None]:
        if self._vocal(k):
            return self._vocal_kind(k), None
        if self._low(k):
            return self._low_kind(k)
        return "verse", None

    def _vocal(self, k: int) -> bool:
        return not self._song.no_vocals and self._segments[k].vocal_ratio >= VOCAL_RATIO

    def _low(self, k: int) -> bool:
        return self._song.no_vocals or self._segments[k].vocal_ratio < LOW_VOCAL_RATIO

    def _vocal_kind(self, k: int) -> SectionKind:
        cluster = self._segments[k].cluster
        if cluster == self._chorus:
            return "chorus"
        if self._counts[cluster] >= 2:  # noqa: PLR2004
            return "verse"
        after_chorus = self._first_chorus is not None and k > self._first_chorus
        return "bridge" if after_chorus else "verse"

    def _low_kind(self, k: int) -> tuple[SectionKind, str | None]:
        source = self._solo_source(self._segments[k])
        if source is None:
            return "instrumental", None
        return "solo", _detail(self._segments[k], source)

    def _solo_source(self, segment: SegmentFacts) -> str | None:
        if not segment.shares:
            return None
        source = max(segment.shares, key=lambda name: segment.shares[name])
        share = segment.shares[source]
        median = self._song.median_shares.get(source, 0.0)
        return source if share >= SOLO_SHARE and share >= SOLO_MEDIAN_FACTOR * median else None

    def _chorus_cluster(self) -> str | None:
        """The repeating vocal cluster with the highest mean mix RMS (earlier wins a tie)."""
        energy: dict[str, list[float]] = {}
        for k, segment in enumerate(self._segments):
            if self._vocal(k) and self._counts[segment.cluster] >= 2:  # noqa: PLR2004
                energy.setdefault(segment.cluster, []).append(segment.mix_rms)
        means = {cluster: sum(v) / len(v) for cluster, v in energy.items()}
        return max(means, key=lambda c: means[c], default=None)


def _detail(segment: SegmentFacts, parent: str) -> str:
    """The detail channel of ``parent`` holding at least half its energy, else ``parent``."""
    details = {name: share for name, share in segment.detail_shares.items()
               if name.startswith(parent + ".") and share >= DETAIL_SHARE}  # fmt: skip
    return max(details, key=lambda name: details[name], default=parent)
