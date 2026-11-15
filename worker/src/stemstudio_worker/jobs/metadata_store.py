"""Metadata job records and results (data-model.md 5).

``metadata-jobs/<id>.json`` holds the job and its request; ``metadata-jobs/<id>/result.json``
and ``metadata-jobs/<id>/artwork/{cover,thumb}.jpg`` hold what a completed job found.
"""

import shutil
from datetime import datetime
from pathlib import Path

from pydantic import ValidationError

from stemstudio_worker.jobs.artwork_files import artwork_file, write_artwork
from stemstudio_worker.jobs.json_store import JsonStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.ports import MetadataFindings
from stemstudio_worker.schemas import (
    ArtworkVariant,
    ContractModel,
    CreateMetadataJobRequest,
    MetadataJob,
    MetadataJobState,
    MetadataResult,
)


class StoredMetadataJob(ContractModel):
    """Content of ``metadata-jobs/<id>.json``: the wire job and what it looks up."""

    job: MetadataJob
    request: CreateMetadataJobRequest


def _job_id(record: StoredMetadataJob) -> str:
    return record.job.metadata_job_id


class MetadataJobStore(JsonStore[StoredMetadataJob]):
    """Persists metadata jobs below ``paths.metadata_jobs``."""

    def __init__(self, paths: DataPaths) -> None:
        """Create a store over the metadata job directory of ``paths``."""
        super().__init__(paths.metadata_jobs, StoredMetadataJob, _job_id)
        self._paths = paths

    def save_findings(self, job_id: str, findings: MetadataFindings) -> None:
        """Write the picture (when there is one), then ``result.json`` (atomic replace)."""
        if findings.artwork is not None:
            write_artwork(self._paths.metadata_artwork(job_id), findings.artwork)
        target = self._paths.metadata_result(job_id)
        target.parent.mkdir(parents=True, exist_ok=True)
        temp = target.with_name(target.name + ".tmp")
        temp.write_text(findings.result.model_dump_json(by_alias=True), encoding="utf-8")
        temp.replace(target)

    def result(self, job_id: str) -> MetadataResult | None:
        """The result of a completed job; None when unknown, unfinished or unreadable."""
        record = self.get(job_id)
        if record is None or record.job.status is not MetadataJobState.COMPLETED:
            return None
        try:
            raw = self._paths.metadata_result(job_id).read_text(encoding="utf-8")
            return MetadataResult.model_validate_json(raw)
        except (OSError, ValidationError):
            return None

    def artwork(self, job_id: str, variant: ArtworkVariant) -> Path | None:
        """The picture file of a completed job whose result has artwork, or None."""
        result = self.result(job_id)
        if result is None or result.artwork is None:
            return None
        path = artwork_file(self._paths.metadata_artwork(job_id), variant)
        return path if path.is_file() else None

    def unfinished(self) -> list[StoredMetadataJob]:
        """Every queued or running job, oldest first (re-run after a restart)."""
        pending = [r for r in self.all() if not r.job.status.is_terminal]
        return sorted(pending, key=lambda r: r.job.created_at)

    def expired(self, cutoff: datetime) -> list[str]:
        """Ids of terminal jobs last updated before ``cutoff``."""
        return [
            r.job.metadata_job_id
            for r in self.all()
            if r.job.status.is_terminal and r.job.updated_at < cutoff
        ]

    def remove(self, job_id: str) -> None:
        """Delete the record and the job's files."""
        self.delete(job_id)
        shutil.rmtree(self._paths.metadata_job_dir(job_id), ignore_errors=True)
