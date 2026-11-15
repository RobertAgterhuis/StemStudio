"""Transcription job records and score files (feature 008, data-model.md 2.9).

``transcription-jobs/<id>.json`` holds the job and its request; ``transcription-jobs/<id>/``
the three files ``score.pdf``, ``score.musicxml`` and ``score.mid``. Restart, delete and sweep
follow the render jobs (:class:`~stemstudio_worker.jobs.lane_store.LaneJobStore`).
"""

import hashlib
from collections.abc import Mapping
from pathlib import Path

from stemstudio_worker.jobs.lane_store import LaneJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import (
    SCORE_FILES,
    ContractModel,
    ScoreFileRole,
    TranscriptionFile,
    TranscriptionJob,
    TranscriptionJobRequest,
)


class StoredTranscriptionJob(ContractModel):
    """Content of ``transcription-jobs/<id>.json``: the wire job and its validated request."""

    job: TranscriptionJob
    request: TranscriptionJobRequest


def _job_id(record: StoredTranscriptionJob) -> str:
    return record.job.transcription_job_id


class TranscriptionJobStore(LaneJobStore[StoredTranscriptionJob]):
    """Persists transcription jobs and their files below ``paths.transcription_jobs``."""

    def __init__(self, paths: DataPaths) -> None:
        """Create a store over the transcription job directory of ``paths``."""
        super().__init__(paths.transcription_jobs, StoredTranscriptionJob, _job_id)

    def save_files(
        self, job_id: str, contents: Mapping[ScoreFileRole, bytes]
    ) -> list[TranscriptionFile]:
        """Write the three files of a job and describe them (order pdf, musicxml, midi)."""
        folder = self.output_dir(job_id)
        folder.mkdir(parents=True, exist_ok=True)
        files = []
        for role, (name, content_type) in SCORE_FILES.items():
            data = contents[role]
            (folder / name).write_bytes(data)
            files.append(TranscriptionFile(role=role, file_name=name, content_type=content_type,
                                           size_bytes=len(data),
                                           sha256=hashlib.sha256(data).hexdigest()))  # fmt: skip
        return files

    def file(self, job_id: str, role: ScoreFileRole) -> Path | None:
        """The file of ``role`` of a job, or None when it does not exist."""
        path = self.output_dir(job_id) / SCORE_FILES[role][0]
        return path if path.is_file() else None
