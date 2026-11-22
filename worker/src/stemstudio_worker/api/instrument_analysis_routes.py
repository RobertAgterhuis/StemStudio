"""Routes of the ``instrument-analysis`` lane (worker API 1.9.0, feature 016, DELTA 1).

``POST /v1/instrument-analysis-jobs`` (202 + ``Location``; 400 ``invalid-input-path`` / 404
``input-not-found`` from the path guard, 422 from the schema), ``GET`` (404 for an unknown job) and
an idempotent ``DELETE`` (204; 400 for a malformed id). The body is one of the three request kinds,
selected by ``kind``.
"""

import re
from typing import Annotated, Final

from fastapi import APIRouter, Body, Response
from fastapi.responses import JSONResponse
from starlette.concurrency import run_in_threadpool

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.problems import ProblemError, path_guarded
from stemstudio_worker.api.responses import json_response
from stemstudio_worker.jobs.instrument_analysis_service import InstrumentAnalysisService
from stemstudio_worker.schemas import AnyAnalysisRequest, InstrumentAnalysisJob

router = APIRouter(prefix="/v1")

JOBS: Final = "/instrument-analysis-jobs"
_JOB_ID: Final = re.compile(r"[0-9a-f]{32}")

AnalysisBody = Annotated[AnyAnalysisRequest, Body(discriminator="kind")]
"""The request body, validated by its ``kind``."""


@router.post(JOBS)
async def create_instrument_analysis_job(body: AnalysisBody, runtime: Runtime) -> JSONResponse:
    """Check the input paths and queue the analysis (202)."""
    service = runtime.lanes.instrument_analysis
    job = await run_in_threadpool(_create, service, body)
    return json_response(job, status=202, headers={"Location": f"/v1{JOBS}/{job.analysis_job_id}"})


@router.get(JOBS + "/{analysis_job_id}")
def get_instrument_analysis_job(analysis_job_id: str, runtime: Runtime) -> JSONResponse:
    """State, stage, progress and, when completed, the result."""
    job = runtime.lanes.instrument_analysis.get(analysis_job_id)
    if job is None:
        raise ProblemError(404, "Not found", f"Job {analysis_job_id[:64]} does not exist.")
    return json_response(job)


@router.delete(JOBS + "/{analysis_job_id}", status_code=204)
def delete_instrument_analysis_job(analysis_job_id: str, runtime: Runtime) -> Response:
    """Cancel and remove an analysis job (idempotent)."""
    if not _JOB_ID.fullmatch(analysis_job_id):
        raise ProblemError(400, "Invalid job id", "A job id is 32 lower-case hex characters.")
    runtime.lanes.instrument_analysis.delete(analysis_job_id)
    return Response(status_code=204)


def _create(service: InstrumentAnalysisService, body: AnyAnalysisRequest) -> InstrumentAnalysisJob:
    return path_guarded(lambda: service.create(body))
