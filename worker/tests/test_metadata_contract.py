"""Skeleton tests for the feature 005 contract code: worker API 1.3.0 additions, ports, settings.

The code is at 1.4.0 (features 005 and 007 share one Phase 0); the 1.3.0 document must be
contained unchanged in the 1.4.0 document except for ``/health`` (``Health`` gained the 007
fields), so implementing against either document gives the same result.
"""

from functools import cache
from pathlib import Path
from typing import Any, get_args

import pytest
import yaml
from jsonschema import Draft202012Validator
from pydantic import BaseModel, ValidationError

from stemstudio_worker.config import FakeOnlineMode, OnlineSettings, WorkerSettings, load_settings
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ArtworkError,
    ArtworkFetchError,
    ArtworkImages,
    FetchedAudio,
    ImageNormalizer,
    ImageTooLargeError,
    MetadataFindings,
    MetadataLookup,
    MetadataLookupError,
    MetadataQuery,
    NotAnImageError,
    ProgressCallback,
    SourceInfo,
    UnsupportedImageTypeError,
    WorkerError,
)
from stemstudio_worker.schemas import (
    METADATA_FAILED_TEXT,
    WIKIPEDIA_LICENCE_URL,
    ArtistType,
    ArtworkInfo,
    ArtworkSource,
    ArtworkUpload,
    ArtworkVariant,
    CreateMetadataJobRequest,
    Health,
    Job,
    JobResult,
    MetadataJob,
    MetadataJobState,
    MetadataOutcome,
    MetadataResult,
    SourceMetadata,
    SourceType,
    WikipediaSummary,
)

_SPECS = Path(__file__).resolve().parents[2] / "specs"
_SHA = "a" * 64


@cache
def _document(feature: str) -> dict[str, Any]:
    path = _SPECS / feature / "contracts" / "worker-api.openapi.yaml"
    loaded: dict[str, Any] = yaml.safe_load(path.read_text(encoding="utf-8"))
    return loaded


def _v130() -> dict[str, Any]:
    return _document("005-artwork-band-info")


def _schema_errors(name: str, instance: object) -> list[str]:
    components = _v130()["components"]
    validator = Draft202012Validator({**components["schemas"][name], "components": components})
    return [error.message for error in validator.iter_errors(instance)]


def _example(name: str) -> Any:
    return _v130()["components"]["examples"][name]["value"]


def test_document_versions() -> None:
    assert _v130()["info"]["version"] == "1.3.0"
    assert _document("007-export-library")["info"]["version"] == "1.4.0"


@pytest.mark.parametrize("section", ["paths", "schemas", "responses", "examples", "parameters"])
def test_1_4_0_contains_1_3_0_unchanged_except_health(section: str) -> None:
    def entries(document: dict[str, Any]) -> dict[str, Any]:
        holder = document if section == "paths" else document["components"]
        found: dict[str, Any] = holder[section]
        return found

    newer = entries(_document("007-export-library"))
    changed = [key for key, value in entries(_v130()).items() if newer.get(key) != value]

    assert changed in ([], ["/health"], ["Health"])


@pytest.mark.parametrize(
    ("alias", "schema"),
    [
        (ArtworkSource, "ArtworkSource"),
        (ArtworkVariant, "ArtworkVariant"),
        (MetadataOutcome, "MetadataOutcome"),
        (ArtistType, "ArtistType"),
    ],
)
def test_literals_match_the_contract(alias: object, schema: str) -> None:
    assert list(get_args(alias)) == _v130()["components"]["schemas"][schema]["enum"]


def test_metadata_job_states_match_the_contract() -> None:
    enum = _v130()["components"]["schemas"]["MetadataJobState"]["enum"]

    assert [state.value for state in MetadataJobState] == enum
    assert [state.is_terminal for state in MetadataJobState] == [False, False, True, True]


@pytest.mark.parametrize(
    ("example", "model"),
    [
        ("QueuedJob", Job),
        ("SeparatingJob", Job),
        ("CompletedJob", Job),
        ("FailedJob", Job),
        ("QueuedMetadataJob", MetadataJob),
        ("RunningMetadataJob", MetadataJob),
        ("CompletedMetadataJob", MetadataJob),
        ("FailedMetadataJob", MetadataJob),
        ("FoundMetadataResult", MetadataResult),
        ("NotFoundMetadataResult", MetadataResult),
        ("DisabledMetadataResult", MetadataResult),
    ],
)
def test_examples_round_trip(example: str, model: type[BaseModel]) -> None:
    value = _example(example)

    dumped = model.model_validate(value).model_dump(mode="json", by_alias=True)

    assert dumped == value
    assert _schema_errors(model.__name__, dumped) == []


