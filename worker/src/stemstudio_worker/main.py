"""Composition root and ASGI entry point of the worker.

Run with ``uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000``.

At start-up (lifespan) the device runner is built once (``devices.create_device_runner``: the GPU
probe of feature 003) and shared by the separator, the detail pipeline and ``/health``; when the
scheduler becomes idle the GPU models are released (ADR-012 D6).

The adapters of features 005 and 007 come from the ``metadata`` package (picture normaliser,
song lookup, source details reader; one rate-limited online client) and the ``render`` package
(one library reader shared by ``/health`` and the render executor). Feature 006 adds the lyrics
lookup, the aligner and the vocal loader; alignments run through the shared device runner.
Feature 013 adds the storage services (meter, janitor, rehydrator) over the running jobs.
"""

import importlib
from collections.abc import AsyncIterator, Callable
from contextlib import AbstractAsyncContextManager, asynccontextmanager
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any, Final

from fastapi import FastAPI

from stemstudio_worker.alignment import alignment_status, create_aligner, create_vocal_loader
from stemstudio_worker.analysis import LibrosaDrumTranscriber, create_beat_analyzer
from stemstudio_worker.analysis.structure import FakeStructureAnalyzer, StemsNoveltyAnalyzer
from stemstudio_worker.api import (
    artwork_routes,
    detail_routes,
    instrument_analysis_routes,
    lyrics_routes,
    metadata_routes,
    practice_routes,
    render_routes,
    storage,
    transcription_routes,
)
from stemstudio_worker.api.problems import install_problem_handlers
from stemstudio_worker.api.routes import router
from stemstudio_worker.config import WorkerSettings, load_settings
from stemstudio_worker.detail import create_detail_pipeline
from stemstudio_worker.devices import ManagedRunner, create_device_runner
from stemstudio_worker.documents import ReportLabSetlistRenderer
from stemstudio_worker.engraving import create_score_engraver, fonts
from stemstudio_worker.engraving.score_view import VerovioScoreViewRenderer
from stemstudio_worker.harmony.btc import BtcParts, BtcRecognizer, TorchBtcRuntime
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.instrument_analysis_chords import (
    ChordPipelineParts,
    PipelineChordAnalyzer,
)
from stemstudio_worker.jobs.instrument_analysis_ports import (
    ChordAnalyzer,
    DrumHitsEngine,
    InstrumentAnalysisParts,
)
from stemstudio_worker.jobs.lyrics_runner import LyricsParts, VocalLoader
from stemstudio_worker.jobs.practice_lane import PracticeParts
from stemstudio_worker.jobs.runtime import (
    DeviceHooks,
    OnlineParts,
    RenderParts,
    WorkerParts,
    WorkerRuntime,
    utc_now,
)
from stemstudio_worker.jobs.transcription_pipeline import (
    ScoreRules,
    TranscriptionEnginesPort,
    TranscriptionParts,
)
from stemstudio_worker.lyrics.lookup import create_lyrics_lookup
from stemstudio_worker.metadata import (
    create_image_normalizer,
    create_metadata_lookup,
    create_source_details,
    shared_online_client,
)
from stemstudio_worker.notation import create_notator
from stemstudio_worker.notation.notices import score_warning
from stemstudio_worker.notation.profiles import get_profile, resolve_resolution
from stemstudio_worker.ports import (
    BTC_GPU_NEED,
    WHISPER_GPU_NEED,
    Aligner,
    AnalysisError,
    DeviceRunner,
    DrumTranscriber,
    GpuNeed,
    ImageNormalizer,
    LibraryReader,
    LyricsLookup,
    MetadataLookup,
    Notator,
    PipelineAdapters,
    QualityAnalyzer,
    RenderExecutor,
    ScoreEngraver,
    ScoreViewRenderer,
    SetlistDocumentRenderer,
    StructureAnalyzer,
)
from stemstudio_worker.render import create_library_reader, create_render_executor
from stemstudio_worker.schemas import CONTRACT_VERSION, InstrumentAnalysisModels, ScoreProfile
from stemstudio_worker.separation import create_separator
from stemstudio_worker.storage.services import (
    StorageServices,
    StorageWiring,
    create_storage_services,
)
from stemstudio_worker.transcription import create_transcription_engines
from stemstudio_worker.transcription.models import ModelStore, UrlOpener, https_open
from stemstudio_worker.views_fake import (
    FakeChordAnalyzer,
    FakeDrumHitsTranscriber,
    FakePitchAnalyzer,
)

