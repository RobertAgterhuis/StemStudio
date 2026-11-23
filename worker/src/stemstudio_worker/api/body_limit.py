"""Request body size limits per route (stub)."""

from collections.abc import Mapping

from starlette.types import ASGIApp, Receive, Scope, Send


class BodyLimit:
    """ASGI middleware (stub: passes every request through)."""

    def __init__(self, app: ASGIApp, limits: Mapping[str, int]) -> None:
        """Wrap ``app``."""
        self._app = app
        self._limits = dict(limits)

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        """Pass through."""
        await self._app(scope, receive, send)
