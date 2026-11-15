"""Tests of the metadata job lane, its store and the 24 h sweep (005 T014, ADR-014 D3)."""

import json
import threading
from collections.abc import Iterator
from datetime import datetime, timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.metadata_lane import MetadataLane
from stemstudio_worker.jobs.metadata_store import MetadataJobStore, StoredMetadataJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.scheduler import PriorityScheduler
from stemstudio_worker.ports import MetadataLookupError, MetadataQuery
from stemstudio_worker.schemas import (
    METADATA_FAILED_TEXT,
    CreateMetadataJobRequest,
    MetadataJob,
    MetadataJobState,
)

from .conftest import T0
from .metadata_fakes import COVER, FOUND, REQUEST, THUMB, WAIT, FakeLookup

S = MetadataJobState


class Clock:
    """A settable UTC clock (starts at T0)."""

    def __init__(self) -> None:
        self.now = T0

    def __call__(self) -> datetime:
        return self.now


class Env:
    """A data directory, a clock and a fake lookup; starts lanes and stops them afterwards."""

    def __init__(self, root: Path) -> None:
        self.paths = DataPaths(root)
        self.clock = Clock()
        self.lookup = FakeLookup()
        self.lanes: list[MetadataLane] = []

    def start(self) -> MetadataLane:
        lane = MetadataLane.start(self.paths, self.lookup, self.clock)
        self.lanes.append(lane)
        return lane

    def seed(self, job_id: str, status: MetadataJobState, age: timedelta = timedelta()) -> None:
        """Persist a metadata job record as an earlier worker run would have left it."""
        stamp = T0 - age
        job = MetadataJob(metadata_job_id=job_id, status=status, stage="Searching MusicBrainz",
                          created_at=stamp, updated_at=stamp)  # fmt: skip
        MetadataJobStore(self.paths).save(StoredMetadataJob(job=job, request=REQUEST))


@pytest.fixture
def env(tmp_path: Path) -> Iterator[Env]:
    environment = Env(tmp_path)
    yield environment
    for lane in environment.lanes:
        lane.stop(WAIT)


def _done(lane: MetadataLane) -> None:
    assert lane.join(WAIT), "metadata lane did not become idle"


# ------------------------------------------------------------------ lifecycle


def test_create_queues_a_persisted_job(env: Env) -> None:
    release = env.lookup.block()
    lane = env.start()

    job = lane.create(REQUEST)

    assert (job.status, job.stage, job.error) == (S.QUEUED, "Queued", None)
    assert job.created_at == job.updated_at == T0
    stored = json.loads((env.paths.metadata_jobs / f"{job.metadata_job_id}.json").read_text())
    assert stored["job"]["metadataJobId"] == job.metadata_job_id
    assert stored["request"]["title"] == "Hero of the Day"
    release.set()
    _done(lane)


def test_a_job_runs_with_the_lookup_stages_then_completes(env: Env) -> None:
    release = env.lookup.block()
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    assert env.lookup.entered.wait(WAIT)

    running = lane.get(job_id)
    assert running is not None
    assert (running.status, running.stage) == (S.RUNNING, "Searching MusicBrainz")
    release.set()
    _done(lane)

    done = lane.get(job_id)
    assert done is not None
    assert (done.status, done.stage, done.error) == (S.COMPLETED, "Completed", None)
    result = lane.result(job_id)
    assert result is not None
    assert result.outcome == "not-found"
    assert env.lookup.queries == [MetadataQuery(title="Hero of the Day", artist="Metallica")]


def test_the_query_carries_every_request_field(env: Env) -> None:
    lane = env.start()
    request = CreateMetadataJobRequest(
        title="T", artist="A", album="B", duration_seconds=262.3, isrc="USEE19600011",
        standard_job_id="c" * 32,
    )  # fmt: skip
    lane.create(request)
    _done(lane)

    assert env.lookup.queries == [MetadataQuery("T", "A", "B", 262.3, "USEE19600011")]


