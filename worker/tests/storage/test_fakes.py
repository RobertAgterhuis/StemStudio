"""Skeleton tests of the storage fakes (feature 013): they satisfy the 1.7.0 ports."""

import io
from uuid import UUID

import pytest

from stemstudio_worker.ports import (
    CacheJanitor,
    LibraryStemReader,
    LibraryUnavailableError,
    StemMissingError,
    StorageMeter,
)
from stemstudio_worker.schemas import (
    SkippedEntry,
    StorageCleanupRequest,
    StorageCleanupResult,
    WorkerStorage,
)

from .fakes import (
    REPORT,
    FakeCacheJanitor,
    FakeLibraryStemReader,
    FakeStorageMeter,
)

_TRACK = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f"
_SHA = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b"


def test_fakes_satisfy_the_ports() -> None:
    assert isinstance(FakeStorageMeter(), StorageMeter)
    assert isinstance(FakeCacheJanitor(), CacheJanitor)
    assert isinstance(FakeLibraryStemReader(), LibraryStemReader)


def test_fake_meter_returns_its_report() -> None:
    report = WorkerStorage.model_validate(REPORT)
    meter = FakeStorageMeter(report)

    assert meter.measure() is report
    assert meter.calls == 1
    assert FakeStorageMeter().measure().cache_entries == []


def test_fake_janitor_removes_requested_entries_and_skips_in_use() -> None:
    busy = "d" * 64
    janitor = FakeCacheJanitor(entries={_SHA: 100, busy: 50}, in_use={busy})

    result = janitor.clean(StorageCleanupRequest(cache_entries=["*"]))

    assert result.freed_bytes == 100
    assert result.removed_entries == [_SHA]
    assert result.skipped == [SkippedEntry(audio_sha256=busy, reason="inUse")]
    assert janitor.requests == [StorageCleanupRequest(cache_entries=["*"])]


def test_fake_janitor_reports_unknown_entries() -> None:
    result = FakeCacheJanitor().clean(StorageCleanupRequest(cache_entries=[_SHA]))

    assert result == StorageCleanupResult(
        freed_bytes=0,
        removed_entries=[],
        skipped=[SkippedEntry(audio_sha256=_SHA, reason="notFound")],
    )


def test_fake_stem_reader_serves_and_fails_like_the_port() -> None:
    track = UUID(_TRACK)
    reader = FakeLibraryStemReader({(track, "drums"): b"flac"})

    with reader.open(track, "drums") as handle:
        assert handle.read() == b"flac"
    with pytest.raises(StemMissingError):
        reader.open(track, "bass")
    with pytest.raises(LibraryUnavailableError):
        FakeLibraryStemReader(available=False).open(track, "drums")
    assert isinstance(reader.open(track, "drums"), io.BytesIO)
