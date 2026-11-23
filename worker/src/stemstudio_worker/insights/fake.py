"""Fake quality analyzer: fixed estimates per channel for the fake stack and E2E (data-model 2.6).

No audio is read beyond the existence check. Standard stems: vocals fair (medium bleed of
drums), drums good, bass good, guitar poor (high artefacts, flicker), piano not-present, other
fair (medium bleed of guitar); detail stems good; ``rest`` stems fair (``rest-share``). Every
value agrees with the real rating rules (``rating.rate``), and every label is ``indicative``
as in Phase 0.
"""

import time
from collections.abc import Callable
from typing import Final

from stemstudio_worker.ports import InsightsProgress, QualityAnalysisError, QualityInputs, StemAudio
from stemstudio_worker.schemas import (
    ArtefactEstimate,
    ArtefactKind,
    BleedEstimate,
    QualityLevel,
    QualityRating,
    QualityReasonCode,
    QualityResult,
    StemName,
    StemQuality,
)

FAKE_VERSION: Final = "fake-1"
"""``analyzerVersion`` of the fake."""

FAKE_DURATION_SECONDS: Final = 0.5
"""Duration of every fake job (waited for in ``STEPS`` steps, reported as ``durationSeconds``)."""

STEPS: Final = 5


def _bleed(level: QualityLevel, index: float, source: StemName | None) -> BleedEstimate:
    return BleedEstimate(
        level=level, index=index, main_source=source, co_occurrence=False, reliability="indicative"
    )


def _artefacts(level: QualityLevel, index: float, kinds: list[ArtefactKind]) -> ArtefactEstimate:
    return ArtefactEstimate(level=level, index=index, kinds=kinds, reliability="indicative")


_Fixed = tuple[
    QualityRating, QualityReasonCode, float, float, float, BleedEstimate, ArtefactEstimate
]

_CLEAN: Final[_Fixed] = ("good", "clean", 180.0, 0.7, -9.0, _bleed("low", 0.0, None),
                         _artefacts("low", 0.05, []))  # fmt: skip

_STANDARD: Final[dict[str, _Fixed]] = {
    "vocals": ("fair", "bleed", 182.4, 0.69, -6.1, _bleed("medium", 0.03, "drums"),
               _artefacts("low", 0.1, [])),
    "drums": _CLEAN,
    "bass": _CLEAN,
    "guitar": ("poor", "artefacts", 150.0, 0.62, -8.0, _bleed("low", 0.0, None),
               _artefacts("high", 0.8, ["flicker"])),
    "piano": ("not-present", "not-present", 11.0, 0.04, -34.5, _bleed("high", 0.71, "guitar"),
              _artefacts("low", 0.1, [])),
    "other": ("fair", "bleed", 160.0, 0.66, -10.0, _bleed("medium", 0.03, "guitar"),
              _artefacts("low", 0.1, [])),
}  # fmt: skip

_REST: Final[_Fixed] = ("fair", "artefacts", 120.0, 0.5, -18.0, _bleed("low", 0.0, None),
                        _artefacts("medium", 0.45, ["rest-share"]))  # fmt: skip


def _fixed(channel: str) -> _Fixed:
    if channel.endswith(".rest"):
        return _REST
    return _STANDARD.get(channel, _CLEAN)


def _quality(channel: str) -> StemQuality:
    rating, reason, seconds, share, level_db, bleed, artefacts = _fixed(channel)
    return StemQuality(
        channel=channel,
        rating=rating,
        rating_reason=reason,
        active_seconds=seconds,
        active_share=share,
        level_db=level_db,
        bleed=bleed,
        artefacts=artefacts,
    )


def _check_exists(stem: StemAudio) -> None:
    if not stem.path.is_file():
        msg = f"A stem could not be read: {stem.path.name}"
        raise QualityAnalysisError(msg)


class FakeQualityAnalyzer:
    """Deterministic ``ports.QualityAnalyzer`` (``STEMSTUDIO_QUALITY_ANALYZER=fake``)."""

    def __init__(self, sleep: Callable[[float], None] = time.sleep) -> None:
        """Create the fake; ``sleep`` is injectable so tests do not wait."""
        self._sleep = sleep

    @property
    def version(self) -> str:
        """Always ``fake-1``."""
        return FAKE_VERSION

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Fixed estimates per channel, in input order, after ``FAKE_DURATION_SECONDS``.

        Raises:
            QualityAnalysisError: A stem file does not exist.
            QualityCancelledError: Propagated from ``progress``.
        """
        progress(0.0)
        for stem in inputs.stems:
            _check_exists(stem)
        for step in range(1, STEPS + 1):
            self._sleep(FAKE_DURATION_SECONDS / STEPS)
            progress(step / STEPS)
        return QualityResult(
            analyzer_version=FAKE_VERSION,
            duration_seconds=FAKE_DURATION_SECONDS,
            stems=[_quality(stem.channel) for stem in inputs.stems],
        )