Lifespan = Callable[[FastAPI], AbstractAsyncContextManager[None]]

_ROUTERS = (
    router,
    detail_routes.router,
    metadata_routes.router,
    artwork_routes.router,
    render_routes.router,
    lyrics_routes.router,
    transcription_routes.router,
    storage.router,
    practice_routes.router,
    instrument_analysis_routes.router,
)


@dataclass(frozen=True, slots=True)
class AppOverrides:
    """Parts the tests inject instead of the production factories (all optional).

    Attributes:
        detail_pipeline: Detail step pipeline (default ``create_detail_pipeline``).
        image_normalizer: Source picture normaliser (default ``metadata.create_image_normalizer``).
        metadata_lookup: Song lookup of metadata jobs (default ``metadata.create_metadata_lookup``).
        render_executor: Renderer of render jobs (default ``render.create_render_executor``).
        library: Library reader (default ``render.create_library_reader``).
        lyrics_lookup: LRCLIB lookup of lyrics jobs (default ``lyrics.create_lyrics_lookup``).
        aligner: Lyrics aligner (default ``alignment.create_aligner``).
        vocal_loader: Vocal stem loader of alignments (default ``alignment.create_vocal_loader``).
        transcription_engines: Per-job transcribers (default
            ``transcription.create_transcription_engines``).
        notator: Notes -> score (default ``notation.create_notator``).
        score_engraver: MusicXML -> PDF (default ``engraving.create_score_engraver``).
        structure_analyzer: Section analyser (default :func:`create_structure_analyzer`).
        score_view_renderer: Score views (default ``engraving.score_view``).
        setlist_renderer: Setlist PDFs (default ``documents.ReportLabSetlistRenderer``).
        instrument_analysis: Chords, drum hits and melody parts (default
            :func:`create_instrument_analysis_parts`, feature 016).
    """

    detail_pipeline: DetailPipelinePort | None = None
    image_normalizer: ImageNormalizer | None = None
    metadata_lookup: MetadataLookup | None = None
    render_executor: RenderExecutor | None = None
    library: LibraryReader | None = None
    lyrics_lookup: LyricsLookup | None = None
    aligner: Aligner | None = None
    vocal_loader: VocalLoader | None = None
    transcription_engines: TranscriptionEnginesPort | None = None
    notator: Notator | None = None
    score_engraver: ScoreEngraver | None = None
    structure_analyzer: StructureAnalyzer | None = None
    score_view_renderer: ScoreViewRenderer | None = None
    setlist_renderer: SetlistDocumentRenderer | None = None
    instrument_analysis: InstrumentAnalysisParts | None = None
    quality_analyzer: QualityAnalyzer | None = None


def create_app(
    settings: WorkerSettings | None = None,
    adapters: PipelineAdapters | None = None,
    devices: ManagedRunner | None = None,
    overrides: AppOverrides | None = None,
) -> FastAPI:
    """Create the FastAPI application.

    The job subsystem starts in the lifespan: the device is probed, jobs and detail jobs are
    loaded, interrupted ones are failed ("interrupted by restart") and the scheduler thread
    starts; it stops on shutdown.

    Args:
        settings: Settings to use; read from the environment when omitted.
        adapters: Pipeline adapters; built with the adapter factories when omitted.
        devices: Device runner; built with ``create_device_runner`` (start-up probe) when omitted.
        overrides: Further injected parts; missing ones are built with their factories.

    Returns:
        The configured application.
    """
    resolved = settings or load_settings()
    lifespan = _lifespan(resolved, _Parts(adapters, devices, overrides or AppOverrides()))
    app = FastAPI(title="StemStudio Worker API", version=CONTRACT_VERSION, lifespan=lifespan)
    app.state.settings = resolved
    install_problem_handlers(app)
    for routes in _ROUTERS:
        app.include_router(routes)
    return app


