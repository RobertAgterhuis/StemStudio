"""Start-up reconciliation: jobs interrupted by a restart are failed (FR-009a, FR-019)."""

import logging
import shutil
from collections.abc import Iterable
from pathlib import Path

from stemstudio_worker.jobs.detail_progress import DetailTracker
from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import INTERRUPTED, Clock, JobTracker
from stemstudio_worker.jobs.store import JobStore

_LOG = logging.getLogger(__name__)


def _remove(directories: Iterable[Path]) -> None:
    for directory in directories:
        shutil.rmtree(directory, ignore_errors=True)


def _log(interrupted: list[str], kind: str) -> list[str]:
    if interrupted:
        _LOG.warning("Marked %d %s(s) as interrupted by restart", len(interrupted), kind)
    return interrupted


def reconcile(store: JobStore, paths: DataPaths, clock: Clock) -> list[str]:
    """Fail every non-terminal job with "interrupted by restart" and remove leftover files.

    Must run before the scheduler starts. Removes ``work/<id>/`` and ``uploads/<id>/`` of the
    interrupted jobs and every unfinished ``cache/by-audio/*.partial`` directory.

    Args:
        store: The loaded job store.
        paths: The data directory layout.
        clock: Current UTC time.

    Returns:
        Ids of the jobs that were failed.
    """
    interrupted = [r.job.job_id for r in store.all() if not r.job.status.is_terminal]
    for job_id in interrupted:
        JobTracker(store, job_id, clock).fail(INTERRUPTED)
        _remove((paths.work(job_id), paths.uploads(job_id)))
    _remove(paths.audio_cache.glob("*.partial"))
    return _log(interrupted, "job")


def reconcile_detail_jobs(details: DetailJobStore, paths: DataPaths, clock: Clock) -> list[str]:
    """Fail every non-terminal detail job with "interrupted by restart".

    Finished parent checkpoints (``detail.partial/<parent>/``) are kept so a retry resumes from
    them; unfinished ``<parent>.tmp`` directories are removed.

    Returns:
        Ids of the detail jobs that were failed.
    """
    interrupted = [r.job.detail_job_id for r in details.all() if not r.job.status.is_terminal]
    for detail_id in interrupted:
        DetailTracker(details, detail_id, clock).fail(INTERRUPTED)
    _remove(paths.audio_cache.glob("*/detail.partial/*.tmp"))
    return _log(interrupted, "detail job")
