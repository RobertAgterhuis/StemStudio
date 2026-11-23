"""Fake detail models for ``STEMSTUDIO_SEPARATOR=fake`` (CI, E2E; research.md D-12).

The fake standard separator copies the mix into every stem, so the fake models are chosen per
parent and return scaled copies of their input with a fixed, deterministic outcome table:
drums (kick, snare, toms and the hi-hat of the research R-11 groove), vocals and other split;
guitar has one audible family ("nothing further"); piano's outputs sum to 0.3 x the parent
("not reliable"); bass is never split. Each model call waits
``fake_detail_step_seconds`` in four chunks so E2E tests can observe progress and cancel.
Nothing is downloaded.
"""

import time
from collections.abc import Callable, Mapping
from typing import Final

import numpy as np

from stemstudio_worker.detail.checks import CheckPolicy
from stemstudio_worker.detail.steps import ModelChoice
from stemstudio_worker.ports import DetailAudio, DetailModel, GpuNeed, ProgressCallback
from stemstudio_worker.schemas import DetailModelId, DetailStemName, ParentStemName

CHUNKS: Final = 4
"""Progress reports (and sleeps) per fake model call."""

FAKE_SCALES: Final[Mapping[ParentStemName, Mapping[DetailStemName, float]]] = {
    "drums": {"kick": 0.5, "snare": 0.25, "toms": 0.15, "hihat": 0.1},
    "vocals": {"lead": 0.7, "backing": 0.3},
    "guitar": {"electric-guitar": 1.0, "keys": 0.001},
    "piano": {"keys": 0.15, "synth": 0.15},
    "other": {"brass": 0.4, "strings": 0.3, "woodwinds": 0.3},
}
"""Output scales per parent; outputs are named with their detail stem names."""

FAKE_DETAIL_NEED: Final = GpuNeed("the detail separation", 0)
"""GPU need of a fake model step (named in the fake device's fallback reasons)."""

_FAKE_POLICY: Final = CheckPolicy(model_residual=True, double_count=True)
_PARENT_OF_MODEL: Final[Mapping[DetailModelId, ParentStemName]] = {
    "mdx23c-drumsep-6s": "drums",
    "uvr-mdxnet-kara-2": "vocals",
    "mvsep-mega-53": "other",
    "fake-detail": "other",
}

Sleeper = Callable[[float], None]
"""Waits the given number of seconds (``time.sleep``; injectable for tests)."""


class FakeDetailModel:
    """A ``DetailModel`` returning ``audio x scale`` per output after a fixed delay."""

    def __init__(
        self, scales: Mapping[DetailStemName, float], step_seconds: float, sleeper: Sleeper
    ) -> None:
        """Return the outputs ``scales`` after waiting ``step_seconds`` with ``sleeper``."""
        self._scales = scales
        self._step_seconds = step_seconds
        self._sleeper = sleeper

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        """Wait in :data:`CHUNKS` chunks (progress may cancel), then return scaled copies."""
        for chunk in range(CHUNKS):
            self._sleeper(self._step_seconds / CHUNKS)
            progress((chunk + 1) / CHUNKS, "Splitting")
        return {name: (audio * scale).astype(np.float32) for name, scale in self._scales.items()}


class FakeSource:
    """``ModelSource`` with one fake model per parent (no sum run)."""

    sum_instruments = False

    def __init__(self, step_seconds: float, sleeper: Sleeper = time.sleep) -> None:
        """Delay each model call by ``step_seconds`` using ``sleeper``."""
        self._step_seconds = step_seconds
        self._sleeper = sleeper

    def model(self, parent: ParentStemName) -> DetailModel:
        """Return the fake model of ``parent``."""
        return FakeDetailModel(FAKE_SCALES[parent], self._step_seconds, self._sleeper)

    def choose(self, parent: ParentStemName) -> ModelChoice:
        """Return the fake model of ``parent`` (model id ``fake-detail``)."""
        table = {name: (name,) for name in FAKE_SCALES[parent]}
        load = lambda _device: self.model(parent)  # noqa: E731 - fakes ignore the device
        return ModelChoice("fake-detail", load, table, _FAKE_POLICY, FAKE_DETAIL_NEED)

    def release_gpu(self, keep: GpuNeed | None = None) -> None:
        """Fake models hold no GPU memory."""


class FakeModelProvider:
    """``DetailModelProvider`` serving the fake model of each registry model's parent."""

    def __init__(self, source: FakeSource) -> None:
        """Serve the models of ``source``."""
        self._source = source

    def get(self, model_id: DetailModelId) -> DetailModel:
        """Return the fake model for ``model_id`` (``fake-detail`` acts like ``other``)."""
        return self._source.model(_PARENT_OF_MODEL[model_id])
