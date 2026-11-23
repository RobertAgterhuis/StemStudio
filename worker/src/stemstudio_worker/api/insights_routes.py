"""Routes of the quality jobs on the ``insights`` lane (worker API 1.11.0, feature 020).

``POST`` answers 202 with ``Location`` (400 ``invalid-input-path`` / 404 ``input-not-found`` from
the path guard, 422 from the schema), ``GET`` 404 for an unknown job, ``DELETE`` 204 always
(idempotent; an unknown or malformed id removes nothing). The helpers are shared with the handout
routes.
"""

from typing import Any, Final

from fastapi import APIRouter, Response
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError, path_guarded
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.schemas import QualityJobRequest

router = APIRouter(prefix="/v1")

QUALITY_JOBS: Final = "/quality-jobs"


@router.post(QUALITY_JOBS)
async def create_quality_job(body: QualityJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the stem paths and queue a quality job (202)."""
    return await created(runtime.lanes.insights.quality, body, QUALITY_JOBS, "quality_job_id")


@router.get(QUALITY_JOBS + "/{quality_job_id}")
def get_quality_job(quality_job_id: str, runtime: Runtime) -> JSONResponse:
    """State, stage, progress and, when completed, the estimate of every stem."""
    return json_response(job_or_404(runtime.lanes.insights.quality, quality_job_id))


@router.delete(QUALITY_JOBS + "/{quality_job_id}", status_code=204)
def delete_quality_job(quality_job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove a quality job (idempotent)."""
    runtime.lanes.insights.quality.delete(quality_job_id)
    return Response(status_code=204)


async def created(service: Any, body: BaseModel, base: str, id_field: str) -> JSONResponse:
    """Create the job of ``body`` (path guard problems as 400 / 404); 202 with ``Location``."""
    job = await run_in_threadpool(path_guarded, lambda: service.create(body))
    location = f"/v1{base}/{getattr(job, id_field)}"
    return json_response(job, status=202, headers={"Location": location})


def job_or_404(service: Any, job_id: str) -> Any:
    """The wire job, or a 404 problem."""
    job = service.get(job_id)
    if job is None:
        raise ProblemError(404, "Not found", f"Job {job_id[:64]} does not exist.")
    return job
