"""Shared builders for the transcription tests: audio files and a recording model store."""

from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_worker.ports import WeightsFile


def write_audio(
    path: Path, samples: npt.NDArray[np.float64], rate: int = 44_100, stereo: bool = True
) -> Path:
    """Write ``samples`` (mono) as a 16-bit FLAC, duplicated to two channels by default."""
    data = np.stack([samples, samples], axis=1) if stereo else samples
    sf.write(path, data, rate, format="FLAC", subtype="PCM_16")
    return path


def ramp(seconds: float, rate: int = 44_100) -> npt.NDArray[np.float64]:
    """A rising ramp from -0.5 to 0.5 (positions are recognisable after resampling)."""
    return np.linspace(-0.5, 0.5, int(seconds * rate))


@dataclass
class RecordingModels:
    """A model store that hands out ``<root>/<model_id>.bin`` and records the requests."""

    root: Path
    requested: list[WeightsFile] = field(default_factory=list)

    def ensure(self, weights: WeightsFile) -> Path:
        self.requested.append(weights)
        path = self.root / f"{weights.model_id}.bin"
        path.write_bytes(b"model")
        return path
