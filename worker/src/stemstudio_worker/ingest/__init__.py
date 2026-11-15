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
    details: ``SourceDetails`` protocol (feature 005): artwork candidates and lookup names are
        added by a reader the composition root injects (``ingest`` does not import ``metadata``).
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ingest.details import SourceDetails
from stemstudio_worker.ingest.fetcher import YtDlpFetcher
from stemstudio_worker.ingest.normalizer import FfmpegConfig, FfmpegNormalizer
from stemstudio_worker.ingest.resolver import UrlSourceResolver
from stemstudio_worker.ingest.spotify import SpotdlMetadataClient, SpotdlYouTubeMatcher
from stemstudio_worker.ingest.upload import FileUploadIntake
from stemstudio_worker.ports import IngestAdapters

__all__ = ["create_ingest_adapters"]


def create_ingest_adapters(
    settings: WorkerSettings, details: SourceDetails | None = None
) -> IngestAdapters:
    """Build the production ingest adapters.

    Args:
        settings: Worker settings; ``max_duration_seconds`` bounds the fetcher and normaliser,
            ``max_upload_bytes`` bounds the upload intake.
        details: Source details reader (``metadata.source_artwork.SourceDetailsReader`` in
            production, feature 005); without it no artwork candidates or lookup names are set.

    Returns:
        Resolver (spotDL-backed for Spotify), upload intake, yt-dlp fetcher and ffmpeg
        normaliser.
    """
    return IngestAdapters(
        resolver=UrlSourceResolver(SpotdlMetadataClient(), SpotdlYouTubeMatcher(), details),
        upload_intake=FileUploadIntake(settings.max_upload_bytes, details),
        fetcher=YtDlpFetcher(settings.max_duration_seconds, details=details),
        normalizer=FfmpegNormalizer(
            config=FfmpegConfig(max_duration_seconds=settings.max_duration_seconds)
        ),
    )
