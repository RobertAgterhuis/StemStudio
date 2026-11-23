"""Handout job store of the ``insights`` lane (feature 020, data-model 2.5).

``handout-jobs/<id>.json`` holds the wire job and the validated request, ``<id>/handout.pdf`` the
PDF of a completed job. Restart, delete and the 24-hour sweep as
:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`.
"""

from pathlib import Path
from typing import Final

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.schemas import ContractModel, HandoutJob, HandoutJobRequest

PDF_FILE: Final = "handout.pdf"
"""File name of the PDF (``HandoutResult.fileName``)."""


class StoredHandoutJob(ContractModel):
    """Content of ``handout-jobs/<id>.json``."""

    job: HandoutJob
    request: HandoutJobRequest


def _key(record: StoredHandoutJob) -> str:
    return record.job.handout_job_id


class HandoutJobStore(LaneJobStore[StoredHandoutJob]):
    """Handout job records and their PDFs below ``directory``."""

    def __init__(self, directory: Path) -> None:
        """Create a store over ``directory`` (not loaded yet)."""
        super().__init__(directory, StoredHandoutJob, _key)

    def pdf_path(self, job_id: str) -> Path:
        """Where the PDF of job ``job_id`` is (or will be) written."""
        return self.output_dir(job_id) / PDF_FILE
