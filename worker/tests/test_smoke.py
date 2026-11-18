"""Skeleton smoke tests: the package imports and the placeholder app answers /health."""

import importlib
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker.config import RenderSettings, WorkerSettings
from stemstudio_worker.main import create_app


@pytest.mark.parametrize(
    "module",
    ["api", "jobs", "ingest", "separation", "analysis", "ports", "schemas", "config", "main"],
)
def test_module_imports(module: str) -> None:
    assert importlib.import_module(f"stemstudio_worker.{module}") is not None


def test_health_reports_settings(tmp_path: object) -> None:
    library = Path(str(tmp_path)) / "library"
    library.mkdir()
    settings = WorkerSettings(
        data_dir=Path(str(tmp_path)), separator="fake", device="auto",
        render=RenderSettings(library_dir=library),
    )  # fmt: skip
    with TestClient(create_app(settings)) as client:
        response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "separator": "fake",
        "device": "auto",
        "deviceInfo": {
            "kind": "cpu",
            "name": None,
            "computeCapability": None,
            "totalMemoryMiB": None,
            "freeMemoryMiB": None,
            "reason": None,
        },
        "onlineLookups": True,
        "library": {"readable": True, "reason": None},
        "lanes": {
            "render": {"queued": 0, "running": False},
            "transcribe": {"queued": 0, "running": False},
            "practice": {"queued": 0, "running": False},
        },
        "lyricsAlignment": {
            "available": True,
            "model": "large-v3-turbo",
            "modelCached": True,
            "auto": "gpu",
            "reason": None,
        },
        "transcription": {
            "models": {
                "basic-pitch-icassp2022": "not-downloaded",
                "piano-crnn-note-pedal": "not-downloaded",
            },
            "font": {"leipzig": False},
        },
        "structure": {"analyzer": "stems-novelty", "version": "1.0.0"},
        "contractVersion": "1.8.0",
    }


def test_create_app_reads_environment(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STEMSTUDIO_SEPARATOR", "fake")

    response = TestClient(create_app()).get("/health")

    assert response.json()["separator"] == "fake"
