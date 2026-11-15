"""A composed fake generator stack for API tests (fake engine, real gate/jobs, no ffmpeg)."""

import json
import shutil
from collections.abc import Iterator
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any

import numpy as np
from fastapi.testclient import TestClient

from stemstudio_generator.audio.flac import describe_file
from stemstudio_generator.config import FakeGeneratorMode, GeneratorSettings
from stemstudio_generator.main import Composition, Wiring, compose
from stemstudio_generator.ports import TakeLabels, WrittenFile

from ..contract.docs import V110
from ..fakes import FakeClock
from ..signals import stereo, tone, write

RATE = 48_000


class CopyWriter:
    """``TakeWriter`` without ffmpeg: copies the engine file and records the labels."""

    def __init__(self) -> None:
        self.labels: list[TakeLabels] = []

    def write(self, source: Path, dest: Path, labels: TakeLabels) -> WrittenFile:
        assert source != dest
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, dest)
        self.labels.append(labels)
        return describe_file(dest)


@dataclass
class Ids:
    """Predictable generation ids."""

    counter: int = 0

    def __call__(self) -> str:
        self.counter += 1
        return f"{self.counter:032x}"


@dataclass
class Stack:
    """The composed app with handles for driving the queue."""

    composition: Composition
    client: TestClient
    writer: CopyWriter
    clock: FakeClock
    settings: GeneratorSettings
    ids: Ids = field(default_factory=Ids)

    def run_all(self) -> None:
        """Process every queued job."""
        while self.composition.scheduler.run_next():
            pass

    def get(self, generation_id: str) -> dict[str, Any]:
        response = self.client.get(f"/v1/generations/{generation_id}")
        assert response.status_code == 200, response.text
        body: dict[str, Any] = response.json()
        assert V110.errors("Generation", body) == []
        return body


def settings_for(
    tmp_path: Path, mode: FakeGeneratorMode = "ready", **changes: Any
) -> GeneratorSettings:
    """Fake-engine settings with instant steps and no lock file."""
    base = GeneratorSettings(
        engine="fake",
        fake_mode=mode,
        fake_step_seconds=0.0,
        data_dir=tmp_path / "data",
        model_dir=tmp_path / "models",
        gpu_lease_file=None,
        gpu_memory_wait_seconds=0.0,
    )
    return replace(base, **changes)


def open_stack(settings: GeneratorSettings, ids: Ids | None = None) -> Iterator[Stack]:
    """Compose the stack (worker thread off) and yield it inside a running TestClient."""
    clock, writer, ids = FakeClock(), CopyWriter(), ids or Ids()
    wiring = Wiring(clock=clock, writer=writer, new_id=ids, start_worker=False, sleep=_no_sleep)
    composition = compose(settings, wiring)
    with TestClient(composition.app) as client:
        yield Stack(composition, client, writer, clock, settings, ids)


def _no_sleep(seconds: float) -> None:
    del seconds


def stem(tmp_path: Path, name: str, seconds: float = 30.0, rate: int = RATE) -> Path:
    """A stereo tone stem file."""
    return write(tmp_path / name, stereo(tone(seconds, rate, amplitude=0.3)), rate)


def restyle_params(**changes: Any) -> dict[str, Any]:
    """A valid restyle ``params`` part (range 0-20 s, one take)."""
    restyle = {
        "caption": "Jazz trio, brushed drums",
        "audioCoverStrength": 0.55,
        "seeds": [42],
        "mode": "range",
        "rangeStartSeconds": 0.0,
        "rangeEndSeconds": 20.0,
        **changes,
    }
    return {"kind": "restyle", "restyle": restyle}


def multipart(
    params: Any, stems: list[Path], content_type: str = "audio/wav"
) -> list[tuple[str, tuple[str, bytes, str]]]:
    """Files for ``TestClient.post(files=...)``: the ``params`` part and the ``stem`` parts."""
    raw = params if isinstance(params, bytes) else json.dumps(params).encode()
    parts = [("params", ("params.json", raw, "application/json"))]
    parts += [("stem", (path.name, path.read_bytes(), content_type)) for path in stems]
    return parts


def silence(tmp_path: Path, name: str, seconds: float, rate: int = RATE) -> Path:
    """A silent stereo stem."""
    return write(tmp_path / name, np.zeros((round(seconds * rate), 2), np.float32), rate)
