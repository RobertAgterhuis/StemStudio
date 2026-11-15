"""``AudioFetcher`` implementation that downloads the best audio-only stream with yt-dlp.

yt-dlp is used through its Python API (no subprocess, no shell). The URL is re-validated against
the YouTube allow-list before it is handed to yt-dlp, playlists are never expanded, and the
reported duration is checked before any media is downloaded.
"""

import logging
from collections.abc import Callable
from pathlib import Path
from typing import Any, Final, Protocol, Self

from stemstudio_worker.ingest.details import NoSourceDetails, SourceDetails, with_details
from stemstudio_worker.ingest.limits import DEFAULT_MAX_DURATION_SECONDS, too_long_error
from stemstudio_worker.ingest.text import clean_text
from stemstudio_worker.ingest.urls import parse_source_url
from stemstudio_worker.ports import (
    FetchedAudio,
    InvalidSourceError,
    ProgressCallback,
    SourceInfo,
    SourceUnavailableError,
    WorkerError,
)
from stemstudio_worker.schemas import SourceType

DOWNLOAD_STAGE: Final = "Downloading audio"
"""Stage text reported while downloading (data-model.md)."""

DOWNLOAD_FAILED_MESSAGE: Final = (
    "The audio could not be downloaded from YouTube; the video may be private, "
    "age-restricted or unavailable in your region."
)
"""Job error text for any yt-dlp failure; library details are logged, never shown."""

OUTPUT_STEM: Final = "source"
"""File name (without extension) of the download inside the job's work directory."""

_PARTIAL_SUFFIXES: Final = frozenset({".part", ".ytdl", ".temp", ".tmp"})
_PLAYLIST_TYPES: Final = frozenset({"playlist", "multi_video"})
_ARTIST_KEYS: Final = ("artist", "uploader", "channel")
_LOG = logging.getLogger(__name__)


class YoutubeDLClient(Protocol):
    """The slice of ``yt_dlp.YoutubeDL`` used by the fetcher (a context manager)."""

    def __enter__(self) -> Self:
        """Open the downloader."""
        ...

    def __exit__(self, *exc: object) -> None:
        """Close the downloader."""
        ...

    def extract_info(self, url: str, download: bool = True) -> dict[str, Any] | None:
        """Return the info dict of ``url``."""
        ...

    def process_ie_result(self, ie_result: dict[str, Any], download: bool = True) -> Any:
        """Download the media described by ``ie_result``."""
        ...


YoutubeDLFactory = Callable[[dict[str, Any]], YoutubeDLClient]
"""Builds a ``YoutubeDLClient`` from yt-dlp options."""


def create_youtube_dl(options: dict[str, Any]) -> YoutubeDLClient:
    """Default factory: a real ``yt_dlp.YoutubeDL`` (imported lazily).

    Args:
        options: yt-dlp parameters.

    Returns:
        The downloader.
    """
    from yt_dlp import YoutubeDL  # noqa: PLC0415 - lazy: heavy import

    client: YoutubeDLClient = YoutubeDL(options)
    return client


class YtDlpFetcher:
    """Downloads ``SourceInfo.youtube_url`` with yt-dlp into the job's work directory."""

    def __init__(
        self,
        max_duration_seconds: float = DEFAULT_MAX_DURATION_SECONDS,
        ydl_factory: YoutubeDLFactory | None = None,
        details: SourceDetails | None = None,
    ) -> None:
        """Create the fetcher.

        Args:
            max_duration_seconds: Longest accepted duration as reported by YouTube.
            ydl_factory: Builds the downloader; defaults to ``create_youtube_dl``.
            details: Adds the thumbnail and lookup names (feature 005); none by default.
        """
        self.max_duration_seconds = max_duration_seconds
        self._factory = ydl_factory or create_youtube_dl
        self.details: SourceDetails = details or NoSourceDetails()

    def fetch(self, source: SourceInfo, work_dir: Path, progress: ProgressCallback) -> FetchedAudio:
        """Download the best audio-only stream (see ``ports.AudioFetcher.fetch``).

        The title comes from yt-dlp's ``title``; the artist from ``artist``, else ``uploader``,
        else ``channel``; the album from ``album``. The source details add the thumbnail and the
        cleaned lookup names (best effort).

        Raises:
            InvalidSourceError: The source has no allow-listed YouTube video URL, or is a playlist.
            AudioTooLongError: The reported duration exceeds the limit (nothing is downloaded).
            SourceUnavailableError: yt-dlp failed, the video is live or no file was written.
        """
        url = _allow_listed_youtube_url(source.youtube_url)
        info = self._download(url, _options(work_dir, _ProgressHook(progress)))
        progress(1.0, DOWNLOAD_STAGE)
        fetched = FetchedAudio(
            _downloaded_file(work_dir),
            *_title_and_artist(info),
            album=clean_text(info.get("album")),
        )
        return with_details(lambda: self.details.youtube(fetched, info), fetched)

    def _download(self, url: str, options: dict[str, Any]) -> dict[str, Any]:
        """Check the info of ``url`` and download it; library errors become a readable error."""
        try:
            with self._factory(options) as ydl:
                info = self._checked(ydl.extract_info(url, download=False))
                ydl.process_ie_result(info, download=True)
        except WorkerError:
            raise
        except Exception as exc:
            _LOG.warning("yt-dlp download failed: %s", type(exc).__name__)
            raise SourceUnavailableError(DOWNLOAD_FAILED_MESSAGE) from None
        return info

    def _checked(self, info: dict[str, Any] | None) -> dict[str, Any]:
        """Reject missing info, playlists, live streams and over-long videos before download."""
        if info is None:
            raise SourceUnavailableError(DOWNLOAD_FAILED_MESSAGE)
        _reject_playlists_and_live(info)
        duration = info.get("duration")
        if isinstance(duration, int | float) and duration > self.max_duration_seconds:
            raise too_long_error(self.max_duration_seconds)
        return info


