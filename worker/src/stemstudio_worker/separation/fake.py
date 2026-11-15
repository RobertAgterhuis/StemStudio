"""Fake separator for CI and E2E (``STEMSTUDIO_SEPARATOR=fake``): every stem is the mix.

With a ``DeviceRunner`` (the fake device of ``STEMSTUDIO_FAKE_DEVICE``) the copy runs as one model
step, so the fake stack reports a device record like the real one (feature 003).
"""

from pathlib import Path
from typing import Final

from stemstudio_worker.ports import (
    DeviceRunner,
    GpuNeed,
    ProgressCallback,
    SeparationResult,
    merge_device,
)
from stemstudio_worker.schemas import StemName
from stemstudio_worker.separation.flac_writer import SIX_STEMS, STAGE, read_audio, write_flac

FAKE_NEED: Final = GpuNeed("the standard separation", 0)
"""The fake step's GPU need (named in the fake fallback reasons)."""


class FakeSeparator:
    """Implements the ``Separator`` port by copying the mix into six FLAC stems (model ``fake``)."""

    def __init__(self, devices: DeviceRunner | None = None) -> None:
        """Run the copy through ``devices``; without one the result has no device record."""
        self._devices = devices

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        """Write ``out_dir/<name>.flac`` for all six stems, sample-identical to the input mix.

        Args:
            wav_path: Normalised input audio (44.1 kHz, stereo, 16-bit WAV).
            out_dir: Existing directory for the stem files.
            progress: Receives the fraction of stems written.

        Returns:
            Model ``fake``, the six stem files and (with a runner) the device record.
        """
        if self._devices is None:
            return SeparationResult(model="fake", stems=_copy(wav_path, out_dir, progress))
        copy = lambda _device: _copy(wav_path, out_dir, progress)  # noqa: E731 - one-line step
        stems, use = self._devices.run(copy, FAKE_NEED)
        return SeparationResult(model="fake", stems=stems, device=merge_device(None, use))


def _copy(wav_path: Path, out_dir: Path, progress: ProgressCallback) -> dict[StemName, Path]:
    mix = read_audio(wav_path, dtype="int16")
    stems: dict[StemName, Path] = {}
    progress(0.0, STAGE)
    for index, name in enumerate(SIX_STEMS, start=1):
        stems[name] = out_dir / f"{name}.flac"
        write_flac(stems[name], mix)
        progress(index / len(SIX_STEMS), STAGE)
    return stems
