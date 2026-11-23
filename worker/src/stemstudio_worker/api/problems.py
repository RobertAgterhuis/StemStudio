"""RFC 9457 problem details (``application/problem+json``) for every error response."""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Any

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from stemstudio_worker.ports import InputNotFoundError, InputPathError
from stemstudio_worker.schemas import ProblemDetails

PROBLEM_JSON = "application/problem+json"


class ProblemError(Exception):
    """Raised by route code to answer with a problem response."""

    problem_type = "about:blank"
    """The problem ``type``; :class:`TypedProblemError` sets a specific one (1.4.0)."""

    def __init__(self, status: int, title: str, detail: str) -> None:
        """Create a problem with HTTP ``status``, a short ``title`` and a readable ``detail``."""
        super().__init__(detail)
        self.status = status
        self.title = title
        self.detail = detail


@dataclass(frozen=True, slots=True)
class ProblemKind:
    """A problem with its own ``type`` (e.g. ``invalid-input-path``)."""

    type: str
    status: int
    title: str


class TypedProblemError(ProblemError):
    """A :class:`ProblemError` of a :class:`ProblemKind`."""

    def __init__(self, kind: ProblemKind, detail: str) -> None:
        """Create the problem ``kind`` with a readable ``detail``."""
        super().__init__(kind.status, kind.title, detail)
        self.problem_type = kind.type


def problem_response(
    status: int, title: str, detail: str | None, problem_type: str = "about:blank"
) -> JSONResponse:
    """Build a problem+json response."""
    body = ProblemDetails(type=problem_type, title=title, status=status, detail=detail)
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
    return problem_response(exc.status, exc.title, exc.detail, exc.problem_type)


async def _on_http_error(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, StarletteHTTPException)  # noqa: S101 - registered for this type only
    return problem_response(exc.status_code, _title(exc.status_code), str(exc.detail))


async def _on_validation_error(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, RequestValidationError)  # noqa: S101 - registered for this type only
    return validation_problem(validation_errors(exc.errors()))


def validation_problem(errors: dict[str, list[str]]) -> JSONResponse:
    """The 422 "Validation failed" problem with the ``errors`` extension (field -> messages)."""
    body = ProblemDetails.model_validate(
        {"title": "Validation failed", "status": 422, "detail": "The request body is not valid.",
         "errors": errors}
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


INVALID_INPUT_PATH = ProblemKind("invalid-input-path", 400, "Invalid input path")
"""The path guard refused an input path (render 1.4.0, practice 1.8.0)."""
INPUT_NOT_FOUND = ProblemKind("input-not-found", 404, "Input not found")
"""An input file does not exist."""


def path_guarded[T](create: Callable[[], T]) -> T:
    """``create()`` with the path guard's errors as ``invalid-input-path`` / ``input-not-found``."""
    try:
        return create()
    except InputPathError as exc:
        raise TypedProblemError(INVALID_INPUT_PATH, str(exc)) from exc
    except InputNotFoundError as exc:
        raise TypedProblemError(INPUT_NOT_FOUND, str(exc)) from exc
