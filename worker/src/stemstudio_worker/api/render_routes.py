"""Render job routes of the worker API (1.4.0, feature 007; ADR-016 D5/D6)."""

from fastapi import APIRouter, Response
from fastapi.responses import FileResponse, JSONResponse
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import (
    INPUT_NOT_FOUND,
    INVALID_INPUT_PATH,
    ProblemError,
    ProblemKind,
    TypedProblemError,
    path_guarded,
)
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.jobs.render_service import RenderService
from stemstudio_worker.schemas import RenderJob, RenderJobRequest, RenderJobStatus

router = APIRouter(prefix="/v1/render-jobs")

__all__ = ["INPUT_NOT_FOUND", "INVALID_INPUT_PATH", "RENDER_NOT_COMPLETED", "router"]
RENDER_NOT_COMPLETED = ProblemKind("render-not-completed", 409, "Render not completed")


@router.post("")
async def create_render_job(body: RenderJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the input paths and queue the job on the render lane (202)."""
    job = await run_in_threadpool(_create, runtime.lanes.render, body)
    location = f"/v1/render-jobs/{job.render_job_id}"
    return json_response(job, status=202, headers={"Location": location})


@router.get("/{job_id}")
def get_render_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Status, progress and result of a render job."""
    return json_response(_job(runtime.lanes.render, job_id))


@router.delete("/{job_id}", status_code=204)
def delete_render_job(job_id: str, runtime: Runtime) -> Response:
    """Cancel (when queued or running) and remove a job and its file; always 204."""
    runtime.lanes.render.delete(job_id)
    return Response(status_code=204)


@router.get("/{job_id}/file")
def get_render_job_file(job_id: str, runtime: Runtime) -> FileResponse:
    """The rendered file of a completed job (409 before)."""
    service = runtime.lanes.render
    job = _job(service, job_id)
    if job.status is not RenderJobStatus.COMPLETED or job.result is None:
        detail = f"Render job is in state {job.status.value}."
        raise TypedProblemError(RENDER_NOT_COMPLETED, detail)
    path = service.output(job_id, job.result)
    if path is None:
        raise ProblemError(404, "Not found", "The rendered file no longer exists.")
    return FileResponse(path, media_type=job.result.content_type)


def _create(service: RenderService, body: RenderJobRequest) -> RenderJob:
    return path_guarded(lambda: service.create(body))


def _job(service: RenderService, job_id: str) -> RenderJob:
    job = service.get(job_id)
    if job is None:
        raise ProblemError(404, "Not found", f"Render job {job_id[:64]} does not exist.")
    return job
