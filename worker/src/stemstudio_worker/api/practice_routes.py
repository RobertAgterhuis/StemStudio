"""Routes of the ``practice`` lane (worker API 1.8.0, feature 014): sections, score views, PDFs.

Every kind has ``POST`` (202 + ``Location``; 400 ``invalid-input-path`` / 404 ``input-not-found``
from the path guard, 422 from the schema), ``GET`` (404 for an unknown job) and an idempotent
``DELETE`` (204; 400 for a malformed id). Files of a job answer 409 ``job-not-completed`` before
the job completed.
"""

import re
from typing import Any, Final

from fastapi import APIRouter, Response
from fastapi.responses import FileResponse, JSONResponse
from pydantic import BaseModel
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError, ProblemKind, TypedProblemError
from stemstudio_worker.api.render_routes import INPUT_NOT_FOUND, INVALID_INPUT_PATH
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.ports import InputNotFoundError, InputPathError
from stemstudio_worker.schemas import (
    RenderJobStatus,
    ScoreViewJobRequest,
    SectionJobRequest,
    SetlistDocumentRequest,
)

router = APIRouter(prefix="/v1")

JOB_NOT_COMPLETED: Final = ProblemKind("job-not-completed", 409, "The job has not completed")
_JOB_ID: Final = re.compile(r"[0-9a-f]{32}")
SECTIONS: Final = "/section-jobs"
SCORE_VIEWS: Final = "/score-view-jobs"
DOCUMENTS: Final = "/setlist-document-jobs"
SVG_TYPE: Final = "image/svg+xml"
PDF_TYPE: Final = "application/pdf"


@router.post(SECTIONS)
async def create_section_job(body: SectionJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the stem paths and queue a section job (202)."""
    return await _created(runtime.lanes.practice.sections, body, SECTIONS)


@router.get(SECTIONS + "/{job_id}")
def get_section_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """State, progress and, when completed, the sections."""
    return json_response(_job(runtime.lanes.practice.sections, job_id))


@router.delete(SECTIONS + "/{job_id}", status_code=204)
def delete_section_job(job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove a section job (idempotent)."""
    return _deleted(runtime.lanes.practice.sections, job_id)


@router.post(SCORE_VIEWS)
async def create_score_view_job(body: ScoreViewJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the MusicXML path and queue a score-view job (202)."""
    return await _created(runtime.lanes.practice.score_views, body, SCORE_VIEWS)


@router.get(SCORE_VIEWS + "/{job_id}")
def get_score_view_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """State, progress and, when completed, the pages and the bar map."""
    return json_response(_job(runtime.lanes.practice.score_views, job_id))


@router.delete(SCORE_VIEWS + "/{job_id}", status_code=204)
def delete_score_view_job(job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove a score-view job and its pages (idempotent)."""
    return _deleted(runtime.lanes.practice.score_views, job_id)


@router.get(SCORE_VIEWS + "/{job_id}/pages/{page}")
def get_score_view_page(job_id: str, page: int, runtime: Runtime) -> FileResponse:
    """One SVG page of a completed score-view job (409 before, 404 for an unknown page)."""
    service = runtime.lanes.practice.score_views
    _completed(service, job_id)
    path = service.page(job_id, page) if page >= 1 else None
    if path is None:
        raise ProblemError(404, "Not found", f"Page {page} does not exist.")
    return FileResponse(path, media_type=SVG_TYPE)


@router.post(DOCUMENTS)
async def create_setlist_document_job(
    body: SetlistDocumentRequest, runtime: Runtime
) -> JSONResponse:
    """Queue a setlist PDF job (202)."""
    return await _created(runtime.lanes.practice.documents, body, DOCUMENTS)


@router.get(DOCUMENTS + "/{job_id}")
def get_setlist_document_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """State, progress and, when completed, the page count."""
    return json_response(_job(runtime.lanes.practice.documents, job_id))


@router.delete(DOCUMENTS + "/{job_id}", status_code=204)
def delete_setlist_document_job(job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove a setlist job and its PDF (idempotent)."""
    return _deleted(runtime.lanes.practice.documents, job_id)


@router.get(DOCUMENTS + "/{job_id}/file")
def get_setlist_document_file(job_id: str, runtime: Runtime) -> FileResponse:
    """The PDF of a completed setlist job (409 before)."""
    service = runtime.lanes.practice.documents
    _completed(service, job_id)
    path = service.file(job_id)
    if path is None:
        raise ProblemError(404, "Not found", "The PDF no longer exists.")
    return FileResponse(path, media_type=PDF_TYPE)


async def _created(service: Any, body: BaseModel, base: str) -> JSONResponse:
    job = await run_in_threadpool(_create, service, body)
    return json_response(job, status=202, headers={"Location": f"/v1{base}/{job.job_id}"})


def _create(service: Any, body: BaseModel) -> Any:
    try:
        return service.create(body)
    except InputPathError as exc:
        raise TypedProblemError(INVALID_INPUT_PATH, str(exc)) from exc
    except InputNotFoundError as exc:
        raise TypedProblemError(INPUT_NOT_FOUND, str(exc)) from exc


def _job(service: Any, job_id: str) -> Any:
    job = service.get(job_id)
    if job is None:
        raise ProblemError(404, "Not found", f"Job {job_id[:64]} does not exist.")
    return job


def _completed(service: Any, job_id: str) -> None:
    job = _job(service, job_id)
    if job.status is not RenderJobStatus.COMPLETED:
        raise TypedProblemError(JOB_NOT_COMPLETED, f"The job is {job.status.value}.")


def _deleted(service: Any, job_id: str) -> Response:
    if not _JOB_ID.fullmatch(job_id):
        raise ProblemError(400, "Invalid job id", "A job id is 32 lower-case hex characters.")
    service.delete(job_id)
    return Response(status_code=204)
