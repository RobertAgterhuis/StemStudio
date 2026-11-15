"""``VoiceSampleAnalyzer``: decode, normalise and measure an own-voice sample (010 US3).

WAV, FLAC, MP3 and Ogg are read with soundfile; anything else (WebM/Opus from the browser
recorder) is decoded by ffmpeg from an argument list into a temporary WAV (CTO decision of
2026-11-15; no shell). The sample becomes 48 kHz mono (``normalise_sample``), is written as
16-bit FLAC with exactly the hashed PCM, and its voiced ratio is measured by the
``PitchTracker`` on the CPU (no GPU lease).
"""

import logging
import tempfile
from pathlib import Path
from typing import Final

from stemstudio_generator.audio.commands import CommandRunner, SubprocessRunner
from stemstudio_generator.audio.mix import AudioClip, read_audio
from stemstudio_generator.audio.normalise import (
    SAMPLE_RATE,
    normalise_sample,
    pcm_sha256,
    write_sample_flac,
)
from stemstudio_generator.ports import InvalidInputError, PitchTracker, SampleFacts, voiced_ratio

UNREADABLE_SAMPLE_TEXT: Final = "The sample could not be read as audio."
"""Problem detail of an undecodable sample."""

DECODE_TIMEOUT_SECONDS: Final = 120.0
_LOG: Final = logging.getLogger(__name__)


class SampleAnalyzer:
    """``VoiceSampleAnalyzer`` with soundfile, ffmpeg (fallback) and a pitch tracker."""

    def __init__(
        self, tracker: PitchTracker, decoder: CommandRunner | None = None, ffmpeg: str = "ffmpeg"
    ) -> None:
        """Measure with ``tracker``; ``decoder`` runs ffmpeg (``SubprocessRunner`` by default)."""
        self.tracker = tracker
        self.decoder = decoder or SubprocessRunner()
        self._ffmpeg = ffmpeg

    def analyze(self, upload: Path, dest: Path) -> SampleFacts:
        """Normalise ``upload`` into ``dest`` (FLAC) and measure it."""
        mono = normalise_sample(self._decode(upload))
        write_sample_flac(dest, mono)
        return SampleFacts(
            duration_seconds=mono.size / SAMPLE_RATE,
            voiced_ratio=voiced_ratio(self.tracker.f0(dest)),
            pcm_sha256=pcm_sha256(mono),
        )

    def _decode(self, upload: Path) -> AudioClip:
        try:
            return read_audio(upload)
        except InvalidInputError:
            return self._ffmpeg_decode(upload)

    def _ffmpeg_decode(self, upload: Path) -> AudioClip:
        with tempfile.TemporaryDirectory(prefix="sample-") as scratch:
            wav = Path(scratch) / "decoded.wav"
            args = [
                self._ffmpeg, "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", f"file:{upload.resolve()}", "-vn", "-ac", "1", "-f", "wav", f"file:{wav}",
            ]  # fmt: skip
            result = self.decoder.run(args, DECODE_TIMEOUT_SECONDS)
            if result.returncode != 0 or not wav.is_file():
                _LOG.info("ffmpeg could not decode a voice sample: %s", result.stderr)
                raise InvalidInputError(UNREADABLE_SAMPLE_TEXT)
            return read_audio(wav)
