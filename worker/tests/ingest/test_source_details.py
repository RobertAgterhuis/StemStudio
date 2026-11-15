"""005:T022: ingest hands artwork candidates, album, ISRC and lookup names to the pipeline.

``ingest`` does not import ``metadata`` (plan.md dependency direction): the source details
reader (``metadata.source_artwork.SourceDetailsReader``) is injected through the
``ingest.details.SourceDetails`` protocol; these tests use a fake.
"""

import dataclasses
import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.ingest.details import NoSourceDetails, SourceDetails
from stemstudio_worker.ingest.fetcher import YtDlpFetcher
from stemstudio_worker.ingest.resolver import UrlSourceResolver
from stemstudio_worker.ingest.spotify import SpotdlMetadataClient, SpotifyTrack
from stemstudio_worker.ingest.upload import FileUploadIntake
from stemstudio_worker.ports import ArtworkCandidate, FetchedAudio, SourceInfo
from stemstudio_worker.schemas import SourceType

VIDEO = "dQw4w9WgXcQ"
TRACK_ID = "4cOdK2wGLETKBW3PvgPWqT"
COVER = ArtworkCandidate(source="youtube", url=f"https://i.ytimg.com/vi/{VIDEO}/hqdefault.jpg")
SPOTIFY_COVER = ArtworkCandidate(source="spotify", url="https://i.scdn.co/image/x")
EMBEDDED = ArtworkCandidate(source="embedded", data=b"jpeg")


@dataclass
class FakeDetails:
    """Records the calls and adds fixed values (or raises)."""

    error: Exception | None = None
    calls: list[tuple[str, object]] = field(default_factory=list)

    def youtube(self, fetched: FetchedAudio, info: dict[str, Any]) -> FetchedAudio:
        self._record("youtube", info)
        return dataclasses.replace(fetched, artwork=COVER, lookup_title="Clean")

    def spotify(self, source: SourceInfo, song: object) -> SourceInfo:
        self._record("spotify", song)
        return dataclasses.replace(source, artwork=SPOTIFY_COVER, lookup_title="Clean")

    def upload(self, source: SourceInfo, path: Path) -> SourceInfo:
        self._record("upload", path)
        return dataclasses.replace(source, artwork=EMBEDDED, lookup_title="Tag")

    def _record(self, name: str, value: object) -> None:
        self.calls.append((name, value))
        if self.error is not None:
            raise self.error


class OneShotYoutubeDL:
    """A ``YoutubeDL`` fake that writes one file and returns a fixed info dict."""

    def __init__(self, options: dict[str, Any], info: dict[str, Any]) -> None:
        self.options = options
        self.info = info

    def __enter__(self) -> "OneShotYoutubeDL":
        return self

    def __exit__(self, *exc: object) -> None:
        return None

    def extract_info(self, url: str, download: bool = True) -> dict[str, Any]:
        return self.info

    def process_ie_result(self, ie_result: dict[str, Any], download: bool = True) -> None:
        target = Path(self.options["outtmpl"]["default"]).parent
        (target / "source.webm").write_bytes(b"audio")


INFO: dict[str, Any] = {
    "id": VIDEO,
    "duration": 200,
    "title": "Band - Song (Official Video)",
    "uploader": "Fan",
    "album": "  The Album  ",
}


def _fetch(tmp_path: Path, details: SourceDetails | None, info: dict[str, Any]) -> FetchedAudio:
    fetcher = YtDlpFetcher(ydl_factory=lambda options: OneShotYoutubeDL(options, info), details=details)
    source = SourceInfo(
        SourceType.YOUTUBE, f"youtube:{VIDEO}", "t", None, f"https://www.youtube.com/watch?v={VIDEO}"
    )
    return fetcher.fetch(source, tmp_path, lambda fraction, stage: None)


def test_no_source_details_changes_nothing(tmp_path: Path) -> None:
    details: SourceDetails = NoSourceDetails()
    fetched = FetchedAudio(tmp_path / "a")
    source = SourceInfo(SourceType.UPLOAD, "upload:x", "t", None, None)

    assert details.youtube(fetched, {}) is fetched
    assert details.spotify(source, object()) is source
    assert details.upload(source, tmp_path) is source


def test_fetcher_reports_the_album_and_hands_the_info_to_the_details(tmp_path: Path) -> None:
    details = FakeDetails()

    fetched = _fetch(tmp_path, details, INFO)

    assert fetched.album == "The Album"
    assert (fetched.title, fetched.artist) == ("Band - Song (Official Video)", "Fan")
    assert (fetched.artwork, fetched.lookup_title) == (COVER, "Clean")
    assert details.calls == [("youtube", INFO)]


def test_fetcher_without_details_has_no_artwork(tmp_path: Path) -> None:
    fetched = _fetch(tmp_path, None, INFO)

    assert fetched.artwork is None
    assert fetched.lookup_title is None
    assert fetched.album == "The Album"


def test_fetcher_without_an_album(tmp_path: Path) -> None:
    info = {key: value for key, value in INFO.items() if key != "album"}

    assert _fetch(tmp_path, FakeDetails(), info).album is None


