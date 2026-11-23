"""Signal quality analyzer: the ``ports.QualityAnalyzer`` of the ``insights`` lane (data-model 4).

Reads the six standard stems in one pass (their sum is the mix) and every detail stem, then
estimates bleed and artefacts per stem and rates it. Progress: loading 0-0.2, analysing and
rating 0.2-1.0 (one step per stem). Reliability labels come from the G1 table in
``thresholds.py`` (Phase 0: all ``indicative``).
"""

import time
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np

from stemstudio_worker.insights.artefacts import artefact_index
from stemstudio_worker.insights.bleed import BleedMeasure, bleed_sources, family_of, measure_bleed
from stemstudio_worker.insights.detail import detail_kinds, rest_share
from stemstudio_worker.insights.rating import RatingInputs, artefact_level, bleed_level, rate
from stemstudio_worker.insights.representation import (
    FRAME_SECONDS,
    SILENCE_DB,
    Spectrogram,
    read_spectrogram,
    read_standard,
)
from stemstudio_worker.insights.thresholds import (
    ANALYZER_VERSION,
    ARTEFACT_LEVELS,
    g1_status,
    reliability,
)
from stemstudio_worker.ports import InsightsProgress, QualityAnalysisError, QualityInputs, StemAudio
from stemstudio_worker.schemas import (
    STANDARD_STEMS,
    ArtefactEstimate,
    ArtefactKind,
    BleedEstimate,
    DetailParentInput,
    QualityLevel,
    QualityResult,
    StemQuality,
)

LOADED: Final = 0.2
"""Progress after all stems are read."""

MAX_LEVEL_DB: Final = 20.0

MISSING_STANDARD_TEXT: Final = "All six standard stems are needed (their sum is the mix)"


@dataclass(frozen=True, slots=True)
class _Context:
    """Everything the per-stem measures need."""

    standard: dict[str, Spectrogram]
    details: dict[str, Spectrogram]
    mix: Spectrogram
    parents: Mapping[str, DetailParentInput]

    def spectrogram(self, channel: str) -> Spectrogram:
        return self.standard[channel] if channel in self.standard else self.details[channel]


def _standard_paths(inputs: QualityInputs) -> dict[str, Path]:
    paths = {stem.channel: stem.path for stem in inputs.stems if stem.kind == "standard"}
    if set(paths) != set(STANDARD_STEMS):
        raise QualityAnalysisError(MISSING_STANDARD_TEXT)
    return {name: paths[name] for name in STANDARD_STEMS}


def _load(inputs: QualityInputs, progress: InsightsProgress) -> _Context:
    standard, mix = read_standard(_standard_paths(inputs))
    others = [stem for stem in inputs.stems if stem.kind != "standard"]
    progress(LOADED / 2)
    details: dict[str, Spectrogram] = {}
    for index, stem in enumerate(others, start=1):
        details[stem.channel] = read_spectrogram(stem.path)
        progress(LOADED / 2 * (1 + index / len(others)))
    parents: dict[str, DetailParentInput] = {item.parent: item for item in inputs.detail_parents}
    return _Context(standard, details, mix, parents)


def _relative_db(spec: Spectrogram, mix: Spectrogram) -> float:
    energy = float(np.sum(spec.power, dtype=np.float64))
    total = float(np.sum(mix.power, dtype=np.float64))
    if energy <= 0 or total <= 0:
        return SILENCE_DB
    return float(np.clip(10.0 * np.log10(energy / total), SILENCE_DB, MAX_LEVEL_DB))


def _bleed(
    stem: StemAudio, spec: Spectrogram, context: _Context
) -> tuple[BleedEstimate, BleedMeasure]:
    sources = bleed_sources(stem.channel, context.standard, context.details)
    measure = measure_bleed(family_of(stem.channel), spec, sources)
    status = g1_status(stem.channel, stem.kind, "bleed")
    estimate = BleedEstimate(
        level=bleed_level(measure.index, co_occurrence=measure.co_occurrence, status=status),
        index=measure.index,
        main_source=measure.main_source,
        co_occurrence=measure.co_occurrence,
        reliability=reliability(stem.channel, stem.kind, "bleed"),
    )
    return estimate, measure


def _parent_rest_share(stem: StemAudio, context: _Context) -> float | None:
    family = family_of(stem.channel)
    rest = context.details.get(f"{family}.rest")
    return None if rest is None else rest_share(rest, context.standard[family])


def _artefacts(stem: StemAudio, spec: Spectrogram, context: _Context) -> ArtefactEstimate:
    index = artefact_index(spec, context.mix)
    kinds: list[ArtefactKind] = ["dropouts"] if ARTEFACT_LEVELS.level(index) != "low" else []
    parent = context.parents.get(family_of(stem.channel))
    kinds += detail_kinds(stem.kind, parent, _parent_rest_share(stem, context))
    return ArtefactEstimate(
        level=artefact_level(index, status=g1_status(stem.channel, stem.kind, "artefacts")),
        index=index,
        kinds=kinds,
        reliability=reliability(stem.channel, stem.kind, "artefacts"),
    )


def _rating_inputs(spec: Spectrogram, mix: Spectrogram, measure: BleedMeasure,
                   levels: tuple[QualityLevel, QualityLevel]) -> RatingInputs:  # fmt: skip
    active = int(np.count_nonzero(spec.active()))
    return RatingInputs(
        active_seconds=active * FRAME_SECONDS,
        active_share=active / spec.frames if spec.frames else 0.0,
        level_db=_relative_db(spec, mix),
        bleed_index=measure.index,
        bleed_level=levels[0],
        artefact_level=levels[1],
        co_occurrence=measure.co_occurrence,
    )


def _assess(stem: StemAudio, context: _Context) -> StemQuality:
    spec = context.spectrogram(stem.channel)
    bleed, measure = _bleed(stem, spec, context)
    artefacts = _artefacts(stem, spec, context)
    inputs = _rating_inputs(spec, context.mix, measure, (bleed.level, artefacts.level))
    rating, reason = rate(inputs)
    facts = {"active_seconds": inputs.active_seconds, "active_share": inputs.active_share,
             "level_db": inputs.level_db}  # fmt: skip
    return StemQuality(channel=stem.channel, rating=rating, rating_reason=reason,
                       bleed=bleed, artefacts=artefacts, **facts)  # fmt: skip


class SignalQualityAnalyzer:
    """Reference-free bleed and artefact estimates from the stems and their sum (ADR-029)."""

    @property
    def version(self) -> str:
        """``analyzerVersion`` of every result (``thresholds.ANALYZER_VERSION``)."""
        return ANALYZER_VERSION

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Estimate every stem of ``inputs`` (results in input order).

        Raises:
            QualityAnalysisError: A standard stem is missing or a stem cannot be read.
            QualityCancelledError: Propagated from ``progress``.
        """
        started = time.perf_counter()
        progress(0.0)
        context = _load(inputs, progress)
        stems: list[StemQuality] = []
        for index, stem in enumerate(inputs.stems, start=1):
            stems.append(_assess(stem, context))
            progress(LOADED + (1.0 - LOADED) * index / len(inputs.stems))
        return QualityResult(
            analyzer_version=ANALYZER_VERSION,
            duration_seconds=round(time.perf_counter() - started, 3),
            stems=stems,
        )
