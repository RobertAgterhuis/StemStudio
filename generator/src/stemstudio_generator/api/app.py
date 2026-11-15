"""The FastAPI application of one generator or singer instance."""

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI

from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.api.problems import install_problem_handlers
from stemstudio_generator.api.routes import router
from stemstudio_generator.schemas import CONTRACT_VERSION


def create_app(context: ApiContext) -> FastAPI:
    """The app over ``context``; its lifespan runs ``on_start``/``on_stop``."""

    @asynccontextmanager
    async def lifespan(_app: FastAPI) -> AsyncIterator[None]:
        context.on_start()
        try:
            yield
        finally:
            context.on_stop()

    app = FastAPI(
        title="StemStudio Generator API", version=CONTRACT_VERSION, lifespan=lifespan,
        docs_url=None, redoc_url=None, openapi_url=None,
    )  # fmt: skip
    app.state.context = context
    install_problem_handlers(app)
    app.include_router(router)
    return app
