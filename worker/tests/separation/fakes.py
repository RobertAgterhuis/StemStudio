"""Fake Demucs runtime and fake device runner for the separation tests."""

from collections.abc import Callable
from dataclasses import dataclass, field

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import DeviceUse, GpuNeed
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo
from stemstudio_worker.separation.demucs_separator import DemucsModel, DemucsRuntime

SOURCES_6S = ["drums", "bass", "other", "vocals", "guitar", "piano"]
SOURCES_4 = ["drums", "bass", "other", "vocals"]
GPU_NAME = "NVIDIA GeForce RTX 5070 Ti"
RETRY_REASON = "The GPU ran out of memory during htdemucs_6s; the step was repeated on the CPU."
Audio = npt.NDArray[np.float32]


@dataclass
class FakeModel:
    sources: list[str]
    samplerate: int = 44_100


@dataclass
class FakeRuntime:
    """Records calls; ``fail_loading`` names models whose loading raises.

    ``apply_error`` is raised on every device in ``failing_devices`` (all devices when None).
    """

    fail_loading: set[str] = field(default_factory=set)
    fractions: list[float] = field(default_factory=lambda: [0.3, 0.2, 0.7, 1.4])
    loaded: list[str] = field(default_factory=list)
    devices: list[str] = field(default_factory=list)
    apply_error: Exception | None = None
    failing_devices: set[str] | None = None

    def load(self, name: str) -> FakeModel:
        self.loaded.append(name)
        if name in self.fail_loading:
            raise RuntimeError(f"cannot download {name}")
        return FakeModel(SOURCES_6S if name == "htdemucs_6s" else SOURCES_4)

    def separated(self, model: DemucsModel, mix: Audio, device: str) -> Audio:
        """Source ``i`` is the mix scaled by ``(i + 1) / 10``."""
        if self.apply_error and (self.failing_devices is None or device in self.failing_devices):
            raise self.apply_error
        scale = np.arange(1, len(model.sources) + 1, dtype=np.float32)[:, None, None] / 10
        return (mix[None] * scale).astype(np.float32)

    def as_runtime(self) -> DemucsRuntime:
        def apply(
            model: DemucsModel, mix: Audio, device: str, on_fraction: Callable[[float], None]
        ) -> Audio:
            self.devices.append(device)
            for fraction in self.fractions:
                on_fraction(fraction)
            return self.separated(model, mix, device)

        return DemucsRuntime(load=self.load, apply=apply)


@dataclass
class ScriptedRunner:
    """A ``DeviceRunner`` that tries ``first`` and, on any error, retries once on the CPU."""

    first: str = "cpu"
    info: DeviceInfo = CPU_DEVICE
    needs: list[GpuNeed] = field(default_factory=list)
    errors: list[Exception] = field(default_factory=list)

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        self.needs.append(need)
        try:
            return work(self.first), self._use(self.first)
        except Exception as exc:
            self.errors.append(exc)
            if self.first == "cpu":
                raise
        return work("cpu"), DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=RETRY_REASON)

    def _use(self, device: str) -> DeviceUse:
        if device == "cuda":
            return DeviceUse(kind="gpu", torch_device="cuda", name=GPU_NAME)
        return DeviceUse(kind="cpu", torch_device="cpu")
