"""Job runner: gate, model load, OOM retry, failures and cancel (009 T016; data-model 2.4)."""

from pathlib import Path

import pytest

from stemstudio_generator.jobs.progress import JobReporter
from stemstudio_generator.jobs.runner import (
    DOWNLOADING_STAGE,
    ENGINE_ERROR_TEXT,
    LOADING_STAGE,
    WAITING_STAGE,
    JobRunner,
)
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import (
    COVER_TURBO_LOWMEM,
    SVC_FP16,
    Cancelled,
    GpuBusyError,
    GpuOutOfMemoryError,
    InvalidInputError,
    ProgressCallback,
)
from stemstudio_generator.schemas import (
    LOW_MEMORY_RETRY_STAGE,
    OUT_OF_MEMORY_TEXT,
    Generation,
)

from ..fakes import FakeClock
from .helpers import TAKE, FakeEngine, FakeHandler, RecordingGate, calling, make_job, raising


class HistoryStore(JsonJobStore):
    """JSON store that remembers every saved ``(status, progress, stage)``."""

    def __init__(self, root: Path) -> None:
        super().__init__(root)
        self.history: list[tuple[str, int, str]] = []

    def save(self, job: Generation) -> None:
        self.history.append((job.status, job.progress, job.stage))
        super().save(job)


def _run(
    tmp_path: Path, handler: FakeHandler, gate: RecordingGate | None = None
) -> tuple[Generation, HistoryStore, RecordingGate]:
    store, gate = HistoryStore(tmp_path), gate or RecordingGate()
    job = make_job()
    store.save(job)
    reporter = JobReporter(job, store, FakeClock())
    result = JobRunner(gate, {"restyle": handler}).run(reporter)
    return result, store, gate


def _statuses(store: HistoryStore) -> list[str]:
    seen: list[str] = []
    for status, _, _ in store.history:
        if not seen or seen[-1] != status:
            seen.append(status)
    return seen


def test_a_job_runs_through_the_gate_and_completes(tmp_path: Path) -> None:
    handler = FakeHandler()

    job, store, gate = _run(tmp_path, handler)

    assert job.status == "completed"
    assert job.takes == [TAKE]
    assert job.progress == 100
    assert job.device is not None
    assert job.device.name == "Fake GPU"
    assert _statuses(store) == ["queued", "waitingForGpu", "running", "completed"]
    assert ("waitingForGpu", 0, WAITING_STAGE) in store.history
    assert ("running", 0, LOADING_STAGE) in store.history
    assert ("running", 50, "Generating take 1 of 1") in store.history
    assert gate.events == ["acquire", "release"]
    assert handler.engine.loads == ["standard"]


def test_missing_models_are_downloaded_in_their_own_state(tmp_path: Path) -> None:
    engine = FakeEngine(present=False, load_progress=[(0.25, "Downloading model (1.2 of 4.7 GB)")])

    job, store, _ = _run(tmp_path, FakeHandler(engine=engine))

    assert job.status == "completed"
    assert _statuses(store) == [
        "queued",
        "waitingForGpu",
        "downloadingModel",
        "running",
        "completed",
    ]
    assert ("downloadingModel", 0, DOWNLOADING_STAGE) in store.history
    assert ("downloadingModel", 25, "Downloading model (1.2 of 4.7 GB)") in store.history


def test_gate_waits_are_reported_as_waiting_for_gpu(tmp_path: Path) -> None:
    gate = RecordingGate(waits=["Waiting for free GPU memory: needs 8.4 GB, 2.0 GB free"])

    _, store, _ = _run(tmp_path, FakeHandler(), gate)

    assert (
        "waitingForGpu", 0, "Waiting for free GPU memory: needs 8.4 GB, 2.0 GB free"
    ) in store.history  # fmt: skip


def test_first_oom_retries_once_with_the_low_memory_profile(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(GpuOutOfMemoryError()), calling(lambda _: None)])

    job, store, gate = _run(tmp_path, handler)

    assert job.status == "completed"
    assert gate.requested == [("COVER_TURBO", "COVER_TURBO_LOWMEM"), ("COVER_TURBO_LOWMEM",)]
    assert [use.need for use in handler.uses][-1] is COVER_TURBO_LOWMEM
    assert handler.engine.unloads == 1
    assert handler.engine.loads == ["standard", "lowMemory"]
    assert ("running", 0, LOW_MEMORY_RETRY_STAGE) in store.history
    assert "waitingForGpu" not in [status for status, _, _ in store.history[3:]]
    assert gate.events == ["acquire", "release", "acquire", "release"]


def test_a_torch_style_oom_is_recognised_too(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(RuntimeError("CUDA out of memory")), calling(print)])

    job, _, _ = _run(tmp_path, handler)

    assert job.status == "completed"


