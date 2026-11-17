"""Tests of ``storage.meter`` (T025): the walk behind ``GET /v1/storage``."""

import contextlib
import importlib
import os
from datetime import timedelta
from pathlib import Path
from typing import NamedTuple

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.ports import StorageMeter
from stemstudio_worker.schemas import DetailJobState, JobState, LyricsJobState
from stemstudio_worker.storage.meter import DiskStorageMeter, MeterContext
from stemstudio_worker.storage.walk import Usage, tree_usage

from .builders import (
    SHA_A,
    SHA_B,
    SHA_C,
    T0,
    StepClock,
    cache_entry,
    save_alignment,
    save_detail,
    save_job,
    save_lookup,
    stored_job,
    write,
)

_JOB_1, _JOB_2, _JOB_3 = "1" * 32, "2" * 32, "3" * 32


class _DiskUsage(NamedTuple):
    total: int
    used: int
    free: int


class _FakeDisk:
    """``shutil.disk_usage`` stand-in: fixed numbers per path, records the paths asked for."""

    def __init__(self) -> None:
        self.asked: list[Path] = []

    def __call__(self, path: str | os.PathLike[str]) -> _DiskUsage:
        self.asked.append(Path(path))
        return _DiskUsage(total=1000, used=400, free=600)


@pytest.fixture
def paths(tmp_path: Path) -> DataPaths:
    return DataPaths(tmp_path / "data")


@pytest.fixture
def clock() -> StepClock:
    return StepClock()


@pytest.fixture
def disk() -> _FakeDisk:
    return _FakeDisk()


@pytest.fixture
def meter(paths: DataPaths, clock: StepClock, disk: _FakeDisk) -> DiskStorageMeter:
    models = paths.root.parent / "models"
    return DiskStorageMeter(MeterContext(paths, models, clock, disk))


def test_meter_satisfies_the_port(meter: DiskStorageMeter) -> None:
    assert isinstance(meter, StorageMeter)


def test_empty_data_dir_reports_zeros(meter: DiskStorageMeter) -> None:
    report = meter.measure()

    assert report.measured_at == T0
    assert report.cache_entries == []
    assert (report.job_files_bytes, report.job_files_count, report.scratch_bytes) == (0, 0, 0)
    assert report.model_caches == []


def test_volumes_use_disk_usage_of_the_nearest_existing_folder(
    paths: DataPaths, meter: DiskStorageMeter, disk: _FakeDisk
) -> None:
    paths.root.mkdir(parents=True)

    report = meter.measure()

    assert [(v.id, v.free_bytes, v.total_bytes) for v in report.volumes] == [
        ("data", 600, 1000),
        ("models", 600, 1000),
    ]
    assert disk.asked == [paths.root, paths.root.parent]


def test_cache_entry_bytes_per_part(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (150, 40, 7, 3))

    (entry,) = meter.measure().cache_entries

    assert entry.audio_sha256 == SHA_A
    sizes = (entry.standard_bytes, entry.detail_bytes, entry.partial_bytes, entry.artwork_bytes)
    assert sizes == (150, 40, 7, 3)
    assert (entry.job_ids, entry.in_use, entry.from_library, entry.last_used_at) == (
        [], False, False, None,
    )  # fmt: skip


