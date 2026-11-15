"""Composition of the feature 006 alignment parts (006 T041): fake stack, GPU model release."""

from pathlib import Path
from typing import Any

from fastapi.testclient import TestClient

from stemstudio_worker import main
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.ports import WHISPER_GPU_NEED, GpuNeed

from .conftest import Api
from .contract import assert_conforms
from .fakes import ApiFakes
from .lyrics_fakes import WAIT, GatedAligner

TEXT = "Fake line one\nLa la la"


def test_the_fake_stack_aligns_the_vocal_stem_with_the_fake_aligner(api: Api) -> None:
    standard = api.completed_job()
    body = {"mode": "align", "standardJobId": standard, "text": TEXT}

    job_id = api.client.post("/v1/lyrics-jobs", json=body).json()["lyricsJobId"]
    assert api.client.app.state.runtime.scheduler.join(WAIT)  # type: ignore[attr-defined]
    result = api.client.get(f"/v1/lyrics-jobs/{job_id}/result")

    assert_conforms(result, "/v1/lyrics-jobs/{lyricsJobId}/result")
    words = [w for line in result.json()["lyrics"]["lines"] for w in line["words"]]
    assert [w["startMs"] for w in words] == [2000 + 500 * k for k in range(6)]
    assert result.json()["lyrics"]["origin"] == "aligned"


class _Releasing(GatedAligner):
    """A gated aligner that counts releases."""

    released: int = 0

    def release(self) -> None:
        self.released += 1


class _Runner(FakeDeviceRunner):
    """The fake GPU runner that keeps the reclaim hook."""

    reclaim: Any = None

    def reclaim_with(self, reclaim: Any) -> None:
        self.reclaim = reclaim


def test_aligner_models_are_released_when_idle_and_for_other_gpu_steps(tmp_path: Path) -> None:
    aligner, runner = _Releasing(), _Runner("gpu")
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", fake_detail_step_seconds=0.0)
    app = main.create_app(settings, ApiFakes().adapters(), runner,
                          main.AppOverrides(aligner=aligner))  # fmt: skip
    url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
    with TestClient(app) as client:
        client.post("/v1/jobs", json={"sourceType": "youtube", "url": url})
        assert client.app.state.runtime.scheduler.join(WAIT)  # type: ignore[attr-defined]
        idle = aligner.released
        runner.reclaim(WHISPER_GPU_NEED)
        kept = aligner.released
        runner.reclaim(GpuNeed("Mega 53-stem", 9_000))

    assert idle >= 1
    assert kept == idle
    assert aligner.released == idle + 1
