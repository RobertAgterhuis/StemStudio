"""Fake separator for CI and E2E (``STEMSTUDIO_SEPARATOR=fake``): every stem is the mix."""

from pathlib import Path

from stemstudio_worker.ports import ProgressCallback, SeparationResult
from stemstudio_worker.schemas import StemName
from stemstudio_worker.separation.flac_writer import SIX_STEMS, STAGE, read_audio, write_flac


class FakeSeparator:
    """Implements the ``Separator`` port by copying the mix into six FLAC stems (model ``fake``)."""

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        """Write ``out_dir/<name>.flac`` for all six stems, sample-identical to the input mix.

        Args:
            wav_path: Normalised input audio (44.1 kHz, stereo, 16-bit WAV).
            out_dir: Existing directory for the stem files.
            progress: Receives the fraction of stems written.

        Returns:
            Model ``fake`` and the six stem files.
        """
        mix = read_audio(wav_path, dtype="int16")
        stems: dict[StemName, Path] = {}
        progress(0.0, STAGE)
        for index, name in enumerate(SIX_STEMS, start=1):
            stems[name] = out_dir / f"{name}.flac"
            write_flac(stems[name], mix)
            progress(index / len(SIX_STEMS), STAGE)
        return SeparationResult(model="fake", stems=stems)
