"""Quality job store of the ``insights`` lane (feature 020, data-model 2.4).

``quality-jobs/<id>.json`` holds the wire job and the validated request. Restart, delete and the
24-hour sweep follow the render jobs (:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`):
a queued or running job becomes ``failed`` "interrupted by restart" with its stage cleared.
"""

from pathlib import Path

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.schemas import ContractModel, QualityJob, QualityJobRequest


class StoredQualityJob(ContractModel):
    """Content of ``quality-jobs/<id>.json``."""

    job: QualityJob
    request: QualityJobRequest


def _key(record: StoredQualityJob) -> str:
    return record.job.quality_job_id


class QualityJobStore(LaneJobStore[StoredQualityJob]):
    """Quality job records below ``directory`` (one folder per job for its scratch files)."""

    def __init__(self, directory: Path) -> None:
        """Create a store over ``directory`` (not loaded yet)."""
        super().__init__(directory, StoredQualityJob, _key)
