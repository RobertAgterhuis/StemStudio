"""Tests of the composition of the feature 005/007 adapters in ``main.py``.

The adapter packages (``metadata``, ``render``) are built by audio-ingest in parallel; the
composition root looks their factories up by name, so the tests install stand-in modules.
"""

import sys
import types
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import ImageNormalizer

from .fakes import THUMBNAIL, ApiFakes, StaticNormalizer

METADATA = "stemstudio_worker.metadata"


def _module(monkeypatch: pytest.MonkeyPatch, name: str, **members: object) -> None:
    module = types.ModuleType(name)
    for key, value in members.items():
        setattr(module, key, value)
    monkeypatch.setitem(sys.modules, name, module)


def test_a_present_factory_is_returned(monkeypatch: pytest.MonkeyPatch) -> None:
    def factory(settings: WorkerSettings) -> str:
        return "built"

    _module(monkeypatch, METADATA, create_image_normalizer=factory)

    assert main.optional_factory(METADATA, "create_image_normalizer") is factory


def test_a_missing_package_or_name_gives_none(
    monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
) -> None:
    monkeypatch.setitem(sys.modules, METADATA, None)
    assert main.optional_factory(METADATA, "create_image_normalizer") is None
    _module(monkeypatch, METADATA)
    assert main.optional_factory(METADATA, "create_image_normalizer") is None
    assert "is not available" in caplog.text


def test_a_broken_dependency_of_a_present_package_is_not_hidden(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def broken(name: str) -> types.ModuleType:
        raise ModuleNotFoundError("No module named 'pillow_x'", name="pillow_x")

    monkeypatch.setattr(main.importlib, "import_module", broken)

    with pytest.raises(ModuleNotFoundError):
        main.optional_factory(METADATA, "create_image_normalizer")


def _job_artwork(tmp_path: Path) -> object:
    fakes = ApiFakes()
    fakes.artwork = THUMBNAIL
    app = main.create_app(WorkerSettings(data_dir=tmp_path, separator="fake"), fakes.adapters())
    with TestClient(app) as client:
        job_id = client.post("/v1/jobs", json={"sourceType": "youtube", "url": _URL}).json()
        client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]
        return client.get(f"/v1/jobs/{job_id['jobId']}").json()["artwork"]


_URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"


def test_the_default_normaliser_comes_from_the_metadata_factory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    seen: list[WorkerSettings] = []

    def factory(settings: WorkerSettings) -> ImageNormalizer:
        seen.append(settings)
        return StaticNormalizer()

    _module(monkeypatch, METADATA, create_image_normalizer=factory)

    artwork = _job_artwork(tmp_path)

    assert isinstance(artwork, dict)
    assert artwork["source"] == "youtube"
    assert len(seen) == 1


def test_without_the_metadata_package_jobs_have_no_artwork(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setitem(sys.modules, METADATA, None)

    assert _job_artwork(tmp_path) is None
