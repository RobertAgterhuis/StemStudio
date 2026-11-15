"""``StemMixer`` over the audio helpers: the restyle input of 009 (contract ``createGeneration``).

Each ``stem`` part is decoded, the stems are summed at unity (limited to full scale when the
sum clips), the range is cut for ``mode: range`` (the whole sum for ``inPlace``) and the result
is written as 32-bit float WAV for the engine.
"""

from collections.abc import Sequence
from pathlib import Path

from stemstudio_generator.audio.mix import cut_range, read_audio, sum_stems, write_wav
from stemstudio_generator.ports import MixedSource


class AudioStemMixer:
    """``StemMixer`` with soundfile and numpy (no ffmpeg)."""

    def measure(self, stems: Sequence[Path]) -> float:
        """Length in seconds of the summed stems (raises ``InvalidInputError``)."""
        return sum_stems([read_audio(path) for path in stems]).clip.duration_seconds

    def mix(
        self, stems: Sequence[Path], dest: Path, cut: tuple[float, float] | None
    ) -> MixedSource:
        """Sum the stems, cut to ``cut`` (seconds) when given, and write ``dest`` as WAV."""
        result = sum_stems([read_audio(path) for path in stems])
        clip = result.clip if cut is None else cut_range(result.clip, *cut)
        write_wav(dest, clip)
        return MixedSource(
            path=dest, duration_seconds=clip.duration_seconds, clipped=result.clipped
        )
