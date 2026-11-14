"""Tests of the pipeline runner and the FIFO job queue (T061)."""

import logging
import threading
from dataclasses import dataclass
from pathlib import Path

import pytest

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.queue import JobQueue
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.ports import AudioTooLongError, SourceUnavailableError
from stemstudio_worker.schemas import Job, JobState, SourceMetadata, SourceType

from .conftest import RecordFactory
from .fakes import GRID, FakeClock, Fakes, RecordingStore

JOB = "a" * 32
SHA = "e" * 64
S = JobState


@dataclass
class Env:
    store: RecordingStore
    fakes: Fakes
    paths: DataPaths
    runner: JobRunner

    def add(self, record: StoredJob) -> str:
        self.store.save(record)
        self.store.history.clear()
        return record.job.job_id

    def final(self, job_id: str = JOB) -> StoredJob:
        record = self.store.get(job_id)
        assert record is not None
        return record


@pytest.fixture
def env(tmp_path: Path) -> Env:
    paths = DataPaths(tmp_path)
    store = RecordingStore(paths.jobs)
    fakes = Fakes.create()
    return Env(
        store, fakes, paths, JobRunner(JobContext(store, paths, FakeClock()), fakes.adapters())
    )


def _url_job(make_record: RecordFactory) -> StoredJob:
    record = make_record()
    metadata = SourceMetadata(
        title="Song", artist="Artist", source_url="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
    )
    return record.model_copy(update={"job": record.job.model_copy(update={"metadata": metadata})})


def _upload_job(env: Env, make_record: RecordFactory) -> StoredJob:
    upload_dir = env.paths.uploads(JOB)
    upload_dir.mkdir(parents=True)
    (upload_dir / "input.mp3").write_bytes(b"mp3")
    return make_record(source_key="upload:" + "c" * 64).model_copy(
        update={"upload_file": "input.mp3"}
    )


def _stages(history: list[tuple[JobState, str, int]]) -> list[tuple[JobState, str]]:
    stages: list[tuple[JobState, str]] = []
    for status, stage, _ in history:
        if not stages or stages[-1] != (status, stage):
            stages.append((status, stage))
    return stages


def test_url_job_runs_every_stage_in_order(env: Env, make_record: RecordFactory) -> None:
    env.runner.process(env.add(_url_job(make_record)))

    assert env.fakes.calls.names == ["fetch", "normalize", "separate", "analyze"]
    assert _stages(env.store.history) == [
        (S.DOWNLOADING, "Downloading audio"),
        (S.DOWNLOADING, "Preparing audio"),
        (S.SEPARATING, "Separating stems"),
        (S.ANALYSING, "Analysing beats"),
        (S.COMPLETED, "Completed"),
    ]


@pytest.mark.parametrize(
    "case",
    [
        (S.DOWNLOADING, 0, 10),
        (S.SEPARATING, 10, 90),
        (S.ANALYSING, 90, 99),
        (S.COMPLETED, 100, 100),
    ],
)
def test_progress_stays_in_range_per_state_and_never_decreases(
    env: Env, make_record: RecordFactory, case: tuple[JobState, int, int]
) -> None:
    status, low, high = case
    env.runner.process(env.add(_url_job(make_record)))

    progress = [p for _, _, p in env.store.history]
    assert progress == sorted(progress)
    assert all(low <= p <= high for s, _, p in env.store.history if s is status)


def test_separation_progress_is_10_plus_80_times_fraction(
    env: Env, make_record: RecordFactory
) -> None:
    env.runner.process(env.add(_url_job(make_record)))

    separating = [p for s, _, p in env.store.history if s is S.SEPARATING]
    assert separating == [10, 30, 50, 90]


