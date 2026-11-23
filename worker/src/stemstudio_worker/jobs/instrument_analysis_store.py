"""Job store of the ``instrument-analysis`` lane (feature 016, DELTA 1).

``instrument-analysis-jobs/<id>.json`` holds the wire job and the validated request (one of the
three kinds); ``<id>/`` is the job's work folder. Restart, delete and the 24-hour sweep follow the
render jobs (:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`, ADR-003).
"""

from typing import Annotated, Final, Self

from pydantic import Field, model_validator

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.schemas import AnyAnalysisRequest, ContractModel, InstrumentAnalysisJob

STORE_DIR: Final = "instrument-analysis-jobs"
"""Directory of the job records below the data directory."""


class StoredAnalysisJob(ContractModel):
    """Content of ``instrument-analysis-jobs/<id>.json``."""

    job: InstrumentAnalysisJob
    request: Annotated[AnyAnalysisRequest, Field(discriminator="kind")]

    @model_validator(mode="after")
    def _check_kind(self) -> Self:
        """The job's kind is the request's kind."""
        if self.job.kind != self.request.kind:
            msg = "job and request kinds differ"
            raise ValueError(msg)
        return self


def _job_id(record: StoredAnalysisJob) -> str:
    return record.job.analysis_job_id


class InstrumentAnalysisStore(LaneJobStore[StoredAnalysisJob]):
    """The analysis job records with their work folders."""

    @classmethod
    def of(cls, paths: DataPaths) -> "InstrumentAnalysisStore":
        """A store below ``paths.root`` (not loaded yet)."""
        return cls(paths.root / STORE_DIR, StoredAnalysisJob, _job_id)

    @classmethod
    def loaded(cls, paths: DataPaths, clock: Clock) -> "InstrumentAnalysisStore":
        """Load every record and fail the jobs a restart interrupted."""
        store = cls.of(paths)
        store.load_all()
        store.reconcile(clock)
        return store
