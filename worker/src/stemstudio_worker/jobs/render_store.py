"""Render job records (feature 007, data-model.md 2.5).

``render-jobs/<id>.json`` holds the job and its request; ``render-jobs/<id>/output.<ext>`` the
rendered file. Restart, delete and sweep follow ``lane_store.LaneJobStore`` (unfinished jobs
fail with "interrupted by restart"; terminal jobs are swept after 24 hours).
"""

from stemstudio_worker.jobs.lane_store import MAX_AGE, LaneJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import ContractModel, RenderJob, RenderJobRequest

__all__ = ["MAX_AGE", "RenderJobStore", "StoredRenderJob"]


class StoredRenderJob(ContractModel):
    """Content of ``render-jobs/<id>.json``: the wire job and its validated request."""

    job: RenderJob
    request: RenderJobRequest


def _job_id(record: StoredRenderJob) -> str:
    return record.job.render_job_id


class RenderJobStore(LaneJobStore[StoredRenderJob]):
    """Persists render jobs below ``paths.render_jobs``.

    The output folder is named after the job (the renderer's dither seed).
    """

    def __init__(self, paths: DataPaths) -> None:
        """Create a store over the render job directory of ``paths``."""
        super().__init__(paths.render_jobs, StoredRenderJob, _job_id)