def test_completed_job_has_result_and_cached_stems(env: Env, make_record: RecordFactory) -> None:
    env.runner.process(env.add(_url_job(make_record)))

    record = env.final()
    assert (record.job.status, record.job.progress, record.job.error) == (S.COMPLETED, 100, None)
    result = record.result
    assert result is not None
    assert (result.job_id, result.source_key, result.model) == (
        JOB,
        "youtube:dQw4w9WgXcQ",
        "htdemucs",
    )
    assert (result.duration_seconds, result.audio_sha256, result.beat_grid) == (12.5, SHA, GRID)
    assert result.metadata == record.job.metadata
    assert result.completed_at == record.job.updated_at
    assert [(s.name, s.path, s.size_bytes) for s in result.stems] == [
        (n, f"/v1/jobs/{JOB}/stems/{n}", len(n) * 10) for n in ("vocals", "drums", "bass", "other")
    ]
    assert record.audio_sha256 == SHA
    stems_dir = env.paths.by_audio(SHA) / "stems"
    assert sorted(p.name for p in stems_dir.iterdir()) == [
        "bass.flac", "drums.flac", "other.flac", "vocals.flac"
    ]  # fmt: skip


def test_upload_job_skips_download_and_normalises_the_upload(
    env: Env, make_record: RecordFactory
) -> None:
    env.runner.process(env.add(_upload_job(env, make_record)))

    assert env.fakes.calls.names == ["normalize", "separate", "analyze"]
    assert env.fakes.calls.paths[0] == env.paths.uploads(JOB) / "input.mp3"
    assert env.store.history[0] == (S.DOWNLOADING, "Preparing audio", 0)
    assert env.final().job.status is S.COMPLETED


def test_worker_error_message_becomes_job_error(env: Env, make_record: RecordFactory) -> None:
    env.fakes.ingest.fail["normalize"] = AudioTooLongError("Audio is longer than 15 minutes")

    env.runner.process(env.add(_url_job(make_record)))

    job = env.final().job
    assert (job.status, job.stage, job.error) == (
        S.FAILED,
        "Failed",
        "Audio is longer than 15 minutes",
    )
    assert job.progress == env.store.history[-2][2]  # progress unchanged by the failure
    assert env.fakes.calls.names == ["fetch", "normalize"]


def test_unexpected_exception_becomes_unexpected_error_and_is_logged(
    env: Env, make_record: RecordFactory, caplog: pytest.LogCaptureFixture
) -> None:
    env.fakes.separator.error = ZeroDivisionError("bug with C:\\private\\path")

    with caplog.at_level(logging.ERROR):
        env.runner.process(env.add(_url_job(make_record)))

    assert env.final().job.error == "Unexpected error"
    assert "ZeroDivisionError" in caplog.text


@pytest.mark.parametrize("fail", [False, True])
def test_work_and_upload_directories_are_removed_when_terminal(
    env: Env, make_record: RecordFactory, fail: bool
) -> None:
    if fail:
        env.fakes.ingest.fail["normalize"] = SourceUnavailableError("gone")

    env.runner.process(env.add(_upload_job(env, make_record)))

    assert env.final().job.status is (S.FAILED if fail else S.COMPLETED)
    assert not env.paths.work(JOB).exists()
    assert not env.paths.uploads(JOB).exists()


def test_long_error_messages_are_clipped(env: Env, make_record: RecordFactory) -> None:
    env.fakes.ingest.fail["fetch"] = SourceUnavailableError("x" * 2000)

    env.runner.process(env.add(_url_job(make_record)))

    assert env.final().job.error == "x" * 1000


@pytest.mark.parametrize("status", [S.COMPLETED, S.FAILED])
def test_unknown_or_terminal_jobs_are_ignored(
    env: Env, make_record: RecordFactory, status: JobState
) -> None:
    env.add(make_record(status=status))

    env.runner.process(JOB)
    env.runner.process("f" * 32)

    assert env.fakes.calls.names == []
    assert env.store.history == []


def test_source_type_is_preserved(env: Env, make_record: RecordFactory) -> None:
    env.runner.process(env.add(_upload_job(env, make_record)))

    assert env.final().job.source_type is SourceType.UPLOAD


# ---------------------------------------------------------------- queue


class _ConcurrencyProbe:
    """Records the processing order and the peak number of concurrently running jobs."""

    def __init__(self) -> None:
        self.order: list[str] = []
        self.peak = 0
        self._active = 0
        self._lock = threading.Lock()

    def __call__(self, job_id: str) -> None:
        with self._lock:
            self._active += 1
            self.peak = max(self.peak, self._active)
        self.order.append(job_id)
        with self._lock:
            self._active -= 1


