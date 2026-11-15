"""``VoiceSampleAnalyzer``: decode, normalise and measure an own-voice sample (010 US3).

Uploads are untrusted. The container is identified by its magic bytes before anything decodes
it; anything else (playlists such as HLS ``.m3u8``, ffconcat or DASH manifests disguised with an
audio extension) is refused as unreadable and never reaches ffmpeg:

* WAV, FLAC, MP3 and Ogg are read with soundfile, after ``soundfile.info`` showed a length of
  at most ``SAMPLE_MAX_SECONDS + 1`` (a longer sample is reported ``tooLong`` without decoding,
  so a small file claiming hours cannot exhaust memory).
* WebM/Matroska, Ogg/Opus and MP4 (browser recorders) are decoded by ffmpeg from an argument
  list (no shell) with an explicit demuxer (``-f``), ``-protocol_whitelist file`` and
  ``-t SAMPLE_MAX_SECONDS + 1`` (CTO decision of 2026-11-15 and security review).

The sample becomes 48 kHz mono (``normalise_sample``), is written as 16-bit FLAC with exactly the
hashed PCM, and its voiced ratio is measured by the ``PitchTracker`` on the CPU (no GPU lease).
"""

import hashlib
import logging
import tempfile
from pathlib import Path
from typing import Final

import soundfile as sf

from stemstudio_generator.audio.commands import CommandRunner, SubprocessRunner
from stemstudio_generator.audio.mix import AudioClip, read_audio
from stemstudio_generator.audio.normalise import (
    SAMPLE_RATE,
    normalise_sample,
    pcm_sha256,
    write_sample_flac,
)
from stemstudio_generator.config import SAMPLE_MAX_SECONDS
from stemstudio_generator.ports import InvalidInputError, PitchTracker, SampleFacts, voiced_ratio

UNREADABLE_SAMPLE_TEXT: Final = "The sample could not be read as audio."
"""Problem detail of an undecodable or refused sample."""

DECODE_TIMEOUT_SECONDS: Final = 120.0
DECODE_CAP_SECONDS: Final = int(SAMPLE_MAX_SECONDS) + 1
"""At most this much audio is decoded (a longer sample is ``tooLong`` anyway)."""

_HEADER_BYTES: Final = 16
_MAGIC: Final = (
    (0, b"fLaC", "soundfile"), (0, b"OggS", "soundfile"), (0, b"ID3", "soundfile"),
    (0, b"\x1aE\xdf\xa3", "matroska"), (4, b"ftyp", "mov"),
)  # fmt: skip
_EMPTY_SHA256: Final = hashlib.sha256(b"").hexdigest()
_LOG: Final = logging.getLogger(__name__)


def container(header: bytes) -> str | None:
    """The container of a file from its first bytes: ``soundfile`` formats or an ffmpeg demuxer.

    Returns ``"soundfile"`` (WAV, FLAC, MP3, Ogg), ``"matroska"`` (WebM/Matroska), ``"mov"``
    (MP4) or None for anything else.
    """
    if _wav(header) or _mp3_frame(header):
        return "soundfile"
    matches = (kind for offset, magic, kind in _MAGIC if header[offset:].startswith(magic))
    return next(matches, None)


def _wav(header: bytes) -> bool:
    return header[:4] == b"RIFF" and header[8:12] == b"WAVE"


def _mp3_frame(header: bytes) -> bool:
    """An MPEG audio frame sync (11 set bits) at the start."""
    return len(header) > 1 and header[0] == 0xFF and header[1] & 0xE0 == 0xE0  # noqa: PLR2004


class SampleAnalyzer:
    """``VoiceSampleAnalyzer`` with soundfile, ffmpeg (browser formats) and a pitch tracker."""

    def __init__(
        self, tracker: PitchTracker, decoder: CommandRunner | None = None, ffmpeg: str = "ffmpeg"
    ) -> None:
        """Measure with ``tracker``; ``decoder`` runs ffmpeg (``SubprocessRunner`` by default)."""
        self.tracker = tracker
        self.decoder = decoder or SubprocessRunner()
        self._ffmpeg = ffmpeg

    def analyze(self, upload: Path, dest: Path) -> SampleFacts:
        """Normalise ``upload`` into ``dest`` (FLAC) and measure it (``tooLong`` undecoded)."""
        kind = container(_header(upload))
        if kind is None:
            raise InvalidInputError(UNREADABLE_SAMPLE_TEXT)
        if kind == "soundfile":
            seconds = _soundfile_seconds(upload)
            if seconds > DECODE_CAP_SECONDS:
                return SampleFacts(seconds, 0.0, _EMPTY_SHA256)
            clip = self._soundfile(upload, seconds)
        else:
            clip = self._ffmpeg_decode(upload, kind)
        return self._measure(clip, dest)

    def _measure(self, clip: AudioClip, dest: Path) -> SampleFacts:
        mono = normalise_sample(clip)
        write_sample_flac(dest, mono)
        ratio = voiced_ratio(self.tracker.f0(dest))
        return SampleFacts(mono.size / SAMPLE_RATE, ratio, pcm_sha256(mono))

    def _soundfile(self, upload: Path, seconds: float) -> AudioClip:
        """Decode with soundfile; Ogg/Opus that libsndfile cannot read goes to ffmpeg."""
        del seconds  # checked by the caller
        try:
            return read_audio(upload)
        except InvalidInputError:
            if _header(upload)[:4] != b"OggS":
                raise
            return self._ffmpeg_decode(upload, "ogg")

    def _ffmpeg_decode(self, upload: Path, demuxer: str) -> AudioClip:
        with tempfile.TemporaryDirectory(prefix="sample-") as scratch:
            wav = Path(scratch) / "decoded.wav"
            result = self.decoder.run(self._args(upload, demuxer, wav), DECODE_TIMEOUT_SECONDS)
            if result.returncode != 0 or not wav.is_file():
                _LOG.info("ffmpeg could not decode a voice sample: %s", result.stderr)
                raise InvalidInputError(UNREADABLE_SAMPLE_TEXT)
            return read_audio(wav)

    def _args(self, upload: Path, demuxer: str, wav: Path) -> list[str]:
        return [
            self._ffmpeg, "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-protocol_whitelist", "file", "-f", demuxer, "-i", f"file:{upload.resolve()}",
            "-t", str(DECODE_CAP_SECONDS), "-vn", "-ac", "1", "-f", "wav", f"file:{wav}",
        ]  # fmt: skip


def _header(path: Path) -> bytes:
    with path.open("rb") as source:
        return source.read(_HEADER_BYTES)


def _soundfile_seconds(path: Path) -> float:
    """Length from the header (``soundfile.info``) without decoding."""
    try:
        return float(sf.info(path).duration)
    except (sf.LibsndfileError, RuntimeError, OSError) as error:
        if _header(path)[:4] == b"OggS":
            return 0.0  # Ogg/Opus: libsndfile may not know it; ffmpeg caps the decode
        raise InvalidInputError(UNREADABLE_SAMPLE_TEXT) from error
