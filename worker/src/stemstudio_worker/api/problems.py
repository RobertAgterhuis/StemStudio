"""RFC 9457 problem details (``application/problem+json``) for every error response."""

from collections.abc import Sequence
from typing import Any

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from stemstudio_worker.schemas import ProblemDetails

PROBLEM_JSON = "application/problem+json"


class ProblemError(Exception):
    """Raised by route code to answer with a problem response."""

    def __init__(self, status: int, title: str, detail: str) -> None:
        """Create a problem with HTTP ``status``, a short ``title`` and a readable ``detail``."""
        super().__init__(detail)
        self.status = status
        self.title = title
        self.detail = detail


def problem_response(status: int, title: str, detail: str | None) -> JSONResponse:
    """Build a problem+json response."""
    body = ProblemDetails(title=title, status=status, detail=detail)
    return JSONResponse(body.model_dump(), status_code=status, media_type=PROBLEM_JSON)


def install_problem_handlers(app: FastAPI) -> None:
    """Render route problems, framework HTTP errors and validation errors as problem+json.

    A request body that fails its schema (routes with a typed body, 1.3.0) answers 422
    "Validation failed" with an ``errors`` extension: field path -> messages.
    """
    app.add_exception_handler(ProblemError, _on_problem)
    app.add_exception_handler(StarletteHTTPException, _on_http_error)
    app.add_exception_handler(RequestValidationError, _on_validation_error)


async def _on_problem(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, ProblemError)  # noqa: S101 - registered for this type only
    return problem_response(exc.status, exc.title, exc.detail)


async def _on_http_error(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, StarletteHTTPException)  # noqa: S101 - registered for this type only
    return problem_response(exc.status_code, _title(exc.status_code), str(exc.detail))


async def _on_validation_error(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, RequestValidationError)  # noqa: S101 - registered for this type only
    body = ProblemDetails.model_validate(
        {"title": "Validation failed", "status": 422, "detail": "The request body is not valid.",
         "errors": validation_errors(exc.errors())}
    )  # fmt: skip
    return JSONResponse(body.model_dump(), status_code=422, media_type=PROBLEM_JSON)


def validation_errors(errors: Sequence[Any]) -> dict[str, list[str]]:
    """Group pydantic errors by field path (``body`` prefix dropped; ``body`` for the whole)."""
    grouped: dict[str, list[str]] = {}
    for error in errors:
        location = [str(part) for part in error.get("loc", ())][1:]
        grouped.setdefault(".".join(location) or "body", []).append(str(error.get("msg", "")))
    return grouped


def _title(status: int) -> str:
    return {404: "Not found", 405: "Method not allowed"}.get(status, "Bad request")
