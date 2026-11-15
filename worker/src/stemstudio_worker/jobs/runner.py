"""Pipeline runner: fetch or accept -> normalise -> separate -> analyse -> cache -> complete."""

import logging
import shutil
from dataclasses import dataclass
from pathlib import Path

from stemstudio_worker.jobs import progress as p
from stemstudio_worker.jobs.cache import ResultCache, rebind, stem_infos
from stemstudio_worker.jobs.models import StoredJob, fetched_metadata, source_of
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    NormalizedAudio,
    PipelineAdapters,
    SeparationResult,
    WorkerError,
)
from stemstudio_worker.schemas import (
    CPU_DEVICE,
    DeviceInfo,
    JobDevice,
    JobResult,
    JobState,
    SourceMetadata,
)

_LOG = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class JobContext:
    """Where jobs live and how time is read.

    Attributes:
        store: The job store.
        paths: The data directory layout.
        clock: Current UTC time.
        device: The worker's device from the start-up probe; a separation is planned on it.
    """

    store: JobStore
    paths: DataPaths
    clock: p.Clock
    device: DeviceInfo = CPU_DEVICE


class JobRunner:
    """Processes one job at a time through the adapters (called by the queue thread)."""

    def __init__(self, context: JobContext, adapters: PipelineAdapters) -> None:
        """Create a runner over ``context`` (store, paths, clock) using ``adapters``."""
        self._store = context.store
        self._adapters = adapters
        self._paths = context.paths
        self._clock = context.clock
        self._planned = JobDevice(kind=context.device.kind, name=context.device.name)
        self._cache = ResultCache(context.paths)

    def process(self, job_id: str) -> None:
        """Run the job to a terminal state; unknown and terminal jobs are ignored.

        ``WorkerError`` messages become the job error; any other exception is logged and
        reported as "Unexpected error". Temporary and upload files are removed afterwards.
        """
        record = self._store.get(job_id)
        if record is None or record.job.status.is_terminal:
            return
        try:
            self._guarded(record, p.JobTracker(self._store, job_id, self._clock))
        finally:
            self._cleanup(job_id)

    def _guarded(self, record: StoredJob, tracker: p.JobTracker) -> None:
        """Run the job; failures become its error text (unexpected ones are logged)."""
        try:
            self._run(record, tracker)
        except WorkerError as exc:
            tracker.fail(str(exc) or p.UNEXPECTED_ERROR)
        except Exception:
            _LOG.exception("Job %s failed unexpectedly", record.job.job_id)
            tracker.fail(p.UNEXPECTED_ERROR)

    def _run(self, record: StoredJob, tracker: p.JobTracker) -> None:
        key = record.job.source_key
        result = self._cache.lookup_source(key) or self._from_audio(record, tracker)
        self._cache.link_source(key, result.audio_sha256)
        tracker.complete(rebind(result, tracker.job, self._clock()))

    def _from_audio(self, record: StoredJob, tracker: p.JobTracker) -> JobResult:
        audio = self._prepare(record, tracker)
        return self._cache.lookup_audio(audio.sha256) or self._produce(record, audio, tracker)

    def _produce(
        self, record: StoredJob, audio: NormalizedAudio, tracker: p.JobTracker
    ) -> JobResult:
        separation = self._separate(record.job.job_id, audio, tracker)
        tracker.update(JobState.ANALYSING, p.ANALYSING, p.ANALYSIS_START)
        grid = self._adapters.analyzer.analyze(audio.wav_path)
        result = JobResult(
            job_id=record.job.job_id, source_key=record.job.source_key,
            metadata=tracker.job.metadata or SourceMetadata(title="Untitled"),
            duration_seconds=audio.duration_seconds, audio_sha256=audio.sha256,
            model=separation.model, stems=stem_infos(record.job.job_id, separation.stems),
            beat_grid=grid, completed_at=self._clock(),
        )  # fmt: skip
        self._cache.store(result, separation.stems)
        return result

    def _prepare(self, record: StoredJob, tracker: p.JobTracker) -> NormalizedAudio:
        work = self._paths.work(record.job.job_id)
        work.mkdir(parents=True, exist_ok=True)
        source_file = self._input_file(record, work, tracker)
        tracker.update(JobState.DOWNLOADING, p.PREPARING, 0)
        return self._adapters.ingest.normalizer.normalize(source_file, work / "audio.wav")

    def _input_file(self, record: StoredJob, work: Path, tracker: p.JobTracker) -> Path:
        if record.upload_file is not None:
            return self._paths.uploads(record.job.job_id) / record.upload_file
        tracker.update(JobState.DOWNLOADING, p.DOWNLOADING, 0)
        report = tracker.callback(JobState.DOWNLOADING, p.DOWNLOADING, p.download_percent)
        fetched = self._adapters.ingest.fetcher.fetch(source_of(record.job), work, report)
        metadata = fetched_metadata(record.job, fetched)
        if metadata is not None:
            tracker.set_metadata(metadata)
        return fetched.path

    def _separate(
        self, job_id: str, audio: NormalizedAudio, tracker: p.JobTracker
    ) -> SeparationResult:
        out_dir = self._paths.work(job_id) / "stems"
        out_dir.mkdir(parents=True, exist_ok=True)
        tracker.set_device(self._planned)
        tracker.update(JobState.SEPARATING, p.SEPARATING, p.SEPARATION_START)
        report = tracker.callback(JobState.SEPARATING, p.SEPARATING, p.separation_percent)
        separation = self._adapters.separator.separate(audio.wav_path, out_dir, report)
        if separation.device is not None:
            tracker.set_device(separation.device)
        return separation

    def _cleanup(self, job_id: str) -> None:
        shutil.rmtree(self._paths.work(job_id), ignore_errors=True)
        shutil.rmtree(self._paths.uploads(job_id), ignore_errors=True)
