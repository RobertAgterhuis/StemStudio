"""T043: spotDL-backed metadata client and YouTube matcher, with spotDL replaced by fakes."""

import sys
import types
from dataclasses import dataclass
from typing import Any

import pytest

from stemstudio_worker.ingest import spotify
from stemstudio_worker.ingest.spotify import (
    SpotdlMetadataClient,
    SpotdlYouTubeMatcher,
    SpotifyTrack,
)
from stemstudio_worker.ports import SourceUnavailableError

TRACK_ID = "4cOdK2wGLETKBW3PvgPWqT"


@dataclass
class FakeSong:
    name: str = "Song Name"
    artist: str = "Main Artist"
    duration: int = 201


def _track(handle: object | None = None) -> SpotifyTrack:
    return SpotifyTrack(TRACK_ID, "Song Name", "Main Artist", 201.0, handle=handle)


def test_metadata_client_maps_spotdl_song_and_requests_canonical_url() -> None:
    requested: list[str] = []
    song = FakeSong()

    def loader(url: str) -> FakeSong:
        requested.append(url)
        return song

    track = SpotdlMetadataClient(loader).track(TRACK_ID)

    assert requested == [f"https://open.spotify.com/track/{TRACK_ID}"]
    assert track == SpotifyTrack(TRACK_ID, "Song Name", "Main Artist", 201.0)
    assert track.handle is song


def test_metadata_client_maps_empty_artist_to_none_and_truncates_title() -> None:
    track = SpotdlMetadataClient(lambda _: FakeSong(name="x" * 600, artist=" ")).track(TRACK_ID)

    assert track.artist is None
    assert len(track.title) == 500


@pytest.mark.parametrize("song", [FakeSong(name="  "), None])
def test_metadata_client_rejects_missing_title(song: FakeSong | None) -> None:
    with pytest.raises(SourceUnavailableError, match="could not be retrieved"):
        SpotdlMetadataClient(lambda _: song).track(TRACK_ID)


def test_metadata_client_hides_library_errors() -> None:
    def loader(url: str) -> FakeSong:
        raise RuntimeError("token=SECRET traceback details")

    with pytest.raises(SourceUnavailableError) as info:
        SpotdlMetadataClient(loader).track(TRACK_ID)

    assert "SECRET" not in str(info.value)
    assert info.value.__cause__ is None


def test_matcher_searches_with_the_spotdl_song_handle() -> None:
    song = FakeSong()
    seen: list[object] = []

    def search(handle: object) -> str | None:
        seen.append(handle)
        return "https://www.youtube.com/watch?v=abcdefghijk"

    url = SpotdlYouTubeMatcher(search).match(_track(handle=song))

    assert url == "https://www.youtube.com/watch?v=abcdefghijk"
    assert seen == [song]


def test_matcher_without_handle_returns_none() -> None:
    assert SpotdlYouTubeMatcher(lambda _: "unused").match(_track()) is None


def test_matcher_hides_search_errors() -> None:
    def search(handle: object) -> str | None:
        raise OSError("network down at 10.0.0.1")

    with pytest.raises(SourceUnavailableError) as info:
        SpotdlYouTubeMatcher(search).match(_track(handle=FakeSong()))

    assert "10.0.0.1" not in str(info.value)


class _FakeSpotifyError(Exception):
    pass


class _FakeSpotifyClient:
    init_calls: list[dict[str, Any]] = []
    initialised = False

    @classmethod
    def init(cls, **kwargs: Any) -> None:
        cls.init_calls.append(kwargs)
        if cls.initialised:
            raise _FakeSpotifyError("A spotify client has already been initialized")
        cls.initialised = True


class _FakeSongType:
    urls: list[str] = []

    @classmethod
    def from_url(cls, url: str) -> FakeSong:
        cls.urls.append(url)
        return FakeSong()


def _install(monkeypatch: pytest.MonkeyPatch, name: str, **attrs: Any) -> None:
    module = types.ModuleType(name)
    for key, value in attrs.items():
        setattr(module, key, value)
    monkeypatch.setitem(sys.modules, name, module)


@pytest.fixture
def fake_spotdl(monkeypatch: pytest.MonkeyPatch) -> None:
    _FakeSpotifyClient.init_calls = []
    _FakeSpotifyClient.initialised = False
    _FakeSongType.urls = []
    for package in ("spotdl", "spotdl.types", "spotdl.utils", "spotdl.providers"):
        _install(monkeypatch, package)
    _install(monkeypatch, "spotdl.types.song", Song=_FakeSongType)
    _install(
        monkeypatch,
        "spotdl.utils.spotify",
        SpotifyClient=_FakeSpotifyClient,
        SpotifyError=_FakeSpotifyError,
    )
    _install(
        monkeypatch,
        "spotdl.utils.config",
        DEFAULT_CONFIG={"client_id": "public-id", "client_secret": "public-app-value"},
    )


@pytest.mark.usefixtures("fake_spotdl")
def test_load_spotdl_song_initialises_client_once_from_spotdl_defaults() -> None:
    first = spotify.load_spotdl_song(f"https://open.spotify.com/track/{TRACK_ID}")
    spotify.load_spotdl_song(f"https://open.spotify.com/track/{TRACK_ID}")

    assert first == FakeSong()
    assert _FakeSongType.urls == [f"https://open.spotify.com/track/{TRACK_ID}"] * 2
    assert _FakeSpotifyClient.init_calls[0] == {
        "client_id": "public-id",
        "client_secret": "public-app-value",
    }


def _provider(url: str | None, calls: list[str]) -> type:
    class Provider:
        def search(self, song: object) -> str | None:
            calls.append(type(self).__name__)
            return url

    return Provider


@pytest.mark.usefixtures("fake_spotdl")
def test_search_prefers_youtube_music_then_falls_back_to_youtube(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[str] = []
    _install(
        monkeypatch,
        "spotdl.providers.audio",
        YouTubeMusic=_provider(None, calls),
        YouTube=_provider("https://www.youtube.com/watch?v=abcdefghijk", calls),
    )

    url = spotify.search_youtube_with_spotdl(FakeSong())

    assert url == "https://www.youtube.com/watch?v=abcdefghijk"
    assert len(calls) == 2


@pytest.mark.usefixtures("fake_spotdl")
def test_search_returns_first_hit_and_none_without_hits(monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[str] = []
    _install(
        monkeypatch,
        "spotdl.providers.audio",
        YouTubeMusic=_provider("https://music.youtube.com/watch?v=abcdefghijk", calls),
        YouTube=_provider(None, calls),
    )
    assert spotify.search_youtube_with_spotdl(FakeSong()) is not None
    assert len(calls) == 1

    _install(
        monkeypatch,
        "spotdl.providers.audio",
        YouTubeMusic=_provider(None, calls),
        YouTube=_provider(None, calls),
    )
    assert spotify.search_youtube_with_spotdl(FakeSong()) is None
