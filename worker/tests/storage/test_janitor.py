"""Tests of ``storage.janitor`` (T027): ``POST /v1/storage/cleanup`` under the queue lock."""

import os
from collections.abc import Iterator
from contextlib import contextmanager
from dataclasses import dataclass, field
from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import CacheJanitor
from stemstudio_worker.schemas import (
    DetailJobState,
    JobState,
    SkippedEntry,
    StorageCleanupRequest,
)
from stemstudio_worker.storage.janitor import DiskCacheJanitor, JanitorContext

from .builders import (
    SHA_A,
    SHA_B,
    SHA_C,
    T0,
    StepClock,
    cache_entry,
    save_detail,
    stored_job,
    write,
)

_JOB_1, _JOB_2, _JOB_3, _JOB_4 = "1" * 32, "2" * 32, "3" * 32, "4" * 32
_ENTRY = (100, 40, 7, 3)
_ENTRY_BYTES = 150


@dataclass
class _Lock:
    """Queue lock stand-in: records which paths existed on entry and on exit."""

    watched: Path | None = None
    log: list[tuple[str, bool]] = field(default_factory=list)

    @contextmanager
    def __call__(self) -> Iterator[None]:
        self.log.append(("enter", self._exists()))
        yield
        self.log.append(("exit", self._exists()))

    def _exists(self) -> bool:
        return self.watched is not None and self.watched.exists()


@dataclass
class _Env:
    paths: DataPaths
    jobs: JobStore
    details: DetailJobStore
    clock: StepClock
    lock: _Lock
    calls: list[str]

    def janitor(self) -> DiskCacheJanitor:
        return DiskCacheJanitor(
            JanitorContext(
                paths=self.paths, jobs=self.jobs, details=self.details, clock=self.clock,
                lock=self.lock, sweep_artwork=lambda: self.calls.append("sweep"),
                on_change=lambda: self.calls.append("changed"),
            )
        )  # fmt: skip

    def clean(self, **request: object) -> tuple[int, list[str], list[SkippedEntry]]:
        result = self.janitor().clean(StorageCleanupRequest.model_validate(request))
        return result.freed_bytes, result.removed_entries, result.skipped

    def save(self, record: StoredJob) -> None:
        self.jobs.save(record)


@pytest.fixture
def env(tmp_path: Path) -> _Env:
    paths = DataPaths(tmp_path)
    jobs, details = JobStore(paths.jobs), DetailJobStore(paths.detail_jobs)
    return _Env(paths, jobs, details, StepClock(T0 + timedelta(days=30)), _Lock(), [])


def _pointer(paths: DataPaths, key: str, sha: str) -> Path:
    target = paths.by_source(key)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(f'{{"audioSha256": "{sha}"}}', encoding="utf-8")
    return target


def _age(path: Path, env: _Env, hours: float) -> Path:
    """Set the modification time of ``path`` to ``hours`` before the fake clock."""
    stamp = (env.clock.now - timedelta(hours=hours)).timestamp()
    os.utime(path, (stamp, stamp))
    return path


def test_janitor_satisfies_the_port(env: _Env) -> None:
    assert isinstance(env.janitor(), CacheJanitor)


def test_default_request_removes_nothing(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)

    assert env.clean() == (0, [], [])
    assert env.paths.by_audio(SHA_A).is_dir()


def test_removes_a_requested_entry_with_exact_freed_bytes(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)
    cache_entry(env.paths, SHA_B, _ENTRY)

    assert env.clean(cacheEntries=[SHA_A]) == (_ENTRY_BYTES, [SHA_A], [])
    assert not env.paths.by_audio(SHA_A).exists()
    assert env.paths.by_audio(SHA_B).is_dir()


def test_deletes_while_holding_the_queue_lock(env: _Env) -> None:
    env.lock.watched = cache_entry(env.paths, SHA_A, _ENTRY)

    env.clean(cacheEntries=[SHA_A])

    assert env.lock.log == [("enter", True), ("exit", False)]
    assert env.calls == ["changed"]


def test_entries_in_use_are_skipped(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)
    cache_entry(env.paths, SHA_B, _ENTRY)
    env.save(stored_job(_JOB_1, SHA_A, JobState.SEPARATING))
    save_detail(env.paths, "d" * 32, SHA_B, DetailJobState.QUEUED)

    freed, removed, skipped = env.clean(cacheEntries=[SHA_A, SHA_B])

    assert (freed, removed) == (0, [])
    assert skipped == [
        SkippedEntry(audio_sha256=SHA_A, reason="inUse"),
        SkippedEntry(audio_sha256=SHA_B, reason="inUse"),
    ]
    assert env.paths.by_audio(SHA_A).is_dir()


def test_unknown_entries_are_reported_once(env: _Env) -> None:
    _freed, _removed, skipped = env.clean(cacheEntries=[SHA_C, SHA_C])

    assert skipped == [SkippedEntry(audio_sha256=SHA_C, reason="notFound")]


def test_wildcard_removes_every_entry_not_in_use(env: _Env) -> None:
    for sha in (SHA_A, SHA_B, SHA_C):
        cache_entry(env.paths, sha, _ENTRY)
    env.save(stored_job(_JOB_1, SHA_B, JobState.QUEUED))
    write(env.paths.audio_cache / f"{SHA_C}.partial" / "stems" / "vocals.flac", 5)

    freed, removed, skipped = env.clean(cacheEntries=["*"])

    assert (freed, removed) == (2 * _ENTRY_BYTES, [SHA_A, SHA_C])
    assert skipped == [SkippedEntry(audio_sha256=SHA_B, reason="inUse")]
    assert (env.paths.audio_cache / f"{SHA_C}.partial").is_dir()


