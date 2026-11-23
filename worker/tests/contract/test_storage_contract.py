"""Skeleton tests for the feature 013 contract code: worker API 1.7.0, storage ports and fakes.

The code follows the 1.7.0 document (``specs/013-library-care/contracts``), which contains the
008 document (1.6.0) unchanged plus the ``storage`` tag (three operations, eight schemas).
"""

from datetime import UTC, datetime
from typing import Any, get_args
from uuid import UUID

import pytest
from pydantic import BaseModel, ValidationError

from stemstudio_worker.ports import (
    LibraryUnavailableError,
    StemMissingError,
    WorkerError,
)
from stemstudio_worker.schemas import (
    CONTRACT_VERSION,
    LIBRARY_UNAVAILABLE_TEXT,
    MODEL_STEMS,
    CacheEntryUsage,
    ModelCacheUsage,
    ModelName,
    RehydratedJob,
    RehydrateRequest,
    SkippedEntry,
    SourceType,
    StorageCleanupRequest,
    StorageCleanupResult,
    VolumeUsage,
    WorkerStorage,
)

from .docs import WorkerDocument, changed_entries

V160 = WorkerDocument("008-sheet-music")
V170 = WorkerDocument("013-library-care")
_TRACK = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f"
_SHA = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b"
_SIX = ["vocals", "drums", "bass", "guitar", "piano", "other"]
_NEW_SCHEMAS = {
    "VolumeUsage",
    "CacheEntryUsage",
    "ModelCacheUsage",
    "WorkerStorage",
    "StorageCleanupRequest",
    "StorageCleanupResult",
    "RehydrateRequest",
    "RehydratedJob",
}


def _request_example(path: str) -> Any:
    body = V170.root["paths"][path]["post"]["requestBody"]["content"]["application/json"]
    return dict(body["example"])


def _rehydrate(**changes: object) -> dict[str, object]:
    return {**_request_example("/v1/jobs/rehydrate"), **changes}


def test_contract_version_is_1_7_0() -> None:
    assert V170.root["info"]["version"] == "1.7.0"
    assert CONTRACT_VERSION == "1.11.0"  # 020 Phase 0; see test_instrument_views_contract.py


def test_1_7_0_contains_1_6_0_unchanged() -> None:
    for section in ("paths", "schemas", "examples", "responses", "parameters"):
        assert changed_entries(V160, V170, section) == set(), section
    added = set(V170.section("schemas")) - set(V160.section("schemas"))
    assert added == _NEW_SCHEMAS
    assert set(V170.section("paths")) - set(V160.section("paths")) == {
        "/v1/storage",
        "/v1/storage/cleanup",
        "/v1/jobs/rehydrate",
    }


@pytest.mark.parametrize(
    ("example", "model"),
    [
        ("WorkerStorageReport", WorkerStorage),
        ("StorageCleanupDone", StorageCleanupResult),
        ("RehydratedJobCreated", RehydratedJob),
    ],
)
def test_examples_round_trip(example: str, model: type[BaseModel]) -> None:
    V170.assert_round_trip(example, model)


@pytest.mark.parametrize(
    ("path", "model"),
    [("/v1/storage/cleanup", StorageCleanupRequest), ("/v1/jobs/rehydrate", RehydrateRequest)],
)
def test_request_examples_round_trip(path: str, model: type[BaseModel]) -> None:
    example = _request_example(path)
    dumped = model.model_validate(example).model_dump(mode="json", by_alias=True)

    assert dumped == example
    assert V170.errors(model.__name__, dumped) == []


@pytest.mark.parametrize(
    "model",
    [VolumeUsage, CacheEntryUsage, ModelCacheUsage, WorkerStorage, StorageCleanupResult],
)
def test_models_forbid_unknown_fields(model: type[BaseModel]) -> None:
    with pytest.raises(ValidationError, match="Extra inputs"):
        model.model_validate({"unexpected": 1})


def test_cleanup_request_defaults_to_nothing() -> None:
    request = StorageCleanupRequest()

    assert request.model_dump(by_alias=True) == {
        "cacheEntries": [],
        "scratch": False,
        "oldJobFiles": False,
    }
    assert not request.all_entries
    assert V170.errors("StorageCleanupRequest", {}) == []


def test_cleanup_request_accepts_the_wildcard_alone() -> None:
    request = StorageCleanupRequest.model_validate({"cacheEntries": ["*"]})

    assert request.all_entries
    assert StorageCleanupRequest(cache_entries=[_SHA]).all_entries is False


@pytest.mark.parametrize(
    "entries",
    [["*", _SHA], ["*", "*"], [_SHA.upper()], ["abc"], [_SHA + "0"]],
)
def test_cleanup_request_rejects_bad_entries(entries: list[str]) -> None:
    with pytest.raises(ValidationError):
        StorageCleanupRequest.model_validate({"cacheEntries": entries})


def test_cleanup_request_rejects_more_than_100000_entries() -> None:
    with pytest.raises(ValidationError):
        StorageCleanupRequest(cache_entries=[_SHA] * 100_001)


