"""In-test fakes for every pipeline port, plus a recording store and a fake clock."""

import hashlib
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    IngestAdapters,
    NormalizedAudio,
    PipelineAdapters,
    ProgressCallback,
    SeparationResult,
    SourceInfo,
)
from stemstudio_worker.schemas import BeatGrid, JobState, StemName

from .conftest import T0

FOUR: list[StemName] = ["vocals", "drums", "bass", "other"]
GRID = BeatGrid(bpm=120, beats=[0.5, 1.0], downbeats=[0.5], confidence=0.9, reliable=True)


class FakeClock:
    """Returns T0, T0 + 1 s, T0 + 2 s, ..."""

    def __init__(self) -> None:
        self.ticks = 0

    def __call__(self) -> datetime:
        self.ticks += 1
        return T0 + timedelta(seconds=self.ticks)


class RecordingStore(JobStore):
    """A real store that also records every saved (status, stage, progress)."""

    def __init__(self, jobs_dir: Path) -> None:
        super().__init__(jobs_dir)
        self.history: list[tuple[JobState, str, int]] = []

    def save(self, record: StoredJob) -> None:
        self.history.append((record.job.status, record.job.stage, record.job.progress))
        super().save(record)


@dataclass
class Calls:
    """What the fakes were called with, in order."""

    names: list[str] = field(default_factory=list)
    paths: list[Path] = field(default_factory=list)


class FakeIngest:
    """Resolver, intake, fetcher and normalizer; ``fail`` maps a method name to an exception."""

    def __init__(self, calls: Calls, sha: str = "e" * 64) -> None:
        self.calls = calls
        self.sha = sha
        self.fail: dict[str, Exception] = {}

    def _enter(self, name: str, path: Path) -> None:
        self.calls.names.append(name)
        self.calls.paths.append(path)
        if name in self.fail:
            raise self.fail[name]

    def resolve(self, url: str) -> SourceInfo:
        raise AssertionError("the runner never resolves")

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        raise AssertionError("the runner never accepts uploads")

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> Path:
        self._enter("fetch", work_dir)
        for fraction in (0.0, 0.5, 1.0):
            progress(fraction, "Downloading")
        target = work_dir / "download.webm"
        target.write_bytes(b"webm")
        return target

    def normalize(self, input_path: Path, output_wav: Path) -> NormalizedAudio:
        self._enter("normalize", input_path)
        output_wav.write_bytes(b"RIFF" + input_path.read_bytes())
        return NormalizedAudio(output_wav, 12.5, self.sha)


class FakeSeparator:
    """Writes tiny stem files and reports 0.25, 0.5, 1.0."""

    def __init__(self, calls: Calls, stems: list[StemName] = FOUR) -> None:
        self.calls = calls
        self.stems = stems
        self.error: Exception | None = None

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        self.calls.names.append("separate")
        if self.error:
            raise self.error
        for fraction in (0.25, 0.5, 1.0):
            progress(fraction, "Separating stems")
        paths = {name: out_dir / f"{name}.flac" for name in self.stems}
        for name, path in paths.items():
            path.write_bytes(name.encode() * 10)
        return SeparationResult(model="htdemucs", stems=paths)


class FakeAnalyzer:
    """Returns :data:`GRID`."""

    def __init__(self, calls: Calls) -> None:
        self.calls = calls

    def analyze(self, wav_path: Path) -> BeatGrid:
        self.calls.names.append("analyze")
        return GRID


@dataclass
class Fakes:
    """All fakes wired together."""

    calls: Calls
    ingest: FakeIngest
    separator: FakeSeparator
    analyzer: FakeAnalyzer

    @classmethod
    def create(cls) -> "Fakes":
        calls = Calls()
        return cls(calls, FakeIngest(calls), FakeSeparator(calls), FakeAnalyzer(calls))

    def adapters(self) -> PipelineAdapters:
        ingest = IngestAdapters(self.ingest, self.ingest, self.ingest, self.ingest)
        return PipelineAdapters(ingest=ingest, separator=self.separator, analyzer=self.analyzer)


def sha256_of(data: bytes) -> str:
    """Hex SHA-256 of ``data``."""
    return hashlib.sha256(data).hexdigest()
