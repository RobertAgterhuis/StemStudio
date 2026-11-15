"""Skeleton tests for the pipeline composition contract (ports + factory stubs)."""

from pathlib import Path

import pytest

from stemstudio_worker.ports import (
    EmptyUploadError,
    FetchedAudio,
    IngestAdapters,
    InvalidSourceError,
    NormalizedAudio,
    PipelineAdapters,
    ProgressCallback,
    SeparationResult,
    SourceInfo,
    UnsupportedMediaError,
    UploadTooLargeError,
)
from stemstudio_worker.schemas import BeatGrid, SourceType

_SOURCE = SourceInfo(SourceType.UPLOAD, "upload:" + "a" * 64, "Song", None, None)


class _Fake:
    """One object satisfying every port structurally."""

    def resolve(self, url: str) -> SourceInfo:
        return _SOURCE

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        return _SOURCE

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> FetchedAudio:
        return FetchedAudio(work_dir / "a.webm")

    def normalize(self, input_path: Path, output_wav: Path) -> NormalizedAudio:
        return NormalizedAudio(output_wav, 1.0, "b" * 64)

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        return SeparationResult("fake", {"vocals": out_dir / "vocals.flac"})

    def analyze(self, wav_path: Path) -> BeatGrid:
        return BeatGrid(bpm=0, beats=[], downbeats=[], confidence=0, reliable=False)


def test_pipeline_adapters_compose_fakes(tmp_path: Path) -> None:
    fake = _Fake()
    ingest = IngestAdapters(resolver=fake, upload_intake=fake, fetcher=fake, normalizer=fake)
    adapters = PipelineAdapters(ingest=ingest, separator=fake, analyzer=fake)

    assert adapters.ingest.upload_intake.accept("Song.mp3", tmp_path / "x") is _SOURCE
    assert adapters.separator.separate(tmp_path, tmp_path, lambda f, s: None).model == "fake"
    assert adapters.analyzer.analyze(tmp_path).reliable is False


def test_fetched_audio_metadata_is_optional(tmp_path: Path) -> None:
    fetched = _Fake().fetch(_SOURCE, tmp_path, lambda f, s: None)

    assert (fetched.path, fetched.title, fetched.artist) == (tmp_path / "a.webm", None, None)


@pytest.mark.parametrize("error", [EmptyUploadError, UploadTooLargeError, UnsupportedMediaError])
def test_upload_errors_are_invalid_source_errors(error: type[InvalidSourceError]) -> None:
    assert issubclass(error, InvalidSourceError)