def test_found_results_store_the_cover_art_files(env: Env) -> None:
    env.lookup.findings = FOUND
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    _done(lane)

    result = lane.result(job_id)
    assert result is not None
    assert result.artwork == FOUND.result.artwork
    cover, thumb = lane.artwork(job_id, "cover"), lane.artwork(job_id, "thumb")
    assert cover is not None
    assert thumb is not None
    assert (cover.read_bytes(), thumb.read_bytes()) == (COVER, THUMB)
    assert cover.parent == env.paths.metadata_artwork(job_id)
    assert env.paths.metadata_result(job_id).is_file()


def test_artwork_is_none_without_a_picture_or_for_unknown_jobs(env: Env) -> None:
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    _done(lane)

    assert lane.artwork(job_id, "cover") is None
    assert lane.artwork("f" * 32, "cover") is None
    assert lane.artwork("not-an-id", "thumb") is None


def test_a_failed_recording_search_fails_the_job_with_the_fixed_text(env: Env) -> None:
    env.lookup.error = MetadataLookupError("MusicBrainz answered 503 three times")
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    _done(lane)

    job = lane.get(job_id)
    assert job is not None
    assert (job.status, job.stage, job.error) == (S.FAILED, "Failed", METADATA_FAILED_TEXT)
    assert lane.result(job_id) is None


def test_an_unexpected_error_fails_only_that_job(
    env: Env, caplog: pytest.LogCaptureFixture
) -> None:
    env.lookup.error = RuntimeError("boom")
    lane = env.start()
    first = lane.create(REQUEST).metadata_job_id
    _done(lane)
    env.lookup.error = None
    second = lane.create(REQUEST).metadata_job_id
    _done(lane)

    failed, completed = lane.get(first), lane.get(second)
    assert failed is not None
    assert completed is not None
    assert (failed.status, failed.error) == (S.FAILED, METADATA_FAILED_TEXT)
    assert completed.status is S.COMPLETED
    assert "failed unexpectedly" in caplog.text


def test_unknown_ids_and_unfinished_results_are_none(env: Env) -> None:
    release = env.lookup.block()
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id

    assert lane.get("0" * 32) is None
    assert lane.get("../etc") is None
    assert lane.result(job_id) is None
    assert lane.result("0" * 32) is None
    release.set()
    _done(lane)


def test_a_terminal_job_is_not_run_again_when_submitted_twice(env: Env) -> None:
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    _done(lane)

    lane.runner.process(job_id)
    lane.runner.process("0" * 32)

    assert len(env.lookup.queries) == 1


# ------------------------------------------------------------------ restart


@pytest.mark.parametrize("status", [S.QUEUED, S.RUNNING])
def test_restart_runs_unfinished_jobs_again_from_the_start(
    env: Env, status: MetadataJobState
) -> None:
    env.seed("1" * 32, status)
    lane = env.start()
    _done(lane)

    job = lane.get("1" * 32)
    assert job is not None
    assert job.status is S.COMPLETED
    assert env.lookup.queries == [MetadataQuery(title="Hero of the Day", artist="Metallica")]


def test_restart_requeues_running_jobs_before_the_lane_starts(env: Env) -> None:
    env.seed("1" * 32, S.RUNNING)
    release = env.lookup.block()
    env.seed("2" * 32, S.QUEUED, age=timedelta(seconds=5))
    lane = env.start()
    assert env.lookup.entered.wait(WAIT)

    waiting = lane.get("1" * 32)
    assert waiting is not None
    assert (waiting.status, waiting.stage) == (S.QUEUED, "Queued")
    release.set()
    _done(lane)
    assert len(env.lookup.queries) == 2


@pytest.mark.parametrize("status", [S.COMPLETED, S.FAILED])
def test_restart_leaves_terminal_jobs_alone(env: Env, status: MetadataJobState) -> None:
    env.seed("3" * 32, status)
    lane = env.start()
    _done(lane)

    job = lane.get("3" * 32)
    assert job is not None
    assert job.status is status
    assert env.lookup.queries == []


def test_a_damaged_record_file_is_skipped(env: Env) -> None:
    env.paths.metadata_jobs.mkdir(parents=True)
    (env.paths.metadata_jobs / f"{'4' * 32}.json").write_text("{", encoding="utf-8")
    lane = env.start()
    _done(lane)

    assert lane.get("4" * 32) is None


