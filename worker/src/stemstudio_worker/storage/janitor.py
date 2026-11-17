"""``CacheJanitor`` over the worker data directory (``POST /v1/storage/cleanup``, feature 013).

Everything runs under the queue lock (no scheduler unit starts while files are removed) after the
cache users were read from the job files, so an entry a queued or running job uses is skipped.
Freed bytes are measured (before minus after), so they are exact even when a removal fails.
"""

import shutil
from collections.abc import Callable
from contextlib import AbstractContextManager, nullcontext
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path
from typing import Final, Literal

from pydantic import ValidationError

from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.schemas import (
    ContractModel,
    JobState,
    SkippedEntry,
    StorageCleanupRequest,
    StorageCleanupResult,
)
from stemstudio_worker.storage.meter import cache_entry_dirs, scratch_roots
from stemstudio_worker.storage.users import CacheUsers, read_cache_users
from stemstudio_worker.storage.walk import tree_usage

OLD_JOB_AGE: Final = timedelta(days=7)
"""Terminal job files older than this are "old job files"."""

STALE_SCRATCH_AGE: Final = timedelta(hours=1)
"""A scratch folder of no known job is removed only when untouched this long (an upload is
stored before its job file is written)."""


def _nothing() -> None:
    """Default hook: nothing to do."""


@dataclass(frozen=True, slots=True)
class JanitorContext:
    """What the janitor cleans and the hooks around it.

    Attributes:
        paths: Worker data directory layout.
        jobs: The live standard job store (evicted jobs are marked, old ones deleted through it).
        details: The live detail job store (old ones deleted through it).
        clock: Current UTC time.
        lock: The queue lock (``PriorityScheduler.hold`` plus the storage lock).
        sweep_artwork: Removes the expired user pictures (``UserArtworkStore.sweep``).
        on_change: Called after a clean-up (drops the meter's cached report).
    """

    paths: DataPaths
    jobs: JobStore
    details: DetailJobStore
    clock: Clock
    lock: Callable[[], AbstractContextManager[object]] = nullcontext
    sweep_artwork: Callable[[], object] = _nothing
    on_change: Callable[[], None] = _nothing


SkipReason = Literal["inUse", "notFound"]


class _Pointer(ContractModel):
    audio_sha256: str


@dataclass(slots=True)
class _Tally:
    freed: int = 0
    removed: list[str] = field(default_factory=list)
    skipped: list[SkippedEntry] = field(default_factory=list)

    def result(self) -> StorageCleanupResult:
        return StorageCleanupResult(
            freed_bytes=self.freed, removed_entries=self.removed, skipped=self.skipped
        )


