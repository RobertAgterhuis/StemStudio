"""Transcription job routes of the worker API (1.6.0, feature 008; ADR-017)."""

from typing import get_args

from fastapi import APIRouter
from fastapi.concurrency import run_in_threadpool
from starlette.responses import FileResponse, JSONResponse, Response

from stemstudio_worker.api import problems
from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.render_routes import INPUT_NOT_FOUND, INVALID_INPUT_PATH
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.jobs.transcription_service import TranscriptionService
from stemstudio_worker.ports import InputNotFoundError, InputPathError
from stemstudio_worker.schemas import (
    SCORE_FILES,
    RenderJobStatus,
    ScoreFileRole,
    TranscriptionJob,
    TranscriptionJobRequest,
)

router = APIRouter(prefix="/v1/transcription-jobs")

NOT_COMPLETED = problems.ProblemKind(
    "transcription-not-completed", 409, "Transcription not completed"
)
_ROLES: tuple[ScoreFileRole, ...] = get_args(ScoreFileRole)


@router.post("")
async def create_transcription_job(body: TranscriptionJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the input paths and queue the job on the ``transcribe`` lane (202)."""
    job = await run_in_threadpool(_create, runtime.lanes.transcription, body)
    location = f"/v1/transcription-jobs/{job.transcription_job_id}"
    return json_response(job, status=202, headers={"Location": location})


@router.get("/{job_id}")
def get_transcription_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Status, stage, progress, device and result of a transcription job."""
    return json_response(_job(runtime.lanes.transcription, job_id))


@router.delete("/{job_id}", status_code=204)
def delete_transcription_job(job_id: str, runtime: Runtime) -> Response:
    """Cancel (when queued or running) and remove a job and its files; always 204."""
    runtime.lanes.transcription.delete(job_id)
    return Response(status_code=204)


@router.get("/{job_id}/files/{role}")
def get_transcription_file(job_id: str, role: str, runtime: Runtime) -> FileResponse:
    """One file of a completed job (409 before completion, 404 for unknown roles)."""
    service = runtime.lanes.transcription
    job = _job(service, job_id)
    if role not in _ROLES:
        raise problems.ProblemError(404, "Not found", f"No file {role[:32]} for a score.")
    if job.status is not RenderJobStatus.COMPLETED:
        detail = f"Transcription job is in state {job.status.value}."
        raise problems.TypedProblemError(NOT_COMPLETED, detail)
    known = _ROLES[_ROLES.index(role)]
    path = service.file(job_id, known)
    if path is None:
        raise problems.ProblemError(404, "Not found", "The score file no longer exists.")
    return FileResponse(path, media_type=SCORE_FILES[known][1])


def _create(service: TranscriptionService, body: TranscriptionJobRequest) -> TranscriptionJob:
    try:
        return service.create(body)
    except InputPathError as exc:
        raise problems.TypedProblemError(INVALID_INPUT_PATH, str(exc)) from exc
    except InputNotFoundError as exc:
        raise problems.TypedProblemError(INPUT_NOT_FOUND, str(exc)) from exc


def _job(service: TranscriptionService, job_id: str) -> TranscriptionJob:
    job = service.get(job_id)
    if job is None:
        raise problems.ProblemError(
            404, "Not found", f"Transcription job {job_id[:64]} does not exist."
        )
    return job
