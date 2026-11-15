"""FastAPI dependencies of the worker routes."""

from typing import Annotated

from fastapi import Depends, Request

from stemstudio_worker.jobs.runtime import WorkerRuntime


def get_runtime(request: Request) -> WorkerRuntime:
    """Return the runtime started by the application lifespan."""
    runtime: WorkerRuntime = request.app.state.runtime
    return runtime


Runtime = Annotated[WorkerRuntime, Depends(get_runtime)]
"""Route parameter type that injects the :class:`WorkerRuntime`."""
