"""The real chords pipeline of the ``instrument-analysis`` lane (data-model 2.3, R-04, R-13).

Binding order: mix the harmonic stems to mono 22,050 Hz (and the bass stem for ``full``) ->
recognizer of the requested engine (``null`` = ``STEMSTUDIO_CHORD_ENGINE``; BTC falls back to
chroma-hmm by itself) -> post-processing (beat snapping, merge, slash bass) -> key and key
regions (skipped when every segment is ``N``). Progress: 0-0.05 mix, 0.05-0.80 model, 0.85 after
post-processing, 0.95 after the key; the service reports 1.0.
"""

from collections.abc import Callable, Mapping
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs, ChordOutcome
from stemstudio_worker.ports import (
    CHORD_SAMPLE_RATE,
    AnalysisError,
    AnalysisProgress,
    ChordFrames,
    ChordPostProcessor,
    ChordRecognizer,
    HarmonicMixer,
    KeyEstimate,
    KeyEstimator,
    MonoAudio,
    PostProcessOptions,
)
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    AnalysisWarning,
    ChordChart,
    ChordEngine,
    ChordsJobRequest,
    InstrumentAnalysisModels,
)

MIXED: Final = 0.05
"""Progress after the mixdown."""

MODELLED: Final = 0.80
"""Progress after the recognizer."""

POST_PROCESSED: Final = 0.85
"""Progress after beat snapping and merging."""

KEYED: Final = 0.95
"""Progress after the key estimate."""

_FALLBACK: Final = AnalysisWarning(code="fallback-engine",
                                   text=ANALYSIS_WARNING_TEXTS["fallback-engine"])  # fmt: skip

KeyApplier = Callable[[ChordChart, KeyEstimate], ChordChart]
"""Puts a key estimate into a chart (key, regions, ``preferFlats``, ``key-uncertain``)."""


@dataclass(frozen=True, slots=True)
class ChordPipelineParts:
    """The parts of the real pipeline (audio-ingest and beat-analysis adapters, BTC).

    Attributes:
        mixer: Harmonic mixdown.
        recognizers: Recognizer per engine (``btc``, ``chroma-hmm``).
        postprocessor: Frames -> segments.
        keys: Key estimator.
        apply_key: Merges the estimate into the chart.
    """

    mixer: HarmonicMixer
    recognizers: Mapping[ChordEngine, ChordRecognizer]
    postprocessor: ChordPostProcessor
    keys: KeyEstimator
    apply_key: KeyApplier


def scaled(progress: AnalysisProgress, low: float, high: float) -> AnalysisProgress:
    """``progress`` of a sub-step mapped from 0..1 into ``low..high``."""

    def report(fraction: float, /) -> None:
        progress(low + (high - low) * fraction)

    return report


class PipelineChordAnalyzer:
    """``ChordAnalyzer`` over the real parts."""

    def __init__(
        self,
        parts: ChordPipelineParts,
        default_engine: ChordEngine,
        models: Callable[[], InstrumentAnalysisModels],
    ) -> None:
        """Run ``parts``; ``default_engine`` for ``engine: null``; ``models`` for ``/health``."""
        self._parts = parts
        self._default = default_engine
        self._models = models

    @property
    def default_engine(self) -> ChordEngine:
        """The configured engine (``STEMSTUDIO_CHORD_ENGINE``)."""
        return self._default

    def models(self) -> InstrumentAnalysisModels:
        """Readiness of the BTC weights."""
        return self._models()

    def analyze(
        self, request: ChordsJobRequest, inputs: ChordInputs, progress: AnalysisProgress
    ) -> ChordOutcome:
        """The chart with key; see the module docstring for the order.

        Raises:
            AnalysisError: The engine is not available or a step failed readably.
        """
        recognizer = self._recognizer(request.engine or self._default)
        progress(0.0)
        audio, bass = self._mix(request, inputs)
        progress(MIXED)
        frames = recognizer.recognize(audio, request.vocabulary, scaled(progress, MIXED, MODELLED))
        options = PostProcessOptions(grid=request.grid, vocabulary=request.vocabulary, bass=bass)
        chart = self._parts.postprocessor.segments(frames, options)
        progress(POST_PROCESSED)
        chart = self._keyed(chart, frames, request)
        progress(KEYED)
        return ChordOutcome(chart=_with_fallback(chart, frames), device=frames.device)

    def _recognizer(self, engine: ChordEngine) -> ChordRecognizer:
        recognizer = self._parts.recognizers.get(engine)
        if recognizer is None:
            raise AnalysisError(f"The chord engine {engine} is not available on this worker.")
        return recognizer

    def _mix(
        self, request: ChordsJobRequest, inputs: ChordInputs
    ) -> tuple[MonoAudio, MonoAudio | None]:
        mixer = self._parts.mixer
        audio = mixer.mix(inputs.harmonic, CHORD_SAMPLE_RATE)
        if request.vocabulary != "full" or inputs.bass is None:
            return audio, None
        return audio, mixer.mix([inputs.bass], CHORD_SAMPLE_RATE)

    def _keyed(
        self, chart: ChordChart, frames: ChordFrames, request: ChordsJobRequest
    ) -> ChordChart:
        if chart.no_chords:
            return chart
        estimate = self._parts.keys.estimate(frames.chroma, chart.segments, request.grid)
        return self._parts.apply_key(chart, estimate)


def _with_fallback(chart: ChordChart, frames: ChordFrames) -> ChordChart:
    """Add ``fallback-engine`` once when BTC fell back to chroma-hmm."""
    if not frames.fallback or any(w.code == "fallback-engine" for w in chart.warnings):
        return chart
    return chart.model_copy(update={"warnings": [*chart.warnings, _FALLBACK]})
