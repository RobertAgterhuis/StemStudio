"""The ``practice`` lane: one thread, FIFO across section, score-view and setlist jobs (ADR-023).

The three job services of :mod:`~stemstudio_worker.jobs.practice_service` queue their jobs here
with their own process function; the lane runs one job at a time in submission order and owns the
sweep of the three stores (``Health.lanes.practice``).
"""

from collections.abc import Callable
from dataclasses import dataclass

from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.practice_store import PracticeStores
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import ScoreViewRenderer, SetlistDocumentRenderer, StructureAnalyzer
from stemstudio_worker.schemas import LaneHealth, StructureHealth


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


def _unknown_kind(job_id: str) -> None:  # pragma: no cover - every job names its process
    """Default process of the lane: jobs are always submitted with their kind's function."""
    raise LookupError(job_id)


class PracticeLane:
    """The shared practice lane over :class:`PracticeStores`."""

    def __init__(self, stores: PracticeStores, clock: Clock) -> None:
        """Create a stopped lane over loaded ``stores``; ``clock`` stamps the sweep."""
        self.stores = stores
        self.scheduler = LaneScheduler("practice", _unknown_kind)
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

    def start(self) -> None:
        """Start the lane thread."""
        self.scheduler.start()

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane thread; a running job fails at the next start."""
        self.scheduler.stop(timeout)

    def join(self, timeout: float | None = None) -> bool:
        """Block until no job waits or runs; False on timeout."""
        return self.scheduler.join(timeout)

    def health(self) -> LaneHealth:
        """Waiting jobs and whether one runs (``Health.lanes.practice``)."""
        return self.scheduler.health()

    def sweep(self) -> list[str]:
        """Remove terminal jobs of every kind older than 24 hours."""
        return self.stores.sweep(self._clock())
