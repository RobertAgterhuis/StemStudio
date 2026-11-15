"""The metadata job lane (feature 005, ADR-014 D3).

Metadata jobs are network-only: they run one at a time on a :class:`LaneScheduler` of their own,
never behind a separation and never on the GPU. State machine ``queued -> running ->
completed | failed`` (no cancel). A worker restart runs ``queued`` and ``running`` jobs again
from the start (a lookup keeps no partial state). Terminal jobs are swept after 24 hours.
"""

import logging
from datetime import timedelta
from pathlib import Path
from typing import Any

from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.metadata_store import MetadataJobStore, StoredMetadataJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import (
    MetadataFindings,
    MetadataLookup,
    MetadataLookupError,
    MetadataQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    METADATA_FAILED_TEXT,
    ArtworkVariant,
    CreateMetadataJobRequest,
    MetadataJob,
    MetadataJobState,
    MetadataResult,
)

_LOG = logging.getLogger(__name__)

QUEUED = "Queued"
SEARCHING = "Searching MusicBrainz"
COMPLETED = "Completed"
FAILED = "Failed"
MAX_AGE = timedelta(hours=24)
"""Terminal metadata jobs older than this are swept with their files."""

_MAX_STAGE = 200
_S = MetadataJobState


def query_of(request: CreateMetadataJobRequest) -> MetadataQuery:
    """The lookup query of a request (``standardJobId`` is only for correlation)."""
    return MetadataQuery(
        title=request.title, artist=request.artist, album=request.album,
        duration_seconds=request.duration_seconds, isrc=request.isrc,
    )  # fmt: skip


class DisabledLookup:
    """Lookup used when no ``metadata`` package is installed: every job is ``disabled``."""

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:  # noqa: ARG002 - port signature
        """Complete without any request (outcome ``disabled``)."""
        return MetadataFindings(MetadataResult(outcome="disabled"))


class _Tracker:
    """Applies state and stage changes of one metadata job to the store."""

    def __init__(self, store: MetadataJobStore, job_id: str, clock: Clock) -> None:
        self._store = store
        self._job_id = job_id
        self._clock = clock

    def move(self, status: MetadataJobState, stage: str, error: str | None = None) -> None:
        record = self._store.get(self._job_id)
        if record is None:  # pragma: no cover - jobs are only swept when terminal
            return
        job = record.job
        if (job.status, job.stage, job.error) != (status, stage[:_MAX_STAGE], error):
            self._save(record, status=status, stage=stage[:_MAX_STAGE], error=error)

    def stage(self, fraction: float, stage: str) -> None:  # noqa: ARG002 - port signature
        """``ProgressCallback`` of the lookup: the running job shows the lookup's stage."""
        self.move(_S.RUNNING, stage)

    def _save(self, record: StoredMetadataJob, **changes: Any) -> None:
        job = record.job.model_copy(update={**changes, "updated_at": self._clock()})
        self._store.save(record.model_copy(update={"job": job}))


class MetadataRunner:
    """Runs one metadata job to a terminal state (called on the lane thread)."""

    def __init__(self, store: MetadataJobStore, lookup: MetadataLookup, clock: Clock) -> None:
        """Run jobs of ``store`` through ``lookup``, stamping times with ``clock``."""
        self._store = store
        self._lookup = lookup
        self._clock = clock

    def process(self, job_id: str) -> None:
        """Look the job up and store its findings; unknown and terminal jobs are ignored.

        A failed recording search (``MetadataLookupError``) and any unexpected error (logged)
        fail the job with the fixed text of data-model.md 2.7.
        """
        record = self._store.get(job_id)
        if record is None or record.job.status.is_terminal:
            return
        tracker = _Tracker(self._store, job_id, self._clock)
        tracker.move(_S.RUNNING, SEARCHING)
        try:
            self._run(record, tracker)
        except MetadataLookupError:
            tracker.move(_S.FAILED, FAILED, METADATA_FAILED_TEXT)
        except Exception:
            _LOG.exception("Metadata job %s failed unexpectedly", job_id)
            tracker.move(_S.FAILED, FAILED, METADATA_FAILED_TEXT)

    def _run(self, record: StoredMetadataJob, tracker: _Tracker) -> None:
        findings = self._lookup.lookup(query_of(record.request), tracker.stage)
        self._store.save_findings(record.job.metadata_job_id, findings)
        tracker.move(_S.COMPLETED, COMPLETED)


class MetadataLane:
    """Creates, reads and sweeps metadata jobs and runs them on their own lane thread."""

    def __init__(self, store: MetadataJobStore, runner: MetadataRunner, clock: Clock) -> None:
        """Wire a loaded ``store`` and its ``runner``; use :meth:`start` in production."""
        self._store = store
        self.runner = runner
        self._clock = clock
        self._lane = LaneScheduler("metadata", runner.process)

    @classmethod
    def start(cls, paths: DataPaths, lookup: MetadataLookup, clock: Clock) -> "MetadataLane":
        """Load the jobs, queue the unfinished ones again (oldest first) and start the lane."""
        store = MetadataJobStore(paths)
        store.load_all()
        lane = cls(store, MetadataRunner(store, lookup, clock), clock)
        for record in store.unfinished():
            _Tracker(store, record.job.metadata_job_id, clock).move(_S.QUEUED, QUEUED)
            lane._lane.submit(record.job.metadata_job_id)
        lane._lane.start()
        return lane

    def create(self, request: CreateMetadataJobRequest) -> MetadataJob:
        """Persist a queued job for ``request`` and queue it on the lane."""
        now = self._clock()
        job = MetadataJob(metadata_job_id=new_id(), status=_S.QUEUED, stage=QUEUED,
                          created_at=now, updated_at=now)  # fmt: skip
        self._store.save(StoredMetadataJob(job=job, request=request))
        self._lane.submit(job.metadata_job_id)
        return job

    def get(self, job_id: str) -> MetadataJob | None:
        """The job with ``job_id``, or None."""
        record = self._store.get(job_id)
        return None if record is None else record.job

    def result(self, job_id: str) -> MetadataResult | None:
        """The result of a completed job, or None."""
        return self._store.result(job_id)

    def artwork(self, job_id: str, variant: ArtworkVariant) -> Path | None:
        """The picture file of a completed job whose result has artwork, or None."""
        return self._store.artwork(job_id, variant)

    def sweep(self) -> list[str]:
        """Remove terminal jobs (record, result and picture) older than 24 hours."""
        expired = self._store.expired(self._clock() - MAX_AGE)
        for job_id in expired:
            self._store.remove(job_id)
        return expired

    def join(self, timeout: float | None = None) -> bool:
        """Block until the lane is idle; False on timeout."""
        return self._lane.join(timeout)

    def stop(self, timeout: float | None = None) -> None:
        """Stop the lane thread; unfinished jobs run again at the next start."""
        self._lane.stop(timeout)
