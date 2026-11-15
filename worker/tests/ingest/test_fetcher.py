"""T045: yt-dlp fetcher with a fake ``YoutubeDL`` factory (no network)."""

import sys
import types
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Self

import pytest

from stemstudio_worker.ingest import fetcher as fetcher_module
from stemstudio_worker.ingest.fetcher import DOWNLOAD_STAGE, YtDlpFetcher
from stemstudio_worker.ports import (
    AudioTooLongError,
    FetchedAudio,
    InvalidSourceError,
    SourceInfo,
    SourceUnavailableError,
)
from stemstudio_worker.schemas import SourceType

VIDEO_URL = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
Hook = Callable[[dict[str, Any]], None]


@dataclass
class FakeYoutubeDL:
    """Mimics the slice of ``yt_dlp.YoutubeDL`` the fetcher uses."""

    options: dict[str, Any]
    info: dict[str, Any] | None = field(default_factory=lambda: {"id": "x", "duration": 212})
    hook_events: list[dict[str, Any]] = field(default_factory=list)
    file_names: tuple[str, ...] = ("source.webm",)
    download_error: Exception | None = None
    extracted: list[tuple[str, bool]] = field(default_factory=list)
    processed: int = 0
    closed: bool = False

    def __enter__(self) -> Self:
        return self

    def __exit__(self, *exc: object) -> None:
        self.closed = True

    def extract_info(self, url: str, download: bool = True) -> dict[str, Any] | None:
        self.extracted.append((url, download))
        return self.info

    def process_ie_result(self, ie_result: dict[str, Any], download: bool = True) -> dict[str, Any]:
        self.processed += 1
        if self.download_error is not None:
            raise self.download_error
        for event in self.hook_events:
            for hook in self.options["progress_hooks"]:
                hook(event)
        target = Path(self.options["outtmpl"]["default"]).parent
        for name in self.file_names:
            (target / name).write_bytes(b"audio")
        return ie_result


@dataclass
class Recorder:
    """Records progress callbacks."""

    events: list[tuple[float, str]] = field(default_factory=list)

    def __call__(self, fraction: float, stage: str) -> None:
        self.events.append((fraction, stage))


@dataclass
class Factory:
    """Builds one ``FakeYoutubeDL`` with preset behaviour and remembers it."""

    preset: dict[str, Any] = field(default_factory=dict)
    instances: list[FakeYoutubeDL] = field(default_factory=list)

    def __call__(self, options: dict[str, Any]) -> FakeYoutubeDL:
        instance = FakeYoutubeDL(options, **self.preset)
        self.instances.append(instance)
        return instance


def _source(url: str | None = VIDEO_URL) -> SourceInfo:
    return SourceInfo(SourceType.YOUTUBE, "youtube:dQw4w9WgXcQ", "t", None, url)


def _fetch(tmp_path: Path, factory: Factory, recorder: Recorder | None = None) -> FetchedAudio:
    return YtDlpFetcher(ydl_factory=factory).fetch(_source(), tmp_path, recorder or Recorder())


def test_fetch_downloads_best_audio_only_into_work_dir(tmp_path: Path) -> None:
    factory = Factory()

    result = _fetch(tmp_path, factory)

    ydl = factory.instances[0]
    assert result.path == tmp_path / "source.webm"
    assert result.path.read_bytes() == b"audio"
    assert ydl.options["format"] == "bestaudio/best"
    assert ydl.options["noplaylist"] is True
    assert Path(ydl.options["outtmpl"]["default"]).parent == tmp_path
    assert ydl.extracted == [(VIDEO_URL, False)]
    assert ydl.processed == 1
    assert ydl.closed


def test_fetch_downloads_the_canonical_url_of_a_spotify_match(tmp_path: Path) -> None:
    factory = Factory()
    source = SourceInfo(
        SourceType.SPOTIFY,
        "spotify:4cOdK2wGLETKBW3PvgPWqT",
        "t",
        "a",
        "https://youtu.be/dQw4w9WgXcQ",
    )

    YtDlpFetcher(ydl_factory=factory).fetch(source, tmp_path, Recorder())

    assert factory.instances[0].extracted == [(VIDEO_URL, False)]


PROGRESS_EVENTS: list[dict[str, Any]] = [
    {"status": "downloading", "downloaded_bytes": 50, "total_bytes": 100},
    {"status": "downloading", "downloaded_bytes": 20, "total_bytes_estimate": 100},
    {"status": "downloading", "downloaded_bytes": 10},
    {"status": "downloading", "downloaded_bytes": 300, "total_bytes": 200},
    {"status": "error"},
    {"status": "finished", "downloaded_bytes": 100, "total_bytes": 100},
]


def test_fetch_reports_non_decreasing_progress_ending_at_one(tmp_path: Path) -> None:
    recorder = Recorder()

    _fetch(tmp_path, Factory({"hook_events": PROGRESS_EVENTS}), recorder)

    fractions = [fraction for fraction, _ in recorder.events]
    assert fractions == sorted(fractions)
    assert (fractions[0], fractions[-1]) == (0.5, 1.0)
    assert all(0.0 <= fraction <= 1.0 for fraction in fractions)
    assert {stage for _, stage in recorder.events} == {DOWNLOAD_STAGE}


def test_fetch_rejects_reported_duration_over_limit_before_downloading(tmp_path: Path) -> None:
    factory = Factory({"info": {"id": "x", "duration": 900.5}})

    with pytest.raises(AudioTooLongError) as info:
        _fetch(tmp_path, factory)

    assert str(info.value) == "Audio is longer than 15 minutes"
    assert factory.instances[0].processed == 0


