"""The ``stems-novelty`` section analyser (data-model 2.2; beat-analysis, T051)."""

from typing import Final

from stemstudio_worker.ports import SectionProgress, StemFiles, StructureError
from stemstudio_worker.schemas import SectionJobRequest, SectionResult, StructureAnalyzerName

ANALYZER_VERSION: Final = "1.0.0"


class StemsNoveltyAnalyzer:
    """:class:`~stemstudio_worker.ports.StructureAnalyzer` ``stems-novelty`` 1.0.0."""

    name: StructureAnalyzerName = "stems-novelty"
    version: str = ANALYZER_VERSION

    def analyze(
        self, request: SectionJobRequest, files: StemFiles, progress: SectionProgress
    ) -> SectionResult:
        """Detect the sections (T051 replaces this placeholder)."""
        del request, files, progress
        raise StructureError("Section detection is not available yet.")
