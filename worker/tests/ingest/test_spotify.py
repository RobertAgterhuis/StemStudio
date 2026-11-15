"""T043: spotDL-backed metadata client and YouTube matcher, with spotDL replaced by fakes."""

import sys
import types
from dataclasses import dataclass
from typing import Any, ClassVar

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
    init_calls: ClassVar[list[dict[str, Any]]] = []
    initialised: ClassVar[bool] = False

    @classmethod
    def init(cls, **kwargs: Any) -> None:
        cls.init_calls.append(kwargs)
        if cls.initialised:
            raise _FakeSpotifyError("A spotify client has already been initialized")
        cls.initialised = True


class _FakeSongType:
    urls: ClassVar[list[str]] = []

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
    monkeypatch.setattr(spotify, "_client_ready", False)
    for package in ("spotdl", "spotdl.types", "spotdl.utils", "spotdl.providers"):
        _install(monkeypatch, package)
    _install(monkeypatch, "spotdl.types.song", Song=_FakeSongType)
    spotify_module = {"SpotifyClient": _FakeSpotifyClient, "SpotifyError": _FakeSpotifyError}
    _install(monkeypatch, "spotdl.utils.spotify", **spotify_module)
    config = {"client_id": "public-id", "client_secret": "public-app-value"}
    _install(monkeypatch, "spotdl.utils.config", DEFAULT_CONFIG=config)


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


@pytest.mark.usefixtures("fake_spotdl")
def test_load_spotdl_song_tolerates_a_client_initialised_elsewhere() -> None:
    _FakeSpotifyClient.initialised = True

    assert spotify.load_spotdl_song("https://open.spotify.com/track/x") == FakeSong()
    assert len(_FakeSpotifyClient.init_calls) == 1


def _fake_youtube_dl(result: dict[str, Any] | None, seen: list[Any]) -> type:
    class FakeYoutubeDL:
        def __init__(self, options: dict[str, Any]) -> None:
            seen.append(options)

        def __enter__(self) -> "FakeYoutubeDL":
            return self

        def __exit__(self, *exc: object) -> None:
            return None

        def extract_info(self, query: str, download: bool) -> dict[str, Any] | None:
            seen.append((query, download))
            return result

    return FakeYoutubeDL


def test_search_runs_one_flat_yt_dlp_search_and_picks_a_match(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    seen: list[Any] = []
    entries = [{"id": "abcdefghijk", "duration": 199.0}]
    _install(monkeypatch, "yt_dlp", YoutubeDL=_fake_youtube_dl({"entries": entries}, seen))

    url = spotify.search_youtube_with_spotdl(FakeSong())

    assert url == "https://www.youtube.com/watch?v=abcdefghijk"
    assert seen[0]["extract_flat"] == "in_playlist"
    assert seen[1] == ("ytsearch5:Main Artist - Song Name", False)


@pytest.mark.parametrize("result", [None, {}, {"entries": None}, {"entries": []}])
def test_search_without_results_returns_none(
    monkeypatch: pytest.MonkeyPatch, result: dict[str, Any] | None
) -> None:
    _install(monkeypatch, "yt_dlp", YoutubeDL=_fake_youtube_dl(result, []))

    assert spotify.search_youtube_with_spotdl(FakeSong()) is None


def test_pick_match_skips_entries_with_implausible_duration_or_no_id() -> None:
    entries: list[dict[str, Any]] = [
        {"id": "longversion", "duration": 3750},
        {"id": None, "duration": 200},
        {"id": "rightlength", "duration": 190},
    ]

    assert spotify.pick_match(entries, 201.0) == "https://www.youtube.com/watch?v=rightlength"


def test_pick_match_tolerance_is_ten_percent_for_long_tracks() -> None:
    assert spotify.pick_match([{"id": "a", "duration": 540}], 600.0) is not None
    assert spotify.pick_match([{"id": "a", "duration": 539}], 600.0) is None


def test_pick_match_with_unknown_duration_takes_the_first_entry() -> None:
    entries: list[dict[str, Any]] = [{"id": "first"}, {"id": "second", "duration": 10}]

    assert spotify.pick_match(entries, 0.0) == "https://www.youtube.com/watch?v=first"


def test_pick_match_returns_none_when_nothing_qualifies() -> None:
    assert spotify.pick_match([{"id": "a", "duration": 100}], 201.0) is None
