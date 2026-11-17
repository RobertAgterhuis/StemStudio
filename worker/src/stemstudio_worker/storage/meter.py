"""``StorageMeter`` over the worker data and model directories (``GET /v1/storage``, feature 013).

The walk never reads file contents (only the job files, to attribute cache entries) and never
follows links; the report is kept for 30 seconds or until :meth:`DiskStorageMeter.invalidate`.
"""

import os
import re
import shutil
import threading
from collections.abc import Callable
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path
from typing import Final, Literal, Protocol

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.schemas import CacheEntryUsage, ModelCacheUsage, VolumeUsage, WorkerStorage
from stemstudio_worker.storage.model_catalog import models_in
from stemstudio_worker.storage.users import CacheUsers, read_cache_users
from stemstudio_worker.storage.walk import Usage, tree_usage, trees_usage

CACHE_TTL: Final = timedelta(seconds=30)
"""How long a report is reused."""

_ENTRY: Final = re.compile(r"[0-9a-f]{64}")
_FOLDER: Final = re.compile(r"[A-Za-z0-9._-]{1,64}")


class DiskUsageResult(Protocol):
    """What ``shutil.disk_usage`` returns (the members the meter reads)."""

    @property
    def total(self) -> int:
        """Total bytes."""
        ...

    @property
    def free(self) -> int:
        """Free bytes."""
        ...


DiskUsage = Callable[[str | os.PathLike[str]], DiskUsageResult]
"""``shutil.disk_usage`` or a fake."""


@dataclass(frozen=True, slots=True)
class MeterContext:
    """What the meter walks and how it reads time and free space.

    Attributes:
        paths: Worker data directory layout.
        model_dir: ``STEMSTUDIO_MODEL_DIR``.
        clock: Current UTC time (``measuredAt``, the 30 s cache).
        disk_usage: Free/total bytes of a path.
    """

    paths: DataPaths
    model_dir: Path
    clock: Clock
    disk_usage: DiskUsage = shutil.disk_usage


def cache_entry_dirs(paths: DataPaths) -> list[Path]:
    """The per-audio cache entries (folders named by 64 hex characters), sorted."""
    root = paths.audio_cache
    if not root.is_dir():
        return []
    return sorted(p for p in root.iterdir() if _ENTRY.fullmatch(p.name) and p.is_dir())


def job_file_roots(paths: DataPaths) -> list[Path]:
    """Folders of every job kind's files."""
    return [paths.jobs, paths.detail_jobs, paths.metadata_jobs, paths.render_jobs,
            paths.lyrics_jobs, paths.transcription_jobs]  # fmt: skip


def scratch_roots(paths: DataPaths) -> list[Path]:
    """Scratch folders: ``work/``, ``uploads/`` and ``artwork/``."""
    return [paths.root / "work", paths.root / "uploads", paths.user_artwork_root]


class DiskStorageMeter:
    """Measures the worker volumes (``StorageMeter``)."""

    def __init__(self, context: MeterContext) -> None:
        """Create a meter over ``context``."""
        self._context = context
        self._lock = threading.Lock()
        self._report: WorkerStorage | None = None

    def measure(self) -> WorkerStorage:
        """The cached report when younger than 30 seconds, else a new walk."""
        with self._lock:
            now = self._context.clock()
            report = self._report
            if report is None or now - report.measured_at >= CACHE_TTL:
                report = self._report = self._walk(now)
            return report

    def invalidate(self) -> None:
        """Drop the cached report (after a clean-up or a rehydration)."""
        with self._lock:
            self._report = None

    def _walk(self, now: datetime) -> WorkerStorage:
        paths = self._context.paths
        users = read_cache_users(paths)
        jobs = trees_usage(job_file_roots(paths))
        return WorkerStorage(
            measured_at=now, volumes=self._volumes(),
            cache_entries=[entry_usage(d, users) for d in cache_entry_dirs(paths)],
            job_files_bytes=jobs.bytes, job_files_count=jobs.files,
            scratch_bytes=trees_usage(scratch_roots(paths)).bytes,
            model_caches=model_caches(self._context.model_dir),
        )  # fmt: skip

    def _volumes(self) -> list[VolumeUsage]:
        return [self._volume("data", self._context.paths.root),
                self._volume("models", self._context.model_dir)]  # fmt: skip

    def _volume(self, volume: Literal["data", "models"], path: Path) -> VolumeUsage:
        usage = self._context.disk_usage(_existing(path))
        return VolumeUsage(id=volume, free_bytes=usage.free, total_bytes=usage.total)


def entry_usage(entry: Path, users: CacheUsers) -> CacheEntryUsage:
    """Sizes and users of one cache entry folder."""
    sha = entry.name
    standard = tree_usage(entry / "result.json") + tree_usage(entry / "stems")
    return CacheEntryUsage(
        audio_sha256=sha, standard_bytes=standard.bytes,
        detail_bytes=_bytes(entry / "detail"), partial_bytes=_bytes(entry / "detail.partial"),
        artwork_bytes=_bytes(entry / "artwork"), job_ids=users.job_ids.get(sha, []),
        in_use=sha in users.in_use, from_library=sha in users.from_library,
        last_used_at=users.last_used.get(sha),
    )  # fmt: skip


def model_caches(model_dir: Path) -> list[ModelCacheUsage]:
    """One row per top-level folder of the model cache (plain folder names only), sorted."""
    return [ModelCacheUsage(id=f.name, bytes=_bytes(f), models=models_in(f))
            for f in _model_folders(model_dir)]  # fmt: skip


def _model_folders(model_dir: Path) -> list[Path]:
    if not model_dir.is_dir():
        return []
    return sorted(p for p in model_dir.iterdir() if p.is_dir() and _FOLDER.fullmatch(p.name))


def _bytes(path: Path) -> int:
    usage: Usage = tree_usage(path)
    return usage.bytes


def _existing(path: Path) -> Path:
    """``path`` or its nearest existing ancestor (free space of a folder not created yet)."""
    while not path.exists() and path.parent != path:
        path = path.parent
    return path
