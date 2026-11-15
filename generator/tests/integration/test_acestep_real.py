"""Real ACE-Step 1.5 cover of a 10 s mix on the GPU (009 T036; never in CI).

Runs only with ``-m integration`` in the ``acestep`` environment with a usable CUDA GPU, the
pinned weights under ``STEMSTUDIO_MODEL_DIR`` (default ``/models``) and ``ffmpeg``/``ffprobe``
on the PATH; skipped otherwise.
"""

import json
import os
import shutil
from collections.abc import Iterator
from dataclasses import replace
from pathlib import Path

import numpy as np
import pytest
import soundfile as sf
from fastapi.testclient import TestClient

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.main import Wiring, compose
from stemstudio_generator.ports import ACESTEP_CHECKPOINT_DIR

from ..signals import noise, stereo, tone, write
from .tools import ffprobe_tags, wait_for

pytestmark = [pytest.mark.integration, pytest.mark.gpu]

CAPTION = "Jazz trio, brushed drums, upright bass, warm piano"


def _require_gpu_stack(model_dir: Path) -> None:
    torch = pytest.importorskip("torch")
    pytest.importorskip("acestep.handler")
    if not torch.cuda.is_available():
        pytest.skip("no CUDA GPU")
    if not (model_dir / ACESTEP_CHECKPOINT_DIR / "REVISION").is_file():
        pytest.skip(f"no pinned ACE-Step weights under {model_dir}")
    if shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None:
        pytest.skip("ffmpeg/ffprobe not on the PATH")


@pytest.fixture
def client(tmp_path: Path) -> Iterator[TestClient]:
    model_dir = Path(os.environ.get("STEMSTUDIO_MODEL_DIR", "/models"))
    _require_gpu_stack(model_dir)
    settings = replace(
        GeneratorSettings(), data_dir=tmp_path / "data", model_dir=model_dir, gpu_lease_file=None
    )
    with TestClient(compose(settings, Wiring()).app) as test_client:
        yield test_client


def _mix(tmp_path: Path) -> Path:
    """A 10 s two-chord mix with a little noise (self-made, no third-party audio)."""
    first, second = tone(5.0, freq=220.0, amplitude=0.25), tone(5.0, freq=330.0, amplitude=0.25)
    signal = np.concatenate([first, second]) + noise(10.0, amplitude=0.02)
    return write(tmp_path / "mix.wav", stereo(signal.astype(np.float32)))


def _create(client: TestClient, mix: Path) -> str:
    restyle = {
        "caption": CAPTION, "audioCoverStrength": 0.55, "seeds": [42], "mode": "range",
        "rangeStartSeconds": 0.0, "rangeEndSeconds": 10.0,
    }  # fmt: skip
    body = json.dumps({"kind": "restyle", "restyle": restyle})
    files = [
        ("params", ("params.json", body, "application/json")),
        ("stem", ("mix.wav", mix.read_bytes(), "audio/wav")),
    ]  # fmt: skip
    created = client.post("/v1/generations", files=files)
    assert created.status_code == 202, created.text
    generation_id: str = created.json()["generationId"]
    return generation_id


def test_a_real_cover_of_10_seconds_is_a_labelled_48k_flac(
    client: TestClient, tmp_path: Path
) -> None:
    generation_id = _create(client, _mix(tmp_path))

    job = wait_for(client, generation_id)

    assert job["status"] == "completed", job
    take = tmp_path / "take.flac"
    take.write_bytes(client.get(f"/v1/generations/{generation_id}/takes/1").content)
    assert sf.info(take).duration == pytest.approx(10.0, abs=0.05)
    tags = ffprobe_tags(take)
    assert (tags["_RATE"], tags["_BITS"], tags["_CHANNELS"]) == ("48000", "24", "2")
    assert (tags["AI_GENERATED"], tags["COMMENT"]) == ("true", "AI-generated with StemStudio")
    assert tags["GENERATOR"].startswith("ACE-Step 1.5 (")
    assert (tags["STEMSTUDIO_GENERATION"], tags["STEMSTUDIO_TAKE"]) == (generation_id, "1")
