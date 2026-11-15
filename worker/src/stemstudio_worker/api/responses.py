"""Request body parsing and JSON responses shared by the worker routes."""

from pathlib import Path
from typing import get_args

from pydantic import BaseModel, ValidationError
from starlette.responses import FileResponse, JSONResponse

from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.schemas import ArtworkVariant

_VARIANTS: tuple[ArtworkVariant, ...] = get_args(ArtworkVariant)


def parse_body[M: BaseModel](model: type[M], raw: bytes) -> M:
    """Validate a JSON request body as ``model``; any problem is a 400 with the field path."""
    try:
        return model.model_validate_json(raw)
    except ValidationError as exc:
        error = exc.errors()[0]
        location = ".".join(str(part) for part in error["loc"])
        detail = f"{location}: {error['msg']}" if location else str(error["msg"])
        raise ProblemError(400, "Invalid request", detail) from exc


def json_response(
    model: BaseModel, status: int = 200, headers: dict[str, str] | None = None
) -> JSONResponse:
    """Serialise ``model`` with camelCase aliases."""
    body = model.model_dump(mode="json", by_alias=True)
    return JSONResponse(body, status_code=status, headers=headers)


def artwork_response(directory: Path, variant: str, sha256: str) -> FileResponse:
    """Serve ``<directory>/<variant>.jpg`` with ``ETag`` = the quoted cover hash.

    Raises:
        ProblemError: 404 for an unknown variant or a missing file.
    """
    path = directory / f"{variant}.jpg"
    if variant not in _VARIANTS or not path.is_file():
        raise ProblemError(404, "Not found", f"Artwork {variant[:64]} does not exist.")
    return FileResponse(path, media_type="image/jpeg", headers={"ETag": f'"{sha256}"'})
