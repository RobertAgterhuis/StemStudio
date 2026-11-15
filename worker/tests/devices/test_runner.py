"""Device runners (T013, ADR-012 D3-D5): lease, NVML check, memory fraction, OOM -> CPU retry."""

import threading
from collections.abc import Callable

import pytest
import torch

from stemstudio_worker.devices.lease import GpuLease
from stemstudio_worker.devices.oom import is_gpu_out_of_memory
from stemstudio_worker.devices.runner import CpuRunner, GpuRunner, GpuTools, TorchCudaMemory
from stemstudio_worker.ports import DeviceRunner, DeviceUse, GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

from .fakes import RTX, FakeMemory, FakeNvml

GPU = DeviceInfo(
    kind="gpu", name=RTX, compute_capability="12.0", total_memory_mib=16_000, free_memory_mib=15_000
)
MEGA = GpuNeed("Mega 53-stem", 10_600)
GPU_USE = DeviceUse(kind="gpu", torch_device="cuda", name=RTX)
OOM_REASON = "The GPU ran out of memory during Mega 53-stem; the step was repeated on the CPU."


class Steps:
    """A model step recording the devices it was given; fails on the devices in ``errors``."""

    def __init__(self, errors: dict[str, Exception] | None = None) -> None:
        self.devices: list[str] = []
        self.errors = errors or {}

    def __call__(self, device: str) -> str:
        self.devices.append(device)
        if device in self.errors:
            raise self.errors[device]
        return f"result on {device}"


def _gpu(free: int = 12_000, memory: FakeMemory | None = None) -> tuple[GpuRunner, FakeMemory]:
    fake = memory or FakeMemory()
    tools = GpuTools(nvml=FakeNvml(total=16_000, free=[free]), memory=fake, lease=GpuLease())
    return GpuRunner(GPU, tools), fake


def test_cpu_runner_runs_every_step_on_the_cpu() -> None:
    runner = CpuRunner()
    steps = Steps()

    assert isinstance(runner, DeviceRunner)
    assert runner.info == CPU_DEVICE
    assert runner.run(steps, MEGA) == ("result on cpu", DeviceUse(kind="cpu", torch_device="cpu"))
    assert steps.devices == ["cpu"]


def test_cpu_runner_keeps_the_probe_reason_and_lets_errors_through() -> None:
    info = DeviceInfo(kind="cpu", reason="CUDA is not available")
    runner = CpuRunner(info)
    runner.release()

    assert runner.info is info
    with pytest.raises(ValueError, match="boom"):
        runner.run(Steps({"cpu": ValueError("boom")}), MEGA)


def test_a_fitting_step_runs_on_the_gpu_under_a_memory_fraction() -> None:
    memory = FakeMemory()
    runner, _ = _gpu(free=12_000, memory=memory)

    def step(device: str) -> str:
        memory.events.append(f"work {device}")
        return "done"

    assert isinstance(runner, DeviceRunner)
    assert runner.info is GPU
    assert runner.run(step, MEGA) == ("done", GPU_USE)
    assert memory.events == [
        f"fraction {(12_000 - 256) / 16_000:.6f}",
        "work cuda",
        "fraction 1.000000",
    ]


def test_too_little_free_memory_runs_the_step_on_the_cpu_without_running_on_cuda() -> None:
    runner, memory = _gpu(free=9_700)
    steps = Steps()

    result, use = runner.run(steps, MEGA)

    assert (result, steps.devices, memory.events) == ("result on cpu", ["cpu"], ["empty_cache"])
    assert use == DeviceUse(
        kind="cpu", torch_device="cpu",
        fallback_reason=(
            "Not enough free GPU memory for Mega 53-stem (needs 10600 MiB, 9700 MiB free); "
            "ran on the CPU."
        ),
    )  # fmt: skip


def test_exactly_the_needed_memory_fits() -> None:
    runner, _ = _gpu(free=10_600)
    assert runner.run(Steps(), MEGA)[1] == GPU_USE


