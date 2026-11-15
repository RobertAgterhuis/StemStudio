"""Tests of the per-source and per-audio result cache and of duplicate submissions (T063)."""

import json
import threading
from pathlib import Path

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.jobs.service import JobService
from stemstudio_worker.ports import SourceInfo
from stemstudio_worker.schemas import Job, JobState, SourceType

from .fakes import FakeClock, Fakes, RecordingStore

SHA = "e" * 64
KEY = "youtube:dQw4w9WgXcQ"
S = JobState
SOURCE = SourceInfo(
    SourceType.YOUTUBE, KEY, "Song", "Artist", "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
)
OTHER = SourceInfo(
    SourceType.YOUTUBE,
    "youtube:aaaaaaaaaaa",
    "Other",
    None,
    "https://www.youtube.com/watch?v=aaaaaaaaaaa",
)


class Env:
    def __init__(self, root: Path) -> None:
        self.paths = DataPaths(root)
        self.store = RecordingStore(self.paths.jobs)
        self.fakes = Fakes.create()
        self.submitted: list[str] = []
        clock = FakeClock()
        self.runner = JobRunner(JobContext(self.store, self.paths, clock), self.fakes.adapters())
        self.service = JobService(self.store, self.submitted.append, clock)

    def run(self, source: SourceInfo) -> Job:
        job, created = self.service.create(source)
        assert created
        self.fakes.calls.names.clear()
        self.store.history.clear()
        self.runner.process(job.job_id)
        record = self.store.get(job.job_id)
        assert record is not None
        return record.job


@pytest.fixture
def env(tmp_path: Path) -> Env:
    return Env(tmp_path)


def _fail(env: Env, job: Job) -> None:
    record = env.store.get(job.job_id)
    assert record is not None
    env.store.save(record.model_copy(update={"job": job.model_copy(update={"status": S.FAILED})}))


def test_full_run_writes_source_pointer_and_audio_entry(env: Env) -> None:
    env.run(SOURCE)

    pointer = json.loads(env.paths.by_source(KEY).read_text(encoding="utf-8"))
    assert pointer == {"audioSha256": SHA}
    assert env.paths.by_source(KEY).name == "youtube_dQw4w9WgXcQ.json"
    assert (env.paths.by_audio(SHA) / "result.json").is_file()


def test_source_cache_hit_completes_from_queued_without_processing(env: Env) -> None:
    first = env.run(SOURCE)
    _fail(env, first)  # a failed job no longer blocks a new one for the same key

    second = env.run(SOURCE)

    assert env.fakes.calls.names == []
    assert env.store.history == [(S.COMPLETED, "Completed", 100)]
    record = env.store.get(second.job_id)
    assert record is not None
    assert record.result is not None
    assert record.result.job_id == second.job_id
    assert record.result.stems[0].path == f"/v1/jobs/{second.job_id}/stems/vocals"
    assert record.audio_sha256 == SHA


def test_audio_cache_hit_after_normalisation_skips_separation(env: Env) -> None:
    env.run(SOURCE)

    other = env.run(OTHER)  # different URL, identical normalised audio

    assert env.fakes.calls.names == ["fetch", "normalize"]
    assert [s for s, _, _ in env.store.history][-1] is S.COMPLETED
    assert {s for s, _, _ in env.store.history} == {S.DOWNLOADING, S.COMPLETED}
    record = env.store.get(other.job_id)
    assert record is not None
    assert record.result is not None
    assert record.result.metadata.title == "Other"
    assert record.result.source_key == "youtube:aaaaaaaaaaa"
    assert json.loads(env.paths.by_source(OTHER.source_key).read_text(encoding="utf-8")) == {
        "audioSha256": SHA
    }


@pytest.mark.parametrize(
    ("damage", "separated"),
    [("missing-entry", True), ("corrupt-pointer", False), ("corrupt-result", True)],
)
def test_damaged_cache_is_treated_as_a_miss(env: Env, damage: str, separated: bool) -> None:
    _fail(env, env.run(SOURCE))
    if damage == "missing-entry":
        (env.paths.by_audio(SHA) / "result.json").unlink()
    elif damage == "corrupt-pointer":
        env.paths.by_source(KEY).write_text("{oops", encoding="utf-8")
    else:
        (env.paths.by_audio(SHA) / "result.json").write_text("[]", encoding="utf-8")

    job = env.run(SOURCE)

    assert job.status is S.COMPLETED
    assert ("separate" in env.fakes.calls.names) is separated  # a bad pointer still hits by audio


# ---------------------------------------------------------------- duplicate submissions


def test_create_queues_new_job_with_metadata(env: Env) -> None:
    job, created = env.service.create(SOURCE)

    assert created
    assert env.submitted == [job.job_id]
    assert (job.status, job.stage, job.progress) == (S.QUEUED, "Queued", 0)
    assert job.metadata is not None
    assert job.metadata.source_url == SOURCE.youtube_url
    assert job.created_at == job.updated_at


def test_create_returns_existing_non_failed_job(env: Env) -> None:
    first, _ = env.service.create(SOURCE)

    again, created = env.service.create(SOURCE)

    assert not created
    assert again == first
    assert env.submitted == [first.job_id]


def test_create_after_failure_queues_a_new_job(env: Env) -> None:
    first, _ = env.service.create(SOURCE)
    _fail(env, first)

    second, created = env.service.create(SOURCE)

    assert created
    assert second.job_id != first.job_id


def test_create_keeps_given_job_id_and_upload_file(env: Env) -> None:
    upload = SourceInfo(SourceType.UPLOAD, "upload:" + "c" * 64, "My Song", None, None)

    job, _ = env.service.create(upload, job_id="b" * 32, upload_file="input.flac")

    record = env.store.get("b" * 32)
    assert record is not None
    assert (job.job_id, record.upload_file) == ("b" * 32, "input.flac")


def test_concurrent_duplicate_creates_share_one_job(env: Env) -> None:
    barrier = threading.Barrier(8)
    results: list[tuple[Job, bool]] = []

    def submit() -> None:
        barrier.wait()
        results.append(env.service.create(SOURCE))

    threads = [threading.Thread(target=submit) for _ in range(8)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert sum(created for _, created in results) == 1
    assert len({job.job_id for job, _ in results}) == 1
    assert len(env.submitted) == 1


def test_source_cache_hit_reuses_the_downloaded_youtube_title(env: Env) -> None:
    env.fakes.ingest.fetched_title = "Real Title"
    _fail(env, env.run(SOURCE))

    second = env.run(SOURCE)  # resolved again with the placeholder title "Song"

    record = env.store.get(second.job_id)
    assert record is not None
    assert record.result is not None
    assert record.job.metadata == record.result.metadata
    assert record.result.metadata.title == "Real Title"


def test_source_cache_hit_keeps_spotify_metadata(env: Env) -> None:
    spotify = SourceInfo(
        SourceType.SPOTIFY, "spotify:4cOdK2wGLETKBW3PvgPWqT", "Track", "Band", SOURCE.youtube_url
    )
    env.run(SOURCE)  # cached under the YouTube key; same audio
    first = env.run(spotify)
    _fail(env, first)

    second = env.run(spotify)

    assert second.metadata is not None
    assert (second.metadata.title, second.metadata.artist) == ("Track", "Band")