class DiskCacheJanitor:
    """Removes cache entries, scratch and old job files that no job uses (``CacheJanitor``)."""

    def __init__(self, context: JanitorContext) -> None:
        """Create a janitor over ``context``."""
        self._context = context
        self._paths = context.paths

    def clean(self, request: StorageCleanupRequest) -> StorageCleanupResult:
        """Remove what ``request`` asks for under the queue lock; see the module docstring."""
        with self._context.lock():
            users = read_cache_users(self._paths)
            tally = self._entries(request, users)
            if request.scratch:
                tally.freed += self._scratch(users)
            if request.old_job_files:
                tally.freed += self._old_job_files()
        self._context.on_change()
        return tally.result()

    def _entries(self, request: StorageCleanupRequest, users: CacheUsers) -> _Tally:
        tally = _Tally()
        for sha in self._wanted(request):
            reason = self._skip_reason(sha, users)
            if reason is None:
                tally.freed += self._remove_entry(sha)
                tally.removed.append(sha)
            else:
                tally.skipped.append(SkippedEntry(audio_sha256=sha, reason=reason))
        return tally

    def _wanted(self, request: StorageCleanupRequest) -> list[str]:
        if request.all_entries:
            return [entry.name for entry in cache_entry_dirs(self._paths)]
        return list(dict.fromkeys(request.cache_entries))

    def _skip_reason(self, sha: str, users: CacheUsers) -> SkipReason | None:
        if not self._paths.by_audio(sha).is_dir():
            return "notFound"
        return "inUse" if sha in users.in_use else None

    def _remove_entry(self, sha: str) -> int:
        """Remove the entry and its source pointers and mark its jobs evicted; freed bytes."""
        freed = _remove(self._paths.by_audio(sha))
        for pointer in self._pointers_to(sha):
            freed += _remove(pointer)
        for record in self._context.jobs.all():
            if record.audio_sha256 == sha and not record.cache_evicted:
                self._context.jobs.save(record.model_copy(update={"cache_evicted": True}))
        return freed

    def _pointers_to(self, sha: str) -> list[Path]:
        folder = self._paths.source_pointers
        return [p for p in sorted(folder.glob("*.json")) if _pointer_sha(p) == sha]

    def _scratch(self, users: CacheUsers) -> int:
        cutoff = self._context.clock() - STALE_SCRATCH_AGE
        work, uploads, artwork = scratch_roots(self._paths)
        freed = sum(
            _remove(folder)
            for root in (work, uploads)
            for folder in _folders(root)
            if _removable_scratch(folder, users, cutoff)
        )
        return freed + _measured(artwork, self._context.sweep_artwork)

    def _old_job_files(self) -> int:
        cutoff = self._context.clock() - OLD_JOB_AGE
        return self._old_standard_files(cutoff) + self._old_detail_files(cutoff)

    def _old_standard_files(self, cutoff: datetime) -> int:
        old = [r.job.job_id for r in self._context.jobs.all() if self._old_standard(r, cutoff)]
        return sum(self._delete_standard(job_id) for job_id in old)

    def _old_detail_files(self, cutoff: datetime) -> int:
        old = [d.job.detail_job_id for d in self._context.details.all()
               if _expired(d.job.status.is_terminal, d.job.updated_at, cutoff)]  # fmt: skip
        return sum(self._delete_detail(detail_id) for detail_id in old)

    def _old_standard(self, record: StoredJob, cutoff: datetime) -> bool:
        """Terminal, older than ``cutoff`` and without a live cache entry to point at."""
        job = record.job
        if not _expired(job.status.is_terminal, job.updated_at, cutoff):
            return False
        if job.status is JobState.FAILED or record.cache_evicted or record.audio_sha256 is None:
            return True
        return not self._paths.by_audio(record.audio_sha256).is_dir()

    def _delete_standard(self, job_id: str) -> int:
        file, folder = self._paths.jobs / f"{job_id}.json", self._paths.jobs / job_id
        freed = tree_usage(file).bytes + _remove(folder)
        self._context.jobs.delete(job_id)
        return freed

    def _delete_detail(self, detail_id: str) -> int:
        freed = tree_usage(self._paths.detail_jobs / f"{detail_id}.json").bytes
        self._context.details.delete(detail_id)
        return freed


def _expired(terminal: bool, updated_at: datetime, cutoff: datetime) -> bool:
    """A terminal job last updated before ``cutoff``."""
    return terminal and updated_at < cutoff


def _removable_scratch(folder: Path, users: CacheUsers, cutoff: datetime) -> bool:
    """A terminal job's folder, or a folder of no known job untouched since ``cutoff``."""
    terminal = users.jobs.get(folder.name)
    if terminal is not None:
        return terminal
    return datetime.fromtimestamp(folder.stat().st_mtime, cutoff.tzinfo) < cutoff


def _folders(root: Path) -> list[Path]:
    if not root.is_dir():
        return []
    return sorted(p for p in root.iterdir() if p.is_dir() and not p.is_symlink())


def _measured(root: Path, action: Callable[[], object]) -> int:
    """Run ``action`` and return how many bytes below ``root`` it freed."""
    before = tree_usage(root).bytes
    action()
    return max(before - tree_usage(root).bytes, 0)


def _remove(path: Path) -> int:
    """Remove a file or folder tree (errors ignored); the bytes actually freed."""
    before = tree_usage(path).bytes
    if path.is_dir() and not path.is_symlink():
        shutil.rmtree(path, ignore_errors=True)
    else:
        path.unlink(missing_ok=True)
    return before - tree_usage(path).bytes


def _pointer_sha(path: Path) -> str | None:
    try:
        return _Pointer.model_validate_json(path.read_text(encoding="utf-8")).audio_sha256
    except (OSError, ValidationError):
        return None