@pytest.mark.parametrize(
    "error",
    [
        torch.cuda.OutOfMemoryError("CUDA out of memory. Tried to allocate 256.00 MiB"),
        RuntimeError("[ONNXRuntimeError] : 6 : RUNTIME_EXCEPTION : Failed to allocate memory"),
        RuntimeError("CUDA error: out of memory"),
    ],
)
def test_a_gpu_out_of_memory_error_is_retried_once_on_the_cpu(error: Exception) -> None:
    runner, memory = _gpu()
    steps = Steps({"cuda": error})

    result, use = runner.run(steps, MEGA)

    assert (result, steps.devices) == ("result on cpu", ["cuda", "cpu"])
    assert use == DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=OOM_REASON)
    assert memory.events[-2:] == ["fraction 1.000000", "empty_cache"]


def test_an_out_of_memory_error_mapped_by_the_model_is_recognised_by_its_cause() -> None:
    def step(device: str) -> str:
        if device == "cuda":
            try:
                raise torch.cuda.OutOfMemoryError("CUDA out of memory")
            except RuntimeError as exc:
                raise MemoryError("alloc failed") from exc
        return "cpu result"

    runner, _ = _gpu()
    assert runner.run(step, MEGA) == (
        "cpu result",
        DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=OOM_REASON),
    )


def test_an_error_of_the_cpu_retry_propagates() -> None:
    runner, _ = _gpu()
    steps = Steps({"cuda": torch.cuda.OutOfMemoryError("oom"), "cpu": ValueError("cpu failed")})

    with pytest.raises(ValueError, match="cpu failed"):
        runner.run(steps, MEGA)
    assert steps.devices == ["cuda", "cpu"]


def test_other_gpu_errors_propagate_without_a_retry() -> None:
    runner, memory = _gpu()
    steps = Steps({"cuda": RuntimeError("CUDA error: an illegal memory access was encountered")})

    with pytest.raises(RuntimeError, match="illegal memory access"):
        runner.run(steps, MEGA)
    assert steps.devices == ["cuda"]
    assert "empty_cache" not in memory.events
    assert memory.events[-1] == "fraction 1.000000"


def test_an_unreadable_nvml_runs_the_step_on_the_gpu_without_a_fraction() -> None:
    memory = FakeMemory()
    nvml = FakeNvml(error=RuntimeError("NVML gone"))
    runner = GpuRunner(GPU, GpuTools(nvml=nvml, memory=memory, lease=GpuLease()))

    assert runner.run(Steps(), MEGA) == ("result on cuda", GPU_USE)
    assert memory.events == []


def test_a_gpu_without_reported_total_memory_uses_the_nvml_total() -> None:
    memory = FakeMemory()
    tools = GpuTools(nvml=FakeNvml(total=8_000, free=[4_256]), memory=memory, lease=GpuLease())
    runner = GpuRunner(GPU.model_copy(update={"total_memory_mib": None}), tools)

    runner.run(Steps(), GpuNeed("small", 100))
    assert memory.events[0] == "fraction 0.500000"


def test_the_lease_lets_one_gpu_step_run_at_a_time() -> None:
    runner, _ = _gpu()
    second_ran = threading.Event()
    waited: list[bool] = []

    def first(device: str) -> str:
        other = threading.Thread(target=lambda: runner.run(lambda d: second_ran.set(), MEGA))
        other.start()
        waited.append(not second_ran.wait(0.2))
        return "first"

    runner.run(first, MEGA)
    assert second_ran.wait(5)
    assert waited == [True]


def test_the_lease_is_released_when_a_step_fails() -> None:
    lease = GpuLease()
    tools = GpuTools(nvml=FakeNvml(), memory=FakeMemory(), lease=lease)
    runner = GpuRunner(GPU, tools)

    with pytest.raises(KeyError):
        runner.run(Steps({"cuda": KeyError("x")}), GpuNeed("small", 100))
    assert not lease.held
    with lease.hold():
        assert lease.held


def _reclaiming(free: list[int]) -> tuple[GpuRunner, FakeMemory]:
    """A GPU runner whose NVML reads ``free`` in turn and whose reclaimer is recorded."""
    memory = FakeMemory()
    tools = GpuTools(nvml=FakeNvml(total=16_000, free=free), memory=memory, lease=GpuLease())
    runner = GpuRunner(GPU, tools)
    runner.reclaim_with(lambda need: memory.events.append(f"reclaim {need.label}"))
    return runner, memory