def default_adapters(settings: WorkerSettings, devices: ManagedRunner) -> PipelineAdapters:
    """Build the production adapters from the ingest, separation and analysis factories.

    The ingest adapters get the ``metadata`` source details reader, which adds the artwork
    candidates, album, ISRC and lookup names of feature 005 to resolved and fetched sources.
    """
    details = create_source_details(settings)
    return PipelineAdapters(
        ingest=create_ingest_adapters(settings, details=details),
        separator=create_separator(settings, devices),
        analyzer=create_beat_analyzer(settings),
    )


def default_online(settings: WorkerSettings, overrides: AppOverrides) -> OnlineParts:
    """The feature 005 parts: injected ones, else built by the ``metadata`` factories."""
    normalizer = overrides.image_normalizer or create_image_normalizer(settings)
    lookup = overrides.metadata_lookup or create_metadata_lookup(settings)
    return OnlineParts(normalizer=normalizer, lookup=lookup)


def default_render(settings: WorkerSettings, overrides: AppOverrides) -> RenderParts:
    """The feature 007 parts: one library reader shared by ``/health`` and the executor."""
    library = overrides.library or create_library_reader(settings)
    executor = overrides.render_executor or create_render_executor(settings, library)
    return RenderParts(executor=executor, library=library)


def default_lyrics(
    settings: WorkerSettings, overrides: AppOverrides, devices: ManagedRunner
) -> LyricsParts:
    """The feature 006 parts: injected ones, else the ``lyrics``/``alignment`` factories.

    The LRCLIB lookup uses the process-wide 005 online client (one shared rate limiter).
    """
    lookup = overrides.lyrics_lookup or create_lyrics_lookup(
        settings, shared_online_client(settings.online)
    )
    status = alignment_status(settings)
    return LyricsParts(
        lookup=lookup,
        aligner=overrides.aligner or create_aligner(settings),
        vocals=overrides.vocal_loader or create_vocal_loader(),
        devices=devices,
        model_cached=status.model_cached,
        health=status.health,
    )


class _Parts:
    """The optional, injected parts of the app (tests); missing ones are built at start-up."""

    def __init__(
        self,
        adapters: PipelineAdapters | None,
        devices: ManagedRunner | None,
        overrides: AppOverrides,
    ) -> None:
        self.adapters = adapters
        self.devices = devices
        self.overrides = overrides

    def start(self, settings: WorkerSettings) -> WorkerRuntime:
        """Build what is missing (one device runner for everything) and start the runtime."""
        devices = self.devices or create_device_runner(settings)
        pipeline = self.overrides.detail_pipeline or create_detail_pipeline(settings, devices)
        adapters = self.adapters or default_adapters(settings, devices)
        lyrics = default_lyrics(settings, self.overrides, devices)
        analysis, btc = self._analysis(settings, devices)
        hooks = _GpuModels(pipeline, lyrics.aligner, btc).hooks(devices)
        online = default_online(settings, self.overrides)
        render = default_render(settings, self.overrides)
        scores = default_transcription(settings, self.overrides, devices, render.library)
        practice = default_practice(settings, self.overrides)
        parts = WorkerParts(adapters, pipeline, hooks, online, render, lyrics, scores, practice,
                            analysis)  # fmt: skip
        return WorkerRuntime.start_with(settings, parts)

    def _analysis(
        self, settings: WorkerSettings, devices: ManagedRunner
    ) -> tuple[InstrumentAnalysisParts, BtcRecognizer | None]:
        """The injected analysis parts (no BTC to release), else the configured ones (016)."""
        if self.overrides.instrument_analysis is not None:
            return self.overrides.instrument_analysis, None
        return create_instrument_analysis_parts(settings, devices)


