"""Request body parsing and JSON responses shared by the worker routes."""

from pydantic import BaseModel, ValidationError
from starlette.responses import JSONResponse

from stemstudio_worker.api.problems import ProblemError


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
