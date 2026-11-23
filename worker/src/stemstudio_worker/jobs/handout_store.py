"""Handout job store of the ``insights`` lane (feature 020, data-model 2.5). Red stub."""

from pathlib import Path

from stemstudio_worker.schemas import ContractModel, HandoutJob, HandoutJobRequest


class StoredHandoutJob(ContractModel):
    """Content of ``handout-jobs/<id>.json``."""

    job: HandoutJob
    request: HandoutJobRequest


class HandoutJobStore:
    """Handout job records (stub)."""

    def __init__(self, directory: Path) -> None:
        """Stub."""
        raise NotImplementedError