def _pitch_range(profile: ScoreProfile) -> tuple[int, int]:
    found = get_profile(profile)
    return found.low, found.high


SCORE_RULES = ScoreRules(pitch_range=_pitch_range, resolution=resolve_resolution,
                         warning=score_warning)  # fmt: skip
"""The ``notation`` profile facts the transcription jobs use."""


def default_transcription(
    settings: WorkerSettings,
    overrides: AppOverrides,
    devices: ManagedRunner,
    library: LibraryReader,
) -> TranscriptionParts:
    """The feature 008 parts: injected ones, else the transcription/notation/engraving factories."""
    engines = overrides.transcription_engines or create_transcription_engines(
        settings, devices, LibrosaDrumTranscriber()
    )
    notator = overrides.notator or create_notator()
    engraver = overrides.score_engraver or create_score_engraver()
    return TranscriptionParts(engines, notator, engraver, SCORE_RULES, library,
                              fonts.leipzig_available)  # fmt: skip


def create_structure_analyzer(settings: WorkerSettings) -> StructureAnalyzer:
    """The configured section analyser: ``fake`` or ``real`` (``stems-novelty``, feature 014)."""
    if settings.structure_analyzer == "fake":
        return FakeStructureAnalyzer()
    return StemsNoveltyAnalyzer()


def default_practice(settings: WorkerSettings, overrides: AppOverrides) -> PracticeParts:
    """The feature 014 parts: the injected ones, else the configured analyser and renderers."""
    analyzer = overrides.structure_analyzer or create_structure_analyzer(settings)
    views = overrides.score_view_renderer or VerovioScoreViewRenderer()
    documents = overrides.setlist_renderer or ReportLabSetlistRenderer()
    return PracticeParts(analyzer=analyzer, score_views=views, documents=documents)


class _GpuModels:
    """The cached models: the detail pipeline's (``release_gpu``), the aligner's and BTC's."""

    def __init__(self, pipeline: object, aligner: object, btc: object = None) -> None:
        self._pipeline: Callable[..., None] | None = getattr(pipeline, "release_gpu", None)
        self._aligner: Callable[[], None] | None = getattr(aligner, "release", None)
        self._btc: Callable[[], None] | None = getattr(btc, "release", None)

    def hooks(self, devices: ManagedRunner) -> DeviceHooks:
        """Register :meth:`reclaim` on ``devices``; the device hooks with the idle release."""
        devices.reclaim_with(self.reclaim)
        return DeviceHooks(info=devices.info, on_idle=self.idle_release(devices))

    def reclaim(self, need: GpuNeed) -> None:
        """Release the models a step that does not fit does not need (keeps its own)."""
        if self._pipeline is not None:
            self._pipeline(keep=need)
        for drop, own in ((self._aligner, WHISPER_GPU_NEED), (self._btc, BTC_GPU_NEED)):
            if drop is not None and need != own:
                drop()

    def idle_release(self, devices: ManagedRunner) -> Callable[[], None]:
        """Idle hook: drop every cached model, then return the GPU memory."""

        def release() -> None:
            for drop in (self._pipeline, self._aligner, self._btc):
                if drop is not None:
                    drop()
            devices.release()

        return release


def default_storage(runtime: WorkerRuntime, settings: WorkerSettings) -> StorageServices:
    """The feature 013 storage services over the running job subsystem."""
    wiring = StorageWiring(
        paths=runtime.paths, jobs=runtime.store, details=runtime.details,
        model_dir=settings.model_dir, queue_hold=runtime.scheduler.hold,
        sweep_artwork=runtime.lanes.artwork.sweep, clock=utc_now,
    )  # fmt: skip
    return create_storage_services(wiring)


def _lifespan(settings: WorkerSettings, parts: _Parts) -> Lifespan:
    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        runtime = parts.start(settings)
        app.state.runtime = runtime
        app.state.storage = default_storage(runtime, settings)
        try:
            yield
        finally:
            runtime.stop()

    return lifespan