def test_only_hex_entry_folders_count_as_entries(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    write(paths.audio_cache / f"{SHA_B}.partial" / "stems" / "vocals.flac", 5)
    write(paths.audio_cache / "stray.txt", 5)

    entries = meter.measure().cache_entries

    assert [e.audio_sha256 for e in entries] == [SHA_A]


def test_job_ids_come_from_the_job_files(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    later = T0 + timedelta(hours=1)
    save_job(paths, stored_job(_JOB_1, SHA_A, JobState.COMPLETED))
    save_job(paths, stored_job(_JOB_2, SHA_A, JobState.COMPLETED, updated=later))
    save_job(paths, stored_job(_JOB_3, SHA_A, JobState.FAILED))

    (entry,) = meter.measure().cache_entries

    assert entry.job_ids == [_JOB_1, _JOB_2]
    assert entry.last_used_at == later
    assert not entry.in_use


def test_evicted_jobs_are_not_attributed(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    save_job(paths, stored_job(_JOB_1, SHA_A, JobState.COMPLETED, cacheEvicted=True))

    (entry,) = meter.measure().cache_entries

    assert entry.job_ids == []


def test_rehydrated_entries_are_from_library(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    save_job(paths, stored_job(_JOB_1, SHA_A, JobState.COMPLETED, fromLibrary=True))

    (entry,) = meter.measure().cache_entries

    assert entry.from_library


@pytest.mark.parametrize("status", [JobState.QUEUED, JobState.SEPARATING, JobState.ANALYSING])
def test_non_terminal_standard_job_marks_in_use(
    paths: DataPaths, meter: DiskStorageMeter, status: JobState
) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    save_job(paths, stored_job(_JOB_1, SHA_A, status))

    (entry,) = meter.measure().cache_entries

    assert entry.in_use
    assert entry.job_ids == [_JOB_1]


@pytest.mark.parametrize(
    ("status", "in_use"), [(DetailJobState.RUNNING, True), (DetailJobState.COMPLETED, False)]
)
def test_detail_jobs_mark_in_use_while_active(
    paths: DataPaths, meter: DiskStorageMeter, status: DetailJobState, *, in_use: bool
) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    save_detail(paths, "d" * 32, SHA_A, status)

    (entry,) = meter.measure().cache_entries

    assert entry.in_use is in_use
    assert entry.last_used_at == T0


@pytest.mark.parametrize(
    ("status", "in_use"), [(LyricsJobState.QUEUED, True), (LyricsJobState.FAILED, False)]
)
def test_alignments_mark_their_standard_jobs_entry(
    paths: DataPaths, meter: DiskStorageMeter, status: LyricsJobState, *, in_use: bool
) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    save_job(paths, stored_job(_JOB_1, SHA_A, JobState.COMPLETED))
    save_alignment(paths, "e" * 32, _JOB_1, status)
    save_alignment(paths, "f" * 32, "9" * 32, LyricsJobState.QUEUED)
    save_lookup(paths, "0" * 32, _JOB_1)

    (entry,) = meter.measure().cache_entries

    assert entry.in_use is in_use


def test_unreadable_job_files_are_skipped(paths: DataPaths, meter: DiskStorageMeter) -> None:
    cache_entry(paths, SHA_A, (20, 0, 0, 0))
    write(paths.jobs / f"{_JOB_1}.json", 3)

    (entry,) = meter.measure().cache_entries

    assert entry.job_ids == []


def test_job_files_and_scratch(paths: DataPaths, meter: DiskStorageMeter) -> None:
    write(paths.jobs / "x.bin", 4)
    write(paths.job_artwork(_JOB_1) / "cover.jpg", 6)
    write(paths.detail_jobs / "y.bin", 1)
    for folder in (paths.metadata_jobs, paths.render_jobs, paths.lyrics_jobs):
        write(folder / "z.bin", 2)
    write(paths.transcription_jobs / "s" / "score.pdf", 3)
    write(paths.work(_JOB_2) / "audio.wav", 100)
    write(paths.uploads(_JOB_2) / "input.mp3", 20)
    write(paths.user_artwork(_JOB_3) / "cover.jpg", 5)

    report = meter.measure()

    assert (report.job_files_bytes, report.job_files_count) == (20, 7)
    assert report.scratch_bytes == 125


def test_model_caches_per_top_level_folder(paths: DataPaths, meter: DiskStorageMeter) -> None:
    models = paths.root.parent / "models"
    write(models / "torch" / "hub" / "checkpoints" / "955717e8-8726e21a.th", 30)
    write(models / "whisper" / "large-v3-turbo.pt", 50)
    write(models / "acestep" / "weights.bin", 7)
    write(models / "loose-file.bin", 9)
    (models / "bad name").mkdir()

    caches = meter.measure().model_caches

    assert [(c.id, c.bytes, c.models) for c in caches] == [
        ("acestep", 7, []),
        ("torch", 30, ["htdemucs"]),
        ("whisper", 50, ["whisper-large-v3-turbo"]),
    ]


def test_report_is_cached_for_30_seconds(
    paths: DataPaths, meter: DiskStorageMeter, clock: StepClock
) -> None:
    first = meter.measure()
    cache_entry(paths, SHA_C, (20, 0, 0, 0))

    clock.advance(29)
    assert meter.measure() is first
    clock.advance(1)
    assert [e.audio_sha256 for e in meter.measure().cache_entries] == [SHA_C]


def test_invalidate_forces_a_new_walk(meter: DiskStorageMeter) -> None:
    first = meter.measure()

    meter.invalidate()

    assert meter.measure() is not first


def _directory_link(link: Path, target: Path) -> None:
    """A symbolic link, or a junction where links need a privilege (Windows)."""
    try:
        link.symlink_to(target, target_is_directory=True)
    except OSError:
        importlib.import_module("_winapi").CreateJunction(str(target), str(link))


def test_walk_does_not_follow_links(tmp_path: Path) -> None:
    outside = write(tmp_path / "outside" / "big.bin", 1000)
    inside = tmp_path / "inside"
    write(inside / "small.bin", 3)
    _directory_link(inside / "dir-link", outside.parent)
    with contextlib.suppress(OSError):
        (inside / "file-link").symlink_to(outside)

    assert tree_usage(inside) == Usage(3, 1)
    assert tree_usage(inside / "dir-link") == Usage()


def test_unreadable_folders_count_as_empty(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    write(tmp_path / "sub" / "a.bin", 4)

    def refuse(_path: object) -> object:
        raise PermissionError

    monkeypatch.setattr(os, "scandir", refuse)

    assert tree_usage(tmp_path) == Usage()


def test_walk_of_a_file_and_of_nothing(tmp_path: Path) -> None:
    assert tree_usage(write(tmp_path / "one.bin", 12)) == Usage(12, 1)
    assert tree_usage(tmp_path / "missing") == Usage(0, 0)
    assert Usage(1, 2) + Usage(3, 4) == Usage(4, 6)
