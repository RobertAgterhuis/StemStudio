"""``GET /health`` and readiness (009 T051, US6, FR-010)."""

from pathlib import Path
from typing import Any

import pytest

from stemstudio_generator.devices.probe import NO_CUDA_BUILD, NOT_AVAILABLE
from stemstudio_generator.main import Wiring, compose
from stemstudio_generator.schemas import CONTRACT_VERSION, DeviceInfo

from ..contract.docs import V110
from .stack import Stack, multipart, open_stack, restyle_params, settings_for, stem


def _health(stack: Stack) -> dict[str, Any]:
    response = stack.client.get("/health")
    assert response.status_code == 200
    body: dict[str, Any] = response.json()
    assert V110.errors("GeneratorHealth", body) == []
    return body


def test_a_ready_fake_stack_reports_gpu_models_and_queue(stack: Stack, tmp_path: Path) -> None:
    files = multipart(restyle_params(), [stem(tmp_path, "a.wav")])
    stack.client.post("/v1/generations", files=files)

    health = _health(stack)

    assert health["contractVersion"] == CONTRACT_VERSION
    assert (health["engine"], health["kinds"], health["voiceSampleCheck"]) == (
        "fake", ["restyle", "song"], False,
    )  # fmt: skip
    assert (health["ready"], health["reason"], health["device"]["kind"]) == (True, None, "gpu")
    assert [model["id"] for model in health["models"]] == [
        "acestep-v15-turbo", "acestep-vae", "qwen3-embedding-0.6b", "acestep-5hz-lm-1.7b",
    ]  # fmt: skip
    assert health["queue"] == {"queued": 1, "running": None}


def test_fake_no_gpu_mode_is_not_ready_with_the_probe_reason(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "no-gpu")):
        health = _health(stack)

        assert (health["ready"], health["reason"]) == (False, NOT_AVAILABLE)
        assert health["device"] == {"kind": "cpu", "name": None, "computeCapability": None,
                                    "totalMemoryMiB": None, "freeMemoryMiB": None,
                                    "reason": NOT_AVAILABLE}  # fmt: skip


@pytest.mark.parametrize("kind", ["restyle"])
def test_every_create_is_refused_without_a_gpu(tmp_path: Path, kind: str) -> None:
    for stack in open_stack(settings_for(tmp_path, "no-gpu")):
        response = stack.client.post(
            "/v1/generations", files=multipart(restyle_params(), [stem(tmp_path, "a.wav")])
        )

        assert response.status_code == 409
        assert response.json() == {
            "type": "about:blank", "title": "GPU required", "status": 409,
            "detail": NOT_AVAILABLE, "code": "gpuRequired",
        }  # fmt: skip
        assert stack.composition.store.list_all() == []


def test_a_real_engine_without_torch_starts_not_ready_with_the_cpu_build_reason(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    settings = settings_for(tmp_path, engine="acestep")
    monkeypatch.setitem(__import__("sys").modules, "torch", None)
    composition = compose(settings, Wiring(start_worker=False))
    from fastapi.testclient import TestClient  # noqa: PLC0415

    with TestClient(composition.app) as client:
        health = client.get("/health").json()

    assert V110.errors("GeneratorHealth", health) == []
    assert (health["engine"], health["ready"], health["reason"]) == (
        "acestep",
        False,
        NO_CUDA_BUILD,
    )
    assert all(model["present"] is False for model in health["models"])


def test_a_real_gpu_probe_result_makes_the_service_ready(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    gpu = DeviceInfo(kind="gpu", name="NVIDIA GeForce RTX 5070 Ti", compute_capability="12.0",
                     total_memory_mib=16_303, free_memory_mib=14_000)  # fmt: skip
    monkeypatch.setattr("stemstudio_generator.main.GpuProbe.probe", lambda self: gpu)
    monkeypatch.setattr("stemstudio_generator.main.load_torch_cap", _NoCap)
    composition = compose(settings_for(tmp_path, engine="acestep"), Wiring(start_worker=False))
    from fastapi.testclient import TestClient  # noqa: PLC0415

    with TestClient(composition.app) as client:
        health = client.get("/health").json()

    assert (health["ready"], health["device"]["name"]) == (True, "NVIDIA GeForce RTX 5070 Ti")


class _NoCap:
    def limit(self, fraction: float) -> None:
        del fraction

    def reset(self) -> None:
        """Nothing."""
