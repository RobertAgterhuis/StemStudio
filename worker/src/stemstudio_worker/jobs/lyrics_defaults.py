"""Stand-ins for the feature 006 adapters of a runtime built without them (tests, partial builds).

Lookups complete as ``disabled`` without any request; alignments fail with the fixed alignment
text; model steps run on the CPU.
"""

from collections.abc import Callable
from pathlib import Path

import numpy as np
import numpy.typing as npt

from stemstudio_worker.jobs.lyrics_runner import LyricsParts
from stemstudio_worker.ports import (
    AlignmentError,
    AlignRequest,
    DeviceUse,
    GpuNeed,
    LyricsQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    CPU_DEVICE,
    DeviceInfo,
    LyricsResult,
    TimedLyrics,
)


class DisabledLyricsLookup:
    """Lookup of a worker without the ``lyrics`` package: every job completes as ``disabled``."""

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:  # noqa: ARG002 - port signature
        """Complete without any request (outcome ``disabled``)."""
        return LyricsResult(outcome="disabled")


class UnavailableAligner:
    """Aligner of a worker without the ``alignment`` package: every alignment fails."""

    def align(self, request: AlignRequest, device: str, progress: ProgressCallback) -> TimedLyrics:  # noqa: ARG002 - port signature
        """Fail with ``ALIGNMENT_FAILED_TEXT``."""
        raise AlignmentError(ALIGNMENT_FAILED_TEXT)


class UnavailableVocals:
    """Vocal loader of a worker without the ``alignment`` package: every load fails."""

    def load(self, path: Path) -> npt.NDArray[np.float32]:  # noqa: ARG002 - port signature
        """Fail with ``ALIGNMENT_FAILED_TEXT``."""
        raise AlignmentError(ALIGNMENT_FAILED_TEXT)


class CpuOnlyRunner:
    """Device runner that runs every step on the CPU (runtimes built without device hooks)."""

    @property
    def info(self) -> DeviceInfo:
        """Always :data:`CPU_DEVICE`."""
        return CPU_DEVICE

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:  # noqa: ARG002 - port signature
        """Run ``work("cpu")``."""
        return work("cpu"), DeviceUse(kind="cpu", torch_device="cpu")


def default_lyrics_parts() -> LyricsParts:
    """The stand-ins above as one :class:`LyricsParts`."""
    return LyricsParts(DisabledLyricsLookup(), UnavailableAligner(), UnavailableVocals(),
                       CpuOnlyRunner())  # fmt: skip
