"""Internal ports of the ``instrument-analysis`` lane (feature 016, data-model 2.3, 3.1, 5.1).

The service runs one step per job kind: a :class:`ChordAnalyzer` (the real pipeline mixdown ->
recognizer -> post-processing -> key, or the fake chords), the 008 ``DrumTranscriber`` and a
``PitchAnalyzer`` over the vocal stem read by the ``HarmonicMixer``. The parts are composed in
``main.py`` (:class:`InstrumentAnalysisParts`).
"""

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final, Literal, Protocol

from stemstudio_worker.ports import (
    AnalysisError,
    AnalysisProgress,
    DeviceUse,
    DrumTranscriber,
    HarmonicMixer,
    PitchAnalyzer,
)
from stemstudio_worker.schemas import (
    ChordChart,
    ChordEngine,
    ChordsJobRequest,
    InstrumentAnalysisModels,
    PitchEngine,
)

DrumHitsEngine = Literal["drum-onsets", "fake"]
"""``DrumHits.engine``."""


@dataclass(frozen=True, slots=True)
class ChordInputs:
    """Resolved files of a chords job.

    Attributes:
        harmonic: The harmonic stems to sum (1-4).
        bass: The bass stem for slash chords; None = no inversions.
    """

    harmonic: tuple[Path, ...]
    bass: Path | None = None


@dataclass(frozen=True, slots=True)
class ChordOutcome:
    """The chart of a chords job and where the model ran (None for CPU-only engines)."""

    chart: ChordChart
    device: DeviceUse | None = None


class ChordAnalyzer(Protocol):
    """A chords job end to end (``jobs.instrument_analysis_chords`` or ``views_fake``)."""

    @property
    def default_engine(self) -> ChordEngine:
        """The engine a request with ``engine: null`` gets (``STEMSTUDIO_CHORD_ENGINE``)."""
        ...

    def models(self) -> InstrumentAnalysisModels:
        """Readiness of the BTC weights (``Health.instrumentAnalysis.models``)."""
        ...

    def analyze(
        self, request: ChordsJobRequest, inputs: ChordInputs, progress: AnalysisProgress
    ) -> ChordOutcome:
        """The chart with key and key regions.

        Raises:
            AnalysisError: Shown to the user.
            AnalysisCancelledError: Propagated from ``progress``.
        """
        ...


@dataclass(frozen=True, slots=True)
class InstrumentAnalysisParts:
    """What the analysis jobs run (composed in ``main.py``).

    Attributes:
        chords: The chords step.
        drums: Onsets per drum detail stem (008 ``LibrosaDrumTranscriber`` or the fake).
        drum_engine: ``DrumHits.engine`` of ``drums``.
        pitch: Reference melody of a vocal stem.
        mixer: Reads stems to mono (the vocal stem at ``PITCH_SAMPLE_RATE``).
    """

    chords: ChordAnalyzer
    drums: DrumTranscriber
    drum_engine: DrumHitsEngine
    pitch: PitchAnalyzer
    mixer: HarmonicMixer

    @property
    def pitch_engine(self) -> PitchEngine:
        """``Health.instrumentAnalysis.pitchEngine``."""
        return self.pitch.engine


UNAVAILABLE_TEXT: Final = "Instrument analyses are not available on this worker."
"""Failure text of every analysis job of a worker composed without analysis parts."""


class NoAnalysis:
    """Every part of a worker composed without analysis parts: each job fails readably."""

    engine: Final = "pyin"
    default_engine: Final = "btc"

    def models(self) -> InstrumentAnalysisModels:
        """Nothing is cached."""
        return InstrumentAnalysisModels()

    def analyze(self, *_args: object) -> Any:
        """Fail with ``UNAVAILABLE_TEXT`` (chords and pitch)."""
        raise AnalysisError(UNAVAILABLE_TEXT)

    def transcribe(self, *_args: object) -> Any:
        """Fail with ``UNAVAILABLE_TEXT`` (drum hits)."""
        raise AnalysisError(UNAVAILABLE_TEXT)

    def mix(self, *_args: object) -> Any:
        """Fail with ``UNAVAILABLE_TEXT`` (vocal stem)."""
        raise AnalysisError(UNAVAILABLE_TEXT)


def unavailable_parts() -> InstrumentAnalysisParts:
    """Parts whose jobs all fail with ``UNAVAILABLE_TEXT`` (``WorkerParts`` default)."""
    missing: Any = NoAnalysis()
    return InstrumentAnalysisParts(chords=missing, drums=missing, drum_engine="drum-onsets",
                                   pitch=missing, mixer=missing)  # fmt: skip
