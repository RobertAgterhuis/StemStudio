"""``/health`` device info and the composition of the device runner (T027/T028, FR-003, FR-006)."""

from collections.abc import Callable
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.devices import ManagedRunner
from stemstudio_worker.devices.runner import CpuRunner
from stemstudio_worker.ports import DeviceUse, GpuNeed, PipelineAdapters
from stemstudio_worker.schemas import DeviceInfo
from stemstudio_worker.separation.fake import FakeSeparator

from .contract import assert_conforms
from .fakes import YOUTUBE, ApiFakes

GPU = DeviceInfo(kind="gpu", name="NVIDIA GeForce RTX 5070 Ti", compute_capability="12.0",
                 total_memory_mib=16_303, free_memory_mib=15_469)  # fmt: skip


class RecordingRunner:
    """A GPU ``ManagedRunner`` that counts its steps and idle releases."""

    info = GPU

    def __init__(self) -> None:
        self.needs: list[GpuNeed] = []
        self.released = 0
        self.reclaimers: list[Callable[[GpuNeed], None]] = []

    def reclaim_with(self, reclaim: Callable[[GpuNeed], None]) -> None:
        self.reclaimers.append(reclaim)

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        self.needs.append(need)
        return work("cuda"), DeviceUse(kind="gpu", torch_device="cuda", name=GPU.name)

    def release(self) -> None:
        self.released += 1


def _settings(tmp_path: Path, **changes: object) -> WorkerSettings:
    return WorkerSettings(data_dir=tmp_path, separator="fake", fake_detail_step_seconds=0.0,
                          **changes)  # type: ignore[arg-type]  # fmt: skip


def _complete(client: TestClient) -> dict[str, object]:
    job_id = client.post("/v1/jobs", json={"sourceType": "youtube", "url": YOUTUBE}).json()["jobId"]
    client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]
    body: dict[str, object] = client.get(f"/v1/jobs/{job_id}").json()
    return body


def test_health_reports_the_fake_gpu_and_jobs_record_it(tmp_path: Path) -> None:
    settings = _settings(tmp_path, fake_device="gpu")
    with TestClient(main.create_app(settings, adapters=_fake_adapters())) as client:
        response = client.get("/health")
        job = _complete(client)

    assert_conforms(response, "/health")
    info = response.json()["deviceInfo"]
    assert (info["kind"], info["name"]) == ("gpu", "Fake GPU")
    assert job["device"] == {"kind": "gpu", "name": "Fake GPU", "cpuFallback": False,
                             "fallbackReason": None}  # fmt: skip


def test_an_injected_runner_is_shared_reported_and_released_when_idle(tmp_path: Path) -> None:
    runner = RecordingRunner()
    assert isinstance(runner, ManagedRunner)
    app = main.create_app(_settings(tmp_path), adapters=_fake_adapters(runner), devices=runner)
    with TestClient(app) as client:
        health = client.get("/health").json()
        job = _complete(client)

    assert health["deviceInfo"]["name"] == GPU.name
    assert health["deviceInfo"]["totalMemoryMiB"] == 16_303
    assert job["device"] == {"kind": "gpu", "name": GPU.name, "cpuFallback": False,
                             "fallbackReason": None}  # fmt: skip
    assert runner.released >= 1


@pytest.fixture
def built(monkeypatch: pytest.MonkeyPatch) -> list[RecordingRunner]:
    """Replace the real factories: record every device runner ``main`` builds."""
    runners: list[RecordingRunner] = []

    def factory(settings: WorkerSettings) -> RecordingRunner:
        runners.append(RecordingRunner())
        return runners[-1]

    fakes = ApiFakes()
    monkeypatch.setattr(main, "create_device_runner", factory)
    monkeypatch.setattr(main, "create_ingest_adapters", lambda s: fakes.adapters().ingest)
    monkeypatch.setattr(main, "create_beat_analyzer", lambda s: fakes)
    return runners


def test_the_default_composition_builds_one_runner_for_every_consumer(
    tmp_path: Path, built: list[RecordingRunner]
) -> None:
    with TestClient(main.create_app(_settings(tmp_path))) as client:
        job = _complete(client)
        detail = client.post("/v1/detail-jobs", json={"standardJobId": job["jobId"]}).json()
        client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]
        status = client.get(f"/v1/detail-jobs/{detail['detailJobId']}").json()

    assert len(built) == 1
    (reclaim,) = built[0].reclaimers
    reclaim(GpuNeed("UVR MDX-Net KARA 2", 3_600))  # the fake pipeline has no GPU models
    assert {need.label for need in built[0].needs} == {
        "the standard separation", "the detail separation",
    }  # fmt: skip
    assert status["device"]["kind"] == "gpu"


def test_an_injected_pipeline_without_gpu_models_still_releases_the_runner(
    tmp_path: Path,
) -> None:
    runner = RecordingRunner()

    class Pipeline:
        separator_version = "1"

        def plan(self, stems: object, partial: object) -> list[object]:
            return []

        def outcomes(self, names: object, partial: object) -> list[object]:
            return []

    app = main.create_app(_settings(tmp_path), adapters=_fake_adapters(runner),
                          devices=runner, overrides=main.AppOverrides(Pipeline()))  # type: ignore[arg-type]  # fmt: skip
    with TestClient(app) as client:
        _complete(client)
    assert runner.released >= 1
    runner.reclaimers[0](GpuNeed("KARA 2", 3_600))  # nothing to release: no error


def test_a_cpu_worker_reports_the_cpu_with_the_probe_reason(tmp_path: Path) -> None:
    info = DeviceInfo(kind="cpu", reason="No usable NVIDIA GPU: CUDA is not available.")
    app = main.create_app(_settings(tmp_path), adapters=_fake_adapters(), devices=CpuRunner(info))
    with TestClient(app) as client:
        response = client.get("/health")
    assert_conforms(response, "/health")
    assert response.json()["deviceInfo"] == {
        "kind": "cpu", "name": None, "computeCapability": None, "totalMemoryMiB": None,
        "freeMemoryMiB": None, "reason": info.reason,
    }  # fmt: skip


def _fake_adapters(runner: ManagedRunner | None = None) -> PipelineAdapters:
    return ApiFakes().adapters(FakeSeparator(runner) if runner else None)
