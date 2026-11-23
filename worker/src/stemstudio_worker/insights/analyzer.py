"""Signal quality analyzer (``ports.QualityAnalyzer``). Stub for the red commit of T034."""

from stemstudio_worker.ports import InsightsProgress, QualityInputs
from stemstudio_worker.schemas import QualityResult


class SignalQualityAnalyzer:
    """Stub."""

    @property
    def version(self) -> str:
        """Stub."""
        raise NotImplementedError

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Stub."""
        raise NotImplementedError
