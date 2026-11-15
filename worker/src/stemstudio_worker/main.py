"""Composition root and ASGI entry point of the worker.

Run with ``uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000``.

At start-up (lifespan) the device runner is built once (``devices.create_device_runner``: the GPU
probe of feature 003) and shared by the separator, the detail pipeline and ``/health``; when the
scheduler becomes idle the GPU models are released (ADR-012 D6).
"""

from collections.abc import AsyncIterator, Callable
from contextlib import AbstractAsyncContextManager, asynccontextmanager

from fastapi import FastAPI

from stemstudio_worker.analysis import create_beat_analyzer
from stemstudio_worker.api import detail_routes
from stemstudio_worker.api.problems import install_problem_handlers
from stemstudio_worker.api.routes import router
from stemstudio_worker.config import WorkerSettings, load_settings
from stemstudio_worker.detail import create_detail_pipeline
from stemstudio_worker.devices import ManagedRunner, create_device_runner
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.runtime import DeviceHooks, WorkerParts, WorkerRuntime
from stemstudio_worker.ports import PipelineAdapters
from stemstudio_worker.schemas import CONTRACT_VERSION
from stemstudio_worker.separation import create_separator

Lifespan = Callable[[FastAPI], AbstractAsyncContextManager[None]]


def create_app(
    settings: WorkerSettings | None = None,
    adapters: PipelineAdapters | None = None,
    detail_pipeline: DetailPipelinePort | None = None,
    devices: ManagedRunner | None = None,
) -> FastAPI:
    """Create the FastAPI application.

    The job subsystem starts in the lifespan: the device is probed, jobs and detail jobs are
    loaded, interrupted ones are failed ("interrupted by restart") and the scheduler thread
    starts; it stops on shutdown.

    Args:
        settings: Settings to use; read from the environment when omitted.
        adapters: Pipeline adapters; built with the adapter factories when omitted.
        detail_pipeline: Detail step pipeline; built with ``create_detail_pipeline`` when omitted.
        devices: Device runner; built with ``create_device_runner`` (start-up probe) when omitted.

    Returns:
        The configured application.
    """
    resolved = settings or load_settings()
    app = FastAPI(
        title="StemStudio Worker API",
        version=CONTRACT_VERSION,
        lifespan=_lifespan(resolved, _Parts(adapters, detail_pipeline, devices)),
    )
    app.state.settings = resolved
    install_problem_handlers(app)
    app.include_router(router)
    app.include_router(detail_routes.router)
    return app


def default_adapters(settings: WorkerSettings, devices: ManagedRunner) -> PipelineAdapters:
    """Build the production adapters from the ingest, separation and analysis factories."""
    return PipelineAdapters(
        ingest=create_ingest_adapters(settings),
        separator=create_separator(settings, devices),
        analyzer=create_beat_analyzer(settings),
    )


class _Parts:
    """The optional, injected parts of the app (tests); missing ones are built at start-up."""

    def __init__(
        self,
        adapters: PipelineAdapters | None,
        pipeline: DetailPipelinePort | None,
        devices: ManagedRunner | None,
    ) -> None:
        self.adapters = adapters
        self.pipeline = pipeline
        self.devices = devices

    def start(self, settings: WorkerSettings) -> WorkerRuntime:
        """Build what is missing (one device runner for everything) and start the runtime."""
        devices = self.devices or create_device_runner(settings)
        pipeline = self.pipeline or create_detail_pipeline(settings, devices)
        adapters = self.adapters or default_adapters(settings, devices)
        hooks = DeviceHooks(info=devices.info, on_idle=_idle_release(devices, pipeline))
        return WorkerRuntime.start_with(settings, WorkerParts(adapters, pipeline, hooks))


def _idle_release(devices: ManagedRunner, pipeline: DetailPipelinePort) -> Callable[[], None]:
    """Drop the pipeline's GPU models (when it has any), then return the GPU memory."""
    release_models: Callable[[], None] | None = getattr(pipeline, "release_gpu", None)

    def release() -> None:
        if release_models is not None:
            release_models()
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
