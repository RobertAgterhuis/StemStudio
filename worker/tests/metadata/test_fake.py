"""005:T034: the fake online provider matches ``tests/fixtures/metadata/fake-provider.json``.

The E2E specs (``band-info.spec.ts``, ``artwork.spec.ts``) read their expectations from that
file, so the fake must return exactly its values (qa 005:T009 request).
"""

import json
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.config import FakeOnlineMode, OnlineSettings, WorkerSettings
from stemstudio_worker.metadata import create_image_normalizer, create_metadata_lookup
from stemstudio_worker.metadata.fake import (
    FAKE_SOURCE_COLOUR,
    FakeImageNormalizer,
    FakeMetadataLookup,
)
from stemstudio_worker.metadata.images import PillowImageNormalizer
from stemstudio_worker.metadata.lookup import OnlineMetadataLookup
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ImageNormalizer,
    MetadataLookup,
    MetadataLookupError,
    MetadataQuery,
)
from stemstudio_worker.schemas import MetadataResult

from .images import close_to, pixel, solid

PROVIDER: dict[str, Any] = json.loads(
    (
        Path(__file__).resolve().parents[3]
        / "tests"
        / "fixtures"
        / "metadata"
        / "fake-provider.json"
    ).read_text(encoding="utf-8")
)
FOUND: dict[str, Any] = PROVIDER["found"]
QUERY = MetadataQuery(title="My Imported Song", artist="Whoever", duration_seconds=200.0)
MODES: list[FakeOnlineMode] = ["found", "not-found", "failed", "off"]


def _run(mode: FakeOnlineMode, query: MetadataQuery = QUERY) -> Any:
    return FakeMetadataLookup(mode).lookup(query, lambda fraction, stage: None)


def test_the_file_lists_every_mode() -> None:
    assert sorted(PROVIDER["modes"]) == sorted(MODES)


@pytest.mark.parametrize("mode", [m for m in MODES if m != "failed"])
def test_outcome_artwork_and_warnings_per_mode(mode: FakeOnlineMode) -> None:
    expected = PROVIDER["modes"][mode]

    findings = _run(mode)

    result = findings.result
    assert result.outcome == expected["outcome"]
    assert result.warnings == expected["warnings"]
    assert (result.artwork.source if result.artwork else None) == expected["artworkSource"]
    assert (findings.artwork is None) == (expected["artworkSource"] is None)
    MetadataResult.model_validate_json(result.model_dump_json(by_alias=True))


def test_failed_mode_fails_the_job_with_the_listed_error() -> None:
    with pytest.raises(MetadataLookupError) as raised:
        _run("failed")

    assert str(raised.value) == PROVIDER["modes"]["failed"]["error"]


def test_found_artwork_has_the_listed_colour() -> None:
    findings = _run("found")

    assert findings.artwork is not None
    colour = tuple(PROVIDER["modes"]["found"]["artworkColour"])
    assert close_to(pixel(findings.artwork.cover, (300, 300)), colour, tolerance=6)
    assert findings.result.artwork == findings.artwork.info("coverartarchive", None)


def test_found_recording_album_and_year() -> None:
    result = _run("found").result

    assert result.recording is not None
    assert result.recording.title == QUERY.title
    assert result.release_group is not None
    album, year = result.release_group.title, result.release_group.first_release_year
    assert (album, year) == (FOUND["recording"]["album"], FOUND["recording"]["year"])
    assert f"{album} ({year})" == FOUND["mixerAlbumText"]


def test_found_artist_is_exactly_the_listed_artist() -> None:
    artist = _run("found").result.artist

    assert artist is not None
    dumped = artist.model_dump(by_alias=True, mode="json")
    expected = FOUND["artist"]
    assert {key: dumped[key] for key in expected if key != "summary"} == {
        key: value for key, value in expected.items() if key != "summary"
    }
    summary = dumped["summary"]
    assert {key: summary[key] for key in expected["summary"]} == expected["summary"]


def test_found_result_names_every_listed_source() -> None:
    result = _run("found").result

    present = {
        "MusicBrainz": result.recording is not None,
        "Wikidata": result.artist is not None and result.artist.wikidata_id is not None,
        "Wikipedia": result.artist is not None and result.artist.summary is not None,
        "Cover Art Archive": result.artwork is not None,
    }
    assert sorted(name for name, ok in present.items() if ok) == sorted(FOUND["sources"])


def test_corrected_lookup_keeps_the_artist_and_follows_the_title() -> None:
    corrected = FOUND["correctedLookup"]
    query = MetadataQuery(title=corrected["title"], artist=corrected["artist"])

    result = _run("found", query).result

    assert result.recording is not None
    assert result.recording.title == corrected["title"]
    assert result.artist == _run("found").result.artist


def test_recording_ids_are_stable_per_title() -> None:
    first, second = _run("found").result.recording, _run("found").result.recording
    other = _run("found", MetadataQuery(title="Another Song")).result.recording

    assert first is not None
    assert second is not None
    assert other is not None
    assert first.mbid == second.mbid != other.mbid


def test_fake_source_artwork_is_generated_for_url_candidates() -> None:
    normalizer = FakeImageNormalizer()

    images = normalizer.normalize(
        ArtworkCandidate(source="youtube", url="https://i.ytimg.com/vi/x/hq.jpg")
    )

    assert close_to(pixel(images.cover, (300, 300)), FAKE_SOURCE_COLOUR, tolerance=6)


def test_fake_normalizer_re_encodes_embedded_pictures() -> None:
    images = FakeImageNormalizer().normalize(
        ArtworkCandidate(source="embedded", data=solid((64, 64), (10, 200, 10)))
    )

    assert close_to(pixel(images.cover, (300, 300)), (10, 200, 10))


def _settings(separator: str, **online: Any) -> WorkerSettings:
    return WorkerSettings(
        data_dir=Path("unused"), separator=separator, online=OnlineSettings(**online)
    )  # type: ignore[arg-type]


def test_factories_build_the_fakes_on_the_fake_stack() -> None:
    lookup: MetadataLookup = create_metadata_lookup(_settings("fake", fake_mode="not-found"))
    normalizer: ImageNormalizer = create_image_normalizer(_settings("fake"))

    assert isinstance(lookup, FakeMetadataLookup)
    assert lookup.mode == "not-found"
    assert isinstance(normalizer, FakeImageNormalizer)


def test_lookups_off_win_over_the_fake_mode() -> None:
    lookup = create_metadata_lookup(_settings("fake", fake_mode="found", lookups_enabled=False))

    assert isinstance(lookup, FakeMetadataLookup)
    assert lookup.mode == "off"


def test_factories_build_the_online_adapters_with_one_shared_client() -> None:
    settings = _settings("demucs", contact_url="https://stemstudio.example/")

    lookup = create_metadata_lookup(settings)
    normalizer = create_image_normalizer(settings)

    assert isinstance(lookup, OnlineMetadataLookup)
    assert isinstance(normalizer, PillowImageNormalizer)
    assert lookup.client is normalizer.client
    assert create_image_normalizer(settings).client is normalizer.client