# ------------------------------------------------------------------ feature 016: instrument views

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

    def build(self, target: str, label: str, **kwargs: Any) -> Any:
        """``<module>.<Name>(**kwargs)`` of ``target`` = ``module:Name``, or a missing part."""
        module, _, name = target.partition(":")
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
    mixer = lookup.build("analysis_inputs.mixdown:SoundfileHarmonicMixer", "mixdown",
                         allowed_root=settings.render.library_dir)  # fmt: skip
    btc = None if analysis.chord_engine == "fake" else _btc(settings, devices, lookup)
    chords = FakeChordAnalyzer() if btc is None else _pipeline(settings, mixer, btc, lookup)
    drums, engine = _drums(settings)
    parts = InstrumentAnalysisParts(chords=chords, drums=drums, pitch=_pitch(settings, lookup),
                                    mixer=mixer, drum_engine=engine)  # fmt: skip
    return parts, btc


def _pitch(settings: WorkerSettings, lookup: _Lookup) -> Any:
    """The configured ``PitchAnalyzer`` (L2's pYIN/YIN, or the fake)."""
    engine = settings.instrument_analysis.pitch_engine
    if engine == "fake":
        return FakePitchAnalyzer()
    return lookup.build("analysis.vocal_pitch:LibrosaPitchAnalyzer", engine, engine=engine)


def _drums(settings: WorkerSettings) -> tuple[DrumTranscriber, DrumHitsEngine]:
    """The 008 drum transcriber of the configured transcriber and its ``DrumHits.engine``."""
    if settings.transcription.transcriber == "fake":
        return FakeDrumHitsTranscriber(), "fake"
    return LibrosaDrumTranscriber(), "drum-onsets"


def _btc(settings: WorkerSettings, devices: DeviceRunner, lookup: _Lookup) -> BtcRecognizer:
    fallback = lookup.build("harmony.chroma_hmm:ChromaHmmRecognizer", "chroma-hmm")
    runtime = TorchBtcRuntime(settings.instrument_analysis.threads)
    return BtcRecognizer(BtcParts(ChordModelStore(settings.model_dir), devices, runtime, fallback))


def _pipeline(
    settings: WorkerSettings, mixer: Any, btc: BtcRecognizer, lookup: _Lookup
) -> ChordAnalyzer:
    key_module = _module(lookup.loader, "harmony.key")
    parts = ChordPipelineParts(
        mixer=mixer,
        recognizers={"btc": btc, "chroma-hmm": btc.fallback},
        postprocessor=lookup.build("harmony.postprocess:BeatSnapPostProcessor", "postprocess"),
        keys=lookup.build("harmony.key:HmmKeyEstimator", "key"),
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


class ChordModelStore(ModelStore):
    """The 008 model store with the BTC weights below ``STEMSTUDIO_MODEL_DIR/harmony``."""

    def __init__(self, model_dir: Path, opener: UrlOpener = https_open) -> None:
        """Keep weights below ``model_dir/harmony``; fetch with ``opener``."""
        super().__init__(model_dir, opener)
        self._root = model_dir / "harmony"


QUALITY_ANALYZERS: Final[dict[str, str]] = {}


def create_quality_analyzer(
    settings: WorkerSettings, loader: Loader = importlib.import_module
) -> Any:
    """Stub."""
    raise NotImplementedError


def default_insights(
    settings: WorkerSettings, overrides: AppOverrides, loader: Loader = importlib.import_module
) -> Any:
    """Stub."""
    raise NotImplementedError


def run() -> None:  # pragma: no cover - thin CLI wrapper around uvicorn
    """Start the worker on 0.0.0.0:8000 (console script ``stemstudio-worker``)."""
    import uvicorn  # noqa: PLC0415 - imported lazily so tests do not need a server

    uvicorn.run(create_app(), host="0.0.0.0", port=8000)  # noqa: S104 - container-internal port
