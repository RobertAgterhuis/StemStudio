"""ASGI entry point (placeholder: only ``/health``; the stem-separation agent adds the job routes).

Run with ``uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000``.
"""

from fastapi import FastAPI

from stemstudio_worker.config import WorkerSettings, load_settings
from stemstudio_worker.schemas import CONTRACT_VERSION, Health


def create_app(settings: WorkerSettings | None = None) -> FastAPI:
    """Create the FastAPI application.

    Args:
        settings: Settings to use; read from the environment when omitted.

    Returns:
        The configured application.
    """
    resolved = settings or load_settings()
    app = FastAPI(title="StemStudio Worker API", version=CONTRACT_VERSION)

    @app.get("/health", response_model=Health, response_model_by_alias=True)
    def health() -> Health:
        """Liveness probe."""
        return Health(separator=resolved.separator, device=resolved.device)

    return app


def run() -> None:  # pragma: no cover - thin CLI wrapper around uvicorn
    """Start the worker on 0.0.0.0:8000 (console script ``stemstudio-worker``)."""
    import uvicorn  # noqa: PLC0415 - imported lazily so tests do not need a server

    uvicorn.run(create_app(), host="0.0.0.0", port=8000)  # noqa: S104 - container-internal port
