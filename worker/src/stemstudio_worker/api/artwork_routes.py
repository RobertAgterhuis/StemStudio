"""User picture routes: ``POST /v1/artwork`` and ``GET /v1/artwork/{id}/{variant}`` (005, FR-013).

The picture is only re-encoded (nothing is attached to a job) and kept for one hour.
"""

from fastapi import APIRouter, Request
from fastapi.responses import FileResponse, JSONResponse
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile

from stemstudio_worker.api.dependencies import Runtime
from stemstudio_worker.api.multipart import INVALID, parse_limited_form
from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.api.responses import artwork_response, json_response
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ArtworkError,
    ImageTooLargeError,
    NotAnImageError,
    UnsupportedImageTypeError,
)
from stemstudio_worker.schemas import ArtworkUpload

router = APIRouter(prefix="/v1/artwork")

MAX_PICTURE_BYTES = 10 * 1024 * 1024
"""Largest accepted user picture (10 MiB = 10,485,760 bytes)."""

_TOO_LARGE = ProblemError(413, "Picture too large", "The picture is larger than 10 MiB.")
_NO_PART = "Send the picture as multipart part 'file'."
_REJECTIONS: tuple[tuple[type[ArtworkError], int, str], ...] = (
    (NotAnImageError, 400, "Not an image"),
    (UnsupportedImageTypeError, 415, "Unsupported picture type"),
    (ImageTooLargeError, 422, "Picture too large"),
)


@router.post("")
async def normalize_artwork(request: Request, runtime: Runtime) -> JSONResponse:
    """Normalise the multipart part ``file`` into a cover and a thumbnail (201)."""
    data = await _picture(request)
    upload = await run_in_threadpool(_normalize, runtime, data)
    location = f"/v1/artwork/{upload.artwork_id}/cover"
    return json_response(upload, status=201, headers={"Location": location})


@router.get("/{artwork_id}/{variant}")
def get_normalized_artwork(artwork_id: str, variant: str, runtime: Runtime) -> FileResponse:
    """One variant of a normalised user picture, as JPEG."""
    store = runtime.lanes.artwork
    info = store.info(artwork_id)
    if info is None:
        raise ProblemError(404, "Not found", f"Picture {artwork_id[:64]} does not exist.")
    return artwork_response(store.directory(artwork_id), variant, info.sha256)


async def _picture(request: Request) -> bytes:
    """The bytes of part ``file`` (at most 10 MiB); 400 without a part or for an empty file."""
    media_type = request.headers.get("content-type", "").split(";")[0].strip().lower()
    if media_type != "multipart/form-data":
        raise ProblemError(400, INVALID, _NO_PART)
    form = await parse_limited_form(request, MAX_PICTURE_BYTES, _TOO_LARGE)
    try:
        data = await _read_part(form.get("file"))
    finally:
        await form.close()
    if not data:
        raise ProblemError(400, INVALID, "The file is empty.")
    return data


async def _read_part(part: object) -> bytes:
    if not isinstance(part, UploadFile):
        raise ProblemError(400, INVALID, _NO_PART)
    data = await part.read(MAX_PICTURE_BYTES + 1)
    if len(data) > MAX_PICTURE_BYTES:
        raise _TOO_LARGE
    return data


def _normalize(runtime: WorkerRuntime, data: bytes) -> ArtworkUpload:
    normalizer = runtime.lanes.normalizer
    if normalizer is None:
        raise ProblemError(503, "Pictures unavailable", "This worker cannot process pictures.")
    try:
        images = normalizer.normalize(ArtworkCandidate("user", data=data))
    except ArtworkError as exc:
        raise _rejection(exc) from exc
    return runtime.lanes.artwork.add(images)


def _rejection(exc: ArtworkError) -> ProblemError:
    """Map a normaliser rejection to its contract status (400 for anything unexpected)."""
    status, title = next(
        ((status, title) for kind, status, title in _REJECTIONS if isinstance(exc, kind)),
        (400, "Not an image"),
    )
    return ProblemError(status, title, str(exc))
