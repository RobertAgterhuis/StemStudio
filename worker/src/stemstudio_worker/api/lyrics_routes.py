"""Lyrics job routes of the worker API (1.5.0, feature 006; ADR-015 D2/D3/D6).

Results are copyrighted lyrics: served with ``Cache-Control: no-store`` and never logged.
"""

from collections.abc import Callable
from typing import Annotated

from fastapi import APIRouter, Body, Response
from fastapi.concurrency import run_in_threadpool
from fastapi.responses import JSONResponse

from stemstudio_worker.api import problems
from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.jobs.detail_service import UnknownJobError
from stemstudio_worker.jobs.lyrics_lane import LyricsConflictError, LyricsService
from stemstudio_worker.schemas import CreateLyricsJobRequest

router = APIRouter(prefix="/v1/lyrics-jobs")

_TITLES = {
    "standard-job-not-completed": "Standard job not completed",
    "vocal-stem-missing": "Vocal stem missing",
    "lyrics-job-not-cancellable": "Lyrics job cannot be cancelled",
    "lyrics-not-completed": "Lyrics job not completed",
}
_NO_STORE = {"Cache-Control": "no-store"}


def _call[T](action: Callable[[], T]) -> T:
    """Run a service call; unknown jobs become 404 and conflicts typed 409 problems."""
    try:
        return action()
    except UnknownJobError as exc:
        raise problems.ProblemError(404, "Not found", str(exc)) from exc
    except LyricsConflictError as exc:
        kind = problems.ProblemKind(exc.kind, 409, _TITLES.get(exc.kind, "Conflict"))
        raise problems.TypedProblemError(kind, str(exc)) from exc


def _service(runtime: Runtime) -> LyricsService:
    return runtime.lanes.lyrics


@router.post("")
async def create_lyrics_job(
    body: Annotated[CreateLyricsJobRequest, Body()], runtime: Runtime
) -> JSONResponse:
    """Queue a lookup (network lane) or an alignment (scheduler unit) (202)."""
    service = _service(runtime)
    job = await run_in_threadpool(_call, lambda: service.create(body))
    location = f"/v1/lyrics-jobs/{job.lyrics_job_id}"
    return json_response(job, status=202, headers={"Location": location})


@router.get("/{job_id}")
def get_lyrics_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Status, stage, progress, device and estimate of a lyrics job."""
    return json_response(_call(lambda: _service(runtime).get(job_id)))


@router.delete("/{job_id}", status_code=204)
def delete_lyrics_job(job_id: str, runtime: Runtime) -> Response:
    """Remove a job and its result (a running alignment stops); 404 when unknown."""
    _call(lambda: _service(runtime).delete(job_id))
    return Response(status_code=204)


@router.get("/{job_id}/result")
def get_lyrics_result(job_id: str, runtime: Runtime) -> JSONResponse:
    """Outcome and document of a completed job, never cached (409 before completion)."""
    result = _call(lambda: _service(runtime).result(job_id))
    return json_response(result, headers=_NO_STORE)


@router.post("/{job_id}/cancel")
def cancel_lyrics_job(job_id: str, runtime: Runtime) -> JSONResponse:
    """Cancel a queued or running alignment (409 for lookups and finished jobs)."""
    return json_response(_call(lambda: _service(runtime).cancel(job_id)))
