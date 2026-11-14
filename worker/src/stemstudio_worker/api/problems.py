"""RFC 9457 problem details (``application/problem+json``) for every error response."""

from fastapi import FastAPI, Request
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
    """Render :class:`ProblemError` and framework HTTP errors (404, 405, ...) as problem+json."""
    app.add_exception_handler(ProblemError, _on_problem)
    app.add_exception_handler(StarletteHTTPException, _on_http_error)


async def _on_problem(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, ProblemError)  # noqa: S101 - registered for this type only
    return problem_response(exc.status, exc.title, exc.detail)


async def _on_http_error(_request: Request, exc: Exception) -> JSONResponse:
    assert isinstance(exc, StarletteHTTPException)  # noqa: S101 - registered for this type only
    return problem_response(exc.status_code, _title(exc.status_code), str(exc.detail))


def _title(status: int) -> str:
    return {404: "Not found", 405: "Method not allowed"}.get(status, "Bad request")
