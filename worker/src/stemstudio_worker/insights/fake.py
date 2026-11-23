"""Fake quality analyzer. Stub for the red commit of T036."""

import time
from collections.abc import Callable

from stemstudio_worker.ports import InsightsProgress, QualityInputs
from stemstudio_worker.schemas import QualityResult

FAKE_VERSION = "fake-1"
FAKE_DURATION_SECONDS = 0.5


class FakeQualityAnalyzer:
    """Stub."""

    def __init__(self, sleep: Callable[[float], None] = time.sleep) -> None:
        self._sleep = sleep

    @property
    def version(self) -> str:
        """Stub."""
        raise NotImplementedError

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Stub."""
        raise NotImplementedError
