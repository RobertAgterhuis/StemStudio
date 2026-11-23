"""The ``HandoutRenderer`` of feature 020 (WeasyPrint + pypdf). Red stub."""

from collections.abc import Mapping
from pathlib import Path

from stemstudio_worker.ports import HandoutRenderError, InsightsProgress, RenderedHandout
from stemstudio_worker.schemas import HandoutJobRequest


class WeasyHandoutRenderer:
    """Stub."""

    def render(
        self, request: HandoutJobRequest, files: Mapping[str, Path], progress: InsightsProgress
    ) -> RenderedHandout:
        """Stub."""
        del request, files, progress
        raise HandoutRenderError
