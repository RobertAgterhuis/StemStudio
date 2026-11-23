"""Quality jobs on the ``insights`` lane (feature 020, data-model 2.3 and 2.4).

``POST /v1/quality-jobs``: every stem path must match ``ports.QUALITY_PATH_PATTERNS`` (stems and
detail stems only) and pass the library's path guard when the job is created (400 / 404). When
the job runs the paths are resolved again and handed to the injected ``QualityAnalyzer`` as
``StemAudio`` in request order.
Stages: ``loading`` 0-0.2, ``analysing`` 0.2-0.9, ``rating`` 0.9-1.0.
"""

import math
from pathlib import Path
from typing import Final, cast

from stemstudio_worker.jobs.input_guard import check_input
from stemstudio_worker.jobs.insights_service import InsightsContext, InsightsJobService, stage_in
from stemstudio_worker.jobs.lane_service import Outcome
from stemstudio_worker.jobs.quality_store import StoredQualityJob
from stemstudio_worker.ports import (
    InsightsProgress,
    QualityAnalysisError,
    QualityAnalyzer,
    QualityCancelledError,
    QualityInputs,
    RenderError,
    StemAudio,
    is_quality_input_path,
)
from stemstudio_worker.schemas import (
    QualityJob,
    QualityJobRequest,
    QualityResult,
    QualityStage,
    RenderJobStatus,
)

UNAVAILABLE_TEXT: Final = "Stem quality analysis is not available on this worker."
"""Failure text of every quality job when no analyzer could be loaded."""

QUALITY_STAGES: Final = ((0.2, "loading"), (0.9, "analysing"), (math.inf, "rating"))


def stage_of(fraction: float) -> QualityStage:
    """The contract stage of a quality job at ``fraction`` (0..1)."""
    return cast(QualityStage, stage_in(QUALITY_STAGES, fraction))


class UnavailableAnalyzer:
    """Stands in when the configured analyzer cannot be imported: every job fails readably."""

    version: str = "0.0.0"

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        """Fail with ``UNAVAILABLE_TEXT``."""
        del inputs, progress
        raise QualityAnalysisError(UNAVAILABLE_TEXT)


class QualityService(InsightsJobService[StoredQualityJob]):
    """Quality jobs (``POST /v1/quality-jobs``)."""

    LABEL = "Quality"
    CANCELLED = QualityCancelledError
    ERRORS = (QualityAnalysisError, RenderError)
    ID_FIELD = "quality_job_id"
    STAGES = QUALITY_STAGES

    def __init__(self, context: InsightsContext, analyzer: QualityAnalyzer) -> None:
        """Serve the quality store of ``context`` with ``analyzer``."""
        super().__init__(context, context.lane.stores.quality)
        self._analyzer = analyzer

    def create(self, request: QualityJobRequest) -> QualityJob:
        """Check every stem path (400 / 404 as ``check_input``) and queue the job."""
        for path in request.input_paths():
            check_input(self._library.resolve, path, is_quality_input_path)
        job = QualityJob(**self._stamp())
        created: QualityJob = self._queue(StoredQualityJob(job=job, request=request))
        return created

    def run(self, record: StoredQualityJob, out_dir: Path) -> Outcome:  # noqa: ARG002
        """Resolve the stems and analyse them."""
        report = self._reporter(record.job.quality_job_id)
        report(0.0)
        request = record.request
        stems = tuple(StemAudio(stem.channel, stem.kind, self._library.resolve(stem.path))
                      for stem in request.stems)  # fmt: skip
        inputs = QualityInputs(stems=stems, detail_parents=tuple(request.detail_parents))
        result = self._analyzer.analyze(inputs, report)
        return {"status": RenderJobStatus.COMPLETED, "progress": 1.0, "result": result}
