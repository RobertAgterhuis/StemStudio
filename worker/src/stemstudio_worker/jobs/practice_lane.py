"""The ``practice`` lane: one thread, FIFO across section, score-view and setlist jobs (ADR-023).

The three job services of :mod:`~stemstudio_worker.jobs.practice_service` queue their jobs here
with their own process function; the lane runs one job at a time in submission order and owns the
sweep of the three stores (``Health.lanes.practice``).
"""

from collections.abc import Callable
from dataclasses import dataclass

from stemstudio_worker.jobs.lanes import SharedLane
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.practice_store import PracticeStores
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import ScoreViewRenderer, SetlistDocumentRenderer, StructureAnalyzer
from stemstudio_worker.schemas import StructureHealth


@dataclass(frozen=True, slots=True)
class PracticeParts:
    """What the practice jobs run (composed in ``main.py``; None = not available).

    Attributes:
        analyzer: The section analyser (``Health.structure``).
        score_views: MusicXML -> SVG pages and bar map.
        documents: Setlist PDFs.
    """

    analyzer: StructureAnalyzer | None = None
    score_views: ScoreViewRenderer | None = None
    documents: SetlistDocumentRenderer | None = None

    def structure_health(self) -> StructureHealth:
        """``Health.structure``: the analyser's name and version (the default without one)."""
        if self.analyzer is None:
            return StructureHealth()
        return StructureHealth(analyzer=self.analyzer.name, version=self.analyzer.version)


class PracticeLane(SharedLane):
    """The shared practice lane over :class:`PracticeStores`."""

    def __init__(self, stores: PracticeStores, clock: Clock) -> None:
        """Create a stopped lane over loaded ``stores``; ``clock`` stamps the sweep."""
        super().__init__("practice")
        self.stores = stores
        self._clock = clock

    @classmethod
    def loaded(cls, paths: DataPaths, clock: Clock) -> "PracticeLane":
        """Load the three stores and fail the jobs a restart interrupted (the lane is stopped)."""
        stores = PracticeStores.of(paths)
        stores.load_all()
        stores.reconcile(clock)
        return cls(stores, clock)

    def submit(self, process: Callable[[str], None], job_id: str) -> None:
        """Queue ``process(job_id)`` behind the jobs of every kind already waiting."""
        self.scheduler.submit_call(process, job_id)

    def sweep(self) -> list[str]:
        """Remove terminal jobs of every kind older than 24 hours."""
        return self.stores.sweep(self._clock())
