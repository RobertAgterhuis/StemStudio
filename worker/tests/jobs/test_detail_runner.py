"""Tests of the detail job runner and the detail restart reconciliation (T044, FR-008..FR-019)."""

import errno
import logging
from dataclasses import dataclass
from pathlib import Path

import pytest

from stemstudio_worker.jobs import detail_texts as texts
from stemstudio_worker.jobs.detail_runner import FINALISE_WEIGHT, DetailRunner
from stemstudio_worker.jobs.detail_service import DetailContext, DetailService
from stemstudio_worker.jobs.detail_store import DetailJobStore, StoredDetailJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.reconcile import reconcile_detail_jobs
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    DetailCancelledError,
    DetailModelUnavailableError,
    DetailSeparationError,
)
from stemstudio_worker.schemas import DetailJob, DetailJobState

from .detail_env import SHA, STD, completed_standard
from .detail_fakes import FakePipeline
from .fakes import FakeClock

D = DetailJobState


class HistoryStore(DetailJobStore):
    """Records (status, stage, progress, currentParent) of every save."""

    def __init__(self, directory: Path) -> None:
        super().__init__(directory)
        self.history: list[tuple[D, str, int, str | None]] = []

    def save(self, record: StoredDetailJob) -> None:
        job = record.job
        self.history.append((job.status, job.stage, job.progress, job.current_parent))
        super().save(record)


@dataclass
class Env:
    paths: DataPaths
    details: HistoryStore
    service: DetailService
    pipeline: FakePipeline
    runner: DetailRunner

    def create(self) -> str:
        job, _ = self.service.create(STD)
        return job.detail_job_id

    def drive(self, detail_id: str, limit: int = 20) -> int:
        units = 1
        while self.runner.step(detail_id):
            units += 1
            assert units < limit
        return units

    def job(self, detail_id: str) -> DetailJob:
        record = self.details.get(detail_id)
        assert record is not None
        return record.job


def _env(root: Path, pipeline: FakePipeline | None = None) -> Env:
    paths = DataPaths(root)
    jobs = JobStore(paths.jobs)
    completed_standard(paths, jobs)
    details = HistoryStore(paths.detail_jobs)
    context = DetailContext(jobs, details, paths, FakeClock())
    fake = pipeline or FakePipeline()
    service = DetailService(context, lambda detail_id: None)
    return Env(paths, details, service, fake, DetailRunner(context, fake))


@pytest.fixture
def env(tmp_path: Path) -> Env:
    return _env(tmp_path)


def test_a_detail_job_runs_plan_steps_and_finalise(env: Env) -> None:
    detail_id = env.create()
    assert env.drive(detail_id) == 4
    stages = [(s, st, p) for s, st, _, p in env.details.history]
    assert stages[0] == (D.QUEUED, "Queued", None)
    assert (D.RUNNING, texts.STARTING, None) in stages
    assert (D.RUNNING, "Splitting drums (1 of 2)", "drums") in stages
    assert (D.RUNNING, "Splitting vocals (2 of 2)", "vocals") in stages
    assert stages[-1] == (D.COMPLETED, "Completed", None)
    assert env.job(detail_id).progress == 100


def test_progress_never_decreases_and_stays_below_100_while_running(env: Env) -> None:
    env.drive(env.create())
    progress = [p for _, _, p, _ in env.details.history]
    assert progress == sorted(progress)
    running = [p for s, _, p, _ in env.details.history if s is D.RUNNING]
    assert min(running) >= 1
    assert max(running) <= 99
    assert FINALISE_WEIGHT == 0.01


def test_the_plan_receives_the_cached_standard_stems_in_order(env: Env) -> None:
    env.drive(env.create())
    (stems,) = env.pipeline.plans
    assert list(stems) == ["vocals", "drums", "bass", "guitar", "piano", "other"]
    assert stems["drums"] == env.paths.stem_file(SHA, "drums")


def test_completion_moves_the_checkpoints_and_writes_the_result(env: Env) -> None:
    detail_id = env.create()
    env.drive(detail_id)
    record = env.details.get(detail_id)
    assert record is not None
    assert record.result is not None
    assert record.result.separator_version == "1"
    assert [p.parent for p in record.result.parents] == [
        "vocals", "drums", "bass", "guitar", "piano", "other",
    ]  # fmt: skip
    drums = record.result.parents[1]
    assert drums.stems[0].path == f"/v1/detail-jobs/{detail_id}/stems/drums/kick"
    assert env.paths.detail_stem(SHA, "drums", "kick").is_file()
    assert (env.paths.detail_final(SHA) / "result.json").is_file()
    assert not env.paths.detail_partial(SHA).exists()


def test_a_cached_result_completes_a_new_job_in_one_unit(env: Env) -> None:
    first = env.create()
    env.drive(first)
    env.details.save(env.details.get(first).model_copy(  # type: ignore[union-attr]
        update={"job": env.job(first).model_copy(update={"status": D.FAILED})}
    ))  # fmt: skip
    second = env.create()
    assert env.drive(second) == 1
    record = env.details.get(second)
    assert record is not None
    assert record.job.status is D.COMPLETED
    assert record.result is not None
    assert record.result.detail_job_id == second
    assert record.result.parents[1].stems[0].path.startswith(f"/v1/detail-jobs/{second}/")
    assert len(env.pipeline.plans) == 1


