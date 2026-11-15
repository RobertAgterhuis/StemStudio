"""Metadata job routes of the worker API (1.3.0, feature 005; ADR-014 D3)."""

from fastapi import APIRouter
from fastapi.responses import FileResponse, JSONResponse

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.api.responses import artwork_response, json_response
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.schemas import (
    CreateMetadataJobRequest,
    MetadataJob,
    MetadataJobState,
    MetadataResult,
)

router = APIRouter(prefix="/v1/metadata-jobs")


@router.post("")
def create_metadata_job(body: CreateMetadataJobRequest, runtime: Runtime) -> JSONResponse:
    """Queue a lookup on the metadata lane; an invalid body is a 422 problem."""
    job = runtime.lanes.metadata.create(body)
    location = f"/v1/metadata-jobs/{job.metadata_job_id}"
    return json_response(job, status=202, headers={"Location": location})


@router.get("/{job_id}")
def get_metadata_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Status and stage of a metadata job."""
    return json_response(_job(runtime, job_id))


@router.get("/{job_id}/result")
def get_metadata_result(job_id: str, runtime: Runtime) -> JSONResponse:
    """Result of a completed metadata job (409 before)."""
    return json_response(_result(runtime, job_id))


@router.get("/{job_id}/artwork/{variant}")
def get_metadata_artwork(job_id: str, variant: str, runtime: Runtime) -> FileResponse:
    """The Cover Art Archive picture of a completed job, as JPEG."""
    artwork = _result(runtime, job_id).artwork
    if artwork is None:
        raise ProblemError(404, "Not found", "This metadata job found no album cover.")
    directory = runtime.paths.metadata_artwork(job_id)
    return artwork_response(directory, variant, artwork.sha256)


def _job(runtime: WorkerRuntime, job_id: str) -> MetadataJob:
    job = runtime.lanes.metadata.get(job_id)
    if job is None:
        raise ProblemError(404, "Not found", f"Metadata job {job_id[:64]} does not exist.")
    return job


def _result(runtime: WorkerRuntime, job_id: str) -> MetadataResult:
    job = _job(runtime, job_id)
    result = runtime.lanes.metadata.result(job_id)
    if job.status is not MetadataJobState.COMPLETED or result is None:
        detail = f"Metadata job is in state {job.status.value}."
        raise ProblemError(409, "Job not completed", detail)
    return result