def test_skipped_entry_reasons_match_the_contract() -> None:
    item = V170.schemas["StorageCleanupResult"]["properties"]["skipped"]["items"]

    assert set(get_args(SkippedEntry.model_fields["reason"].annotation)) == set(
        item["properties"]["reason"]["enum"]
    )
    with pytest.raises(ValidationError):
        SkippedEntry.model_validate({"audioSha256": _SHA, "reason": "busy"})


def test_volume_ids_match_the_contract() -> None:
    enum = V170.schemas["VolumeUsage"]["properties"]["id"]["enum"]

    assert set(get_args(VolumeUsage.model_fields["id"].annotation)) == set(enum)


@pytest.mark.parametrize("folder", ["", "a/b", "..x/", "x" * 65, "with space"])
def test_model_cache_id_is_a_plain_folder_name(folder: str) -> None:
    with pytest.raises(ValidationError):
        ModelCacheUsage(id=folder, bytes=0, models=[])


def test_cache_entry_rejects_bad_hashes_and_job_ids() -> None:
    good = V170.example("WorkerStorageReport")["cacheEntries"][0]
    with pytest.raises(ValidationError):
        CacheEntryUsage.model_validate({**good, "audioSha256": "x" * 64})
    with pytest.raises(ValidationError):
        CacheEntryUsage.model_validate({**good, "jobIds": ["not-a-job-id"]})
    with pytest.raises(ValidationError):
        CacheEntryUsage.model_validate({**good, "standardBytes": -1})


def test_rehydrate_request_parses_the_track_id_as_uuid() -> None:
    request = RehydrateRequest.model_validate(_rehydrate())

    assert request.track_id == UUID(_TRACK)
    assert request.source_type is SourceType.YOUTUBE
    assert request.stems_match_model()


@pytest.mark.parametrize(
    "track_id", [_TRACK.upper(), _TRACK.replace("-", ""), "{" + _TRACK + "}", "../x", 5]
)
def test_rehydrate_request_requires_the_lower_case_d_format(track_id: object) -> None:
    with pytest.raises(ValidationError):
        RehydrateRequest.model_validate(_rehydrate(trackId=track_id))


@pytest.mark.parametrize(
    "stems",
    [["vocals", "drums", "bass"], [*_SIX, "vocals"], ["vocals", "vocals", "bass", "other"], []],
)
def test_rehydrate_request_needs_4_to_6_unique_stems(stems: list[str]) -> None:
    with pytest.raises(ValidationError):
        RehydrateRequest.model_validate(_rehydrate(stems=stems))
    assert V170.errors("RehydrateRequest", _rehydrate(stems=stems)) != []


def test_rehydrate_request_rejects_a_bad_source_key() -> None:
    with pytest.raises(ValidationError):
        RehydrateRequest.model_validate(_rehydrate(sourceKey="youtube:../../etc"))


@pytest.mark.parametrize(
    ("model", "stems", "expected"),
    [
        ("htdemucs_6s", _SIX, True),
        ("fake", list(reversed(_SIX)), True),
        ("htdemucs", ["other", "bass", "drums", "vocals"], True),
        ("htdemucs", ["vocals", "drums", "bass", "guitar"], False),
        ("htdemucs_6s", ["vocals", "drums", "bass", "other"], False),
    ],
)
def test_stems_must_equal_the_model_set_for_a_422(
    model: str, stems: list[str], expected: bool
) -> None:
    request = RehydrateRequest.model_validate(_rehydrate(model=model, stems=stems))

    assert request.stems_match_model() is expected


def test_model_stems_cover_every_model_as_documented() -> None:
    assert set(MODEL_STEMS) == set(get_args(ModelName))
    assert MODEL_STEMS["htdemucs"] == frozenset({"vocals", "drums", "bass", "other"})
    assert MODEL_STEMS["htdemucs_6s"] == MODEL_STEMS["fake"] == frozenset(_SIX)


def test_rehydrated_job_status_is_always_completed() -> None:
    example = V170.example("RehydratedJobCreated")
    with pytest.raises(ValidationError):
        RehydratedJob.model_validate({**example, "status": "running"})
    assert RehydratedJob.model_validate({k: v for k, v in example.items() if k != "status"})


def test_library_errors_are_worker_errors_with_readable_text() -> None:
    missing = StemMissingError("drums")

    assert isinstance(LibraryUnavailableError(), WorkerError)
    assert str(LibraryUnavailableError()) == LIBRARY_UNAVAILABLE_TEXT
    assert isinstance(missing, WorkerError)
    assert missing.stem == "drums"
    assert "drums" in str(missing)


def test_worker_storage_requires_every_field() -> None:
    errors = V170.errors("WorkerStorage", {"measuredAt": datetime.now(UTC).isoformat()})

    assert errors
    with pytest.raises(ValidationError):
        WorkerStorage.model_validate({"measuredAt": "2026-11-16T14:00:00Z"})


def test_rehydrate_request_accepts_a_uuid_object_in_python() -> None:
    request = RehydrateRequest(
        track_id=UUID(_TRACK),
        source_type=SourceType.UPLOAD,
        source_key="upload:abc",
        model="htdemucs",
        stems=["vocals", "drums", "bass", "other"],
    )

    assert request.model_dump(mode="json", by_alias=True)["trackId"] == _TRACK
