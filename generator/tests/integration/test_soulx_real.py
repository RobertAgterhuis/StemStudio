"""Real SoulX-Singer-SVC conversion and RMVPE sample check on the GPU (010 T043; never in CI).

Runs only with ``-m integration`` in the ``soulx`` environment with the SoulX-Singer source on
the path, a usable CUDA GPU, the pinned singer weights under ``STEMSTUDIO_MODEL_DIR`` and the
qa voice fixtures (``tests/fixtures/generate_voice_fixtures.py``): ``sung-target-10s.wav`` is the
vocal stem, ``own-voice-20s.wav`` the voice sample (both synthetic, CC0).
"""

import json
import os
import shutil
from collections.abc import Iterator
from dataclasses import replace
from pathlib import Path

import pytest
import soundfile as sf
from fastapi.testclient import TestClient

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.main import Wiring, compose
from stemstudio_generator.ports import SOULX_SVC_FILE

from .tools import ffprobe_tags, wait_for

pytestmark = [pytest.mark.integration, pytest.mark.gpu]

FIXTURES = Path(__file__).resolve().parents[3] / "tests" / "fixtures" / "audio"
TARGET = FIXTURES / "sung-target-10s.wav"
PROMPT = FIXTURES / "own-voice-20s.wav"


def _require(model_dir: Path) -> None:
    torch = pytest.importorskip("torch")
    pytest.importorskip("soulxsinger.models.soulxsinger_svc")
    if not torch.cuda.is_available():
        pytest.skip("no CUDA GPU")
    if not (model_dir / SOULX_SVC_FILE).is_file():
        pytest.skip(f"no pinned SoulX weights under {model_dir}")
    if not (TARGET.is_file() and PROMPT.is_file()):
        pytest.skip("run tests/fixtures/generate_voice_fixtures.py first")
    if shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None:
        pytest.skip("ffmpeg/ffprobe not on the PATH")


@pytest.fixture
def singer(tmp_path: Path) -> Iterator[TestClient]:
    model_dir = Path(os.environ.get("STEMSTUDIO_MODEL_DIR", "/models"))
    _require(model_dir)
    settings = replace(
        GeneratorSettings(), engine="soulx", kinds=("voiceConversion",),
        data_dir=tmp_path / "data", model_dir=model_dir, gpu_lease_file=None,
    )  # fmt: skip
    with TestClient(compose(settings, Wiring()).app) as client:
        yield client


def test_the_own_voice_fixture_passes_the_real_sample_check(singer: TestClient) -> None:
    files = {"sample": (PROMPT.name, PROMPT.read_bytes(), "audio/wav")}

    check = singer.post("/v1/voice-samples/check", files=files).json()

    print(f"sample check voicedRatio={check['voicedRatio']}")  # noqa: T201 - hand-off evidence
    assert check["ok"] is True, check


def test_a_real_conversion_of_10_seconds_is_labelled_converted(
    singer: TestClient, tmp_path: Path
) -> None:
    params = {"kind": "voiceConversion", "voiceConversion": {"profileName": "Fixture voice"}}
    files = [
        ("params", ("params.json", json.dumps(params), "application/json")),
        ("target", (TARGET.name, TARGET.read_bytes(), "audio/wav")),
        ("prompt", (PROMPT.name, PROMPT.read_bytes(), "audio/wav")),
    ]
    created = singer.post("/v1/generations", files=files)
    assert created.status_code == 202, created.text
    generation_id = created.json()["generationId"]

    job = wait_for(singer, generation_id)

    assert job["status"] == "completed", job
    take = tmp_path / "take.flac"
    take.write_bytes(singer.get(f"/v1/generations/{generation_id}/takes/1").content)
    assert sf.info(take).duration == pytest.approx(10.0, abs=0.05)
    tags = ffprobe_tags(take)
    assert (tags["AI_VOICE"], tags["AI_VOICE_PROFILE"]) == ("converted", "Fixture voice")
    assert tags["GENERATOR"].startswith("SoulX-Singer-SVC (")
    vocal = singer.get(f"/v1/generations/{generation_id}/takes/1/vocal")
    assert vocal.status_code == 200
