"""The ``insights`` lane and its job services as the runtime holds them (feature 020, ADR-029).

``InsightsParts`` is what the composition root injects (analyzer, renderer, the ``Health.insights``
it measured, the thread cap); ``InsightsServices`` loads the lane (restart handling), builds the
services and starts the lane thread.
"""

from dataclasses import dataclass, field

from stemstudio_worker.jobs.insights_lane import InsightsLane, ThreadLimit
from stemstudio_worker.jobs.insights_service import InsightsContext
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.quality_service import QualityService, UnavailableAnalyzer
from stemstudio_worker.ports import LibraryReader, QualityAnalyzer
from stemstudio_worker.schemas import INSIGHTS_NOT_CHECKED, InsightsHealth

DEFAULT_THREADS = 4
"""``STEMSTUDIO_INSIGHTS_THREADS`` when unset (data-model 1)."""


@dataclass(frozen=True, slots=True)
class InsightsParts:
    """What the insights jobs run (composed in ``main.py``).

    Attributes:
        analyzer: Stems -> quality estimates (beat-analysis ``insights/``).
        health: ``Health.insights`` (analyzer kind and version, renderer, fonts).
        threads: Intra-op thread limit of every insights job.
    """

    analyzer: QualityAnalyzer = field(default_factory=UnavailableAnalyzer)
    health: InsightsHealth = INSIGHTS_NOT_CHECKED
    threads: int = DEFAULT_THREADS


@dataclass(frozen=True, slots=True)
class InsightsServices:
    """The ``insights`` lane and its job services (``Lanes.insights``)."""

    lane: InsightsLane
    parts: InsightsParts
    quality: QualityService

    @classmethod
    def start(cls, context: InsightsContext, parts: InsightsParts) -> "InsightsServices":
        """Build the services over the (loaded, reconciled) lane of ``context`` and start it."""
        quality = QualityService(context, parts.analyzer)
        context.lane.start()
        return cls(context.lane, parts, quality)


def start_insights(paths: DataPaths, library: LibraryReader, clock: Clock,
                   parts: InsightsParts) -> InsightsServices:  # fmt: skip
    """Load the lane (interrupted jobs failed), build the services and start the lane."""
    lane = InsightsLane.loaded(paths, clock, ThreadLimit(parts.threads))
    return InsightsServices.start(InsightsContext(lane, library, clock), parts)