def test_a_cached_result_of_another_version_is_recomputed(tmp_path: Path) -> None:
    env = _env(tmp_path, FakePipeline(separator_version="0"))
    first = env.create()
    env.drive(first)
    newer = _env(tmp_path / "unused", FakePipeline(separator_version="1"))
    runner = DetailRunner(
        DetailContext(JobStore(env.paths.jobs), env.details, env.paths, FakeClock()),
        newer.pipeline,
    )
    env.details.save(env.details.get(first).model_copy(  # type: ignore[union-attr]
        update={"job": env.job(first).model_copy(update={"status": D.CANCELLED})}
    ))  # fmt: skip
    env.details.load_all()
    second = env.create()
    while runner.step(second):
        pass
    assert len(newer.pipeline.plans) == 1
    assert env.job(second).status is D.COMPLETED


@pytest.mark.parametrize(
    ("error", "text"),
    [
        (MemoryError(), texts.NO_MEMORY),
        (OSError(errno.ENOSPC, "No space left on device"), texts.NO_DISK),
        (DetailModelUnavailableError("Detail model could not be downloaded; try again when online."),
         "Detail model could not be downloaded; try again when online."),
        (DetailSeparationError("Detail separation of drums failed."),
         "Detail separation of drums failed."),
        (OSError(errno.EACCES, "denied"), texts.UNEXPECTED),
        (RuntimeError("bug"), texts.UNEXPECTED),
    ],
)  # fmt: skip
def test_errors_become_fixed_texts_and_leave_no_detail_result(
    tmp_path: Path, error: BaseException, text: str
) -> None:
    env = _env(tmp_path, FakePipeline(errors={"vocals": error}))
    detail_id = env.create()
    env.drive(detail_id)
    job = env.job(detail_id)
    assert (job.status, job.stage, job.error, job.current_parent) == (
        D.FAILED,
        "Failed",
        text,
        None,
    )
    assert not env.paths.detail_final(SHA).exists()
    assert (env.paths.detail_partial(SHA) / "drums" / "kick.flac").is_file()


def test_an_unexpected_error_is_logged(tmp_path: Path, caplog: pytest.LogCaptureFixture) -> None:
    env = _env(tmp_path, FakePipeline(errors={"drums": RuntimeError("bug")}))
    with caplog.at_level(logging.ERROR):
        env.drive(env.create())
    assert "failed unexpectedly" in caplog.text


def test_missing_standard_stems_fail_the_job(env: Env) -> None:
    detail_id = env.create()
    env.paths.stem_file(SHA, "vocals").unlink()
    assert env.drive(detail_id) == 1
    assert env.job(detail_id).error == texts.STEMS_MISSING
    assert env.pipeline.plans == []


def test_cancel_between_steps_stops_the_job(env: Env) -> None:
    detail_id = env.create()
    assert env.runner.step(detail_id)
    assert env.runner.step(detail_id)
    env.service.cancel(detail_id)
    assert not env.runner.step(detail_id)
    assert env.job(detail_id).status is D.CANCELLED
    assert env.pipeline.runs == [("drums",)]
    assert not env.paths.detail_final(SHA).exists()


def test_cancel_during_a_step_is_honoured_at_the_next_progress_report(tmp_path: Path) -> None:
    pipeline = FakePipeline()
    env = _env(tmp_path, pipeline)
    detail_id = env.create()
    pipeline.during["drums"] = lambda: env.service.cancel(detail_id)
    env.drive(detail_id)
    job = env.job(detail_id)
    assert (job.status, job.stage, job.error) == (D.CANCELLED, "Cancelled", None)
    assert pipeline.runs == [("drums",)]
    assert not (env.paths.detail_partial(SHA) / "drums").exists()


def test_a_cancelled_error_without_a_request_still_ends_cancelled(tmp_path: Path) -> None:
    env = _env(tmp_path, FakePipeline(errors={"drums": DetailCancelledError("stop")}))
    detail_id = env.create()
    env.drive(detail_id)
    assert env.job(detail_id).status is D.CANCELLED


def test_unknown_and_terminal_jobs_have_no_units(env: Env) -> None:
    assert not env.runner.step("f" * 32)
    detail_id = env.create()
    env.service.cancel(detail_id)
    assert not env.runner.step(detail_id)
    assert env.pipeline.plans == []


def test_reconcile_fails_unfinished_detail_jobs_and_keeps_checkpoints(env: Env) -> None:
    queued = env.create()
    env.runner.step(queued)
    env.runner.step(queued)
    partial = env.paths.detail_partial(SHA)
    (partial / "vocals.tmp").mkdir()
    interrupted = reconcile_detail_jobs(env.details, env.paths, FakeClock())
    assert interrupted == [queued]
    job = env.job(queued)
    assert (job.status, job.error, job.current_parent) == (D.FAILED, texts.INTERRUPTED, None)
    assert (partial / "drums" / "kick.flac").is_file()
    assert not (partial / "vocals.tmp").exists()
    assert reconcile_detail_jobs(env.details, env.paths, FakeClock()) == []
