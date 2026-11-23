"""Quality jobs on the ``insights`` lane (feature 020). Red stub."""

from typing import Final

from stemstudio_worker.ports import InsightsProgress, QualityInputs
from stemstudio_worker.schemas import QualityResult, QualityStage

UNAVAILABLE_TEXT: Final = "Stem quality analysis is not available on this worker."


def stage_of(fraction: float) -> QualityStage:
    """Stub."""
    raise NotImplementedError


class UnavailableAnalyzer:
    """Stub."""

    version = "0.0.0"

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Stub."""
        raise NotImplementedError
