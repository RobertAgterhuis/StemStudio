"""T043: URL validation, source keys and Spotify resolution against the binding vectors."""

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.ingest.resolver import UrlSourceResolver
from stemstudio_worker.ingest.spotify import NO_MATCH_MESSAGE, SpotifyTrack
from stemstudio_worker.ingest.urls import parse_source_url
from stemstudio_worker.ports import InvalidSourceError, SourceInfo, SourceUnavailableError
from stemstudio_worker.schemas import SourceType

VECTORS_PATH = (
    Path(__file__).resolve().parents[3]
    / "specs"
    / "001-stemstudio-mvp"
    / "contracts"
    / "source-url-vectors.json"
)
VECTORS: dict[str, Any] = json.loads(VECTORS_PATH.read_text(encoding="utf-8"))
ACCEPTED: list[dict[str, str]] = VECTORS["accepted"]
REJECTED: list[dict[str, str]] = VECTORS["rejected"]

TRACK_ID = "4cOdK2wGLETKBW3PvgPWqT"
MATCH_URL = "https://music.youtube.com/watch?v=abcdefghijk&feature=share"


@dataclass
class FakeMetadataClient:
    """Records looked-up ids and returns a fixed track (or raises)."""

    track_to_return: SpotifyTrack | None = None
    error: Exception | None = None
    calls: list[str] = field(default_factory=list)

    def track(self, track_id: str) -> SpotifyTrack:
        self.calls.append(track_id)
        if self.error is not None:
            raise self.error
        assert self.track_to_return is not None
        return self.track_to_return


@dataclass
class FakeMatcher:
    """Returns a fixed YouTube URL (or None) and records the tracks it was asked for."""

    url: str | None = MATCH_URL
    calls: list[SpotifyTrack] = field(default_factory=list)

    def match(self, track: SpotifyTrack) -> str | None:
        self.calls.append(track)
        return self.url


def _spotify_track() -> SpotifyTrack:
    return SpotifyTrack(
        track_id=TRACK_ID,
        title="Spotify Title",
        artist="Spotify Artist",
        duration_seconds=212.0,
    )


def _resolver(
    client: FakeMetadataClient | None = None, matcher: FakeMatcher | None = None
) -> UrlSourceResolver:
    return UrlSourceResolver(
        client or FakeMetadataClient(track_to_return=_spotify_track()), matcher or FakeMatcher()
    )


def test_vectors_file_is_the_binding_version() -> None:
    assert VECTORS["version"] == "1.0.1"
    assert len(ACCEPTED) == 16
    assert len(REJECTED) == 22


@pytest.mark.parametrize("vector", ACCEPTED, ids=[v["url"] for v in ACCEPTED])
def test_parse_accepts_vector_with_expected_key_and_canonical_url(vector: dict[str, str]) -> None:
    parsed = parse_source_url(vector["url"])

    assert parsed.source_type == SourceType(vector["sourceType"])
    assert parsed.source_key == vector["sourceKey"]
    assert parsed.canonical_url == vector["canonicalUrl"]


@pytest.mark.parametrize("vector", REJECTED, ids=[v["url"] or "<empty>" for v in REJECTED])
def test_parse_rejects_vector_with_readable_message(vector: dict[str, str]) -> None:
    with pytest.raises(InvalidSourceError) as info:
        parse_source_url(vector["url"])

    message = str(info.value)
    assert message
    assert message[0].isupper()
    assert message.endswith(".")


@pytest.mark.parametrize(
    "url",
    [
        "https://user@www.youtube.com/watch?v=dQw4w9WgXcQ",
        "https://www.youtube.com:8443/watch?v=dQw4w9WgXcQ",
        "https://www.youtube.com:notaport/watch?v=dQw4w9WgXcQ",
        "https://open.spotify.com/intl-nl",
        "https://open.spotify.com/track",
        "https://open.spotify.com/user/someone",
        "https://www.youtube.com/shorts/",
        "https://youtu.be/dQw4w9WgXcQ/extra",
        "https://www.youtube.com/watch?v=dQw4w9WgXcQ!",
        "https://www.youtube.com/" + "a" * 2100,
    ],
)
def test_parse_rejects_additional_unsafe_or_malformed_forms(url: str) -> None:
    with pytest.raises(InvalidSourceError):
        parse_source_url(url)


CONTROL_CHAR_URLS = [
    "https://youtu.be/dQw4w9WgXcQ\n",
    "https://youtu.be/dQw4w9WgXcQ\r\n",
    "https://www.youtube.com/watch?v=dQw4w9WgXcQ\n",
    "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT\n",
    "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT\r\n",
    "\nhttps://youtu.be/dQw4w9WgXcQ",
    "https://youtu.be/dQw4w\nWgXcQ",
    "https://you\ttu.be/dQw4w9WgXcQ",
    "https://www.youtube.com/watch?v=dQw4w9\rWgXcQ",
    "https://open.spotify.com/track/4cOdK2wGLETKB\x00W3PvgPWqT",
    "https://youtu.be/dQw4w9WgXcQ\x7f",
    "https://www.youtube.com/watch?v=dQw4w9WgXcQ%0A",
    "https://www.youtube.com/watch?v=dQw4w9WgXc%0A",
    "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT%0A",
]


