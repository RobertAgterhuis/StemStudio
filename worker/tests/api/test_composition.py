"""Tests of the composition of the feature 005 adapters in ``main.py``.

The production factories come from ``stemstudio_worker.metadata`` (audio-ingest); the tests
replace them on ``main`` to see what the composition root hands to the pipeline.
"""

from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ingest.details import SourceDetails
from stemstudio_worker.ports import ImageNormalizer, IngestAdapters

from .fakes import THUMBNAIL, ApiFakes, StaticNormalizer

_URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"


def _job_artwork(tmp_path: Path) -> object:
    fakes = ApiFakes()
    fakes.artwork = THUMBNAIL
    app = main.create_app(WorkerSettings(data_dir=tmp_path, separator="fake"), fakes.adapters())
    with TestClient(app) as client:
        job_id = client.post("/v1/jobs", json={"sourceType": "youtube", "url": _URL}).json()
        client.app.state.runtime.scheduler.join()  # type: ignore[attr-defined]
        return client.get(f"/v1/jobs/{job_id['jobId']}").json()["artwork"]


def test_the_default_normaliser_comes_from_the_metadata_factory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    seen: list[WorkerSettings] = []

    def factory(settings: WorkerSettings) -> ImageNormalizer:
        seen.append(settings)
        return StaticNormalizer()

    monkeypatch.setattr(main, "create_image_normalizer", factory)

    artwork = _job_artwork(tmp_path)

    assert isinstance(artwork, dict)
    assert artwork["source"] == "youtube"
    assert len(seen) == 1


def test_the_fake_stack_normaliser_gives_jobs_a_picture(tmp_path: Path) -> None:
    artwork = _job_artwork(tmp_path)

    assert isinstance(artwork, dict)
    assert artwork["source"] == "youtube"


def test_the_ingest_adapters_get_the_source_details_reader(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    sentinel = object()
    seen: list[object] = []

    def ingest(settings: WorkerSettings, details: SourceDetails | None = None) -> IngestAdapters:
        seen.append(details)
        return ApiFakes().adapters().ingest

    monkeypatch.setattr(main, "create_source_details", lambda settings: sentinel)
    monkeypatch.setattr(main, "create_ingest_adapters", ingest)

    with TestClient(main.create_app(WorkerSettings(data_dir=tmp_path, separator="fake"))):
        pass

    assert seen == [sentinel]
