"""Routes of ``generator-api.openapi.yaml``: health, generations, cancel, take audio, delete."""

import re
from pathlib import Path
from typing import Final

from fastapi import APIRouter, Request
from fastapi.responses import FileResponse, JSONResponse, Response

from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.api.intake import INTAKES
from stemstudio_generator.api.problems import (
    generation_not_found,
    gpu_required,
    kind_not_served,
    not_found,
)
from stemstudio_generator.api.uploads import GenerationUpload, read_generation_upload
from stemstudio_generator.jobs.progress import QUEUED_STAGE
from stemstudio_generator.schemas import (
    CreateGenerationRequest,
    GeneratedTake,
    Generation,
    GeneratorHealth,
    ModelStatus,
)

_ID: Final = re.compile(r"^[0-9a-f]{32}$")
_INDEX: Final = re.compile(r"^[12]$")
FLAC_MEDIA_TYPE: Final = "audio/flac"

router = APIRouter()


def context_of(request: Request) -> ApiContext:
    """The instance's services."""
    context: ApiContext = request.app.state.context
    return context


@router.get("/health")
def get_health(request: Request) -> JSONResponse:
    """Liveness, readiness (usable GPU), models and queue."""
    context = context_of(request)
    device, settings = context.device, context.settings
    ready = device.kind == "gpu"
    health = GeneratorHealth(
        engine=settings.engine, kinds=list(settings.kinds),
        voice_sample_check=settings.voice_sample_check, ready=ready,
        reason=None if ready else device.reason, device=device,
        models=_unique(context.models()), queue=context.scheduler.queue_status(),
    )  # fmt: skip
    return JSONResponse(health.model_dump(mode="json", by_alias=True))


@router.post("/v1/generations")
async def create_generation(request: Request) -> JSONResponse:
    """Queue a generation job (``202`` with ``Location``)."""
    context = context_of(request)
    upload = await read_generation_upload(request)
    try:
        job = await _accept(context, upload)
    finally:
        if upload.form is not None:
            await upload.form.close()
    location = f"/v1/generations/{job.generation_id}"
    return JSONResponse(_wire(job), status_code=202, headers={"Location": location})


@router.get("/v1/generations/{generation_id}")
def get_generation(request: Request, generation_id: str) -> JSONResponse:
    """Status, stage, progress and takes."""
    return JSONResponse(_wire(_job(context_of(request), generation_id)))


@router.delete("/v1/generations/{generation_id}")
def delete_generation(request: Request, generation_id: str) -> Response:
    """Delete the job and its files (a running job is cancelled first)."""
    if not _ID.match(generation_id) or not context_of(request).scheduler.delete(generation_id):
        raise generation_not_found(generation_id)
    return Response(status_code=204)


@router.post("/v1/generations/{generation_id}/cancel")
def cancel_generation(request: Request, generation_id: str) -> JSONResponse:
    """Cancel (no-op for a terminal job)."""
    job = context_of(request).scheduler.cancel(generation_id) if _ID.match(generation_id) else None
    if job is None:
        raise generation_not_found(generation_id)
    return JSONResponse(_wire(job))


@router.get("/v1/generations/{generation_id}/takes/{index}")
def get_take_audio(request: Request, generation_id: str, index: str) -> FileResponse:
    """One take (FLAC; ``ETag`` = SHA-256; single byte ranges)."""
    context = context_of(request)
    take = _take(context, generation_id, index)
    return _flac(context.files.take(generation_id, take.index), take.sha256)


def _flac(path: Path, sha256: str) -> FileResponse:
    return FileResponse(path, media_type=FLAC_MEDIA_TYPE, headers={"etag": f'"{sha256}"'})


def _take(context: ApiContext, generation_id: str, index: str) -> GeneratedTake:
    job = _job(context, generation_id)
    number = int(index) if _INDEX.match(index) else 0
    if job.status != "completed" or number > len(job.takes) or number < 1:
        raise not_found(f"Take {index} of generation {generation_id} does not exist.")
    return job.takes[number - 1]


async def _accept(context: ApiContext, upload: GenerationUpload) -> Generation:
    request = upload.request
    intake = INTAKES.get(request.kind)
    if request.kind not in context.settings.kinds or intake is None:
        raise kind_not_served(request.kind, context.settings.kinds)
    if context.device.kind != "gpu":
        raise gpu_required(context.device.reason)
    job = _new_job(context, request)
    try:
        await intake(context, upload, job.generation_id)
    except BaseException:
        context.files.remove(job.generation_id)
        raise
    return context.scheduler.submit(job)


def _new_job(context: ApiContext, request: CreateGenerationRequest) -> Generation:
    now = context.clock.now()
    member = {"voiceConversion": "voice_conversion"}.get(request.kind, request.kind)
    return Generation.model_validate({
        "generation_id": context.new_id(), "kind": request.kind, "status": "queued",
        "progress": 0, "stage": QUEUED_STAGE, member: getattr(request, member),
        "created_at": now, "updated_at": now,
    })  # fmt: skip


def _job(context: ApiContext, generation_id: str) -> Generation:
    job = context.scheduler.get(generation_id) if _ID.match(generation_id) else None
    if job is None:
        raise generation_not_found(generation_id)
    return job


def _wire(job: Generation) -> object:
    return job.model_dump(mode="json", by_alias=True)


def _unique(models: list[ModelStatus]) -> list[ModelStatus]:
    return list({model.id: model for model in models}.values())
