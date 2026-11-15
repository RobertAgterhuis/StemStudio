"""Artwork normalisation and online metadata lookups of feature 005 (audio-ingest agent, ADR-014).

The package imports only ``ports``, ``schemas`` and ``config`` (plan.md dependency direction);
the composition root (``main.py``) builds its adapters with the factories below and hands them
to the job pipeline and the ingest adapters. Every outbound request goes through one
process-wide ``http.OnlineClient`` (https, allow-listed hosts, <= 3 re-validated redirects,
10 MB cap, 10 s timeouts, 1 request/s per service, Retry-After and back-off) with a User-Agent
that contains no personal data.

Factories (composition root):
    create_image_normalizer(settings): ``ImageNormalizer`` (Pillow; the fake on the fake stack).
    create_metadata_lookup(settings): ``MetadataLookup`` (online; the fake on the fake stack).
    create_source_details(settings): reader of source artwork and lookup names, passed to
        ``ingest.create_ingest_adapters``.

Modules:
    user_agent: The ``StemStudio/<version> ( <contact> )`` User-Agent and contact URL check.
    ratelimit: Per-host-family token bucket, Retry-After parsing and back-off delays.
    http: The allow-listed ``OnlineClient`` and the response parsing helpers.
    images: ``PillowImageNormalizer`` (cover 600 px, thumbnail 160 px).
    titles: YouTube title cleaning and name normalisation for comparisons.
    source_artwork: YouTube thumbnail, Spotify cover, embedded upload cover and tags.
    matcher, musicbrainz: Recording search (ISRC first) and the precision-first matcher.
    coverart, wikidata, wikipedia: The other services of a metadata job.
    lookup: ``OnlineMetadataLookup`` (the metadata job steps).
    fake: The deterministic fake provider of ``STEMSTUDIO_FAKE_ONLINE``.
"""

from functools import cache

from stemstudio_worker.config import OnlineSettings, WorkerSettings
from stemstudio_worker.metadata.fake import FakeImageNormalizer, FakeMetadataLookup
from stemstudio_worker.metadata.http import OnlineClient, create_online_client
from stemstudio_worker.metadata.images import PillowImageNormalizer
from stemstudio_worker.metadata.lookup import OnlineMetadataLookup
from stemstudio_worker.metadata.source_artwork import SourceDetailsReader
from stemstudio_worker.ports import ImageNormalizer, MetadataLookup

__all__ = ["create_image_normalizer", "create_metadata_lookup", "create_source_details"]


@cache
def _shared_client(online: OnlineSettings) -> OnlineClient:
    """One online client (and rate limiter) per process and settings."""
    return create_online_client(online)


def create_image_normalizer(settings: WorkerSettings) -> ImageNormalizer:
    """The picture normaliser: Pillow with the shared client, or the fake on the fake stack.

    Args:
        settings: Worker settings (``separator``, ``online``).

    Returns:
        ``PillowImageNormalizer`` or ``FakeImageNormalizer`` (``separator=fake``).
    """
    if settings.separator == "fake":
        return FakeImageNormalizer()
    return PillowImageNormalizer(_shared_client(settings.online))


def create_metadata_lookup(settings: WorkerSettings) -> MetadataLookup:
    """The song lookup of metadata jobs.

    Args:
        settings: Worker settings; ``online.lookups_enabled=False`` gives ``disabled`` results
            (the fake runs in mode ``off``).

    Returns:
        ``OnlineMetadataLookup`` sharing the normaliser's client, or ``FakeMetadataLookup`` in
        ``online.fake_mode`` (``separator=fake``).
    """
    if settings.separator == "fake":
        return FakeMetadataLookup(
            settings.online.fake_mode if settings.online.lookups_enabled else "off"
        )
    return OnlineMetadataLookup(_shared_client(settings.online), settings.online)


def create_source_details(settings: WorkerSettings) -> SourceDetailsReader:  # noqa: ARG001
    """The source details reader for ``ingest.create_ingest_adapters(settings, details)``."""
    return SourceDetailsReader()
