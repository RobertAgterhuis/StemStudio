"""Skeleton smoke tests: the package imports and the placeholder app answers /health."""

import importlib

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.main import create_app


@pytest.mark.parametrize(
    "module",
    ["api", "jobs", "ingest", "separation", "analysis", "ports", "schemas", "config", "main"],
)
def test_module_imports(module: str) -> None:
    assert importlib.import_module(f"stemstudio_worker.{module}") is not None


def test_health_reports_settings(tmp_path: object) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", device="auto")  # type: ignore[arg-type]
    client = TestClient(create_app(settings))

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "separator": "fake",
        "device": "auto",
        "contractVersion": "1.1.0",
    }


def test_create_app_reads_environment(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STEMSTUDIO_SEPARATOR", "fake")

    response = TestClient(create_app()).get("/health")

    assert response.json()["separator"] == "fake"
