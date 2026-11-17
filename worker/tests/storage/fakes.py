"""Fakes of the feature 013 storage ports (meter, cache janitor, library stem reader).

Shared by the stem-separation tests of ``storage/`` and the API tests of the storage routes.
"""

import io
from collections.abc import Mapping
from typing import Any, BinaryIO
from uuid import UUID

from stemstudio_worker.ports import LibraryUnavailableError, StemMissingError
from stemstudio_worker.schemas import (
    SkippedEntry,
    StemName,
    StorageCleanupRequest,
    StorageCleanupResult,
    WorkerStorage,
)

REPORT: dict[str, Any] = {
    "measuredAt": "2026-11-16T14:00:00Z",
    "volumes": [
        {"id": "data", "freeBytes": 805306368000, "totalBytes": 1081101176832},
        {"id": "models", "freeBytes": 805306368000, "totalBytes": 1081101176832},
    ],
    "cacheEntries": [],
    "jobFilesBytes": 0,
    "jobFilesCount": 0,
    "scratchBytes": 0,
    "modelCaches": [],
}
"""A minimal valid ``WorkerStorage`` document (wire JSON)."""


class FakeStorageMeter:
    """Returns a fixed report and counts the calls."""

    def __init__(self, report: WorkerStorage | None = None) -> None:
        self.report = report or WorkerStorage.model_validate(REPORT)
        self.calls = 0

    def measure(self) -> WorkerStorage:
        self.calls += 1
        return self.report


class FakeCacheJanitor:
    """Keeps cache entries (sha -> bytes) in memory; entries in ``in_use`` are skipped."""

    def __init__(
        self, entries: Mapping[str, int] | None = None, in_use: set[str] | None = None
    ) -> None:
        self.entries = dict(entries or {})
        self.in_use = set(in_use or set())
        self.requests: list[StorageCleanupRequest] = []

    def clean(self, request: StorageCleanupRequest) -> StorageCleanupResult:
        self.requests.append(request)
        wanted = sorted(self.entries) if request.all_entries else list(request.cache_entries)
        removed: list[str] = []
        skipped: list[SkippedEntry] = []
        freed = 0
        for sha in wanted:
            if sha not in self.entries:
                skipped.append(SkippedEntry(audio_sha256=sha, reason="notFound"))
            elif sha in self.in_use:
                skipped.append(SkippedEntry(audio_sha256=sha, reason="inUse"))
            else:
                freed += self.entries.pop(sha)
                removed.append(sha)
        return StorageCleanupResult(freed_bytes=freed, removed_entries=removed, skipped=skipped)


class FakeLibraryStemReader:
    """Serves stems from memory; ``available=False`` simulates a missing ``/library`` mount."""

    def __init__(
        self, stems: Mapping[tuple[UUID, StemName], bytes] | None = None, *, available: bool = True
    ) -> None:
        self.stems = dict(stems or {})
        self.available = available

    def open(self, track_id: UUID, stem: StemName) -> BinaryIO:
        if not self.available:
            raise LibraryUnavailableError
        data = self.stems.get((track_id, stem))
        if data is None:
            raise StemMissingError(stem)
        return io.BytesIO(data)
