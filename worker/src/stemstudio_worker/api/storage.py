"""Storage routes of worker API 1.7.0 (feature 013): report, clean-up and rehydration."""

import json
from collections.abc import Mapping
from typing import Annotated, Any, Final

from fastapi import APIRouter, Depends, Request
from fastapi.responses import JSONResponse
from pydantic import ValidationError
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.problems import PROBLEM_JSON, ProblemError, validation_errors
from stemstudio_worker.api.responses import json_response, parse_body
from stemstudio_worker.ports import LibraryUnavailableError, StemMissingError
from stemstudio_worker.schemas import (
    LIBRARY_UNAVAILABLE_TEXT,
    ProblemDetails,
    RehydratedJob,
    RehydrateRequest,
    StorageCleanupRequest,
)
from stemstudio_worker.storage.rehydrate import InvalidStemError, Rehydrator, StemSetError
from stemstudio_worker.storage.services import StorageServices

router = APIRouter()

PROBLEMS: Final = "https://stemstudio.local/problems/"
STEM_MISSING: Final = f"{PROBLEMS}stem-missing"
LIBRARY_UNAVAILABLE: Final = f"{PROBLEMS}library-unavailable"


def get_storage(request: Request) -> StorageServices:
    """The storage services built by the application lifespan."""
    services: StorageServices = request.app.state.storage
    return services


Storage = Annotated[StorageServices, Depends(get_storage)]
"""Route parameter type that injects the :class:`StorageServices`."""


@router.get("/v1/storage")
async def get_worker_storage(storage: Storage) -> JSONResponse:
    """Disk use of the worker data and model volumes (cached for 30 seconds)."""
    return json_response(await run_in_threadpool(storage.meter.measure))


@router.post("/v1/storage/cleanup")
async def clean_worker_storage(request: Request, storage: Storage) -> JSONResponse:
    """Remove cache entries, scratch and old job files that no job uses (400 on a bad body)."""
    body = parse_body(StorageCleanupRequest, await request.body())
    return json_response(await run_in_threadpool(storage.janitor.clean, body))


@router.post("/v1/jobs/rehydrate")
async def rehydrate_job(request: Request, storage: Storage) -> JSONResponse:
    """Re-create a completed standard job from library stems (201 new, 200 existing)."""
    body = _rehydrate_body(await request.body())
    if isinstance(body, JSONResponse):
        return body
    return await run_in_threadpool(_rehydrate, storage.rehydrator, body)


def _rehydrate(rehydrator: Rehydrator, body: RehydrateRequest) -> JSONResponse:
    """Run the rehydration; its errors become the 422, 404 and 409 problems of the contract."""
    try:
        job, created = rehydrator.rehydrate(body)
    except (StemSetError, InvalidStemError) as exc:
        return _invalid(str(exc), {"stems": [str(exc)]})
    except StemMissingError as exc:
        return _missing(body, exc)
    except LibraryUnavailableError:
        extensions = {"type": LIBRARY_UNAVAILABLE}
        return _problem(409, "Library unavailable", LIBRARY_UNAVAILABLE_TEXT, extensions)
    return _created(job) if created else json_response(job)


def _rehydrate_body(raw: bytes) -> RehydrateRequest | JSONResponse:
    """Malformed JSON is a 400; a body that does not match the schema a 422 with ``errors``."""
    try:
        document = json.loads(raw)
    except ValueError as exc:
        raise ProblemError(400, "Invalid request", "The request body is not JSON.") from exc
    try:
        return RehydrateRequest.model_validate(document)
    except ValidationError as exc:
        located = [{**e, "loc": ("body", *e["loc"])} for e in exc.errors()]
        return _invalid("The request body is not valid.", validation_errors(located))


def _created(job: RehydratedJob) -> JSONResponse:
    return json_response(job, status=201, headers={"Location": f"/v1/jobs/{job.job_id}"})


def _invalid(detail: str, errors: dict[str, list[str]]) -> JSONResponse:
    return _problem(422, "Validation failed", detail, {"errors": errors})


def _missing(body: RehydrateRequest, error: StemMissingError) -> JSONResponse:
    detail = f"tracks/{body.track_id}/stems/{error.stem}.flac does not exist."
    extensions = {"type": STEM_MISSING, "stem": error.stem}
    return _problem(404, "Stem missing in the library", detail, extensions)


def _problem(status: int, title: str, detail: str, extensions: Mapping[str, Any]) -> JSONResponse:
    """A problem+json response; ``extensions`` may set ``type`` and extension members."""
    document = {"type": "about:blank", "title": title, "status": status, "detail": detail}
    body = ProblemDetails.model_validate({**document, **extensions})
    return JSONResponse(body.model_dump(), status_code=status, media_type=PROBLEM_JSON)
