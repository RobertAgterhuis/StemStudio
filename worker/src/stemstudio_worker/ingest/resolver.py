"""``SourceResolver`` implementation: YouTube URLs offline, Spotify URLs via metadata + match."""

from stemstudio_worker.ingest.spotify import NO_MATCH_MESSAGE, SpotifyMetadataClient, YouTubeMatcher
from stemstudio_worker.ingest.urls import ParsedSourceUrl, parse_source_url
from stemstudio_worker.ports import InvalidSourceError, SourceInfo, SourceUnavailableError
from stemstudio_worker.schemas import SourceType


class UrlSourceResolver:
    """Resolves validated YouTube and Spotify URLs to a downloadable ``SourceInfo``.

    YouTube URLs are resolved without network access; the title is a placeholder naming the
    video id until the job pipeline replaces it with the title the fetcher reports after the
    download (``ports.FetchedAudio``). Spotify URLs are resolved to
    public metadata and matched to a public YouTube video; Spotify audio is never accessed.
    """

    def __init__(self, metadata_client: SpotifyMetadataClient, matcher: YouTubeMatcher) -> None:
        """Create the resolver.

        Args:
            metadata_client: Looks up Spotify track metadata.
            matcher: Finds the YouTube video to download for a Spotify track.
        """
        self._metadata = metadata_client
        self._matcher = matcher

    def resolve(self, url: str) -> SourceInfo:
        """Validate ``url`` and resolve it (see ``ports.SourceResolver.resolve``).

        Raises:
            InvalidSourceError: The URL is not an accepted YouTube video or Spotify track URL.
            SourceUnavailableError: Spotify metadata lookup failed or found no YouTube match.
        """
        parsed = parse_source_url(url)
        if parsed.source_type is SourceType.SPOTIFY:
            return self._resolve_spotify(parsed)
        return SourceInfo(
            source_type=SourceType.YOUTUBE,
            source_key=parsed.source_key,
            title=f"YouTube video {parsed.item_id}",
            artist=None,
            youtube_url=parsed.canonical_url,
        )

    def _resolve_spotify(self, parsed: ParsedSourceUrl) -> SourceInfo:
        """Look up Spotify metadata and the matching YouTube video."""
        track = self._metadata.track(parsed.item_id)
        return SourceInfo(
            source_type=SourceType.SPOTIFY,
            source_key=parsed.source_key,
            title=track.title,
            artist=track.artist,
            youtube_url=_youtube_match(self._matcher.match(track)),
        )


def _youtube_match(url: str | None) -> str:
    """Return the canonical URL of a YouTube match; anything else counts as no match."""
    try:
        parsed = parse_source_url(url or "")
    except InvalidSourceError:
        raise SourceUnavailableError(NO_MATCH_MESSAGE) from None
    if parsed.source_type is not SourceType.YOUTUBE:
        raise SourceUnavailableError(NO_MATCH_MESSAGE)
    return parsed.canonical_url
