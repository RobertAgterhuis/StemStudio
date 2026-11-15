"""Single-thread FIFO scheduler, cancel, delete, idle unload and retention (009 T016)."""

import threading
from datetime import timedelta
from pathlib import Path

from stemstudio_generator.jobs.progress import JobReporter
from stemstudio_generator.jobs.runner import JobOutcome, JobRunner
from stemstudio_generator.jobs.scheduler import Scheduler
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import DeviceUse, ProgressCallback
from stemstudio_generator.schemas import Generation, QueueStatus

from ..fakes import FakeClock
from .helpers import TAKE, FakeHandler, RecordingGate, make_job


class OrderRunner(JobRunner):
    """Real runner that records the order of jobs and idle calls."""

    def __init__(self, handler: FakeHandler) -> None:
        super().__init__(RecordingGate(), {"restyle": handler})
        self.order: list[str] = []
        self.idles = 0

    def run(self, reporter: JobReporter) -> Generation:
        self.order.append(reporter.job.generation_id)
        return super().run(reporter)

    def idle(self) -> None:
        self.idles += 1
        super().idle()


def _scheduler(
    tmp_path: Path, handler: FakeHandler | None = None
) -> tuple[Scheduler, OrderRunner, JsonJobStore, FakeClock]:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    runner = OrderRunner(handler or FakeHandler())
    return Scheduler(store, runner, clock), runner, store, clock


def _status(store: JsonJobStore, number: int) -> str | None:
    job = store.get(make_job(number).generation_id)
    return None if job is None else job.status