class _ProgressHook:
    """yt-dlp progress hook that forwards non-decreasing fractions to a ``ProgressCallback``."""

    def __init__(self, progress: ProgressCallback) -> None:
        """Wrap ``progress``; the last reported fraction starts at 0."""
        self._progress = progress
        self._last = 0.0

    def __call__(self, event: dict[str, Any]) -> None:
        """Forward the fraction of a yt-dlp progress ``event``; unknown progress is ignored."""
        fraction = _fraction(event)
        if fraction is None:
            return
        self._last = max(self._last, min(fraction, 1.0))
        self._progress(self._last, DOWNLOAD_STAGE)


def _fraction(event: dict[str, Any]) -> float | None:
    """Return the completion fraction of a yt-dlp progress event, or None when unknown."""
    status = event.get("status")
    if status == "finished":
        return 1.0
    total = _total_bytes(event)
    if status != "downloading" or not total:
        return None
    return float(event.get("downloaded_bytes") or 0) / float(total)


def _total_bytes(event: dict[str, Any]) -> Any:
    """Exact total size of the download, else yt-dlp's estimate (None when unknown)."""
    return event.get("total_bytes") or event.get("total_bytes_estimate")


def _reject_playlists_and_live(info: dict[str, Any]) -> None:
    """Refuse playlist results and live streams."""
    if info.get("_type") in _PLAYLIST_TYPES:
        msg = "Playlists are not supported; paste the link of a single video or track."
        raise InvalidSourceError(msg)
    if info.get("is_live"):
        raise SourceUnavailableError("Live streams are not supported.")


def _title_and_artist(info: dict[str, Any]) -> tuple[str | None, str | None]:
    """Cleaned title and first usable artist-like field of a yt-dlp info dict."""
    artists = (clean_text(info.get(key)) for key in _ARTIST_KEYS)
    return clean_text(info.get("title")), next((a for a in artists if a), None)


def _allow_listed_youtube_url(url: str | None) -> str:
    """Re-validate the download URL and return its canonical form."""
    parsed = parse_source_url(url or "")
    if parsed.source_type is not SourceType.YOUTUBE:
        msg = "Only YouTube videos can be downloaded."
        raise InvalidSourceError(msg)
    return parsed.canonical_url


def _options(work_dir: Path, hook: _ProgressHook) -> dict[str, Any]:
    """yt-dlp parameters: single video, best audio-only stream, output inside ``work_dir``."""
    return {
        "format": "bestaudio/best",
        "noplaylist": True,
        "outtmpl": {"default": str(work_dir / f"{OUTPUT_STEM}.%(ext)s")},
        "overwrites": True,
        "cachedir": False,
        "quiet": True,
        "no_warnings": True,
        "noprogress": True,
        "progress_hooks": [hook],
        "socket_timeout": 30,
        "retries": 3,
    }


def _downloaded_file(work_dir: Path) -> Path:
    """Return the completed download in ``work_dir`` (partial files are ignored)."""
    files = sorted(
        path
        for path in work_dir.glob(f"{OUTPUT_STEM}.*")
        if path.is_file() and path.suffix not in _PARTIAL_SUFFIXES
    )
    if not files:
        raise SourceUnavailableError(DOWNLOAD_FAILED_MESSAGE)
    return files[0]
