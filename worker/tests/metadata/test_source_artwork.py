"""005:T022: pictures and lookup names offered by the import source (FR-001, research R-02)."""

from dataclasses import dataclass
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.metadata.source_artwork import (
    EmbeddedTags,
    SourceDetailsReader,
    read_embedded,
    spotify_cover,
    youtube_thumbnail,
)
from stemstudio_worker.ports import ArtworkCandidate, FetchedAudio, SourceInfo
from stemstudio_worker.schemas import SourceType

from .images import solid
from .tagged import OTHER_PICTURE, write_flac, write_mp3, write_wav

VIDEO = "dQw4w9WgXcQ"


def _thumb(name: str, width: int | None = None, host: str = "i.ytimg.com") -> dict[str, Any]:
    entry: dict[str, Any] = {"url": f"https://{host}/vi/{VIDEO}/{name}.jpg", "id": name}
    if width is not None:
        entry["width"] = width
        entry["height"] = width * 9 // 16
    return entry


@dataclass
class StubSong:
    """The slice of spotDL's ``Song`` that is read."""

    name: str = "Monkeys Spinning Monkeys"
    artist: str = "Kevin MacLeod"
    duration: float = 125.0
    album_name: str | None = "25 Years (Volume 2)"
    isrc: str | None = "USUAN1400011"
    cover_url: str | None = "https://i.scdn.co/image/ab67616d0000b273abcdef"


# ------------------------------------------------------------------ YouTube


def test_maxres_thumbnail_is_preferred() -> None:
    info = {
        "thumbnails": [
            _thumb("hqdefault", 480),
            _thumb("maxresdefault", 1280),
            _thumb("sddefault", 640),
        ]
    }

    candidate = youtube_thumbnail(info)

    assert candidate == ArtworkCandidate(
        source="youtube", url=f"https://i.ytimg.com/vi/{VIDEO}/maxresdefault.jpg"
    )


def test_sd_beats_hq_and_known_names_need_no_width() -> None:
    info = {"thumbnails": [_thumb("hqdefault"), _thumb("sddefault"), _thumb("default", 120)]}

    candidate = youtube_thumbnail(info)

    assert candidate is not None
    assert candidate.url is not None
    assert candidate.url.endswith("/sddefault.jpg")


def test_largest_other_thumbnail_of_at_least_300_px() -> None:
    info = {"thumbnails": [_thumb("a", 200), _thumb("b", 336), _thumb("c", 310)]}

    candidate = youtube_thumbnail(info)

    assert candidate is not None
    assert candidate.url is not None
    assert candidate.url.endswith("/b.jpg")


@pytest.mark.parametrize(
    "thumbnails",
    [
        [],
        [_thumb("default", 120), _thumb("mqdefault", 299)],
        [_thumb("maxresdefault", 1280, host="example.org")],
        [{"url": f"http://i.ytimg.com/vi/{VIDEO}/maxresdefault.jpg", "width": 1280}],
        [{"width": 1280}, {"url": 42, "width": 1280}, "not a dict"],
    ],
    ids=["none", "too-small", "other-host", "http", "malformed"],
)
def test_no_usable_thumbnail(thumbnails: list[Any]) -> None:
    assert youtube_thumbnail({"thumbnails": thumbnails}) is None


def test_single_thumbnail_field_is_a_fallback() -> None:
    info = {"thumbnail": f"https://i.ytimg.com/vi/{VIDEO}/hqdefault.jpg"}

    candidate = youtube_thumbnail(info)

    assert candidate is not None
    assert candidate.source == "youtube"


@pytest.mark.parametrize("info", [{}, {"thumbnails": None}, {"thumbnails": "x"}])
def test_missing_thumbnails(info: dict[str, Any]) -> None:
    assert youtube_thumbnail(info) is None


# ------------------------------------------------------------------ Spotify


def test_spotify_cover_url() -> None:
    assert spotify_cover(StubSong()) == ArtworkCandidate(
        source="spotify", url="https://i.scdn.co/image/ab67616d0000b273abcdef"
    )


@pytest.mark.parametrize(
    "cover_url",
    [None, "", "https://example.org/x.jpg", "http://i.scdn.co/image/x", 42],
)
def test_spotify_without_a_usable_cover(cover_url: object) -> None:
    song = StubSong()
    song.cover_url = cover_url  # type: ignore[assignment]

    assert spotify_cover(song) is None


def test_spotify_cover_of_an_object_without_the_attribute() -> None:
    assert spotify_cover(object()) is None


# ------------------------------------------------------------------ uploads read with tinytag


def test_mp3_front_cover_and_tags(tmp_path: Path) -> None:
    cover = solid((300, 300))
    path = write_mp3(
        tmp_path / "a.mp3", {"title": "Hero", "artist": "Band", "album": "Load"}, cover
    )

    tags = read_embedded(path)

    assert tags == EmbeddedTags(
        artwork=ArtworkCandidate(source="embedded", data=cover),
        title="Hero",
        artist="Band",
        album="Load",
    )


