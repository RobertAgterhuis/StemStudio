"""Source resolution, upload intake, download and ffmpeg normalisation (audio-ingest agent).

Contract (binding for the parallel wave): the composition root calls
``create_ingest_adapters(settings)``. Nothing heavy (spotDL, yt-dlp) is imported until an
adapter actually needs it, and no adapter touches the network or a subprocess on construction.

Modules:
    urls: URL allow-listing, playlist rejection and source keys.
    spotify: spotDL metadata lookup and YouTube matching (metadata only, never Spotify audio).
    resolver: ``SourceResolver`` implementation.
    upload: ``UploadIntake`` implementation.
    fetcher: ``AudioFetcher`` implementation (yt-dlp Python API).
    normalizer: ``AudioNormalizer`` implementation (ffprobe/ffmpeg via argument lists).
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ingest.fetcher import YtDlpFetcher
from stemstudio_worker.ingest.normalizer import FfmpegNormalizer
from stemstudio_worker.ingest.resolver import UrlSourceResolver
from stemstudio_worker.ingest.spotify import SpotdlMetadataClient, SpotdlYouTubeMatcher
from stemstudio_worker.ingest.upload import FileUploadIntake
from stemstudio_worker.ports import IngestAdapters

__all__ = ["create_ingest_adapters"]


def create_ingest_adapters(settings: WorkerSettings) -> IngestAdapters:
    """Build the production ingest adapters.

    Args:
        settings: Worker settings; ``max_duration_seconds`` bounds the fetcher and normaliser,
            ``max_upload_bytes`` bounds the upload intake.

    Returns:
        Resolver (spotDL-backed for Spotify), upload intake, yt-dlp fetcher and ffmpeg
        normaliser.
    """
    return IngestAdapters(
        resolver=UrlSourceResolver(SpotdlMetadataClient(), SpotdlYouTubeMatcher()),
        upload_intake=FileUploadIntake(max_upload_bytes=settings.max_upload_bytes),
        fetcher=YtDlpFetcher(max_duration_seconds=settings.max_duration_seconds),
        normalizer=FfmpegNormalizer(max_duration_seconds=settings.max_duration_seconds),
    )
