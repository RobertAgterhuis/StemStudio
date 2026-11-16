"""``StemMixer`` and ``VoiceRemixer`` over the audio helpers (009 restyle input, 010 remix).

Restyle: each ``stem`` part is decoded, the stems are summed at unity (limited to full scale
when the sum clips), the range is cut for ``mode: range`` (the whole sum for ``inPlace``) and
the result is written as 32-bit float WAV for the engine. Voice conversion: the 24 kHz
converted vocal is upsampled and mixed into the accompaniment stems (``audio.remix``).
"""

from collections.abc import Sequence
from pathlib import Path

from stemstudio_generator.audio.mix import AudioClip, cut_range, read_audio, sum_stems, write_wav
from stemstudio_generator.audio.remix import remix
from stemstudio_generator.ports import (
    SVC_SAMPLE_RATE,
    MixedSource,
    RemixedFiles,
    RemixRequest,
    Signal,
)


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


class AudioVoiceRemixer:
    """``VoiceRemixer``: 24 kHz converted vocal + accompaniment stems -> mix and vocal WAVs."""

    def remix(self, vocal: Signal, request: RemixRequest) -> RemixedFiles:
        """Mix (``audio.remix``) and write ``mix.wav`` and ``vocal.wav`` into the work dir."""
        voice = AudioClip(vocal.reshape(-1, 1), SVC_SAMPLE_RATE)
        stems = [read_audio(path) for path in request.accompaniment]
        result = remix(voice, stems, request.gain_db)
        return RemixedFiles(
            mix=write_wav(request.work_dir / "mix.wav", result.mix),
            vocal=write_wav(request.work_dir / "vocal.wav", result.vocal),
        )
