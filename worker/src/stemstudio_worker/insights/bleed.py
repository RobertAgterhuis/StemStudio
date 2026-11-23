"""Bleed estimate (data-model 4.2/4.3). Stub for the red commit of T030."""

from collections.abc import Mapping, Sequence
from dataclasses import dataclass

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.schemas import StemName

CO_OCCURRENCE_RATIO = 2.0


@dataclass(frozen=True, slots=True)
class PairFit:
    """Stub."""

    share: float
    full_share: float

    @property
    def guard_ratio(self) -> float:
        """Stub."""
        raise NotImplementedError


@dataclass(frozen=True, slots=True)
class BleedSource:
    """Stub."""

    family: StemName
    spectrogram: Spectrogram


@dataclass(frozen=True, slots=True)
class BleedMeasure:
    """Stub."""

    index: float
    main_source: StemName | None
    co_occurrence: bool


def family_of(channel: str) -> StemName:
    """Stub."""
    raise NotImplementedError


def fit_pair(target: Spectrogram, source: Spectrogram) -> PairFit:
    """Stub."""
    raise NotImplementedError


def measure_bleed(family: str, target: Spectrogram, sources: Sequence[BleedSource]) -> BleedMeasure:
    """Stub."""
    raise NotImplementedError


def bleed_sources(
    channel: str, standard: Mapping[str, Spectrogram], details: Mapping[str, Spectrogram]
) -> list[BleedSource]:
    """Stub."""
    raise NotImplementedError
