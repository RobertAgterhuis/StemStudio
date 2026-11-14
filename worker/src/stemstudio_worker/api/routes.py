"""Routes of the worker HTTP API v1 (``contracts/worker-api.openapi.yaml``)."""

from typing import get_args

from fastapi import APIRouter, Request
from fastapi.responses import FileResponse, JSONResponse
from pydantic import BaseModel, ValidationError
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.api.uploads import create_from_upload
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.ports import SourceInfo, SourceUnavailableError, WorkerError
from stemstudio_worker.schemas import (
    CreateJobRequest,
    Health,
    Job,
    JobResult,
    JobState,
    StemName,
)

router = APIRouter()
_STEM_NAMES: tuple[StemName, ...] = get_args(StemName)


@router.get("/health")
def get_health(request: Request) -> JSONResponse:
    """Liveness probe with the configured separator and device."""
    settings: WorkerSettings = request.app.state.settings
    return _json(Health(separator=settings.separator, device=settings.device))


@router.post("/v1/jobs")
async def create_job(request: Request, runtime: Runtime) -> JSONResponse:
    """Create a job from a JSON URL request or a multipart upload (200 if it already exists)."""
    media_type = request.headers.get("content-type", "").split(";")[0].strip().lower()
    if media_type == "application/json":
        body = _parse_create_request(await request.body())
        job, created = await run_in_threadpool(_create_from_url, runtime, body)
    elif media_type == "multipart/form-data":
        job, created = await create_from_upload(request, runtime)
    else:
        detail = "Send JSON for URLs or multipart/form-data with a part 'file' for uploads."
        raise ProblemError(415, "Unsupported media type", detail)
    if not created:
        return _json(job)
    return _json(job, status=202, headers={"Location": f"/v1/jobs/{job.job_id}"})


@router.get("/v1/jobs/{job_id}")
def get_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Status, stage and progress of a job."""
    return _json(_record(runtime, job_id).job)


@router.get("/v1/jobs/{job_id}/result")
def get_job_result(job_id: str, runtime: Runtime) -> JSONResponse:
    """Result of a completed job."""
    return _json(_completed(_record(runtime, job_id)))


@router.get("/v1/jobs/{job_id}/stems/{stem}")
def get_job_stem(job_id: str, stem: str, runtime: Runtime) -> FileResponse:
    """One stem of a completed job as FLAC."""
    record = _record(runtime, job_id)
    if stem not in _STEM_NAMES:
        raise _missing_stem(stem)
    result = _completed(record)
    path = runtime.paths.stem_file(result.audio_sha256, stem)
    if stem not in {s.name for s in result.stems} or not path.is_file():
        raise _missing_stem(stem)
    return FileResponse(path, media_type="audio/flac")


def _parse_create_request(raw: bytes) -> CreateJobRequest:
    try:
        return CreateJobRequest.model_validate_json(raw)
    except ValidationError as exc:
        error = exc.errors()[0]
        location = ".".join(str(part) for part in error["loc"])
        detail = f"{location}: {error['msg']}" if location else str(error["msg"])
        raise ProblemError(400, "Invalid request", detail) from exc


def _create_from_url(runtime: WorkerRuntime, body: CreateJobRequest) -> tuple[Job, bool]:
    return runtime.service.create(_resolve(runtime, body))


def _resolve(runtime: WorkerRuntime, body: CreateJobRequest) -> SourceInfo:
    try:
        source = runtime.adapters.ingest.resolver.resolve(body.url)
    except WorkerError as exc:
        title = (
            "Source unavailable" if isinstance(exc, SourceUnavailableError) else "Invalid source"
        )
        raise ProblemError(400, title, str(exc)) from exc
    if source.source_type != body.source_type:
        detail = f"The URL is not a {body.source_type.value} URL."
        raise ProblemError(400, "Invalid source", detail)
    return source


def _record(runtime: WorkerRuntime, job_id: str) -> StoredJob:
    record = runtime.store.get(job_id)
    if record is None:
        raise ProblemError(404, "Not found", f"Job {job_id[:64]} does not exist.")
    return record


def _completed(record: StoredJob) -> JobResult:
    if record.job.status is not JobState.COMPLETED or record.result is None:
        detail = f"Job is in state {record.job.status.value}."
        raise ProblemError(409, "Job not completed", detail)
    return record.result


def _missing_stem(stem: str) -> ProblemError:
    return ProblemError(404, "Not found", f"Stem {stem[:64]} does not exist for this job.")


def _json(
    model: BaseModel, status: int = 200, headers: dict[str, str] | None = None
) -> JSONResponse:
    body = model.model_dump(mode="json", by_alias=True)
    return JSONResponse(body, status_code=status, headers=headers)
