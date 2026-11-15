"""Job reporter: state updates, progress, cancel flag (009 T016; data-model 2.4)."""

from pathlib import Path

import pytest

from stemstudio_generator.jobs.progress import CANCELLED_STAGE, COMPLETED_STAGE, JobReporter
from stemstudio_generator.jobs.runner import JobOutcome
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import Cancelled
from stemstudio_generator.schemas import GenerationError

from ..fakes import FakeClock
from .helpers import GPU, TAKE, make_job


def _reporter(tmp_path: Path) -> tuple[JobReporter, JsonJobStore, FakeClock]:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    job = make_job()
    store.save(job)
    return JobReporter(job, store, clock), store, clock


def _stored(store: JsonJobStore, reporter: JobReporter) -> object:
    stored = store.get(reporter.job.generation_id)
    assert stored is not None
    return stored


def test_status_changes_reset_progress_and_are_saved_with_the_time(tmp_path: Path) -> None:
    reporter, store, clock = _reporter(tmp_path)
    clock.advance(5)

    reporter.status("running", "Loading model")

    assert reporter.job.status == "running"
    assert reporter.job.progress == 0
    assert reporter.job.stage == "Loading model"
    assert reporter.job.updated_at == clock.now()
    assert _stored(store, reporter) == reporter.job


def test_progress_is_an_integer_percentage_that_never_goes_back(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)
    reporter.status("running", "Generating")

    reporter.progress(0.426, "Generating take 1 of 2")
    assert reporter.job.progress == 43
    reporter.progress(0.2, "Generating take 1 of 2")
    assert reporter.job.progress == 43
    reporter.progress(1.7, "Generating take 2 of 2")
    assert reporter.job.progress == 100
    assert reporter.job.stage == "Generating take 2 of 2"


def test_negative_progress_is_clamped_to_zero(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)

    reporter.progress(-0.5, "Starting")

    assert reporter.job.progress == 0


def test_wait_moves_a_queued_job_to_waiting_for_gpu(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)

    reporter.wait("Waiting for the GPU")

    assert reporter.job.status == "waitingForGpu"
    assert reporter.job.stage == "Waiting for the GPU"


def test_wait_during_a_running_retry_keeps_the_job_running(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)
    reporter.status("running", "Retrying with low memory")
    reporter.progress(0.3, "Retrying with low memory")

    reporter.wait("Waiting for free GPU memory")

    assert reporter.job.status == "running"
    assert reporter.job.progress == 30
    assert reporter.job.stage == "Waiting for free GPU memory"


def test_admitted_records_the_device(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)

    reporter.admitted(GPU)

    assert reporter.job.device == GPU


def test_complete_stores_takes_warnings_and_full_progress(tmp_path: Path) -> None:
    reporter, store, _ = _reporter(tmp_path)
    reporter.status("running", "Generating")

    reporter.complete(JobOutcome(takes=[TAKE], warnings=["clipped"]))

    assert reporter.job.status == "completed"
    assert reporter.job.progress == 100
    assert reporter.job.stage == COMPLETED_STAGE
    assert reporter.job.takes == [TAKE]
    assert reporter.job.warnings == ["clipped"]
    assert _stored(store, reporter) == reporter.job


def test_fail_stores_the_error(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)

    reporter.fail("gpuBusy", "GPU busy: needs 8.4 GB, 2.0 GB free.")

    assert reporter.job.status == "failed"
    assert reporter.job.error == GenerationError(
        code="gpuBusy", message="GPU busy: needs 8.4 GB, 2.0 GB free."
    )


@pytest.mark.parametrize("status", ["queued", "waitingForGpu"])
def test_cancel_before_the_gpu_is_admitted_is_immediate(tmp_path: Path, status: str) -> None:
    reporter, store, _ = _reporter(tmp_path)
    if status == "waitingForGpu":
        reporter.wait("Waiting for the GPU")

    job = reporter.request_cancel()

    assert job.status == "cancelled"
    assert job.stage == CANCELLED_STAGE
    assert _stored(store, reporter) == job
    with pytest.raises(Cancelled):
        reporter.wait("Waiting for the GPU")


@pytest.mark.parametrize("status", ["running", "downloadingModel"])
def test_cancel_while_running_is_a_flag_checked_by_the_progress_callback(
    tmp_path: Path, status: str
) -> None:
    reporter, _, _ = _reporter(tmp_path)
    reporter.status(status, "Working")  # type: ignore[arg-type]

    job = reporter.request_cancel()

    assert job.status == status
    assert reporter.cancel_requested
    with pytest.raises(Cancelled):
        reporter.progress(0.5, "Generating")
    reporter.cancelled()
    assert reporter.job.status == "cancelled"


def test_cancel_of_a_terminal_job_changes_nothing(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)
    reporter.complete(JobOutcome(takes=[TAKE]))

    job = reporter.request_cancel()

    assert job.status == "completed"
    assert not reporter.cancel_requested


def test_completion_after_a_cancel_request_ends_cancelled(tmp_path: Path) -> None:
    reporter, _, _ = _reporter(tmp_path)
    reporter.status("running", "Generating")
    reporter.request_cancel()

    with pytest.raises(Cancelled):
        reporter.complete(JobOutcome(takes=[TAKE]))


def test_cancelled_is_idempotent(tmp_path: Path) -> None:
    reporter, _, clock = _reporter(tmp_path)
    reporter.request_cancel()
    stamp = reporter.job.updated_at
    clock.advance(5)

    reporter.cancelled()

    assert reporter.job.status == "cancelled"
    assert reporter.job.updated_at == stamp
