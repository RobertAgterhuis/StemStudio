"""Start-up reconciliation: jobs interrupted by a restart are failed (FR-009a)."""

import logging
import shutil

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import INTERRUPTED, Clock, JobTracker
from stemstudio_worker.jobs.store import JobStore

_LOG = logging.getLogger(__name__)


def reconcile(store: JobStore, paths: DataPaths, clock: Clock) -> list[str]:
    """Fail every non-terminal job with "interrupted by restart" and remove leftover files.

    Must run before the queue starts. Removes ``work/<id>/`` and ``uploads/<id>/`` of the
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
        shutil.rmtree(paths.work(job_id), ignore_errors=True)
        shutil.rmtree(paths.uploads(job_id), ignore_errors=True)
    for partial in paths.audio_cache.glob("*.partial"):
        shutil.rmtree(partial, ignore_errors=True)
    if interrupted:
        _LOG.warning("Marked %d job(s) as interrupted by restart", len(interrupted))
    return interrupted