def test_queue_processes_fifo_one_at_a_time() -> None:
    probe = _ConcurrencyProbe()
    queue = JobQueue(probe)
    for job_id in ("1", "2", "3", "4"):
        queue.submit(job_id)

    queue.start()
    queue.join()
    queue.stop()

    assert probe.order == ["1", "2", "3", "4"]
    assert probe.peak == 1


def test_queue_survives_a_failing_job(caplog: pytest.LogCaptureFixture) -> None:
    done: list[str] = []

    def process(job_id: str) -> None:
        if job_id == "bad":
            raise RuntimeError("boom")
        done.append(job_id)

    queue = JobQueue(process)
    queue.start()
    queue.submit("bad")
    queue.submit("good")
    queue.join()
    queue.stop()

    assert done == ["good"]
    assert "boom" in caplog.text


def test_queue_stop_is_idempotent_and_ends_the_thread() -> None:
    queue = JobQueue(lambda job_id: None)
    queue.start()
    queue.start()
    assert queue.running

    queue.stop()
    queue.stop()

    assert not queue.running


# ---------------------------------------------------------------- metadata from the download

YOUTUBE_URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"


def _spotify_job(make_record: RecordFactory) -> StoredJob:
    record = make_record(source_key="spotify:4cOdK2wGLETKBW3PvgPWqT")
    metadata = SourceMetadata(title="Track", artist="Band", source_url=YOUTUBE_URL)
    return record.model_copy(update={"job": record.job.model_copy(update={"metadata": metadata})})


def test_youtube_job_takes_title_and_artist_from_the_download(
    env: Env, make_record: RecordFactory
) -> None:
    env.fakes.ingest.fetched_title, env.fakes.ingest.fetched_artist = "Real Title", "Uploader"

    env.runner.process(env.add(_url_job(make_record)))

    record = env.final()
    expected = SourceMetadata(title="Real Title", artist="Uploader", source_url=YOUTUBE_URL)
    assert record.job.metadata == expected
    assert record.result is not None
    assert record.result.metadata == expected


def test_downloaded_title_is_kept_when_a_later_stage_fails(
    env: Env, make_record: RecordFactory
) -> None:
    env.fakes.ingest.fetched_title = "Real Title"
    env.fakes.ingest.fail["normalize"] = AudioTooLongError("Audio is longer than 15 minutes")

    env.runner.process(env.add(_url_job(make_record)))

    metadata = env.final().job.metadata
    assert metadata is not None
    assert (metadata.title, metadata.artist) == ("Real Title", "Artist")


def test_long_downloaded_title_and_artist_are_clipped(env: Env, make_record: RecordFactory) -> None:
    env.fakes.ingest.fetched_title, env.fakes.ingest.fetched_artist = "t" * 600, "a" * 600

    env.runner.process(env.add(_url_job(make_record)))

    metadata = env.final().job.metadata
    assert metadata is not None
    assert (metadata.title, metadata.artist) == ("t" * 500, "a" * 500)


def test_spotify_metadata_stays_authoritative(env: Env, make_record: RecordFactory) -> None:
    env.fakes.ingest.fetched_title, env.fakes.ingest.fetched_artist = "Video title", "Channel"

    env.runner.process(env.add(_spotify_job(make_record)))

    record = env.final()
    assert record.job.metadata == SourceMetadata(
        title="Track", artist="Band", source_url=YOUTUBE_URL
    )
    assert record.result is not None
    assert record.result.metadata == record.job.metadata


def test_download_without_title_keeps_the_resolved_metadata(
    env: Env, make_record: RecordFactory
) -> None:
    env.runner.process(env.add(_url_job(make_record)))

    assert env.final().job.metadata == SourceMetadata(
        title="Song", artist="Artist", source_url=YOUTUBE_URL
    )


def test_youtube_job_without_metadata_gets_the_downloaded_title(
    env: Env, make_record: RecordFactory
) -> None:
    env.fakes.ingest.fetched_title = "Real Title"

    record = make_record()
    job: Job = record.job.model_copy(update={"metadata": None})

    env.runner.process(env.add(record.model_copy(update={"job": job})))

    metadata = env.final().job.metadata
    assert metadata is not None
    assert (metadata.title, metadata.artist, metadata.source_url) == ("Real Title", None, None)
