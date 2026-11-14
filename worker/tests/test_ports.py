"""Skeleton tests for the pipeline composition contract (ports + factory stubs)."""

from collections.abc import Callable
from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import (
    IngestAdapters,
    NormalizedAudio,
    PipelineAdapters,
    ProgressCallback,
    SeparationResult,
    SourceInfo,
)
from stemstudio_worker.schemas import BeatGrid, SourceType
from stemstudio_worker.separation import create_separator

_SOURCE = SourceInfo(SourceType.UPLOAD, "upload:" + "a" * 64, "Song", None, None)


class _Fake:
    """One object satisfying every port structurally."""

    def resolve(self, url: str) -> SourceInfo:
        return _SOURCE

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        return _SOURCE

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> Path:
        return work_dir / "a.webm"

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


@pytest.mark.parametrize("factory", [create_separator])
def test_factory_stubs_are_placeholders(factory: Callable[[WorkerSettings], object]) -> None:
    with pytest.raises(NotImplementedError, match="agent"):
        factory(WorkerSettings(data_dir=Path("unused")))
