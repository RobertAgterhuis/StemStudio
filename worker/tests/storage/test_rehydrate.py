"""Tests of ``storage.rehydrate`` and ``storage.library`` (T029): jobs from library stems."""

import hashlib
from pathlib import Path
from typing import Any
from uuid import UUID

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.jobs.cache import ResultCache
from stemstudio_worker.jobs.detail_service import DetailContext, DetailService
from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import LibraryStemReader, LibraryUnavailableError, StemMissingError
from stemstudio_worker.schemas import JobState, RehydrateRequest, SourceType, StemName
from stemstudio_worker.storage.library import FileLibraryStemReader
from stemstudio_worker.storage.rehydrate import (
    InvalidStemError,
    RehydrateContext,
    Rehydrator,
    StemSetError,
    flac_duration,
    library_key,
)

from .builders import T0, StepClock, library_stems

_TRACK = UUID("0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f")
_SIX: list[StemName] = ["vocals", "drums", "bass", "guitar", "piano", "other"]


def _request(**changes: object) -> RehydrateRequest:
    body: dict[str, Any] = {
        "trackId": str(_TRACK), "sourceType": "youtube", "sourceKey": "youtube:dQw4w9WgXcQ",
        "model": "htdemucs_6s", "stems": _SIX,
    }  # fmt: skip
    return RehydrateRequest.model_validate({**body, **changes})


class _Env:
    def __init__(self, tmp_path: Path) -> None:
        self.library = tmp_path / "library"
        self.paths = DataPaths(tmp_path / "data", library=self.library)
        self.jobs = JobStore(self.paths.jobs)
        self.clock = StepClock()
        self.events: list[str] = []

    def rehydrator(self, reader: LibraryStemReader | None = None) -> Rehydrator:
        context = RehydrateContext(
            paths=self.paths, jobs=self.jobs, reader=reader or FileLibraryStemReader(self.paths),
            clock=self.clock, lock=self._lock, on_change=lambda: self.events.append("changed"),
        )  # fmt: skip
        return Rehydrator(context)

    def _lock(self) -> "_Recorder":
        return _Recorder(self.events)


class _Recorder:
    def __init__(self, events: list[str]) -> None:
        self._events = events

    def __enter__(self) -> None:
        self._events.append("locked")

    def __exit__(self, *_exc: object) -> None:
        self._events.append("unlocked")


@pytest.fixture
def env(tmp_path: Path) -> _Env:
    return _Env(tmp_path)


def test_key_is_the_hash_of_the_sorted_stem_hashes() -> None:
    digests = {"vocals": "b" * 64, "bass": "a" * 64}

    expected = hashlib.sha256(f"library:bass={'a' * 64},vocals={'b' * 64}".encode()).hexdigest()
    assert library_key(digests) == expected


def test_copies_the_stems_into_a_new_cache_entry(env: _Env) -> None:
    written = library_stems(env.library, _TRACK, _SIX)

    job, created = env.rehydrator().rehydrate(_request())

    digests = {stem: hashlib.sha256(data).hexdigest() for stem, data in written.items()}
    assert created
    assert job.audio_sha256 == library_key(digests)
    for stem, data in written.items():
        assert env.paths.stem_file(job.audio_sha256, stem).read_bytes() == data
    expected = ("completed", "htdemucs_6s", _SIX, T0)
    assert (job.status, job.model, job.stems, job.created_at) == expected
    assert list(env.paths.audio_cache.glob("*.partial")) == []
    assert ResultCache(env.paths).lookup_audio(job.audio_sha256) is not None


def test_writes_a_completed_job_file_flagged_from_library(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)

    job, _created = env.rehydrator().rehydrate(
        _request(sourceType="upload", sourceKey="upload:abc")
    )

    reloaded = JobStore(env.paths.jobs)
    reloaded.load_all()
    record = reloaded.get(job.job_id)
    assert record is not None
    assert record.from_library
    assert (record.job.status, record.job.progress, record.job.stage) == (
        JobState.COMPLETED, 100, "Completed",
    )  # fmt: skip
    assert (record.job.source_type, record.job.source_key) == (SourceType.UPLOAD, "upload:abc")
    assert record.audio_sha256 == job.audio_sha256


def test_the_stored_result_has_the_duration_and_the_stems(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)

    job, _created = env.rehydrator().rehydrate(_request())

    record = env.jobs.get(job.job_id)
    assert record is not None
    assert record.result is not None
    assert record.result.duration_seconds == pytest.approx(0.1)
    assert [s.name for s in record.result.stems] == _SIX


