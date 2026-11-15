"""Fake online provider of the fake stack (``STEMSTUDIO_FAKE_ONLINE``, separator ``fake``).

Deterministic and network-free. ``FakeMetadataLookup`` returns exactly the behaviour table of
``tests/fixtures/metadata/fake-provider.json`` (qa 005:T009; a worker test compares the two):

* ``found``: the fictional artist "The Fixture Band", album "Synthetic Sessions" (2020), a
  placeholder (non-Wikipedia) summary and a generated ``coverartarchive`` picture;
* ``not-found`` / ``off``: outcome ``not-found`` / ``disabled``;
* ``failed``: the job fails with the fixed text (as after three 503 answers).

``FakeImageNormalizer`` generates a solid-colour picture for URL candidates (no download) and
re-encodes embedded pictures with Pillow.
"""

import io
import uuid
from datetime import UTC, datetime
from functools import cache
from typing import Final

from PIL import Image

from stemstudio_worker.config import FakeOnlineMode
from stemstudio_worker.metadata.images import PillowImageNormalizer
from stemstudio_worker.metadata.lookup import STAGES
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ArtworkImages,
    MetadataFindings,
    MetadataLookupError,
    MetadataQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    METADATA_FAILED_TEXT,
    ArtistInfo,
    ArtistMember,
    MetadataResult,
    RecordingMatch,
    ReleaseGroupInfo,
    WikipediaSummary,
)

FAKE_COVER_COLOUR: Final = (40, 90, 160)
"""Colour of the fake Cover Art Archive picture (``fake-provider.json`` ``artworkColour``)."""

FAKE_SOURCE_COLOUR: Final = (160, 90, 40)
"""Colour of the generated source picture of URL candidates."""

FAKE_FETCHED_AT: Final = datetime(2026, 11, 15, 12, 0, tzinfo=UTC)
"""``ArtistInfo.fetched_at`` of the fake artist (fixed, so results are deterministic)."""

_NAMESPACE: Final = uuid.UUID("5a1d3c1e-0c5e-4d9b-9a57-57e3575701d0")
_ALBUM: Final = "Synthetic Sessions"
_YEAR: Final = 2020
_PAGE: Final = "https://en.wikipedia.org/wiki/The_Fixture_Band"

FAKE_ARTIST: Final = ArtistInfo(
    mbid="816012e6-d855-599b-8cb1-0d8bd765ebe1",
    name="The Fixture Band",
    type="group",
    country="NL",
    begin_year=2001,
    end_year=None,
    genres=["synth-pop", "test rock"],
    members=[
        ArtistMember(name="Ada Sample", from_year=2001),
        ArtistMember(name="Bo Waveform", from_year=2001, to_year=2015),
        ArtistMember(name="Cy Decibel", from_year=2015),
    ],
    website="https://example.org/the-fixture-band",
    wikidata_id="Q900000099",
    summary=WikipediaSummary(
        language="en",
        title="The Fixture Band",
        extract=(
            "The Fixture Band is a fictional group invented for the StemStudio tests. "
            "This text is not from Wikipedia."
        ),
        page_url=_PAGE,
        history_url="https://en.wikipedia.org/w/index.php?title=The_Fixture_Band&action=history",
        revision_id="1250000099",
    ),
    fetched_at=FAKE_FETCHED_AT,
)
"""The artist of mode ``found``."""


FAKE_RELEASE_GROUP: Final = ReleaseGroupInfo(
    mbid=str(uuid.uuid5(_NAMESPACE, f"release-group:{_ALBUM}")),
    title=_ALBUM,
    primary_type="Album",
    first_release_year=_YEAR,
)
"""The album of mode ``found`` ("Synthetic Sessions", 2020)."""


class FakeMetadataLookup:
    """``MetadataLookup`` of the fake stack; see the module docstring."""

    def __init__(self, mode: FakeOnlineMode) -> None:
        """Create the fake.

        Args:
            mode: ``found``, ``not-found``, ``failed`` or ``off``.
        """
        self.mode = mode

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        """Return the fixed result of the mode (see ``ports.MetadataLookup.lookup``).

        Raises:
            MetadataLookupError: In mode ``failed``.
        """
        if self.mode == "off":
            return MetadataFindings(MetadataResult(outcome="disabled"))
        progress(0.0, STAGES[0])
        if self.mode == "failed":
            raise MetadataLookupError(METADATA_FAILED_TEXT)
        if self.mode == "not-found":
            return MetadataFindings(MetadataResult(outcome="not-found"))
        return _found(query)


def _found(query: MetadataQuery) -> MetadataFindings:
    """The ``found`` result: the recording title follows the looked-up title."""
    cover = fake_cover()
    result = MetadataResult(
        outcome="found",
        recording=_recording(query),
        release_group=FAKE_RELEASE_GROUP,
        artwork=cover.info("coverartarchive", None),
        artist=FAKE_ARTIST,
    )
    return MetadataFindings(result, cover)


def _recording(query: MetadataQuery) -> RecordingMatch:
    """A recording with the looked-up title and an id that is stable per title."""
    return RecordingMatch(
        mbid=str(uuid.uuid5(_NAMESPACE, f"recording:{query.title}")),
        title=query.title[:500],
        length_seconds=query.duration_seconds,
        score=100,
        matched_by="search",
    )


def _solid(colour: tuple[int, int, int]) -> bytes:
    """A 600 px solid-colour JPEG."""
    buffer = io.BytesIO()
    Image.new("RGB", (600, 600), colour).save(buffer, "JPEG", quality=90)
    return buffer.getvalue()


@cache
def fake_cover() -> ArtworkImages:
    """The normalised fake Cover Art Archive picture (computed once)."""
    candidate = ArtworkCandidate(source="coverartarchive", data=_solid(FAKE_COVER_COLOUR))
    return PillowImageNormalizer().normalize(candidate)


class FakeImageNormalizer:
    """``ImageNormalizer`` of the fake stack: no downloads, generated source pictures."""

    def __init__(self) -> None:
        """Create the fake on top of the real Pillow normaliser (without a client)."""
        self._pillow = PillowImageNormalizer()

    def normalize(self, candidate: ArtworkCandidate) -> ArtworkImages:
        """Normalise embedded bytes; URL candidates become a generated solid picture."""
        data = candidate.data if candidate.data is not None else _solid(FAKE_SOURCE_COLOUR)
        return self._pillow.normalize(ArtworkCandidate(source=candidate.source, data=data))