def test_a_short_step_first_reclaims_the_workers_own_gpu_models_and_then_fits() -> None:
    runner, memory = _reclaiming(free=[1_835, 12_000])
    steps = Steps()

    result, use = runner.run(steps, MEGA)

    assert (result, use, steps.devices) == ("result on cuda", GPU_USE, ["cuda"])
    assert memory.events == [
        "reclaim Mega 53-stem", "empty_cache",
        f"fraction {(12_000 - 256) / 16_000:.6f}", "fraction 1.000000",
    ]  # fmt: skip


def test_a_step_that_still_does_not_fit_after_reclaiming_runs_on_the_cpu() -> None:
    runner, memory = _reclaiming(free=[1_835, 3_000])
    steps = Steps()

    result, use = runner.run(steps, MEGA)

    assert (result, steps.devices) == ("result on cpu", ["cpu"])
    assert use.fallback_reason == (
        "Not enough free GPU memory for Mega 53-stem (needs 10600 MiB, 3000 MiB free); "
        "ran on the CPU."
    )
    assert memory.events == ["reclaim Mega 53-stem", "empty_cache"]


def test_the_reclaim_is_not_needed_when_the_step_fits() -> None:
    runner, memory = _reclaiming(free=[12_000])
    runner.run(Steps(), MEGA)
    assert not any(event.startswith("reclaim") for event in memory.events)


def test_a_failing_reclaimer_is_logged_and_the_step_still_completes(
    caplog: pytest.LogCaptureFixture,
) -> None:
    runner, _ = _gpu(free=1_000)

    def broken(need: GpuNeed) -> None:
        raise RuntimeError("release failed")

    runner.reclaim_with(broken)
    assert runner.run(Steps(), MEGA)[0] == "result on cpu"
    assert "Releasing cached GPU models failed" in caplog.text


def test_cpu_runners_accept_a_reclaimer_and_never_call_it() -> None:
    runner = CpuRunner()
    runner.reclaim_with(lambda need: pytest.fail("a CPU worker has no GPU models"))
    assert runner.run(Steps(), MEGA)[0] == "result on cpu"


def test_release_frees_the_cached_gpu_memory() -> None:
    runner, memory = _gpu()
    runner.release()
    assert memory.events == ["empty_cache"]


@pytest.mark.parametrize(
    ("error", "expected"),
    [
        (torch.cuda.OutOfMemoryError("anything"), True),
        (RuntimeError("Failed to allocate memory for requested buffer"), True),
        (RuntimeError("CUDA out of memory"), True),
        (MemoryError(), False),
        (RuntimeError("illegal memory access"), False),
    ],
)
def test_is_gpu_out_of_memory(error: Exception, expected: bool) -> None:
    assert is_gpu_out_of_memory(error) is expected


def test_is_gpu_out_of_memory_follows_the_cause_chain_and_survives_cycles() -> None:
    inner = RuntimeError("CUDA out of memory")
    outer = ValueError("wrapped")
    outer.__cause__ = inner
    cyclic = ValueError("a")
    cyclic.__context__ = ValueError("b")
    cyclic.__context__.__context__ = cyclic

    assert is_gpu_out_of_memory(outer)
    assert not is_gpu_out_of_memory(cyclic)


def test_torch_cuda_memory_sets_the_fraction_of_gpu_zero_and_empties_the_cache(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[tuple[str, object]] = []
    record: Callable[[str], Callable[..., None]] = lambda name: lambda *a: calls.append((name, a))  # noqa: E731
    monkeypatch.setattr(torch.cuda, "set_per_process_memory_fraction", record("fraction"))
    monkeypatch.setattr(torch.cuda, "empty_cache", record("empty"))
    memory = TorchCudaMemory()

    memory.set_fraction(0.5)
    memory.empty_cache()

    assert calls == [("fraction", (0.5, 0)), ("empty", ())]
