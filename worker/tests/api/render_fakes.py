"""A scriptable fake ``RenderExecutor`` and request bodies for the render route tests."""

import hashlib
import threading
from pathlib import Path
from typing import Any

from stemstudio_worker.ports import RenderProgress
from stemstudio_worker.schemas import RenderJobRequest, RenderResult

WAIT = 5.0
STEM = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/drums.flac"
OUTPUT = b"fLaC rendered audio"


def body(**changes: Any) -> dict[str, Any]:
    """A valid ``RenderJobRequest`` JSON body (the contract's raw stem example) with changes."""
    request: dict[str, Any] = {
        "inputs": [{"path": STEM, "gain": 1, "pan": 0}],
        "masterGain": 1,
        "range": None,
        "output": {"format": "flac", "bitDepth": 16, "mp3BitrateKbps": 320, "dither": False},
        "metadata": {"title": "Song - drums", "artist": "Band", "comment": None},
    }
    request.update(changes)
    return request


class FakeExecutor:
    """Validates and renders without audio; ``gate`` holds a render between its two reports."""

    def __init__(self) -> None:
        self.validate_error: Exception | None = None
        self.render_error: Exception | None = None
        self.gate = threading.Event()
        self.gate.set()
        self.entered = threading.Event()
        self.validated: list[RenderJobRequest] = []
        self.out_dirs: list[Path] = []

    def validate(self, request: RenderJobRequest) -> None:
        self.validated.append(request)
        if self.validate_error is not None:
            raise self.validate_error

    def render(
        self, request: RenderJobRequest, out_dir: Path, progress: RenderProgress
    ) -> RenderResult:
        self.out_dirs.append(out_dir)
        progress(0.25)
        self.entered.set()
        assert self.gate.wait(WAIT), "render gate never opened"
        progress(0.75)
        (out_dir / "output.flac").write_bytes(OUTPUT)
        if self.render_error is not None:
            raise self.render_error
        return RenderResult(
            file_name="output.flac", content_type="audio/flac", size_bytes=len(OUTPUT),
            sha256=hashlib.sha256(OUTPUT).hexdigest(), duration_seconds=1.5,
            peak_dbfs=-0.1, applied_gain_db=0.0,
        )  # fmt: skip
