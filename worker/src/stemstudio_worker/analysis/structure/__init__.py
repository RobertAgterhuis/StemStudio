"""Section detection from stems (feature 014, ADR-023 D1, data-model 2.2-2.4).

Public API:

* :func:`create_structure_analyzer` - factory of the composition root: ``fake`` or the real
  analyser by ``WorkerSettings.structure_analyzer``.
* :class:`StemsNoveltyAnalyzer` - the real analyser (``stems-novelty`` 1.0.0).
* :class:`FakeStructureAnalyzer` - the E2E fake (``fake`` 1.0.0).
"""

from stemstudio_worker.analysis.structure.analyzer import StemsNoveltyAnalyzer
from stemstudio_worker.analysis.structure.fake import FakeStructureAnalyzer
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import StructureAnalyzer

__all__ = ["FakeStructureAnalyzer", "StemsNoveltyAnalyzer", "create_structure_analyzer"]


def create_structure_analyzer(settings: WorkerSettings) -> StructureAnalyzer:
    """Build the configured section analyser.

    Args:
        settings: ``structure_analyzer`` selects ``fake`` or ``real`` (``stems-novelty``).

    Returns:
        A new analyser.
    """
    if settings.structure_analyzer == "fake":
        return FakeStructureAnalyzer()
    return StemsNoveltyAnalyzer()
