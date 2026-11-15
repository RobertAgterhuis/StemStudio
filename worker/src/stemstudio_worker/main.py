"""Composition root and ASGI entry point of the worker.

Run with ``uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000``.
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
from stemstudio_worker.devices import create_device_runner
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.ports import PipelineAdapters
from stemstudio_worker.schemas import CONTRACT_VERSION
from stemstudio_worker.separation import create_separator

Lifespan = Callable[[FastAPI], AbstractAsyncContextManager[None]]


def create_app(
    settings: WorkerSettings | None = None,
    adapters: PipelineAdapters | None = None,
    detail_pipeline: DetailPipelinePort | None = None,
) -> FastAPI:
    """Create the FastAPI application.

    The job subsystem starts in the lifespan: jobs and detail jobs are loaded, interrupted ones
    are failed ("interrupted by restart") and the scheduler thread starts; it stops on shutdown.

    Args:
        settings: Settings to use; read from the environment when omitted.
        adapters: Pipeline adapters; built with the adapter factories when omitted.
        detail_pipeline: Detail step pipeline; built with ``create_detail_pipeline`` when omitted.

    Returns:
        The configured application.
    """
    resolved = settings or load_settings()
    app = FastAPI(
        title="StemStudio Worker API",
        version=CONTRACT_VERSION,
        lifespan=_lifespan(resolved, adapters, detail_pipeline),
    )
    app.state.settings = resolved
    install_problem_handlers(app)
    app.include_router(router)
    app.include_router(detail_routes.router)
    return app


def default_adapters(settings: WorkerSettings) -> PipelineAdapters:
    """Build the production adapters from the ingest, separation and analysis factories."""
    return PipelineAdapters(
        ingest=create_ingest_adapters(settings),
        separator=create_separator(settings, create_device_runner(settings)),
        analyzer=create_beat_analyzer(settings),
    )


def _lifespan(
    settings: WorkerSettings,
    adapters: PipelineAdapters | None,
    detail_pipeline: DetailPipelinePort | None,
) -> Lifespan:
    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        runtime = WorkerRuntime.start(
            settings,
            adapters or default_adapters(settings),
            detail_pipeline or create_detail_pipeline(settings),
        )
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