def test_is_idempotent_for_the_same_stems(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    first, _ = env.rehydrator().rehydrate(_request())

    again, created = env.rehydrator().rehydrate(_request())

    assert not created
    assert again == first
    assert len(env.jobs.all()) == 1


def test_an_evicted_job_gets_its_entry_back(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    first, _ = env.rehydrator().rehydrate(_request())
    record = env.jobs.get(first.job_id)
    assert record is not None
    env.jobs.save(record.model_copy(update={"cache_evicted": True}))
    for stem in _SIX:
        env.paths.stem_file(first.audio_sha256, stem).unlink()

    again, created = env.rehydrator().rehydrate(_request())

    restored = env.jobs.get(first.job_id)
    assert (again.job_id, created) == (first.job_id, False)
    assert restored is not None
    assert not restored.cache_evicted
    assert ResultCache(env.paths).lookup_audio(first.audio_sha256) is not None


def test_an_existing_entry_without_a_job_gets_a_new_job(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    first, _ = env.rehydrator().rehydrate(_request())
    env.jobs.delete(first.job_id)

    again, created = env.rehydrator().rehydrate(_request())

    assert created
    assert again.job_id != first.job_id
    assert again.audio_sha256 == first.audio_sha256


def test_four_stem_model(env: _Env) -> None:
    four: list[StemName] = ["vocals", "drums", "bass", "other"]
    library_stems(env.library, _TRACK, four)

    job, _ = env.rehydrator().rehydrate(_request(model="htdemucs", stems=four))

    assert job.stems == four


def test_the_stem_set_must_equal_the_models(env: _Env) -> None:
    with pytest.raises(StemSetError, match="htdemucs"):
        env.rehydrator().rehydrate(_request(model="htdemucs"))


def test_a_missing_stem_is_reported_and_nothing_is_kept(env: _Env) -> None:
    library_stems(env.library, _TRACK, ["vocals", "drums", "bass", "guitar", "other"])

    with pytest.raises(StemMissingError) as raised:
        env.rehydrator().rehydrate(_request())

    assert raised.value.stem == "piano"
    assert env.jobs.all() == []
    assert list(env.paths.audio_cache.glob("*")) == []
    assert env.events == ["locked", "unlocked"]


def test_an_unreadable_stem_is_invalid(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    (env.library / "tracks" / str(_TRACK) / "stems" / "vocals.flac").write_bytes(b"not flac")

    with pytest.raises(InvalidStemError, match="vocals"):
        env.rehydrator().rehydrate(_request())

    assert list(env.paths.audio_cache.glob("*")) == []


def test_lock_and_change_hook(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)

    env.rehydrator().rehydrate(_request())

    assert env.events == ["locked", "unlocked", "changed"]


def test_durations_are_clamped_to_the_contract_maximum(env: _Env, tmp_path: Path) -> None:
    path = tmp_path / "long.flac"
    sf.write(path, np.zeros((44100, 2), dtype=np.float32), 44100, format="FLAC")

    assert flac_duration(path) == pytest.approx(1.0)
    context = RehydrateContext(
        env.paths, env.jobs, FileLibraryStemReader(env.paths), env.clock, duration=lambda _p: 5000
    )
    library_stems(env.library, _TRACK, _SIX)
    job, _ = Rehydrator(context).rehydrate(_request())
    record = env.jobs.get(job.job_id)
    assert record is not None
    assert record.result is not None
    assert record.result.duration_seconds == 900


def test_a_rehydrated_job_can_start_a_detail_job(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    job, _ = env.rehydrator().rehydrate(_request())
    details = DetailJobStore(env.paths.detail_jobs)
    service = DetailService(DetailContext(env.jobs, details, env.paths, env.clock), lambda _i: None)

    detail, created = service.create(job.job_id)

    assert created
    assert detail.audio_sha256 == job.audio_sha256


# ------------------------------------------------------------------ FileLibraryStemReader


def test_reader_opens_the_uuid_and_enum_path(env: _Env) -> None:
    written = library_stems(env.library, _TRACK, ["piano"])
    reader = FileLibraryStemReader(env.paths)

    with reader.open(_TRACK, "piano") as stream:
        assert stream.read() == written["piano"]
    assert isinstance(reader, LibraryStemReader)


def test_reader_reports_a_missing_stem(env: _Env) -> None:
    env.library.mkdir()

    with pytest.raises(StemMissingError):
        FileLibraryStemReader(env.paths).open(_TRACK, "bass")


@pytest.mark.parametrize("library", [None, "missing"])
def test_reader_without_a_readable_library(tmp_path: Path, library: str | None) -> None:
    paths = DataPaths(tmp_path, library=None if library is None else tmp_path / library)

    with pytest.raises(LibraryUnavailableError):
        FileLibraryStemReader(paths).open(_TRACK, "bass")


def test_reader_maps_other_os_errors_to_an_unavailable_library(env: _Env) -> None:
    (env.library / "tracks" / str(_TRACK) / "stems" / "bass.flac").mkdir(parents=True)

    with pytest.raises(LibraryUnavailableError):
        FileLibraryStemReader(env.paths).open(_TRACK, "bass")


def test_an_empty_stem_is_invalid(env: _Env) -> None:
    library_stems(env.library, _TRACK, _SIX)
    context = RehydrateContext(
        env.paths, env.jobs, FileLibraryStemReader(env.paths), env.clock, duration=lambda _p: 0
    )

    with pytest.raises(InvalidStemError):
        Rehydrator(context).rehydrate(_request())
