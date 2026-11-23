"""Routes of the handout jobs on the ``insights`` lane (worker API 1.11.0, feature 020).

``POST`` 202 with ``Location`` (400 / 404 from the path guard, 422 from the schema: notice texts,
song count, setlist block; also a body over 8 MiB, ``api.body_limit``, and a document over the
job limits of ``jobs.handout_limits``), ``GET`` 404 for an unknown job, ``DELETE`` 204
(idempotent), ``GET .../file`` the PDF of a completed job (409 ``handout-not-completed`` before,
404 when gone).
"""

from typing import Final

from fastapi import APIRouter, Response
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.insights_routes import created, job_or_404
from stemstudio_worker.api.problems import ProblemError, ProblemKind, TypedProblemError
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.jobs.handout_limits import HandoutLimitError
from stemstudio_worker.schemas import HandoutJobRequest, RenderJobStatus

router = APIRouter(prefix="/v1")

HANDOUT_JOBS: Final = "/handout-jobs"
HANDOUT_NOT_COMPLETED: Final = ProblemKind("handout-not-completed", 409, "Handout not completed")
PDF_TYPE: Final = "application/pdf"


@router.post(HANDOUT_JOBS)
async def create_handout_job(body: HandoutJobRequest, runtime: Runtime) -> JSONResponse:
    """Check the job limits and the notation and artwork paths and queue a handout job (202)."""
    try:
        return await created(runtime.lanes.insights.handouts, body, HANDOUT_JOBS, "handout_job_id")
    except HandoutLimitError as exc:
        raise RequestValidationError(exc.errors()) from exc


@router.get(HANDOUT_JOBS + "/{handout_job_id}")
def get_handout_job(handout_job_id: str, runtime: Runtime) -> JSONResponse:
    """State, stage, progress and, when completed, the PDF facts."""
    return json_response(job_or_404(runtime.lanes.insights.handouts, handout_job_id))


@router.delete(HANDOUT_JOBS + "/{handout_job_id}", status_code=204)
def delete_handout_job(handout_job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove a handout job and its PDF (idempotent)."""
    runtime.lanes.insights.handouts.delete(handout_job_id)
    return Response(status_code=204)


@router.get(HANDOUT_JOBS + "/{handout_job_id}/file")
def get_handout_file(handout_job_id: str, runtime: Runtime) -> FileResponse:
    """The PDF of a completed handout job."""
    service = runtime.lanes.insights.handouts
    job = job_or_404(service, handout_job_id)
    if job.status is not RenderJobStatus.COMPLETED:
        raise TypedProblemError(HANDOUT_NOT_COMPLETED, f"Job is in state {job.status.value}.")
    path = service.file(handout_job_id)
    if path is None:
        raise ProblemError(404, "Not found", "The PDF no longer exists.")
    return FileResponse(path, media_type=PDF_TYPE)