def test_inline_examples_round_trip() -> None:
    paths = _v130()["paths"]
    result = paths["/v1/jobs/{jobId}/result"]["get"]["responses"]["200"]["content"]
    upload = paths["/v1/artwork"]["post"]["responses"]["201"]["content"]
    requests = paths["/v1/metadata-jobs"]["post"]["requestBody"]["content"]["application/json"]
    cases: list[tuple[type[BaseModel], Any]] = [
        (JobResult, result["application/json"]["example"]),
        (ArtworkUpload, upload["application/json"]["example"]),
        *[(CreateMetadataJobRequest, e["value"]) for e in requests["examples"].values()],
    ]

    for model, value in cases:
        assert model.model_validate(value).model_dump(mode="json", by_alias=True) == value


def test_completed_job_carries_the_source_artwork() -> None:
    job = Job.model_validate(_example("CompletedJob"))

    assert job.artwork is not None
    assert (job.artwork.source, job.metadata is not None) == ("youtube", True)


def test_jobs_and_metadata_stored_before_1_3_0_still_load() -> None:
    old_job = {k: v for k, v in _example("QueuedJob").items() if k != "artwork"}
    old_metadata = {"title": "t", "artist": None, "sourceUrl": None}

    assert Job.model_validate(old_job).artwork is None
    assert SourceMetadata.model_validate(old_metadata).lookup_title is None


def test_failed_metadata_job_uses_the_fixed_text() -> None:
    assert _example("FailedMetadataJob")["error"] == METADATA_FAILED_TEXT


@pytest.mark.parametrize(
    "field",
    [{"coverBytes": 204_801}, {"thumbBytes": 0}, {"sha256": "A" * 64}, {"source": "lastfm"}],
)
def test_artwork_info_limits(field: dict[str, object]) -> None:
    value = {"source": "user", "sha256": _SHA, "coverBytes": 1, "thumbBytes": 1, **field}

    with pytest.raises(ValidationError):
        ArtworkInfo.model_validate(value)


def test_source_metadata_validates_isrc_and_duration() -> None:
    with pytest.raises(ValidationError):
        SourceMetadata(title="t", isrc="usrc17607839")
    with pytest.raises(ValidationError):
        SourceMetadata(title="t", duration_seconds=0)
    assert SourceMetadata(title="t", isrc="USRC17607839").isrc == "USRC17607839"


def test_create_metadata_job_request_requires_a_title() -> None:
    with pytest.raises(ValidationError):
        CreateMetadataJobRequest.model_validate({"title": ""})
    with pytest.raises(ValidationError):
        CreateMetadataJobRequest.model_validate({"title": "t", "unknown": 1})


def test_wikipedia_summary_defaults_to_the_licence() -> None:
    summary = WikipediaSummary(
        language="en",
        title="T",
        extract="E",
        page_url="https://en.wikipedia.org/wiki/T",
        history_url="https://en.wikipedia.org/w/index.php?title=T&action=history",
        revision_id="1",
    )

    assert (summary.licence, summary.licence_url) == ("CC BY-SA 4.0", WIKIPEDIA_LICENCE_URL)
    assert _schema_errors("WikipediaSummary", summary.model_dump(by_alias=True)) == []


def test_health_reports_online_lookups() -> None:
    assert Health(separator="fake", device="cpu").online_lookups is True
    assert Health(separator="fake", device="cpu", online_lookups=False).online_lookups is False


# ------------------------------------------------------------------ ports


def test_artwork_candidate_needs_exactly_one_of_url_and_data() -> None:
    assert ArtworkCandidate("youtube", url="https://i.ytimg.com/vi/x/hq.jpg").data is None
    assert ArtworkCandidate("embedded", data=b"img").url is None
    with pytest.raises(ValueError, match="exactly one"):
        ArtworkCandidate("spotify")
    with pytest.raises(ValueError, match="exactly one"):
        ArtworkCandidate("spotify", url="https://i.scdn.co/image/x", data=b"img")