# ------------------------------------------------------------------ independence


def test_a_long_separation_does_not_delay_a_metadata_job(env: Env) -> None:
    started, release = threading.Event(), threading.Event()

    def long_separation(_job_id: str) -> None:
        started.set()
        assert release.wait(WAIT)

    separations = PriorityScheduler(long_separation, lambda _detail: False)
    separations.start()
    try:
        separations.submit("a" * 32)
        assert started.wait(WAIT)
        lane = env.start()
        job_id = lane.create(REQUEST).metadata_job_id
        _done(lane)
        job = lane.get(job_id)
        assert job is not None
        assert job.status is S.COMPLETED
        assert not separations.join(0)
    finally:
        release.set()
        separations.stop(WAIT)


# ------------------------------------------------------------------ sweep


def test_sweep_removes_terminal_jobs_older_than_24_hours(env: Env) -> None:
    env.lookup.findings = FOUND
    lane = env.start()
    old = lane.create(REQUEST).metadata_job_id
    _done(lane)
    env.clock.now = T0 + timedelta(hours=23)
    young = lane.create(REQUEST).metadata_job_id
    _done(lane)
    env.clock.now = T0 + timedelta(hours=24, seconds=1)

    assert lane.sweep() == [old]

    assert lane.get(old) is None
    assert not (env.paths.metadata_jobs / f"{old}.json").exists()
    assert not env.paths.metadata_job_dir(old).exists()
    assert lane.get(young) is not None
    assert lane.artwork(young, "cover") is not None


def test_sweep_keeps_unfinished_jobs(env: Env) -> None:
    release = env.lookup.block()
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    env.clock.now = T0 + timedelta(days=3)

    assert lane.sweep() == []
    assert lane.get(job_id) is not None
    release.set()
    _done(lane)


def test_swept_jobs_stay_gone_after_a_restart(env: Env) -> None:
    lane = env.start()
    job_id = lane.create(REQUEST).metadata_job_id
    _done(lane)
    env.clock.now = T0 + timedelta(days=2)
    lane.sweep()
    lane.stop(WAIT)

    assert env.start().get(job_id) is None


# ------------------------------------------------------------------ the generic lane


class Recorder:
    """A lane process function that records ids, can crash and can block."""

    def __init__(self) -> None:
        self.calls: list[str] = []
        self.crash: set[str] = set()
        self.release: dict[str, threading.Event] = {}

    def __call__(self, item: str) -> None:
        self.calls.append(item)
        if item in self.release:
            assert self.release[item].wait(WAIT)
        if item in self.crash:
            raise RuntimeError("boom")


@pytest.fixture
def recorder() -> Iterator[tuple[Recorder, LaneScheduler]]:
    calls = Recorder()
    lane = LaneScheduler("test", calls)
    yield calls, lane
    lane.stop(WAIT)


def test_lane_runs_items_fifo_one_at_a_time(recorder: tuple[Recorder, LaneScheduler]) -> None:
    calls, lane = recorder
    for item in ("a", "b", "c"):
        lane.submit(item)
    lane.start()
    lane.start()

    assert lane.join(WAIT)
    assert calls.calls == ["a", "b", "c"]
    assert lane.running


def test_lane_survives_a_crashing_item(
    recorder: tuple[Recorder, LaneScheduler], caplog: pytest.LogCaptureFixture
) -> None:
    calls, lane = recorder
    calls.crash.add("a")
    lane.start()
    lane.submit("a")
    lane.submit("b")

    assert lane.join(WAIT)
    assert calls.calls == ["a", "b"]
    assert "test lane" in caplog.text


def test_lane_join_times_out_while_an_item_runs(
    recorder: tuple[Recorder, LaneScheduler],
) -> None:
    calls, lane = recorder
    calls.release["a"] = threading.Event()
    lane.start()
    lane.submit("a")

    assert not lane.join(0.05)
    calls.release["a"].set()
    assert lane.join(WAIT)


def test_lane_stop_is_idempotent_and_ends_the_thread() -> None:
    lane = LaneScheduler("idle", lambda _item: None)
    lane.stop()
    lane.start()
    lane.stop(WAIT)

    assert not lane.running
