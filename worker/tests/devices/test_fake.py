"""The fake device for E2E tests (T015, ``STEMSTUDIO_FAKE_DEVICE``, data-model.md 1)."""

from stemstudio_worker.devices.fake import FAKE_GPU, FakeDeviceRunner
from stemstudio_worker.ports import DeviceRunner, DeviceUse, GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE

NEED = GpuNeed("the fake separation", 100)
FAKE_GPU_USE = DeviceUse(kind="gpu", torch_device="cuda", name="Fake GPU")
OOM = DeviceUse(
    kind="cpu", torch_device="cpu",
    fallback_reason=(
        "The GPU ran out of memory during the fake separation; the step was repeated on the CPU."
    ),
)  # fmt: skip


def _devices(runner: FakeDeviceRunner, runs: int) -> list[tuple[str, DeviceUse]]:
    return [runner.run(lambda device: device, NEED) for _ in range(runs)]


def test_the_cpu_fake_runs_on_the_cpu() -> None:
    runner = FakeDeviceRunner("cpu")
    assert isinstance(runner, DeviceRunner)
    assert runner.info == CPU_DEVICE
    assert _devices(runner, 1) == [("cpu", DeviceUse(kind="cpu", torch_device="cpu"))]


def test_the_gpu_fake_reports_a_fake_gpu_for_every_step() -> None:
    runner = FakeDeviceRunner("gpu")
    assert runner.info == FAKE_GPU
    assert (FAKE_GPU.kind, FAKE_GPU.name) == ("gpu", "Fake GPU")
    runner.release()
    assert _devices(runner, 2) == [("cuda", FAKE_GPU_USE)] * 2


def test_the_oom_fake_falls_back_on_the_first_step_of_every_job_context() -> None:
    runner = FakeDeviceRunner("gpu-oom")
    assert runner.info == FAKE_GPU

    first_job = _devices(runner, 3)
    runner.release()
    second_job = _devices(runner, 1)

    assert first_job == [("cpu", OOM), ("cuda", FAKE_GPU_USE), ("cuda", FAKE_GPU_USE)]
    assert second_job == [("cpu", OOM)]