def test_a_second_oom_fails_the_job_with_the_readable_text(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(RuntimeError("CUDA out of memory"))])

    job, _, gate = _run(tmp_path, handler)

    assert job.status == "failed"
    assert job.error is not None
    assert (job.error.code, job.error.message) == ("outOfMemory", OUT_OF_MEMORY_TEXT)
    assert gate.events == ["acquire", "release", "acquire", "release"]
    assert handler.engine.unloads == 2


def test_an_oom_on_the_low_memory_profile_is_not_retried(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(GpuOutOfMemoryError())], job_needs=(COVER_TURBO_LOWMEM,))

    job, _, gate = _run(tmp_path, handler)

    assert job.error is not None
    assert job.error.code == "outOfMemory"
    assert len(gate.requested) == 1


def test_an_oom_without_a_low_memory_profile_is_not_retried(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(GpuOutOfMemoryError())], job_needs=(SVC_FP16,))

    job, _, gate = _run(tmp_path, handler)

    assert job.error is not None
    assert job.error.code == "outOfMemory"
    assert gate.requested == [("SVC_FP16",)]


def test_gpu_busy_from_the_gate_fails_the_job(tmp_path: Path) -> None:
    gate = RecordingGate(error=GpuBusyError("GPU busy: needs 8.4 GB, 2.0 GB free."))

    job, _, _ = _run(tmp_path, FakeHandler(), gate)

    assert job.error is not None
    assert (job.error.code, job.error.message) == (
        "gpuBusy",
        "GPU busy: needs 8.4 GB, 2.0 GB free.",
    )


def test_expected_errors_keep_their_code_and_message(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(InvalidInputError("The stems differ in sample rate."))])

    job, _, gate = _run(tmp_path, handler)

    assert job.error is not None
    assert (job.error.code, job.error.message) == (
        "invalidInput",
        "The stems differ in sample rate.",
    )
    assert gate.events == ["acquire", "release"]


def test_unexpected_errors_become_a_generic_engine_error(tmp_path: Path) -> None:
    handler = FakeHandler(steps=[raising(KeyError("token=secret"))])

    job, _, _ = _run(tmp_path, handler)

    assert job.error is not None
    assert (job.error.code, job.error.message) == ("engineError", ENGINE_ERROR_TEXT)


def test_cancel_during_a_step_ends_cancelled_and_releases_the_lease(tmp_path: Path) -> None:
    def cancel(on_progress: ProgressCallback) -> None:
        raise Cancelled

    job, _, gate = _run(tmp_path, FakeHandler(steps=[calling(cancel)]))

    assert job.status == "cancelled"
    assert job.error is None
    assert gate.events == ["acquire", "release"]


def test_a_cancel_request_is_seen_at_the_next_progress_report(tmp_path: Path) -> None:
    store = HistoryStore(tmp_path)
    job = make_job()
    store.save(job)
    reporter = JobReporter(job, store, FakeClock())

    def request_and_report(on_progress: ProgressCallback) -> None:
        reporter.request_cancel()
        on_progress(0.5, "Generating take 1 of 1")

    result = JobRunner(
        RecordingGate(), {"restyle": FakeHandler(steps=[calling(request_and_report)])}
    ).run(reporter)

    assert result.status == "cancelled"


def test_idle_unloads_every_engine_once(tmp_path: Path) -> None:
    shared = FakeEngine()
    runner = JobRunner(
        RecordingGate(),
        {"restyle": FakeHandler(engine=shared), "song": FakeHandler(engine=shared)},
    )

    runner.idle()

    assert shared.unloads == 1


def test_a_kind_without_a_handler_fails_as_an_engine_error(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    job = make_job()
    store.save(job)

    result = JobRunner(RecordingGate(), {}).run(JobReporter(job, store, FakeClock()))

    assert result.error is not None
    assert result.error.code == "engineError"


@pytest.mark.parametrize("kinds", [("restyle",), ("restyle", "song")])
def test_runner_reports_the_kinds_it_serves(kinds: tuple[str, ...]) -> None:
    runner = JobRunner(RecordingGate(), {kind: FakeHandler() for kind in kinds})  # type: ignore[misc]

    assert runner.kinds == kinds


def test_a_failure_after_a_cancel_request_ends_cancelled(tmp_path: Path) -> None:
    store = JsonJobStore(tmp_path)
    job = make_job()
    store.save(job)
    reporter = JobReporter(job, store, FakeClock())

    def request_then_fail(on_progress: ProgressCallback) -> None:
        reporter.request_cancel()
        raise InvalidInputError("The stems differ in sample rate.")

    handler = FakeHandler(steps=[calling(request_then_fail)])
    result = JobRunner(RecordingGate(), {"restyle": handler}).run(reporter)

    assert result.status == "cancelled"
    assert result.error is None


def test_a_non_oom_failure_of_the_retry_keeps_its_code(tmp_path: Path) -> None:
    handler = FakeHandler(
        steps=[raising(GpuOutOfMemoryError()), raising(InvalidInputError("Unreadable stem."))]
    )

    job, _, _ = _run(tmp_path, handler)

    assert job.error is not None
    assert job.error.code == "invalidInput"