def test_failing_details_never_fail_the_download(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    caplog.set_level(logging.WARNING)

    fetched = _fetch(tmp_path, FakeDetails(error=RuntimeError("secret detail")), INFO)

    assert fetched.path == tmp_path / "source.webm"
    assert fetched.artwork is None
    assert fetched.album == "The Album"
    assert "RuntimeError" in caplog.text
    assert "secret detail" not in caplog.text


# ------------------------------------------------------------------ Spotify


@dataclass
class FakeSong:
    name: str = "Song"
    artist: str = "Artist"
    duration: int = 201
    album_name: object = "  Album  "
    isrc: object = "usev-i96-92306"


@pytest.mark.parametrize(
    ("isrc", "expected"),
    [
        ("USEVI9692306", "USEVI9692306"),
        ("usevi9692306", "USEVI9692306"),
        ("US-EVI-96-92306", "USEVI9692306"),
        ("USEVI969230", None),
        ("12EVI9692306", None),
        ("", None),
        (None, None),
        (42, None),
    ],
)
def test_metadata_client_reads_a_valid_isrc(isrc: object, expected: str | None) -> None:
    client = SpotdlMetadataClient(song_loader=lambda url: FakeSong(isrc=isrc))

    assert client.track(TRACK_ID).isrc == expected


@pytest.mark.parametrize(("album", "expected"), [("  Album  ", "Album"), ("", None), (None, None)])
def test_metadata_client_reads_the_album(album: object, expected: str | None) -> None:
    client = SpotdlMetadataClient(song_loader=lambda url: FakeSong(album_name=album))

    assert client.track(TRACK_ID).album == expected


def test_metadata_client_without_the_005_attributes() -> None:
    @dataclass
    class OldSong:
        name: str = "Song"
        artist: str = "Artist"
        duration: int = 201

    track = SpotdlMetadataClient(song_loader=lambda url: OldSong()).track(TRACK_ID)

    assert (track.album, track.isrc) == (None, None)


@dataclass
class FixedClient:
    song: object = field(default_factory=FakeSong)

    def track(self, track_id: str) -> SpotifyTrack:
        return SpotifyTrack(
            TRACK_ID, "Song", "Artist", 201.0, album="Album", isrc="USEVI9692306", handle=self.song
        )


class FixedMatcher:
    def match(self, track: SpotifyTrack) -> str | None:
        return f"https://www.youtube.com/watch?v={VIDEO}"


def test_resolver_hands_album_isrc_and_the_song_to_the_details() -> None:
    client = FixedClient()
    details = FakeDetails()
    resolver = UrlSourceResolver(client, FixedMatcher(), details)

    source = resolver.resolve(f"https://open.spotify.com/track/{TRACK_ID}")

    assert (source.album, source.isrc) == ("Album", "USEVI9692306")
    assert (source.artwork, source.lookup_title) == (SPOTIFY_COVER, "Clean")
    assert details.calls == [("spotify", client.song)]


def test_resolver_keeps_the_source_when_details_fail() -> None:
    resolver = UrlSourceResolver(FixedClient(), FixedMatcher(), FakeDetails(error=ValueError()))

    source = resolver.resolve(f"https://open.spotify.com/track/{TRACK_ID}")

    assert source.artwork is None
    assert source.isrc == "USEVI9692306"


def test_youtube_urls_are_resolved_without_details() -> None:
    details = FakeDetails()
    resolver = UrlSourceResolver(FixedClient(), FixedMatcher(), details)

    source = resolver.resolve(f"https://youtu.be/{VIDEO}")

    assert source.artwork is None
    assert details.calls == []


# ------------------------------------------------------------------ uploads


def test_upload_intake_hands_the_stored_file_to_the_details(tmp_path: Path) -> None:
    path = tmp_path / "input.mp3"
    path.write_bytes(b"ID3 bytes")
    details = FakeDetails()

    source = FileUploadIntake(details=details).accept("Song.mp3", path)

    assert (source.artwork, source.lookup_title, source.title) == (EMBEDDED, "Tag", "Song")
    assert source.source_key.startswith("upload:")
    assert details.calls == [("upload", path)]


def test_upload_intake_keeps_the_source_when_details_fail(tmp_path: Path) -> None:
    path = tmp_path / "input.flac"
    path.write_bytes(b"fLaC")

    source = FileUploadIntake(details=FakeDetails(error=OSError())).accept("Song.flac", path)

    assert source.artwork is None
    assert source.title == "Song"


def test_factory_injects_the_source_details() -> None:
    details = FakeDetails()
    adapters = create_ingest_adapters(WorkerSettings(data_dir=Path("unused")), details)

    assert isinstance(adapters.upload_intake, FileUploadIntake)
    assert adapters.upload_intake.details is details
    assert isinstance(adapters.fetcher, YtDlpFetcher)
    assert adapters.fetcher.details is details


def test_factory_defaults_to_no_source_details() -> None:
    adapters = create_ingest_adapters(WorkerSettings(data_dir=Path("unused")))

    assert isinstance(adapters.upload_intake, FileUploadIntake)
    assert isinstance(adapters.upload_intake.details, NoSourceDetails)
