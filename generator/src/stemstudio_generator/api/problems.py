"""RFC 9457 problem responses with the contract's ``code`` extension member."""

from typing import Final

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from stemstudio_generator.schemas import (
    GPU_REQUIRED_TITLE,
    KIND_NOT_SERVED_TITLE,
    GenerationKind,
    GeneratorProblemCode,
    Problem,
)

PROBLEM_MEDIA_TYPE: Final = "application/problem+json"
_NOT_FOUND: Final = 404

_KIND_ACTIONS: Final[dict[str, str]] = {
    "restyle": "restyle songs",
    "song": "generate songs",
    "voiceConversion": "convert voices",
}


class ProblemError(Exception):
    """Raised by routes; rendered as a problem response."""

    def __init__(self, problem: Problem) -> None:
        """Raise ``problem``."""
        super().__init__(problem.detail)
        self.problem = problem


def problem_error(
    status: int, title: str, detail: str, code: GeneratorProblemCode | None = None
) -> ProblemError:
    """A ``ProblemError`` with ``status``, ``title``, ``detail`` and the optional ``code``."""
    return ProblemError(Problem(title=title, status=status, detail=detail, code=code))


def invalid_input(detail: str) -> ProblemError:
    """``400 invalidInput``."""
    return problem_error(400, "Invalid input", detail, "invalidInput")


def not_found(detail: str) -> ProblemError:
    """``404`` (no code)."""
    return problem_error(404, "Not found", detail)


def generation_not_found(generation_id: str) -> ProblemError:
    """``404`` for an unknown generation."""
    return not_found(f"Generation {generation_id} does not exist.")


def gpu_required(reason: str | None) -> ProblemError:
    """``409 gpuRequired`` with the probe reason."""
    return problem_error(409, GPU_REQUIRED_TITLE, reason or "No usable GPU.", "gpuRequired")


def kind_not_served(kind: GenerationKind, served: tuple[GenerationKind, ...]) -> ProblemError:
    """``409 kindNotServed``: e.g. a song sent to the singer service."""
    detail = f"This service does not {_KIND_ACTIONS[kind]}; it serves {', '.join(served)}."
    return problem_error(409, KIND_NOT_SERVED_TITLE, detail, "kindNotServed")


def too_large(detail: str) -> ProblemError:
    """``413`` for an oversized part."""
    return problem_error(413, "File too large", detail, "invalidInput")


def unsupported(detail: str) -> ProblemError:
    """``415`` for a body or part of the wrong type."""
    return problem_error(415, "Unsupported file type", detail, "invalidInput")


def source_too_short(end_seconds: float, source_seconds: float) -> ProblemError:
    """``422 sourceTooShort``."""
    detail = f"The range ends at {end_seconds:.1f} s but the source is {source_seconds:.1f} s long."
    return problem_error(422, "Source too short", detail, "sourceTooShort")


def problem_response(problem: Problem, headers: dict[str, str] | None = None) -> JSONResponse:
    """Render ``problem`` as ``application/problem+json``."""
    return JSONResponse(
        problem.model_dump(by_alias=True, exclude_none=True),
        status_code=problem.status,
        media_type=PROBLEM_MEDIA_TYPE,
        headers=headers,
    )


def install_problem_handlers(app: FastAPI) -> None:
    """Render ``ProblemError``, HTTP errors and request validation errors as problems."""
    app.add_exception_handler(ProblemError, _on_problem)  # type: ignore[arg-type]
    app.add_exception_handler(StarletteHTTPException, _on_http)  # type: ignore[arg-type]
    app.add_exception_handler(RequestValidationError, _on_validation)  # type: ignore[arg-type]


async def _on_problem(_request: Request, error: ProblemError) -> JSONResponse:
    return problem_response(error.problem)


async def _on_http(_request: Request, error: StarletteHTTPException) -> JSONResponse:
    title = "Not found" if error.status_code == _NOT_FOUND else "Request failed"
    return problem_response(
        Problem(title=title, status=error.status_code, detail=str(error.detail))
    )


async def _on_validation(_request: Request, error: RequestValidationError) -> JSONResponse:
    return problem_response(invalid_input(_first_message(error)).problem)


def _first_message(error: RequestValidationError) -> str:
    details = error.errors()
    return str(details[0].get("msg", "Invalid request.")) if details else "Invalid request."
