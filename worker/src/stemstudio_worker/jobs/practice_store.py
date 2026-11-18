"""Job stores of the ``practice`` lane (feature 014, data-model 2.8).

``section-jobs/<id>.json``, ``score-view-jobs/<id>.json`` with the pages in ``<id>/`` and
``setlist-document-jobs/<id>.json`` with ``<id>/setlist.pdf``. The record keeps the wire job plus
the internal ``updated_at`` (the 1.8.0 jobs carry no update time) and the validated request.
Restart, delete and the 24-hour sweep follow the render jobs
(:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`).
"""

from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.schemas import (
    ContractModel,
    PracticeJobStatus,
    ScoreViewJob,
    ScoreViewJobRequest,
    SectionJob,
    SectionJobRequest,
    SetlistDocumentJob,
    SetlistDocumentRequest,
)


class StampedSectionJob(SectionJob):
    """A section job with the time of its last change (stored, never sent)."""

    updated_at: datetime


class StampedScoreViewJob(ScoreViewJob):
    """A score-view job with the time of its last change (stored, never sent)."""

    updated_at: datetime


class StampedSetlistDocumentJob(SetlistDocumentJob):
    """A setlist document job with the time of its last change (stored, never sent)."""

    updated_at: datetime


class StoredSectionJob(ContractModel):
    """Content of ``section-jobs/<id>.json``."""

    job: StampedSectionJob
    request: SectionJobRequest


class StoredScoreViewJob(ContractModel):
    """Content of ``score-view-jobs/<id>.json``."""

    job: StampedScoreViewJob
    request: ScoreViewJobRequest


class StoredSetlistDocumentJob(ContractModel):
    """Content of ``setlist-document-jobs/<id>.json``."""

    job: StampedSetlistDocumentJob
    request: SetlistDocumentRequest


def wire_job[J: PracticeJobStatus](job: PracticeJobStatus, wire: type[J]) -> J:
    """The contract model ``wire`` of a stored job (without ``updated_at``)."""
    return wire.model_validate(job.model_dump(exclude={"updated_at"}))


def _job_id(record: Any) -> str:
    job_id: str = record.job.job_id
    return job_id


class PracticeJobStore[R: ContractModel](LaneJobStore[R]):
    """One kind of practice job below its own directory."""

    def __init__(self, directory: Path, model: type[R]) -> None:
        """Create a store of ``model`` records over ``directory``."""
        super().__init__(directory, model, _job_id)
        self.model = model


@dataclass(frozen=True, slots=True)
class PracticeStores:
    """The three stores of the ``practice`` lane.

    Attributes:
        sections: Section jobs.
        score_views: Score-view jobs and their pages.
        documents: Setlist document jobs and their PDFs.
    """

    sections: PracticeJobStore[StoredSectionJob]
    score_views: PracticeJobStore[StoredScoreViewJob]
    documents: PracticeJobStore[StoredSetlistDocumentJob]

    @classmethod
    def of(cls, paths: DataPaths) -> "PracticeStores":
        """Stores over the practice directories of ``paths`` (not loaded yet)."""
        return cls(
            PracticeJobStore(paths.section_jobs, StoredSectionJob),
            PracticeJobStore(paths.score_view_jobs, StoredScoreViewJob),
            PracticeJobStore(paths.setlist_document_jobs, StoredSetlistDocumentJob),
        )

    def all_stores(self) -> tuple[PracticeJobStore[Any], ...]:
        """The three stores in kind order (section, score view, setlist document)."""
        return (self.sections, self.score_views, self.documents)

    def owner(self, record: object) -> PracticeJobStore[Any]:
        """The store that keeps records of the type of ``record``."""
        return next(s for s in self.all_stores() if isinstance(record, s.model))

    def load_all(self) -> None:
        """Read every record file of the three stores."""
        for store in self.all_stores():
            store.load_all()

    def reconcile(self, clock: Clock) -> list[str]:
        """Fail the unfinished jobs of every store ("interrupted by restart")."""
        return [job_id for store in self.all_stores() for job_id in store.reconcile(clock)]

    def sweep(self, now: datetime) -> list[str]:
        """Remove the terminal jobs older than 24 hours of every store."""
        return [job_id for store in self.all_stores() for job_id in store.sweep(now)]
