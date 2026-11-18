"""``/health`` of the practice lane: ``lanes.practice`` and ``structure`` (014 T033, 1.8.0)."""

from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.main import create_app

from .fakes import ApiFakes


@pytest.mark.parametrize(("kind", "name"), [("real", "stems-novelty"), ("fake", "fake")])
def test_health_reports_the_practice_lane_and_the_analyser(
    tmp_path: Path, kind: str, name: str
) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake",
                              structure_analyzer=kind)  # type: ignore[arg-type]  # fmt: skip

    with TestClient(create_app(settings, ApiFakes().adapters())) as client:
        body = client.get("/health").json()
        runtime = client.app.state.runtime  # type: ignore[attr-defined]
        practice = runtime.lanes.practice.lane

    assert body["lanes"]["practice"] == {"queued": 0, "running": False}
    assert body["structure"] == {"analyzer": name, "version": "1.0.0"}
    assert practice.sweep in runtime.lanes.sweeper._tasks
    assert (tmp_path / "section-jobs").is_dir()


def test_health_before_start_up_reports_the_defaults(tmp_path: Path) -> None:
    client = TestClient(create_app(WorkerSettings(data_dir=tmp_path, separator="fake")))

    body = client.get("/health").json()

    assert body["lanes"]["practice"] == {"queued": 0, "running": False}
    assert body["structure"] == {"analyzer": "stems-novelty", "version": "1.0.0"}
