"""Fake detail models and model sources for the step tests."""

from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

from stemstudio_worker.detail.audio_io import write_stem
from stemstudio_worker.detail.checks import to_pcm
from stemstudio_worker.detail.folding import FoldTable
from stemstudio_worker.detail.steps import FAMILIES, PARTITION, ModelChoice
from stemstudio_worker.ports import DetailAudio, DeviceUse, GpuNeed, ProgressCallback
from stemstudio_worker.schemas import (
    CPU_DEVICE,
    DetailModelId,
    DeviceInfo,
    ParentStemName,
    StemName,
)

from .signals import noise, silence, tone

SECONDS = 1.0


class ScaledModel:
    """Returns ``audio x scale`` per output name; reports two chunks; records its inputs."""

    def __init__(self, scales: Mapping[str, float]) -> None:
        self.scales = dict(scales)
        self.inputs: list[DetailAudio] = []

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        self.inputs.append(audio)
        progress(0.5, "chunk")
        progress(1.0, "chunk")
        return {name: (audio * scale).astype(np.float32) for name, scale in self.scales.items()}


class TransformModel:
    """Returns ``transform(audio)`` (output name -> audio)."""

    def __init__(self, transform: Callable[[DetailAudio], dict[str, DetailAudio]]) -> None:
        self.transform = transform

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        return self.transform(audio)


def identity_table(names: tuple[str, ...]) -> FoldTable:
    return {name: (name,) for name in names}  # type: ignore[misc]


@dataclass
class Source:
    """A ``ModelSource`` with fixed choices per parent."""

    choices: dict[ParentStemName, ModelChoice]
    sum_instruments: bool = True
    chosen: list[ParentStemName] = field(default_factory=list)

    released: int = 0
    kept: list[GpuNeed | None] = field(default_factory=list)

    def choose(self, parent: ParentStemName) -> ModelChoice:
        self.chosen.append(parent)
        return self.choices[parent]

    def release_gpu(self, keep: GpuNeed | None = None) -> None:
        self.released += 1
        self.kept.append(keep)


def choice(
    model: object, table: FoldTable, model_id: DetailModelId = "fake-detail", families: bool = False
) -> ModelChoice:
    policy = FAMILIES if families else PARTITION
    return ModelChoice(model_id, lambda device: model, table, policy, NEED)  # type: ignore[arg-type,return-value]


NEED = GpuNeed("a fake detail model", 100)


@dataclass
class RecordingRunner:
    """A ``DeviceRunner`` running on ``first``; on any error it retries once on the CPU."""

    first: str = "cuda"
    info: DeviceInfo = CPU_DEVICE
    needs: list[GpuNeed] = field(default_factory=list)
    errors: list[Exception] = field(default_factory=list)

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        self.needs.append(need)
        try:
            return work(self.first), DeviceUse(kind="gpu", torch_device="cuda", name="GPU")
        except Exception as exc:
            self.errors.append(exc)
        return work("cpu"), DeviceUse(kind="cpu", torch_device="cpu", fallback_reason="retried")


def write_stems(directory: Path, audio: Mapping[StemName, DetailAudio]) -> dict[StemName, Path]:
    """Write ``audio`` as 16-bit FLAC stems; returns name -> path."""
    directory.mkdir(parents=True, exist_ok=True)
    paths: dict[StemName, Path] = {}
    for name, data in audio.items():
        paths[name] = directory / f"{name}.flac"
        write_stem(paths[name], to_pcm(data))
    return paths


def band(silent: tuple[StemName, ...] = ("piano",)) -> dict[StemName, DetailAudio]:
    """Six standard stems; ``silent`` ones are digital silence."""
    audio: dict[StemName, DetailAudio] = {
        "vocals": tone(SECONDS, 0.3, 440.0),
        "drums": noise(SECONDS, 0.4, seed=1),
        "bass": tone(SECONDS, 0.3, 55.0),
        "guitar": tone(SECONDS, 0.3, 330.0),
        "piano": tone(SECONDS, 0.3, 660.0),
        "other": noise(SECONDS, 0.3, seed=2),
    }
    return {name: silence(SECONDS) if name in silent else data for name, data in audio.items()}
