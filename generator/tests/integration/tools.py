"""Helpers of the real-model integration tests (polling, ffprobe)."""

import json
import subprocess
import time
from pathlib import Path
from typing import Any

import pytest
from fastapi.testclient import TestClient

TERMINAL = frozenset({"completed", "failed", "cancelled"})


def wait_for(client: TestClient, generation_id: str, seconds: float = 900.0) -> dict[str, Any]:
    """Poll the generation until it is terminal (fail the test after ``seconds``)."""
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        job: dict[str, Any] = client.get(f"/v1/generations/{generation_id}").json()
        if job["status"] in TERMINAL:
            return job
        time.sleep(1)
    pytest.fail(f"generation {generation_id} did not finish within {seconds} s")


def ffprobe_tags(path: Path) -> dict[str, str]:
    """Vorbis comments (upper-case keys) plus ``_RATE``, ``_BITS`` and ``_CHANNELS``."""
    args = ["ffprobe", "-v", "quiet", "-print_format", "json", "-show_format", "-show_streams"]
    probe = subprocess.run([*args, str(path)], capture_output=True, text=True, check=True)  # noqa: S603
    info = json.loads(probe.stdout)
    stream = info["streams"][0]
    tags = {key.upper(): value for key, value in info["format"].get("tags", {}).items()}
    return tags | {
        "_RATE": str(stream["sample_rate"]),
        "_BITS": str(stream.get("bits_per_raw_sample")),
        "_CHANNELS": str(stream["channels"]),
    }
