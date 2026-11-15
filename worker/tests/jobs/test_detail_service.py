"""Tests of detail job creation (idempotent per audio) and cancellation (T020, FR-010, FR-016)."""

from pathlib import Path

import pytest

from stemstudio_worker.jobs.detail_service import (
    DetailConflictError,
    DetailContext,
    DetailService,
    UnknownJobError,
)
from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import DetailJob, DetailJobState, JobState

from .conftest import build_job
from .detail_env import SHA, STD, completed_standard
from .fakes import FakeClock

D = DetailJobState


class Env:
    def __init__(self, root: Path) -> None:
        self.paths = DataPaths(root)
        self.jobs = JobStore(self.paths.jobs)
        self.details = DetailJobStore(self.paths.detail_jobs)
        self.submitted: list[str] = []
        context = DetailContext(self.jobs, self.details, self.paths, FakeClock())
        self.service = DetailService(context, self.submitted.append)

    def set_status(self, job: DetailJob, status: D) -> None:
        record = self.details.get(job.detail_job_id)
        assert record is not None
        self.details.save(
            record.model_copy(update={"job": job.model_copy(update={"status": status})})
        )

    def stored(self, job: DetailJob) -> StoredDetailJob:
        record = self.details.get(job.detail_job_id)
        assert record is not None
        return record


@pytest.fixture
def env(tmp_path: Path) -> Env:
    environment = Env(tmp_path)
    completed_standard(environment.paths, environment.jobs)
    return environment


def test_create_queues_a_new_detail_job(env: Env) -> None:
    job, created = env.service.create(STD)
    assert created
    assert (job.standard_job_id, job.audio_sha256) == (STD, SHA)
    assert (job.status, job.stage, job.progress, job.current_parent) == (
        D.QUEUED,
        "Queued",
        0,
        None,
    )
    assert env.submitted == [job.detail_job_id]
    assert env.stored(job).job == job


@pytest.mark.parametrize("status", [D.QUEUED, D.RUNNING, D.COMPLETED])
def test_create_reuses_an_active_or_completed_job_for_the_same_audio(env: Env, status: D) -> None:
    first, _ = env.service.create(STD)
    env.set_status(first, status)
    again, created = env.service.create(STD)
    assert not created
    assert again.detail_job_id == first.detail_job_id
    assert env.submitted == [first.detail_job_id]


@pytest.mark.parametrize("status", [D.FAILED, D.CANCELLED])
def test_create_after_failure_or_cancel_queues_a_new_job(env: Env, status: D) -> None:
    first, _ = env.service.create(STD)
    env.set_status(first, status)
    again, created = env.service.create(STD)
    assert created
    assert again.detail_job_id != first.detail_job_id


def test_an_unknown_standard_job_is_rejected(env: Env) -> None:
    with pytest.raises(UnknownJobError, match="does not exist"):
        env.service.create("c" * 32)


def test_a_standard_job_that_is_not_completed_conflicts(env: Env) -> None:
    env.jobs.save(env.jobs.get(STD).model_copy(  # type: ignore[union-attr]
        update={"job": build_job(job_id=STD, status=JobState.SEPARATING), "result": None}
    ))  # fmt: skip
    with pytest.raises(DetailConflictError, match=r"Job is in state separating."):
        env.service.create(STD)


def test_missing_cached_stems_conflict(env: Env) -> None:
    env.paths.stem_file(SHA, "drums").unlink()
    with pytest.raises(DetailConflictError, match="no longer available"):
        env.service.create(STD)


def test_get_and_result_of_unknown_jobs_raise(env: Env) -> None:
    with pytest.raises(UnknownJobError):
        env.service.get("e" * 32)
    with pytest.raises(UnknownJobError):
        env.service.result("e" * 32)


def test_result_of_a_job_that_is_not_completed_conflicts(env: Env) -> None:
    job, _ = env.service.create(STD)
    assert env.service.get(job.detail_job_id).job == job
    with pytest.raises(DetailConflictError, match=r"Detail job is in state queued."):
        env.service.result(job.detail_job_id)


@pytest.mark.parametrize("status", [D.QUEUED, D.RUNNING])
def test_cancel_marks_queued_and_running_jobs_cancelled(env: Env, status: D) -> None:
    job, _ = env.service.create(STD)
    env.set_status(job, status)
    cancelled = env.service.cancel(job.detail_job_id)
    assert (cancelled.status, cancelled.stage, cancelled.current_parent) == (
        D.CANCELLED, "Cancelled", None,
    )  # fmt: skip
    assert env.stored(job).cancel_requested
    assert env.stored(job).job == cancelled


def test_cancel_of_a_cancelled_job_is_a_no_op(env: Env) -> None:
    job, _ = env.service.create(STD)
    first = env.service.cancel(job.detail_job_id)
    assert env.service.cancel(job.detail_job_id) == first


@pytest.mark.parametrize("status", [D.COMPLETED, D.FAILED])
def test_cancel_of_a_completed_or_failed_job_conflicts(env: Env, status: D) -> None:
    job, _ = env.service.create(STD)
    env.set_status(job, status)
    with pytest.raises(DetailConflictError, match=rf"Detail job is in state {status.value}."):
        env.service.cancel(job.detail_job_id)


def test_cancel_of_an_unknown_job_raises(env: Env) -> None:
    with pytest.raises(UnknownJobError):
        env.service.cancel("e" * 32)