def test_flac_front_cover_and_tags(tmp_path: Path) -> None:
    cover = solid((64, 64), image_format="PNG")
    path = write_flac(tmp_path / "a.flac", {"title": "Hero", "artist": "Band"}, cover)

    tags = read_embedded(path)

    assert tags.artwork == ArtworkCandidate(source="embedded", data=cover)
    assert (tags.title, tags.artist, tags.album) == ("Hero", "Band", None)


def test_any_picture_is_used_without_a_front_cover(tmp_path: Path) -> None:
    picture = solid((64, 64))
    path = write_mp3(tmp_path / "a.mp3", {"title": "Hero"}, picture, kind=OTHER_PICTURE)

    assert read_embedded(path).artwork == ArtworkCandidate(source="embedded", data=picture)


def test_tags_without_a_picture(tmp_path: Path) -> None:
    path = write_flac(tmp_path / "a.flac", {"title": "  Hero  ", "artist": " "})

    tags = read_embedded(path)

    assert tags == EmbeddedTags(title="Hero")


def test_wav_has_no_candidate(tmp_path: Path) -> None:
    assert read_embedded(write_wav(tmp_path / "a.wav")) == EmbeddedTags()


@pytest.mark.parametrize("content", [b"", b"garbage that is not audio", b"fLaC\x00"])
def test_unreadable_files_give_empty_tags(tmp_path: Path, content: bytes) -> None:
    path = tmp_path / "a.flac"
    path.write_bytes(content)

    assert read_embedded(path) == EmbeddedTags()


def test_missing_file_gives_empty_tags(tmp_path: Path) -> None:
    assert read_embedded(tmp_path / "missing.mp3") == EmbeddedTags()


# ------------------------------------------------------------------ SourceDetailsReader


def _youtube_source() -> SourceInfo:
    return SourceInfo(
        source_type=SourceType.YOUTUBE,
        source_key=f"youtube:{VIDEO}",
        title=f"YouTube video {VIDEO}",
        artist=None,
        youtube_url=f"https://www.youtube.com/watch?v={VIDEO}",
    )


def test_reader_adds_the_youtube_thumbnail_and_cleaned_names(tmp_path: Path) -> None:
    fetched = FetchedAudio(tmp_path / "source.webm", "Metallica - Hero (Official Video)", "Fan")
    info = {"title": fetched.title, "uploader": "Fan", "thumbnails": [_thumb("hqdefault")]}

    result = SourceDetailsReader().youtube(fetched, info)

    assert result.path == fetched.path
    assert (result.title, result.artist) == (fetched.title, fetched.artist)
    assert (result.lookup_title, result.lookup_artist) == ("Hero", "Metallica")
    assert result.artwork is not None
    assert result.artwork.source == "youtube"


def test_reader_adds_the_spotify_cover_and_lookup_names() -> None:
    source = SourceInfo(
        source_type=SourceType.SPOTIFY,
        source_key="spotify:6rqhFgbbKwnb9MLmUQDhG6",
        title="Hero Of The Day - Remastered",
        artist="Metallica",
        youtube_url=f"https://www.youtube.com/watch?v={VIDEO}",
        album="Load",
        isrc="USEVI9692306",
    )

    result = SourceDetailsReader().spotify(source, StubSong())

    assert result.artwork == spotify_cover(StubSong())
    assert (result.lookup_title, result.lookup_artist) == ("Hero Of The Day", "Metallica")
    assert (result.album, result.isrc, result.title) == ("Load", "USEVI9692306", source.title)


def _upload_source() -> SourceInfo:
    return SourceInfo(
        source_type=SourceType.UPLOAD,
        source_key="upload:" + "a" * 64,
        title="01 my file",
        artist=None,
        youtube_url=None,
    )


def test_reader_adds_embedded_cover_and_tag_names(tmp_path: Path) -> None:
    cover = solid((64, 64))
    path = write_mp3(tmp_path / "x", {"title": "Hero", "artist": "Band", "album": "Load"}, cover)

    result = SourceDetailsReader().upload(_upload_source(), path)

    assert result.artwork == ArtworkCandidate(source="embedded", data=cover)
    assert (result.lookup_title, result.lookup_artist, result.album) == ("Hero", "Band", "Load")
    assert (result.title, result.artist) == ("01 my file", None)


def test_reader_falls_back_to_the_upload_title_without_tags(tmp_path: Path) -> None:
    result = SourceDetailsReader().upload(_upload_source(), write_wav(tmp_path / "x.wav"))

    assert result.artwork is None
    assert (result.lookup_title, result.lookup_artist, result.album) == ("01 my file", None, None)
