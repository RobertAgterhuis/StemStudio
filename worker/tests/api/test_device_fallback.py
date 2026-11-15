"""Fallbacks end to end through the HTTP API (T034, FR-003, FR-004, SC-004).

The composition of ``main`` is used with fake ingest/analysis and fake models, so the device
runner that ``main`` builds (fake ``gpu-oom`` device, or the real factory with a failing or no
probe) drives both the standard and the detail job.
"""

from pathlib import Path
from typing import Any

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import devices, main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail.fake import FakeSource
from stemstudio_worker.detail.steps import DetailPipeline
from stemstudio_worker.devices.probe import NOT_AVAILABLE
from stemstudio_worker.schemas import DeviceInfo
from stemstudio_worker.separation.fake import FakeSeparator

from .contract import assert_conforms
from .fakes import YOUTUBE, ApiFakes

CPU_RECORD = {"kind": "cpu", "name": None, "cpuFallback": False, "fallbackReason": None}


@pytest.fixture(autouse=True)
def fake_models(monkeypatch: pytest.MonkeyPatch) -> None:
    """Fake ingest/analysis; fake separator and detail models built on main's device runner."""
    fakes = ApiFakes()
    monkeypatch.setattr(main, "create_ingest_adapters", lambda s: fakes.adapters().ingest)
    monkeypatch.setattr(main, "create_beat_analyzer", lambda s: fakes)
    monkeypatch.setattr(main, "create_separator", lambda s, runner: FakeSeparator(runner))
    monkeypatch.setattr(
        main, "create_detail_pipeline", lambda s, runner: DetailPipeline(FakeSource(0.0), runner)
    )


def _wait(client: TestClient) -> None:
    client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]


def _import_with_detail(client: TestClient) -> tuple[dict[str, Any], dict[str, Any]]:
    """Run a standard job, then its detail job; return both final status bodies."""
    job_id = client.post("/v1/jobs", json={"sourceType": "youtube", "url": YOUTUBE}).json()["jobId"]
    _wait(client)
    detail_id = client.post("/v1/detail-jobs", json={"standardJobId": job_id}).json()["detailJobId"]
    _wait(client)
    job = client.get(f"/v1/jobs/{job_id}")
    detail = client.get(f"/v1/detail-jobs/{detail_id}")
    assert_conforms(job, "/v1/jobs/{jobId}")
    assert_conforms(detail, "/v1/detail-jobs/{detailJobId}")
    return job.json(), detail.json()


def test_a_simulated_gpu_oom_completes_both_jobs_on_the_cpu_with_the_reason(
    tmp_path: Path,
) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", fake_device="gpu-oom")
    with TestClient(main.create_app(settings)) as client:
        job, detail = _import_with_detail(client)

    assert (job["status"], detail["status"]) == ("completed", "completed")
    assert job["device"] == {
        "kind": "cpu", "name": None, "cpuFallback": True,
        "fallbackReason": "The GPU ran out of memory during the standard separation; "
        "the step was repeated on the CPU.",
    }  # fmt: skip
    assert (detail["device"]["kind"], detail["device"]["name"]) == ("gpu", "Fake GPU")
    assert detail["device"]["cpuFallback"] is True
    assert "ran out of memory" in detail["device"]["fallbackReason"]


def test_a_failing_probe_runs_everything_on_the_cpu_and_reports_why(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    failed = DeviceInfo(kind="cpu", reason=NOT_AVAILABLE)
    monkeypatch.setattr(devices, "default_probe", lambda preference: failed)
    real_factory = devices.create_device_runner
    monkeypatch.setattr(
        main, "create_device_runner", lambda s: real_factory(s, devices.default_probe)
    )
    settings = WorkerSettings(data_dir=tmp_path, separator="demucs", device="cuda")

    with TestClient(main.create_app(settings)) as client:
        health = client.get("/health")
        job, detail = _import_with_detail(client)

    assert_conforms(health, "/health")
    assert (health.json()["device"], health.json()["deviceInfo"]["kind"]) == ("cuda", "cpu")
    assert health.json()["deviceInfo"]["reason"] == NOT_AVAILABLE
    assert job["device"] == CPU_RECORD
    assert detail["device"] == CPU_RECORD


def test_the_cpu_preference_never_probes_and_reports_the_cpu_without_fallback(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def no_torch() -> object:
        raise AssertionError("STEMSTUDIO_DEVICE=cpu must not probe the GPU")

    monkeypatch.setattr(devices, "load_torch_probe", no_torch)
    monkeypatch.setattr(devices, "_nvml", no_torch)
    settings = WorkerSettings(data_dir=tmp_path, separator="demucs", device="cpu")

    with TestClient(main.create_app(settings)) as client:
        health = client.get("/health").json()
        job, detail = _import_with_detail(client)

    assert health["deviceInfo"] == {
        "kind": "cpu", "name": None, "computeCapability": None, "totalMemoryMiB": None,
        "freeMemoryMiB": None, "reason": None,
    }  # fmt: skip
    assert job["device"] == CPU_RECORD
    assert detail["device"] == CPU_RECORD
