"""Fake detail models and model sources for the step tests."""

from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

from stemstudio_worker.detail.audio_io import write_stem
from stemstudio_worker.detail.checks import to_pcm
from stemstudio_worker.detail.folding import FoldTable
from stemstudio_worker.detail.steps import FAMILIES, PARTITION, ModelChoice
from stemstudio_worker.ports import DetailAudio, ProgressCallback
from stemstudio_worker.schemas import DetailModelId, ParentStemName, StemName

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

    def choose(self, parent: ParentStemName) -> ModelChoice:
        self.chosen.append(parent)
        return self.choices[parent]


def choice(
    model: object, table: FoldTable, model_id: DetailModelId = "fake-detail", families: bool = False
) -> ModelChoice:
    return ModelChoice(model_id, model, table, FAMILIES if families else PARTITION)  # type: ignore[arg-type]


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
