"""Routes ``/v1/detail-jobs/**`` of the worker API 1.1.0 (detail separation, feature 002)."""

from collections.abc import Callable
from pathlib import Path
from typing import Final, get_args

from fastapi import APIRouter, Request
from fastapi.responses import FileResponse, JSONResponse
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.api.responses import json_response, parse_body
from stemstudio_worker.jobs.detail_service import DetailConflictError, UnknownJobError
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.schemas import (
    CreateDetailJobRequest,
    DetailJob,
    DetailStemName,
    ParentStemName,
)

router = APIRouter()
_PARENTS: Final = frozenset(get_args(ParentStemName))
_NAMES: Final = frozenset(get_args(DetailStemName))


def _call[T](action: Callable[[], T]) -> T:
    """Run a service call; unknown jobs become 404 and state conflicts 409 problems."""
    try:
        return action()
    except UnknownJobError as exc:
        raise ProblemError(404, "Not found", str(exc)) from exc
    except DetailConflictError as exc:
        raise ProblemError(409, "Conflict", str(exc)) from exc


@router.post("/v1/detail-jobs")
async def create_detail_job(request: Request, runtime: Runtime) -> JSONResponse:
    """Create (or reuse) the detail job for the audio of a completed standard job."""
    body = parse_body(CreateDetailJobRequest, await request.body())
    job, created = await run_in_threadpool(
        _call, lambda: runtime.detail_service.create(body.standard_job_id)
    )
    if not created:
        return json_response(job)
    return json_response(job, 202, {"Location": f"/v1/detail-jobs/{job.detail_job_id}"})


@router.get("/v1/detail-jobs/{detail_job_id}")
def get_detail_job(detail_job_id: str, runtime: Runtime) -> JSONResponse:
    """Status, current parent and progress of a detail job."""
    return json_response(_call(lambda: runtime.detail_service.get(detail_job_id)).job)


@router.post("/v1/detail-jobs/{detail_job_id}/cancel")
def cancel_detail_job(detail_job_id: str, runtime: Runtime) -> JSONResponse:
    """Cancel a queued or running detail job (checkpoints are kept)."""
    job: DetailJob = _call(lambda: runtime.detail_service.cancel(detail_job_id))
    return json_response(job)


@router.get("/v1/detail-jobs/{detail_job_id}/result")
def get_detail_result(detail_job_id: str, runtime: Runtime) -> JSONResponse:
    """Per-parent outcomes and detail stems of a completed detail job."""
    return json_response(_call(lambda: runtime.detail_service.result(detail_job_id)))


@router.get("/v1/detail-jobs/{detail_job_id}/stems/{parent}/{name}")
def get_detail_stem(detail_job_id: str, parent: str, name: str, runtime: Runtime) -> FileResponse:
    """One detail stem of a completed detail job as FLAC."""
    _call(lambda: runtime.detail_service.get(detail_job_id))
    if parent not in _PARENTS or name not in _NAMES:
        raise _missing(parent, name)
    return FileResponse(_stem_path(runtime, detail_job_id, parent, name), media_type="audio/flac")


def _stem_path(runtime: WorkerRuntime, detail_job_id: str, parent: str, name: str) -> Path:
    result = _call(lambda: runtime.detail_service.result(detail_job_id))
    offered = (
        runtime.paths.detail_stem(result.audio_sha256, outcome.parent, stem.name)
        for outcome in result.parents
        for stem in outcome.stems
        if (outcome.parent, stem.name) == (parent, name)
    )
    path = next(offered, None)
    if path is None or not path.is_file():
        raise _missing(parent, name)
    return path


def _missing(parent: str, name: str) -> ProblemError:
    detail = f"Detail stem {parent[:16]}/{name[:32]} does not exist for this detail job."
    return ProblemError(404, "Not found", detail)