def test_artwork_images_describe_themselves() -> None:
    images = ArtworkImages(cover=b"c" * 10, thumb=b"t" * 3, sha256=_SHA)

    info = images.info("coverartarchive", "https://coverartarchive.org/x")

    assert info == ArtworkInfo(
        source="coverartarchive",
        sha256=_SHA,
        cover_bytes=10,
        thumb_bytes=3,
        origin_url="https://coverartarchive.org/x",
    )
    assert images.info("user").origin_url is None


def test_source_info_and_fetched_audio_default_the_005_fields() -> None:
    source = SourceInfo(SourceType.UPLOAD, "upload:x", "Song", None, None)
    fetched = FetchedAudio(Path("a.webm"))

    assert (source.album, source.isrc, source.artwork, source.lookup_title) == (None,) * 4
    assert (fetched.album, fetched.artwork, fetched.lookup_artist) == (None,) * 3


@pytest.mark.parametrize(
    "error",
    [ArtworkFetchError, NotAnImageError, UnsupportedImageTypeError, ImageTooLargeError],
)
def test_artwork_errors_are_worker_errors(error: type[ArtworkError]) -> None:
    assert issubclass(error, ArtworkError)
    assert issubclass(ArtworkError, WorkerError)
    assert issubclass(MetadataLookupError, WorkerError)


class _Normalizer:
    """Structural ``ImageNormalizer``."""

    def normalize(self, candidate: ArtworkCandidate) -> ArtworkImages:
        return ArtworkImages(cover=candidate.data or b"", thumb=b"t", sha256=_SHA)


class _Lookup:
    """Structural ``MetadataLookup`` that reports one stage."""

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        progress(1.0, "Searching MusicBrainz")
        return MetadataFindings(MetadataResult(outcome="not-found"))


def test_metadata_ports_are_structural() -> None:
    normalizer: ImageNormalizer = _Normalizer()
    lookup: MetadataLookup = _Lookup()
    stages: list[str] = []

    images = normalizer.normalize(ArtworkCandidate("embedded", data=b"cover"))
    found = lookup.lookup(MetadataQuery("Song", isrc=None), lambda _f, stage: stages.append(stage))

    assert (images.cover, found.result.outcome, found.artwork, stages) == (
        b"cover",
        "not-found",
        None,
        ["Searching MusicBrainz"],
    )


# ------------------------------------------------------------------ settings


def test_online_settings_defaults() -> None:
    online = load_settings({}).online

    assert online == OnlineSettings()
    assert online == WorkerSettings(data_dir=Path("d")).online
    assert (online.lookups_enabled, online.contact_url, online.wikipedia_languages) == (
        True,
        None,
        ("en",),
    )
    assert online.fake_mode == "found"


_ONLINE_ENV = {
    "STEMSTUDIO_ONLINE_LOOKUPS": "OFF",
    "STEMSTUDIO_CONTACT_URL": " https://example.org/stemstudio ",
    "STEMSTUDIO_WIKIPEDIA_LANGUAGES": "nl, EN,de",
    "STEMSTUDIO_FAKE_ONLINE": "not-found",
}


def test_online_settings_from_the_environment() -> None:
    online = load_settings(_ONLINE_ENV).online

    assert online == OnlineSettings(
        lookups_enabled=False,
        contact_url="https://example.org/stemstudio",
        wikipedia_languages=("nl", "en", "de"),
        fake_mode="not-found",
    )
    assert list(get_args(FakeOnlineMode)) == ["found", "not-found", "failed", "off"]


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("STEMSTUDIO_ONLINE_LOOKUPS", "maybe"),
        ("STEMSTUDIO_FAKE_ONLINE", "flaky"),
        ("STEMSTUDIO_WIKIPEDIA_LANGUAGES", ""),
        ("STEMSTUDIO_WIKIPEDIA_LANGUAGES", "en,nl,de,fr,es,it"),
        ("STEMSTUDIO_WIKIPEDIA_LANGUAGES", "english"),
    ],
)
def test_invalid_online_settings_are_rejected(name: str, value: str) -> None:
    with pytest.raises(ValueError, match=name):
        load_settings({name: value})


def test_an_empty_contact_url_is_unset() -> None:
    assert load_settings({"STEMSTUDIO_CONTACT_URL": "  "}).online.contact_url is None