def test_jobs_run_one_at_a_time_in_submission_order(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    for number in (1, 2, 3):
        scheduler.submit(make_job(number))

    assert scheduler.queue_status() == QueueStatus(queued=3)
    while scheduler.run_next():
        pass

    assert runner.order == [make_job(n).generation_id for n in (1, 2, 3)]
    assert [_status(store, n) for n in (1, 2, 3)] == ["completed"] * 3
    assert scheduler.queue_status() == QueueStatus(queued=0)


def test_run_next_on_an_empty_queue_does_nothing(tmp_path: Path) -> None:
    scheduler, runner, _, _ = _scheduler(tmp_path)

    assert not scheduler.run_next()
    assert runner.idles == 0


def test_idle_hook_runs_only_when_the_queue_is_empty(tmp_path: Path) -> None:
    handler = FakeHandler()
    scheduler, runner, _, _ = _scheduler(tmp_path, handler)
    scheduler.submit(make_job(1))
    scheduler.submit(make_job(2))

    scheduler.run_next()
    assert runner.idles == 0
    scheduler.run_next()

    assert runner.idles == 1
    assert handler.engine.unloads == 1


def test_submit_saves_the_queued_job(tmp_path: Path) -> None:
    scheduler, _, store, _ = _scheduler(tmp_path)

    returned = scheduler.submit(make_job(1))

    assert returned.status == "queued"
    assert store.get(returned.generation_id) == returned
    assert scheduler.get(returned.generation_id) == returned


def test_cancel_of_a_queued_job_is_immediate_and_it_never_runs(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    scheduler.submit(make_job(1))
    scheduler.submit(make_job(2))

    cancelled = scheduler.cancel(make_job(1).generation_id)
    while scheduler.run_next():
        pass

    assert cancelled is not None
    assert cancelled.status == "cancelled"
    assert runner.order == [make_job(2).generation_id]
    assert _status(store, 1) == "cancelled"


def test_cancel_of_an_unknown_job_is_none(tmp_path: Path) -> None:
    scheduler, _, _, _ = _scheduler(tmp_path)

    assert scheduler.cancel("f" * 32) is None


def test_cancel_of_a_terminal_job_returns_it_unchanged(tmp_path: Path) -> None:
    scheduler, _, _, _ = _scheduler(tmp_path)
    scheduler.submit(make_job(1))
    scheduler.run_next()

    job = scheduler.cancel(make_job(1).generation_id)

    assert job is not None
    assert job.status == "completed"


def _cancel_from_inside(scheduler: Scheduler, number: int) -> FakeHandler:
    """A handler whose step asks the scheduler to cancel the running job, then reports."""

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        returned = scheduler.cancel(make_job(number).generation_id)
        assert returned is not None
        assert returned.status == "running"
        on_progress(0.5, "Generating take 1 of 1")
        return JobOutcome(takes=[TAKE])

    return FakeHandler(steps=[step])


def test_cancel_of_the_running_job_sets_the_flag_checked_by_the_step(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    runner = OrderRunner(FakeHandler())
    scheduler = Scheduler(store, runner, clock)
    runner.handlers["restyle"] = _cancel_from_inside(scheduler, 1)
    scheduler.submit(make_job(1))

    scheduler.run_next()

    assert _status(store, 1) == "cancelled"


def test_delete_removes_a_queued_job_and_its_files(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    scheduler.submit(make_job(1))
    work = store.job_dir(make_job(1).generation_id)

    assert scheduler.delete(make_job(1).generation_id)

    assert not scheduler.run_next()
    assert runner.order == []
    assert store.get(make_job(1).generation_id) is None
    assert not work.exists()


def test_delete_of_an_unknown_job_is_false(tmp_path: Path) -> None:
    scheduler, _, _, _ = _scheduler(tmp_path)

    assert not scheduler.delete("f" * 32)


def test_delete_of_the_running_job_cancels_it_and_removes_it_afterwards(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    runner = OrderRunner(FakeHandler())
    scheduler = Scheduler(store, runner, clock)
    seen: list[bool] = []

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        seen.append(scheduler.delete(job.generation_id))
        on_progress(0.5, "Generating")
        return JobOutcome(takes=[TAKE])

    runner.handlers["restyle"] = FakeHandler(steps=[step])
    scheduler.submit(make_job(1))
    scheduler.run_next()

    assert seen == [True]
    assert store.get(make_job(1).generation_id) is None


def test_queue_status_names_the_running_job(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    runner = OrderRunner(FakeHandler())
    scheduler = Scheduler(store, runner, clock)
    seen: list[QueueStatus] = []

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        seen.append(scheduler.queue_status())
        return JobOutcome(takes=[TAKE])

    runner.handlers["restyle"] = FakeHandler(steps=[step])
    scheduler.submit(make_job(1))
    scheduler.submit(make_job(2))
    scheduler.run_next()

    assert seen == [QueueStatus(queued=1, running=make_job(1).generation_id)]


def test_get_of_the_running_job_returns_the_live_state(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    runner = OrderRunner(FakeHandler())
    scheduler = Scheduler(store, runner, clock)
    seen: list[str] = []

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        on_progress(0.4, "Generating take 1 of 1")
        live = scheduler.get(job.generation_id)
        assert live is not None
        seen.append(f"{live.status} {live.progress}")
        return JobOutcome(takes=[TAKE])

    runner.handlers["restyle"] = FakeHandler(steps=[step])
    scheduler.submit(make_job(1))
    scheduler.run_next()

    assert seen == ["running 40"]


def test_terminal_jobs_are_removed_after_24_hours(tmp_path: Path) -> None:
    scheduler, _, store, clock = _scheduler(tmp_path)
    scheduler.submit(make_job(1))
    scheduler.run_next()
    queued = make_job(2)
    store.save(queued)

    clock.advance(24 * 3600 - 1)
    assert scheduler.sweep() == []
    clock.advance(1)
    assert scheduler.sweep() == [make_job(1).generation_id]

    assert store.get(make_job(1).generation_id) is None
    assert store.get(queued.generation_id) is not None


def test_resume_requeues_reconciled_jobs_without_saving_them_again(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    store.save(make_job(1))
    store.save(make_job(2))

    scheduler.resume([make_job(2).generation_id, make_job(1).generation_id, "f" * 32])
    while scheduler.run_next():
        pass

    assert runner.order == [make_job(2).generation_id, make_job(1).generation_id]


def test_the_worker_thread_processes_submitted_jobs(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    done = threading.Event()
    original = runner.idle

    def idle() -> None:
        original()
        done.set()

    runner.idle = idle  # type: ignore[method-assign]
    scheduler.start()
    try:
        scheduler.submit(make_job(1))
        assert done.wait(5)
    finally:
        scheduler.stop()

    assert _status(store, 1) == "completed"


def test_the_worker_thread_sweeps_while_idle(tmp_path: Path) -> None:
    store, clock = JsonJobStore(tmp_path), FakeClock()
    old = make_job(1).model_copy(update={"status": "cancelled", "stage": "Cancelled"})
    store.save(old)
    clock.advance(25 * 3600)
    scheduler = Scheduler(store, OrderRunner(FakeHandler()), clock)
    scheduler.sweep_seconds = 0.01

    scheduler.start()
    try:
        for _ in range(500):
            if scheduler.get(old.generation_id) is None:
                break
            threading.Event().wait(0.01)
    finally:
        scheduler.stop()

    assert store.get(old.generation_id) is None


def test_stop_without_start_is_harmless(tmp_path: Path) -> None:
    scheduler, _, _, _ = _scheduler(tmp_path)

    scheduler.stop()


def test_jobs_older_than_retention_use_updated_at(tmp_path: Path) -> None:
    scheduler, _, store, clock = _scheduler(tmp_path)
    job = make_job(1).model_copy(
        update={"status": "cancelled", "stage": "Cancelled", "updated_at": clock.now()}
    )
    store.save(job)
    clock.advance(23 * 3600)

    assert scheduler.sweep() == []
    clock.advance(timedelta(hours=1).total_seconds())
    assert scheduler.sweep() == [job.generation_id]


def test_queue_entries_whose_job_changed_meanwhile_are_skipped(tmp_path: Path) -> None:
    scheduler, runner, store, _ = _scheduler(tmp_path)
    scheduler.submit(make_job(1))
    store.save(make_job(1).model_copy(update={"status": "cancelled", "stage": "Cancelled"}))

    assert not scheduler.run_next()
    assert runner.order == []


def test_the_worker_thread_survives_a_failing_loop_step(tmp_path: Path) -> None:
    scheduler, _, _, _ = _scheduler(tmp_path)
    scheduler.sweep_seconds = 0.01
    done = threading.Barrier(2, timeout=5)

    def broken_sweep() -> list[str]:
        done.wait()  # the second loop round proves the thread survived the first error
        raise OSError("disk gone")

    scheduler.sweep = broken_sweep  # type: ignore[method-assign]
    scheduler.start()
    try:
        done.wait()
        done.wait()
    finally:
        done.abort()
        scheduler.stop()
