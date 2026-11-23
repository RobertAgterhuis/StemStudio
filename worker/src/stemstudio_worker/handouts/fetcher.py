"""WeasyPrint URL fetcher over the allow-list (feature 020, P0-4). Red stub."""

from weasyprint.urls import URLFetcher

from stemstudio_worker.handouts.resources import AllowList


class AllowListFetcher(URLFetcher):  # type: ignore[misc]
    """Stub."""

    def __init__(self, allowed: AllowList) -> None:
        """Stub."""
        raise NotImplementedError
