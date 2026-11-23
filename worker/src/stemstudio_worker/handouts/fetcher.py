"""The WeasyPrint URL fetcher of handout renders: the allow-list, nothing else (FR-018, P0-4).

WeasyPrint 70 requires an instance of a ``weasyprint.urls.URLFetcher`` subclass that overrides
``fetch(url, headers=None)`` and returns a ``URLFetcherResponse``. This fetcher never calls the
base class's ``fetch``, so no network handler is ever used; ``allowed_protocols=("file",)``,
no redirects and a short timeout are a second guard. Every URL is recorded as ``fetched`` or
``refused`` (tests and logs). Importing this module loads Pango; it is imported lazily by
:mod:`~stemstudio_worker.handouts.layout`.
"""

from typing import Any

from weasyprint.urls import URLFetcher, URLFetcherResponse

from stemstudio_worker.handouts.resources import AllowList, RefusedResourceError

_TIMEOUT_SECONDS = 1


class AllowListFetcher(URLFetcher):  # type: ignore[misc]  # WeasyPrint has no type information
    """Serves the files of an :class:`AllowList`; any other URL raises ``RefusedResourceError``."""

    def __init__(self, allowed: AllowList) -> None:
        """Serve only ``allowed``; record the URLs asked for in ``fetched`` / ``refused``."""
        super().__init__(
            timeout=_TIMEOUT_SECONDS, allowed_protocols=("file",), allow_redirects=False
        )
        self._allowed = allowed
        self.fetched: list[str] = []
        self.refused: list[str] = []

    def fetch(self, url: str, headers: Any = None) -> Any:  # noqa: ARG002 - headers unused
        """The allowed file ``url`` names (WeasyPrint 70 fetcher API).

        Raises:
            RefusedResourceError: ``url`` is not on the allow-list.
        """
        try:
            body, content_type = self._allowed.read(url)
        except RefusedResourceError:
            self.refused.append(url)
            raise
        self.fetched.append(url)
        return URLFetcherResponse(url, body, {"Content-Type": content_type})
