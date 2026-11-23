"""Request body size limits per route (020 security fixes: ``POST /v1/handout-jobs``).

A JSON route reads its whole body before the handler runs, so the limit sits in front of the
app: a ``POST`` to a limited path whose declared ``Content-Length`` or received body (also
chunked, without a length) passes the limit answers 422 "Validation failed" with
``errors.body`` (the contract's ``ValidationFailed`` response) before anything is parsed.
At most the limit is held in memory; a body within it reaches the route unchanged.
"""

from collections.abc import Mapping

from starlette.types import ASGIApp, Message, Receive, Scope, Send

from stemstudio_worker.api.problems import validation_problem


def _size_text(limit: int) -> str:
    mib, rest = divmod(limit, 1024 * 1024)
    return f"{mib} MiB" if mib and not rest else f"{limit} bytes"


class BodyLimit:
    """ASGI middleware: ``POST`` bodies of the listed paths larger than their limit are 422."""

    def __init__(self, app: ASGIApp, limits: Mapping[str, int]) -> None:
        """Wrap ``app``; ``limits`` maps an exact path to its largest body in bytes."""
        self._app = app
        self._limits = dict(limits)

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        """Pass the request on, or answer 422 when its body is over the limit of its path."""
        limit = self._limit_of(scope)
        if limit is None:
            await self._app(scope, receive, send)
            return
        body = await _read_within(scope, receive, limit)
        if body is None:
            message = f"The request body is larger than {_size_text(limit)}."
            await validation_problem({"body": [message]})(scope, receive, send)
            return
        await self._app(scope, _replay(body, receive), send)

    def _limit_of(self, scope: Scope) -> int | None:
        if scope["type"] != "http" or scope["method"] != "POST":
            return None
        return self._limits.get(scope["path"])


async def _read_within(scope: Scope, receive: Receive, limit: int) -> bytes | None:
    """The whole body, or None as soon as it (or its declared length) is over ``limit``."""
    declared = dict(scope["headers"]).get(b"content-length", b"")
    if declared.isdigit() and int(declared) > limit:
        return None
    chunks: list[bytes] = []
    size, more = 0, True
    while more:
        message = await receive()
        chunk = message.get("body", b"")
        size += len(chunk)
        if size > limit:
            return None
        chunks.append(chunk)
        more = message["type"] == "http.request" and message.get("more_body", False)
    return b"".join(chunks)


def _replay(body: bytes, receive: Receive) -> Receive:
    """``receive`` that gives ``body`` once, then the original messages (e.g. a disconnect)."""
    pending = [body]

    async def replay() -> Message:
        if pending:
            return {"type": "http.request", "body": pending.pop(), "more_body": False}
        return await receive()

    return replay
