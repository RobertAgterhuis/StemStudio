"""Section detection from stems (feature 014, ADR-023 D1, data-model 2.2-2.4).

Public API (composed by ``main.create_structure_analyzer``):

* :class:`StemsNoveltyAnalyzer` - the real analyser (``stems-novelty`` 1.0.0).
* :class:`FakeStructureAnalyzer` - the E2E fake (``fake`` 1.0.0).

The package imports only ``ports``, ``schemas``, itself and numpy, scipy, sklearn, librosa and
soundfile (``tools/quality`` rule of 014 T015).
"""

from stemstudio_worker.analysis.structure.analyzer import StemsNoveltyAnalyzer
from stemstudio_worker.analysis.structure.fake import FakeStructureAnalyzer

__all__ = ["FakeStructureAnalyzer", "StemsNoveltyAnalyzer"]