def test_by_source_pointers_to_the_entry_are_removed(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)
    gone = _pointer(env.paths, "youtube:aaa", SHA_A)
    kept = _pointer(env.paths, "youtube:bbb", SHA_B)
    write(env.paths.by_source("youtube:ccc"), 3)

    freed, _removed, _skipped = env.clean(cacheEntries=[SHA_A])

    assert not gone.exists()
    assert kept.exists()
    assert freed == _ENTRY_BYTES + len('{"audioSha256": "' + SHA_A + '"}')


def test_referencing_jobs_are_marked_evicted(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)
    env.save(stored_job(_JOB_1, SHA_A, JobState.COMPLETED))
    env.save(stored_job(_JOB_2, SHA_B, JobState.COMPLETED))

    env.clean(cacheEntries=[SHA_A])

    first, second = env.jobs.get(_JOB_1), env.jobs.get(_JOB_2)
    assert first is not None
    assert second is not None
    assert (first.cache_evicted, second.cache_evicted) == (True, False)
    reloaded = JobStore(env.paths.jobs)
    reloaded.load_all()
    assert reloaded.get(_JOB_1) == first


def test_scratch_of_terminal_and_stale_unknown_jobs(env: _Env) -> None:
    env.save(stored_job(_JOB_1, None, JobState.FAILED))
    env.save(stored_job(_JOB_2, None, JobState.DOWNLOADING))
    write(env.paths.work(_JOB_1) / "audio.wav", 10)
    write(env.paths.work(_JOB_2) / "audio.wav", 20)
    _age(write(env.paths.uploads(_JOB_3) / "input.mp3", 30).parent, env, hours=2)
    _age(write(env.paths.uploads(_JOB_4) / "input.mp3", 40).parent, env, hours=0.5)

    freed, _removed, _skipped = env.clean(scratch=True)

    assert freed == 40
    assert not env.paths.work(_JOB_1).exists()
    assert not env.paths.uploads(_JOB_3).exists()
    assert env.paths.work(_JOB_2).is_dir()
    assert env.paths.uploads(_JOB_4).is_dir()


def test_scratch_sweeps_expired_user_artwork_and_counts_it(env: _Env) -> None:
    picture = write(env.paths.user_artwork("e" * 32) / "cover.jpg", 25)

    def sweep() -> None:
        picture.unlink()
        env.calls.append("sweep")

    context = JanitorContext(env.paths, env.jobs, env.details, env.clock, sweep_artwork=sweep)
    result = DiskCacheJanitor(context).clean(StorageCleanupRequest(scratch=True))

    assert result.freed_bytes == 25
    assert env.calls == ["sweep"]


def test_scratch_ignores_loose_files(env: _Env) -> None:
    loose = _age(write(env.paths.root / "work" / "note.txt", 4), env, hours=5)

    assert env.clean(scratch=True)[0] == 0
    assert loose.exists()


def test_old_job_files_older_than_7_days(env: _Env) -> None:
    old, recent = env.clock.now - timedelta(days=8), env.clock.now - timedelta(days=6)
    cache_entry(env.paths, SHA_A, _ENTRY)
    env.save(stored_job(_JOB_1, SHA_A, JobState.COMPLETED, updated=old))
    env.save(stored_job(_JOB_2, SHA_B, JobState.COMPLETED, updated=old))
    env.save(stored_job(_JOB_3, None, JobState.FAILED, updated=recent))
    env.save(stored_job(_JOB_4, None, JobState.FAILED, updated=old))
    artwork = write(env.paths.job_artwork(_JOB_4) / "cover.jpg", 9)
    sizes = {j: (env.paths.jobs / f"{j}.json").stat().st_size for j in (_JOB_2, _JOB_4)}

    freed, _removed, _skipped = env.clean(oldJobFiles=True)

    assert {r.job.job_id for r in env.jobs.all()} == {_JOB_1, _JOB_3}
    assert not artwork.parent.parent.exists()
    assert freed == sum(sizes.values()) + 9


def test_old_job_files_keep_active_jobs(env: _Env) -> None:
    env.save(stored_job(_JOB_1, None, JobState.QUEUED, updated=env.clock.now - timedelta(days=9)))

    env.clean(oldJobFiles=True)

    assert env.jobs.get(_JOB_1) is not None


def test_evicted_completed_jobs_are_old_job_files(env: _Env) -> None:
    cache_entry(env.paths, SHA_A, _ENTRY)
    old = env.clock.now - timedelta(days=8)
    env.save(stored_job(_JOB_1, SHA_A, JobState.COMPLETED, updated=old, cacheEvicted=True))

    env.clean(oldJobFiles=True)

    assert env.jobs.get(_JOB_1) is None


def test_old_terminal_detail_jobs_are_removed(env: _Env) -> None:
    save_detail(env.paths, "d" * 32, SHA_A, DetailJobState.COMPLETED)
    save_detail(env.paths, "e" * 32, SHA_A, DetailJobState.RUNNING)
    env.details.load_all()

    env.clean(oldJobFiles=True)

    assert [r.job.detail_job_id for r in env.details.all()] == ["e" * 32]
    assert not (env.paths.detail_jobs / f"{'d' * 32}.json").exists()
