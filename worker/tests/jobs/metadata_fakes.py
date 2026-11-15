"""Fakes for the metadata lane tests: a scriptable lookup and result builders."""

import hashlib
import threading
from dataclasses import dataclass, field

from stemstudio_worker.ports import (
    ArtworkImages,
    MetadataFindings,
    MetadataQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import CreateMetadataJobRequest, MetadataResult

WAIT = 5.0
COVER = b"\xff\xd8cover-jpeg"
THUMB = b"\xff\xd8thumb-jpeg"
IMAGES = ArtworkImages(cover=COVER, thumb=THUMB, sha256=hashlib.sha256(COVER).hexdigest())
NOT_FOUND = MetadataFindings(MetadataResult(outcome="not-found"))
FOUND = MetadataFindings(
    MetadataResult(outcome="found", artwork=IMAGES.info("coverartarchive")), IMAGES
)
REQUEST = CreateMetadataJobRequest(title="Hero of the Day", artist="Metallica", isrc=None)


@dataclass
class FakeLookup:
    """Returns ``findings`` (or raises ``error``); can block until ``release`` is set."""

    findings: MetadataFindings = NOT_FOUND
    error: Exception | None = None
    stages: tuple[str, ...] = ("Searching MusicBrainz", "Reading Wikidata")
    queries: list[MetadataQuery] = field(default_factory=list)
    entered: threading.Event = field(default_factory=threading.Event)
    release: threading.Event | None = None

    def block(self) -> threading.Event:
        """Make the next lookups wait until the returned event is set."""
        self.release = threading.Event()
        return self.release

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        self.queries.append(query)
        self.entered.set()
        if self.release is not None:
            assert self.release.wait(WAIT), "lookup was never released"
        for index, stage in enumerate(self.stages):
            progress(index / len(self.stages), stage)
        if self.error is not None:
            raise self.error
        return self.findings