@pytest.mark.parametrize("url", CONTROL_CHAR_URLS, ids=[repr(u) for u in CONTROL_CHAR_URLS])
def test_parse_rejects_trailing_newlines_and_control_characters(url: str) -> None:
    with pytest.raises(InvalidSourceError):
        parse_source_url(url)


@pytest.mark.parametrize(
    ("url", "fragment"),
    [
        ("https://www.youtube.com/playlist?list=PL1", "Playlists"),
        ("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", "albums"),
        ("https://open.spotify.com/artist/0gxyHStUsqpMadRV0Di1Qt", "artists"),
        ("https://open.spotify.com/episode/512ojhOuo1ktJprKbVcKyQ", "episodes"),
        ("https://open.spotify.com/show/5CfCWKI5pZ28U0uOzXkDHe", "shows"),
        ("https://vimeo.com/76979871", "YouTube and Spotify"),
    ],
)
def test_rejection_messages_are_actionable(url: str, fragment: str) -> None:
    with pytest.raises(InvalidSourceError, match=fragment):
        parse_source_url(url)


YOUTUBE_ACCEPTED = [v for v in ACCEPTED if v["sourceType"] == "youtube"]
SPOTIFY_ACCEPTED = [v for v in ACCEPTED if v["sourceType"] == "spotify"]


def _url_ids(vectors: list[dict[str, str]]) -> list[str]:
    return [v["url"] for v in vectors]


def _resolve_with_fakes(url: str) -> tuple[SourceInfo, FakeMetadataClient, FakeMatcher]:
    client = FakeMetadataClient(track_to_return=_spotify_track())
    matcher = FakeMatcher()
    return _resolver(client, matcher).resolve(url), client, matcher


@pytest.mark.parametrize("vector", YOUTUBE_ACCEPTED, ids=_url_ids(YOUTUBE_ACCEPTED))
def test_resolve_youtube_needs_no_spotify_lookup(vector: dict[str, str]) -> None:
    info, client, matcher = _resolve_with_fakes(vector["url"])

    assert info.source_type is SourceType.YOUTUBE
    assert info.source_key == vector["sourceKey"]
    assert info.youtube_url == vector["canonicalUrl"]
    assert info.title
    assert info.artist is None
    assert client.calls == []
    assert matcher.calls == []


@pytest.mark.parametrize("vector", SPOTIFY_ACCEPTED, ids=_url_ids(SPOTIFY_ACCEPTED))
def test_resolve_spotify_uses_spotify_metadata_and_youtube_match(vector: dict[str, str]) -> None:
    info, client, matcher = _resolve_with_fakes(vector["url"])

    assert info.source_type is SourceType.SPOTIFY
    assert info.source_key == vector["sourceKey"]
    assert info.title == "Spotify Title"
    assert info.artist == "Spotify Artist"
    assert info.youtube_url == "https://www.youtube.com/watch?v=abcdefghijk"
    assert client.calls == [TRACK_ID]
    assert matcher.calls == [_spotify_track()]


@pytest.mark.parametrize("vector", REJECTED, ids=[v["url"] or "<empty>" for v in REJECTED])
def test_resolve_rejects_vector_without_any_lookup(vector: dict[str, str]) -> None:
    client = FakeMetadataClient(track_to_return=_spotify_track())

    with pytest.raises(InvalidSourceError):
        _resolver(client).resolve(vector["url"])

    assert client.calls == []


def test_resolve_spotify_without_match_fails_with_fixed_text() -> None:
    resolver = _resolver(matcher=FakeMatcher(url=None))

    with pytest.raises(SourceUnavailableError) as info:
        resolver.resolve(f"https://open.spotify.com/track/{TRACK_ID}")

    assert str(info.value) == "No matching YouTube audio was found for this Spotify track."
    assert str(info.value) == NO_MATCH_MESSAGE


@pytest.mark.parametrize(
    "bad_match",
    [
        "https://evil.example/watch?v=abcdefghijk",
        "https://www.youtube.com/playlist?list=PL1",
        f"https://open.spotify.com/track/{TRACK_ID}",
    ],
)
def test_resolve_spotify_treats_non_youtube_match_as_no_match(bad_match: str) -> None:
    resolver = _resolver(matcher=FakeMatcher(url=bad_match))

    with pytest.raises(SourceUnavailableError, match="No matching YouTube audio"):
        resolver.resolve(f"https://open.spotify.com/track/{TRACK_ID}")


def test_resolve_spotify_metadata_failure_propagates_as_unavailable() -> None:
    error = SourceUnavailableError("The Spotify track details could not be retrieved.")
    resolver = _resolver(client=FakeMetadataClient(error=error))

    with pytest.raises(SourceUnavailableError, match="could not be retrieved"):
        resolver.resolve(f"https://open.spotify.com/track/{TRACK_ID}")
