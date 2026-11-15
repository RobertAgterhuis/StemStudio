"""FLAC I/O of the detail pipeline: channels-first audio, 44.1 kHz, 16-bit PCM (soundfile).

Stems are read and written as int16 samples so a write/read round trip is exact; float audio
handed to models is ``int16 / 32768``.
"""

from pathlib import Path

import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_worker.ports import DetailAudio
from stemstudio_worker.schemas import SAMPLE_RATE

Pcm = npt.NDArray[np.int16]
"""16-bit samples ``(channels, frames)``."""

PCM_SCALE = 32768.0
"""Float full scale of 16-bit samples."""


def read_stem(path: Path) -> DetailAudio:
    """Read a FLAC/WAV file as float32 ``(channels, frames)`` on the 16-bit grid."""
    data, _ = sf.read(path, dtype="int16", always_2d=True)
    return (np.asarray(data).T / PCM_SCALE).astype(np.float32)


def write_stem(path: Path, pcm: Pcm) -> None:
    """Write 16-bit ``(channels, frames)`` samples as 44.1 kHz FLAC."""
    sf.write(path, np.ascontiguousarray(pcm.T), SAMPLE_RATE, format="FLAC", subtype="PCM_16")
