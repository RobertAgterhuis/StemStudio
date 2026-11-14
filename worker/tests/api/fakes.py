"""Fake adapters for the API tests (the real fake separator writes real FLAC stems)."""

import hashlib
import threading
from pathlib import Path

import numpy as np
import soundfile as sf

from stemstudio_worker.ports import (
    FetchedAudio,
    IngestAdapters,
    InvalidSourceError,
    NormalizedAudio,
    PipelineAdapters,
    ProgressCallback,
    SeparationResult,
    Separator,
    SourceInfo,
    SourceUnavailableError,
    UnsupportedMediaError,
)
from stemstudio_worker.schemas import BeatGrid, SourceType
from stemstudio_worker.separation.fake import FakeSeparator

GRID = BeatGrid(bpm=100, beats=[0.1, 0.7], downbeats=[0.1], confidence=0.5, reliable=True)
YOUTUBE = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
FETCHED_TITLE = "Never Gonna Give You Up"
FETCHED_ARTIST = "Rick Astley"
SPOTIFY = "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT"


class ApiFakes:
    """Resolver, intake, fetcher, normalizer and analyzer with test switches.

    Attributes:
        gate: Downloads wait until it is set (set by default).
        normalize_error: Raised by ``normalize`` when not None.
    """

    def __init__(self) -> None:
        self.gate = threading.Event()
        self.gate.set()
        self.normalize_error: Exception | None = None

    def resolve(self, url: str) -> SourceInfo:
        if "nomatch" in url:
            raise SourceUnavailableError(
                "No matching YouTube audio was found for this Spotify track."
            )
        if url == SPOTIFY:
            return SourceInfo(
                SourceType.SPOTIFY, "spotify:4cOdK2wGLETKBW3PvgPWqT", "Track", "Band", YOUTUBE
            )
        if not url.startswith("https://www.youtube.com/watch?v="):
            raise InvalidSourceError("Only YouTube video and Spotify track URLs are supported.")
        video = url.rsplit("=", 1)[1]
        return SourceInfo(SourceType.YOUTUBE, f"youtube:{video}", "Video", None, url)

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        if Path(file_name).suffix.lower() not in {".mp3", ".wav", ".flac"}:
            raise UnsupportedMediaError("Only MP3, WAV and FLAC files are supported.")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        return SourceInfo(SourceType.UPLOAD, f"upload:{digest}", Path(file_name).stem, None, None)

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> FetchedAudio:
        assert self.gate.wait(10), "gate never opened"
        target = work_dir / "download.m4a"
        target.write_bytes(source.source_key.encode())
        return FetchedAudio(target, FETCHED_TITLE, FETCHED_ARTIST)

    def normalize(self, input_path: Path, output_wav: Path) -> NormalizedAudio:
        if self.normalize_error:
            raise self.normalize_error
        tone = np.sin(np.linspace(0, 200, 8_820, dtype=np.float32)) * 0.2
        sf.write(output_wav, np.stack([tone, tone], axis=1), 44_100, subtype="PCM_16")
        digest = hashlib.sha256(input_path.read_bytes()).hexdigest()
        return NormalizedAudio(output_wav, 0.2, digest)

    def analyze(self, wav_path: Path) -> BeatGrid:
        return GRID

    def adapters(self, separator: Separator | None = None) -> PipelineAdapters:
        ingest = IngestAdapters(resolver=self, upload_intake=self, fetcher=self, normalizer=self)
        return PipelineAdapters(
            ingest=ingest, separator=separator or FakeSeparator(), analyzer=self
        )


class FourStemSeparator:
    """Like ``htdemucs``: the fake separator without guitar and piano."""

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        six = FakeSeparator().separate(wav_path, out_dir, progress)
        stems = {n: p for n, p in six.stems.items() if n not in ("guitar", "piano")}
        return SeparationResult(model="htdemucs", stems=stems)
