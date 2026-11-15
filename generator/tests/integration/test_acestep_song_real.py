"""Real ACE-Step 1.5 song of 10 s from two lyric lines on the GPU (010 T026; never in CI).

Same requirements as ``test_acestep_real.py``. The profile that ran (``planned`` with the bf16 LM
or ``direct`` when less than ``SONG_PLANNED`` was free) is printed for the hand-off.
"""

from pathlib import Path

import pytest
import soundfile as sf
from fastapi.testclient import TestClient

from .test_acestep_real import client
from .tools import ffprobe_tags, wait_for

pytestmark = [pytest.mark.integration, pytest.mark.gpu]

__all__ = ["client"]

SONG = {
    "caption": "Acoustic pop, warm guitar, soft drums, female lead vocal, gentle",
    "lyrics": (
        "[verse]\nPaper planes across the morning sky\nEvery little dream is learning how to fly"
    ),
    "instrumental": False,
    "vocalLanguage": "en",
    "durationSeconds": 10.0,
    "seeds": [42],
    "thinking": True,
}


def test_a_real_song_of_10_seconds_is_labelled_synthetic(
    client: TestClient, tmp_path: Path
) -> None:
    created = client.post("/v1/generations", json={"kind": "song", "song": SONG})
    assert created.status_code == 202, created.text
    generation_id = created.json()["generationId"]

    job = wait_for(client, generation_id)

    assert job["status"] == "completed", job
    take = job["takes"][0]
    print(f"song profile={take['profile']} device={job['device']}")  # noqa: T201 - hand-off evidence
    assert (take["vocal"], take["profile"] in {"planned", "direct"}) == ("synthetic", True)
    path = tmp_path / "song.flac"
    path.write_bytes(client.get(f"/v1/generations/{generation_id}/takes/1").content)
    assert sf.info(path).duration == pytest.approx(10.0, abs=0.05)
    tags = ffprobe_tags(path)
    assert (tags["AI_VOICE"], tags["STEMSTUDIO_TAKE"], tags["_RATE"]) == ("synthetic", "1", "48000")
