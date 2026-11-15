"""Composition root and ASGI entry point of the worker.

Run with ``uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000``.

At start-up (lifespan) the device runner is built once (``devices.create_device_runner``: the GPU
probe of feature 003) and shared by the separator, the detail pipeline and ``/health``; when the
scheduler becomes idle the GPU models are released (ADR-012 D6).

The adapters of features 005 and 007 come from the ``metadata`` package (picture normaliser,
song lookup, source details reader; one rate-limited online client) and the ``render`` package
(one library reader shared by ``/health`` and the render executor). Feature 006 adds the lyrics
lookup, the aligner and the vocal loader; alignments run through the shared device runner.
"""

from collections.abc import AsyncIterator, Callable
from contextlib import AbstractAsyncContextManager, asynccontextmanager
from dataclasses import dataclass

from fastapi import FastAPI

from stemstudio_worker.alignment import alignment_status, create_aligner, create_vocal_loader
from stemstudio_worker.analysis import create_beat_analyzer
from stemstudio_worker.api import (
    artwork_routes,
    detail_routes,
    lyrics_routes,
    metadata_routes,
    render_routes,
)
from stemstudio_worker.api.problems import install_problem_handlers
from stemstudio_worker.api.routes import router
from stemstudio_worker.config import WorkerSettings, load_settings
from stemstudio_worker.detail import create_detail_pipeline
from stemstudio_worker.devices import ManagedRunner, create_device_runner
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.lyrics_runner import LyricsParts, VocalLoader
from stemstudio_worker.jobs.runtime import (
    DeviceHooks,
    OnlineParts,
    RenderParts,
    WorkerParts,
    WorkerRuntime,
)
from stemstudio_worker.lyrics.lookup import create_lyrics_lookup
from stemstudio_worker.metadata import (
    _shared_client as shared_online_client,
)
from stemstudio_worker.metadata import (
    create_image_normalizer,
    create_metadata_lookup,
    create_source_details,
)
from stemstudio_worker.ports import (
    WHISPER_GPU_NEED,
    Aligner,
    GpuNeed,
    ImageNormalizer,
    LibraryReader,
    LyricsLookup,
    MetadataLookup,
    PipelineAdapters,
    RenderExecutor,
)
from stemstudio_worker.render import create_library_reader, create_render_executor
from stemstudio_worker.schemas import CONTRACT_VERSION
from stemstudio_worker.separation import create_separator

Lifespan = Callable[[FastAPI], AbstractAsyncContextManager[None]]

_ROUTERS = (
    router,
    detail_routes.router,
    metadata_routes.router,
    artwork_routes.router,
    render_routes.router,
    lyrics_routes.router,
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
    """

    detail_pipeline: DetailPipelinePort | None = None
    image_normalizer: ImageNormalizer | None = None
    metadata_lookup: MetadataLookup | None = None
    render_executor: RenderExecutor | None = None
    library: LibraryReader | None = None
    lyrics_lookup: LyricsLookup | None = None
    aligner: Aligner | None = None
    vocal_loader: VocalLoader | None = None


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
        models = _GpuModels(pipeline, lyrics.aligner)
        devices.reclaim_with(models.reclaim)
        hooks = DeviceHooks(info=devices.info, on_idle=models.idle_release(devices))
        online = default_online(settings, self.overrides)
        render = default_render(settings, self.overrides)
        parts = WorkerParts(adapters, pipeline, hooks, online, render, lyrics)
        return WorkerRuntime.start_with(settings, parts)


class _GpuModels:
    """The worker's cached models: the detail pipeline's (``release_gpu``) and the aligner's."""

    def __init__(self, pipeline: object, aligner: object) -> None:
        self._pipeline: Callable[..., None] | None = getattr(pipeline, "release_gpu", None)
        self._aligner: Callable[[], None] | None = getattr(aligner, "release", None)

    def reclaim(self, need: GpuNeed) -> None:
        """Release the models a step that does not fit does not need (keeps its own)."""
        if self._pipeline is not None:
            self._pipeline(keep=need)
        if self._aligner is not None and need != WHISPER_GPU_NEED:
            self._aligner()

    def idle_release(self, devices: ManagedRunner) -> Callable[[], None]:
        """Idle hook: drop every cached model, then return the GPU memory."""

        def release() -> None:
            for drop in (self._pipeline, self._aligner):
                if drop is not None:
                    drop()
            devices.release()

        return release


def _lifespan(settings: WorkerSettings, parts: _Parts) -> Lifespan:
    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        runtime = parts.start(settings)
        app.state.runtime = runtime
        try:
            yield
        finally:
            runtime.stop()

    return lifespan


def run() -> None:  # pragma: no cover - thin CLI wrapper around uvicorn
    """Start the worker on 0.0.0.0:8000 (console script ``stemstudio-worker``)."""
    import uvicorn  # noqa: PLC0415 - imported lazily so tests do not need a server

    uvicorn.run(create_app(), host="0.0.0.0", port=8000)  # noqa: S104 - container-internal port