def test_fetch_accepts_exactly_the_limit_and_unknown_duration(tmp_path: Path) -> None:
    assert _fetch(tmp_path / "a", _dir_factory(tmp_path / "a", {"id": "x", "duration": 900}))
    assert _fetch(tmp_path / "b", _dir_factory(tmp_path / "b", {"id": "x"}))


def _dir_factory(path: Path, info: dict[str, Any]) -> Factory:
    path.mkdir()
    return Factory({"info": info})


def test_fetch_uses_the_configured_limit(tmp_path: Path) -> None:
    factory = Factory({"info": {"id": "x", "duration": 61}})

    with pytest.raises(AudioTooLongError, match="longer than 1 minutes"):
        YtDlpFetcher(max_duration_seconds=60, ydl_factory=factory).fetch(
            _source(), tmp_path, Recorder()
        )


@pytest.mark.parametrize("kind", ["playlist", "multi_video"])
def test_fetch_rejects_playlist_results(tmp_path: Path, kind: str) -> None:
    factory = Factory({"info": {"_type": kind, "entries": []}})

    with pytest.raises(InvalidSourceError, match="Playlists"):
        _fetch(tmp_path, factory)
    assert factory.instances[0].processed == 0


def test_fetch_rejects_live_streams(tmp_path: Path) -> None:
    with pytest.raises(SourceUnavailableError, match="Live streams"):
        _fetch(tmp_path, Factory({"info": {"id": "x", "is_live": True}}))


def test_fetch_without_info_is_unavailable(tmp_path: Path) -> None:
    with pytest.raises(SourceUnavailableError):
        _fetch(tmp_path, Factory({"info": None}))


def test_download_errors_become_unavailable_without_details(tmp_path: Path) -> None:
    error = RuntimeError("ERROR: [youtube] cookie=SECRET Traceback (most recent call last)")

    with pytest.raises(SourceUnavailableError) as info:
        _fetch(tmp_path, Factory({"download_error": error}))

    message = str(info.value)
    assert "SECRET" not in message
    assert "Traceback" not in message
    assert info.value.__cause__ is None
    assert info.value.__suppress_context__


def test_fetch_without_output_file_is_unavailable(tmp_path: Path) -> None:
    with pytest.raises(SourceUnavailableError):
        _fetch(tmp_path, Factory({"file_names": ("source.webm.part",)}))


def test_fetch_ignores_partial_files(tmp_path: Path) -> None:
    names = ("source.m4a.part", "source.m4a.ytdl", "source.m4a")

    assert _fetch(tmp_path, Factory({"file_names": names})).path == tmp_path / "source.m4a"


@pytest.mark.parametrize(
    "url",
    [
        None,
        "https://evil.example/watch?v=dQw4w9WgXcQ",
        "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT",
    ],
)
def test_fetch_refuses_sources_without_an_allow_listed_youtube_url(
    tmp_path: Path, url: str | None
) -> None:
    factory = Factory()

    with pytest.raises(InvalidSourceError):
        YtDlpFetcher(ydl_factory=factory).fetch(_source(url), tmp_path, Recorder())

    assert factory.instances == []


def test_default_factory_builds_yt_dlp_youtubedl(monkeypatch: pytest.MonkeyPatch) -> None:
    created: list[dict[str, Any]] = []

    class YoutubeDL:
        def __init__(self, params: dict[str, Any]) -> None:
            created.append(params)

    module = types.ModuleType("yt_dlp")
    module.YoutubeDL = YoutubeDL  # type: ignore[attr-defined]
    monkeypatch.setitem(sys.modules, "yt_dlp", module)

    instance = fetcher_module.create_youtube_dl({"format": "bestaudio/best"})

    assert isinstance(instance, YoutubeDL)
    assert created == [{"format": "bestaudio/best"}]


def test_fetcher_exposes_its_limit() -> None:
    assert YtDlpFetcher().max_duration_seconds == 900.0


def test_fetch_returns_the_reported_title_and_artist(tmp_path: Path) -> None:
    info = {"id": "x", "title": "  Never Gonna Give You Up  ", "artist": "Rick Astley",
            "uploader": "RickAstleyVEVO"}  # fmt: skip

    fetched = _fetch(tmp_path, Factory({"info": info}))

    assert (fetched.title, fetched.artist) == ("Never Gonna Give You Up", "Rick Astley")


def test_fetch_falls_back_to_the_uploader_then_the_channel(tmp_path: Path) -> None:
    up = _fetch(tmp_path / "a", _dir_factory(tmp_path / "a", {"id": "x", "uploader": "Up"}))
    ch = _fetch(tmp_path / "b", _dir_factory(tmp_path / "b", {"id": "x", "channel": "Ch"}))

    assert (up.artist, ch.artist) == ("Up", "Ch")


@pytest.mark.parametrize("title", [None, "", "   ", 42])
def test_fetch_without_a_usable_title_reports_none(tmp_path: Path, title: object) -> None:
    fetched = _fetch(tmp_path, Factory({"info": {"id": "x", "title": title}}))

    assert (fetched.title, fetched.artist) == (None, None)


def test_fetch_clips_long_titles_to_500_characters(tmp_path: Path) -> None:
    fetched = _fetch(tmp_path, Factory({"info": {"id": "x", "title": "t" * 600}}))

    assert fetched.title == "t" * 500
