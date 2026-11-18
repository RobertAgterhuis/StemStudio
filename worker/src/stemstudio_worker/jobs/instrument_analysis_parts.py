"""Composition of the ``instrument-analysis`` parts by the settings (feature 016, data-model 1).

``STEMSTUDIO_CHORD_ENGINE=fake`` gives the fake chords of ``views_fake``; otherwise the real
pipeline with BTC (weights below ``STEMSTUDIO_MODEL_DIR/harmony``, chroma-hmm as its fallback)
and chroma-hmm. ``STEMSTUDIO_PITCH_ENGINE`` picks pYIN, YIN or the fake melody; the drum hits use
the 008 drum onsets, or the fake groove with ``STEMSTUDIO_TRANSCRIBER=fake``.

The beat-analysis and audio-ingest adapters (chroma-hmm, post-processing, key, pitch, mixdown)
are looked up by module path through ``loader``, so a worker without one of them still starts:
the missing part fails its jobs readably (``MISSING_PART_TEXT``).
"""

import importlib
from collections.abc import Callable
from dataclasses import dataclass
from types import ModuleType
from typing import Any, Final

from stemstudio_worker.analysis import LibrosaDrumTranscriber
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.harmony.btc import BtcRecognizer, ChordModelStore, TorchBtcRuntime
from stemstudio_worker.jobs.instrument_analysis_chords import (
    ChordPipelineParts,
    PipelineChordAnalyzer,
)
from stemstudio_worker.jobs.instrument_analysis_ports import (
    ChordAnalyzer,
    DrumHitsEngine,
    InstrumentAnalysisParts,
)
from stemstudio_worker.ports import AnalysisError, DeviceRunner
from stemstudio_worker.schemas import InstrumentAnalysisModels
from stemstudio_worker.views_fake import (
    FakeChordAnalyzer,
    FakeDrumHitsTranscriber,
    FakePitchAnalyzer,
)

MISSING_PART_TEXT: Final = "This analysis is not available on this worker."
"""Failure text of a job whose adapter is not installed."""

Loader = Callable[[str], ModuleType]
"""Imports a module by its dotted path (``importlib.import_module``; a fake in tests)."""

_PACKAGE: Final = "stemstudio_worker"


class MissingPart:
    """Stands in for an adapter that is not installed: every call fails readably."""

    def __init__(self, engine: str) -> None:
        """Report ``engine`` as the part's engine name."""
        self.engine = engine

    def __getattr__(self, name: str) -> Callable[..., Any]:
        """Any method fails with ``MISSING_PART_TEXT``."""

        def fail(*_args: object, **_kwargs: object) -> Any:
            raise AnalysisError(MISSING_PART_TEXT)

        return fail


@dataclass(frozen=True, slots=True)
class _Lookup:
    loader: Loader

    def build(self, module: str, name: str, label: str, **kwargs: Any) -> Any:
        """``<package>.<module>.<name>(**kwargs)``, or a :class:`MissingPart`."""
        try:
            factory = getattr(self.loader(f"{_PACKAGE}.{module}"), name)
        except (ImportError, AttributeError):
            return MissingPart(label)
        return factory(**kwargs)


def create_instrument_analysis_parts(
    settings: WorkerSettings, devices: DeviceRunner, loader: Loader = importlib.import_module
) -> tuple[InstrumentAnalysisParts, BtcRecognizer | None]:
    """The parts of the analysis jobs and the BTC recognizer (for the idle release), if any."""
    lookup = _Lookup(loader)
    analysis = settings.instrument_analysis
    mixer = lookup.build("analysis_inputs.mixdown", "SoundfileHarmonicMixer", "mixdown",
                         allowed_root=settings.render.library_dir)  # fmt: skip
    btc = None if analysis.chord_engine == "fake" else _btc(settings, devices, lookup)
    chords = FakeChordAnalyzer() if btc is None else _pipeline(settings, mixer, btc, lookup)
    pitch = (FakePitchAnalyzer() if analysis.pitch_engine == "fake" else
             lookup.build("analysis.vocal_pitch", "LibrosaPitchAnalyzer", analysis.pitch_engine,
                          engine=analysis.pitch_engine))  # fmt: skip
    fake_drums = settings.transcription.transcriber == "fake"
    drums = FakeDrumHitsTranscriber() if fake_drums else LibrosaDrumTranscriber()
    engine: DrumHitsEngine = "fake" if fake_drums else "drum-onsets"
    parts = InstrumentAnalysisParts(chords=chords, drums=drums, pitch=pitch, mixer=mixer,
                                    drum_engine=engine)  # fmt: skip
    return parts, btc


def _btc(settings: WorkerSettings, devices: DeviceRunner, lookup: _Lookup) -> BtcRecognizer:
    fallback = lookup.build("harmony.chroma_hmm", "ChromaHmmRecognizer", "chroma-hmm")
    runtime = TorchBtcRuntime(settings.instrument_analysis.threads)
    return BtcRecognizer(ChordModelStore(settings.model_dir), devices, runtime, fallback)


def _pipeline(
    settings: WorkerSettings, mixer: Any, btc: BtcRecognizer, lookup: _Lookup
) -> ChordAnalyzer:
    key_module = _module(lookup.loader, "harmony.key")
    parts = ChordPipelineParts(
        mixer=mixer,
        recognizers={"btc": btc, "chroma-hmm": btc.fallback},
        postprocessor=lookup.build("harmony.postprocess", "BeatSnapPostProcessor", "postprocess"),
        keys=lookup.build("harmony.key", "HmmKeyEstimator", "key"),
        apply_key=getattr(key_module, "apply_key", MissingPart("key").apply_key),
    )

    def models() -> InstrumentAnalysisModels:
        return InstrumentAnalysisModels.model_validate(btc.models_ready())

    return PipelineChordAnalyzer(parts, settings.instrument_analysis.chord_engine, models)


def _module(loader: Loader, module: str) -> ModuleType | None:
    try:
        return loader(f"{_PACKAGE}.{module}")
    except ImportError:
        return None
