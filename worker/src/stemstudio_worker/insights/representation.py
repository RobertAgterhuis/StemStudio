"""Analysis representation (data-model 4.1). Stub for the red commit of T014."""

from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import QUALITY_SAMPLE_RATE

N_FFT = 2048
HOP = 512
BANDS = 64
FRAME_SECONDS = HOP / QUALITY_SAMPLE_RATE
BLOCK_FRAMES = 1 << 16


@dataclass(frozen=True, slots=True)
class Spectrogram:
    """Stub."""

    power: npt.NDArray[np.float32]
    frame_db: npt.NDArray[np.float32]

    @property
    def frames(self) -> int:
        """Stub."""
        raise NotImplementedError

    @property
    def seconds(self) -> float:
        """Stub."""
        raise NotImplementedError

    def band_db(self) -> npt.NDArray[np.float32]:
        """Stub."""
        raise NotImplementedError

    def active(self) -> npt.NDArray[np.bool_]:
        """Stub."""
        raise NotImplementedError


def mel_filterbank() -> npt.NDArray[np.float32]:
    """Stub."""
    raise NotImplementedError


def band_centres() -> npt.NDArray[np.float64]:
    """Stub."""
    raise NotImplementedError


def read_spectrogram(path: Path) -> Spectrogram:
    """Stub."""
    raise NotImplementedError


def read_standard(paths: Mapping[str, Path]) -> tuple[dict[str, Spectrogram], Spectrogram]:
    """Stub."""
    raise NotImplementedError
