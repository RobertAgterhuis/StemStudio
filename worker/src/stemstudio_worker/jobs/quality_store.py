"""Quality job store of the ``insights`` lane (feature 020, data-model 2.4). Red stub."""

from pathlib import Path

from stemstudio_worker.schemas import ContractModel, QualityJob, QualityJobRequest


class StoredQualityJob(ContractModel):
    """Content of ``quality-jobs/<id>.json``."""

    job: QualityJob
    request: QualityJobRequest


class QualityJobStore:
    """Quality job records (stub)."""

    def __init__(self, directory: Path) -> None:
        """Stub."""
        raise NotImplementedError
