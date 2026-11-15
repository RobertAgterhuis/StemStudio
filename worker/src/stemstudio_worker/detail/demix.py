"""Overlap-add chunked inference for the MSST networks (MDX23C DrumSep, Mega 53 BS-RoFormer).

Re-implemented after ``utils/model_utils.py::demix`` of ZFTurbo MSST (MIT, commit 1f700c38,
"generic" mode): reflect-padded borders, linear fades of chunk/10, first and last fades kept at
one. Only the selected outputs are accumulated (8 family accumulators instead of 53, research.md
D-06). Progress is reported per chunk and may cancel the run.
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import TYPE_CHECKING, Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import DetailAudio, DetailSeparationError, ProgressCallback

if TYPE_CHECKING:
    import torch

Network = Callable[["torch.Tensor"], "torch.Tensor"]
"""``(batch, channels, time) -> (batch, outputs, channels, time)``."""


STAGE: Final = "Splitting"
FAILED: Final = "Detail separation failed: the model could not process the audio."


@dataclass(frozen=True, slots=True)
class DemixPlan:
    """Chunking of one network.

    Attributes:
        chunk_size: Samples per chunk.
        overlap: Chunks overlapping each sample (step = chunk_size / overlap).
    """

    chunk_size: int
    overlap: int


def inference_error(exc: RuntimeError) -> Exception:
    """Map a framework error to ``MemoryError`` (allocation) or ``DetailSeparationError``."""
    if "alloc" in str(exc).lower():
        return MemoryError(str(exc))
    return DetailSeparationError(FAILED)


def _window(size: int, fade: int) -> "torch.Tensor":
    import torch  # noqa: PLC0415 - torch loads only for real models

    window = torch.ones(size)
    window[:fade] = torch.linspace(0, 1, fade)
    window[-fade:] = torch.linspace(1, 0, fade)
    return window


@dataclass(slots=True)
class _Accumulator:
    """Windowed overlap-add sums; allocated when the first chunk reveals the output count."""

    plan: DemixPlan
    total: int
    window: "torch.Tensor"
    result: "torch.Tensor | None" = None
    counter: "torch.Tensor | None" = None

    def add(self, start: int, output: "torch.Tensor") -> None:
        import torch  # noqa: PLC0415 - torch loads only for real models

        if self.result is None or self.counter is None:
            self.result = torch.zeros((*output.shape[:2], self.total))
            self.counter = torch.zeros(self.total)
        length = min(output.shape[-1], self.total - start)
        window = self._window_at(start)
        self.result[..., start : start + length] += output[..., :length] * window[:length]
        self.counter[start : start + length] += window[:length]

    def _window_at(self, start: int) -> "torch.Tensor":
        fade = self.plan.chunk_size // 10
        window = self.window.clone()
        if start == 0:
            window[:fade] = 1
        if start + self.plan.chunk_size // self.plan.overlap >= self.total:
            window[-fade:] = 1
        return window

    def estimate(self) -> npt.NDArray[np.float32]:
        assert self.result is not None  # noqa: S101 - at least one chunk always runs
        assert self.counter is not None  # noqa: S101
        estimate: npt.NDArray[np.float32] = (self.result / self.counter).numpy()
        return estimate


def demix(
    network: Network, mix: DetailAudio, plan: DemixPlan, progress: ProgressCallback
) -> npt.NDArray[np.float32]:
    """Run ``network`` over ``mix`` in overlapping chunks; returns ``(outputs, C, N)``."""
    import torch  # noqa: PLC0415 - torch loads only for real models

    border = plan.chunk_size - plan.chunk_size // plan.overlap
    audio = torch.from_numpy(np.ascontiguousarray(mix, dtype=np.float32))
    padded = audio.shape[-1] > 2 * border > 0
    if padded:
        audio = torch.nn.functional.pad(audio[None], (border, border), mode="reflect")[0]
    accumulator = _Accumulator(
        plan, audio.shape[-1], _window(plan.chunk_size, plan.chunk_size // 10)
    )
    starts = range(0, audio.shape[-1], plan.chunk_size // plan.overlap)
    with torch.inference_mode():
        for index, start in enumerate(starts):
            accumulator.add(start, network(_chunk(audio, start, plan.chunk_size))[0])
            progress((index + 1) / len(starts), STAGE)
    estimate = accumulator.estimate()
    estimate = estimate[..., border:-border] if padded else estimate
    return np.nan_to_num(estimate, nan=0.0).astype(np.float32)


def _chunk(audio: "torch.Tensor", start: int, size: int) -> "torch.Tensor":
    import torch  # noqa: PLC0415 - torch loads only for real models

    part = audio[:, start : start + size]
    mode = "reflect" if part.shape[-1] > size // 2 else "constant"
    return torch.nn.functional.pad(part[None], (0, size - part.shape[-1]), mode=mode)


class MsstModel:
    """``DetailModel`` over an MSST network: named outputs, only ``keep`` accumulated."""

    def __init__(
        self, network: Network, plan: DemixPlan, names: Sequence[str], keep: Sequence[str]
    ) -> None:
        """Wrap ``network`` whose outputs are ``names``; return only the outputs in ``keep``."""
        indices = [list(names).index(name) for name in keep]
        self._network: Network = lambda batch: network(batch)[:, indices]
        self.plan = plan
        self._keep = tuple(keep)

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        """Split ``audio`` into the kept outputs (see :func:`demix`)."""
        try:
            outputs = demix(self._network, audio, self.plan, progress)
        except RuntimeError as exc:
            raise inference_error(exc) from exc
        return dict(zip(self._keep, outputs, strict=True))
