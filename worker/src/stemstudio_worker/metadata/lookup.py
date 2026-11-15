"""``MetadataLookup`` implementation: the steps of a metadata job (ADR-014 D3, data-model 2.4).

MusicBrainz recording (ISRC, then search) -> release group -> Cover Art Archive front ->
MusicBrainz artist (url-rels) -> Wikidata claims and labels -> Wikipedia summary. Only a failed
recording search fails the job (``METADATA_FAILED_TEXT``); a later step that fails adds a
warning and the job completes with what was obtained. ``STEMSTUDIO_ONLINE_LOOKUPS=off`` gives
outcome ``disabled`` without any request.
"""

import logging
from collections.abc import Callable
from datetime import UTC, datetime
from typing import Final

from stemstudio_worker.config import OnlineSettings
from stemstudio_worker.metadata.coverart import CoverArtArchive
from stemstudio_worker.metadata.http import OnlineClient, OnlineRequestError
from stemstudio_worker.metadata.matcher import RecordingFound
from stemstudio_worker.metadata.musicbrainz import ArtistFacts, MusicBrainz
from stemstudio_worker.metadata.wikidata import Wikidata, WikidataFacts
from stemstudio_worker.metadata.wikipedia import Wikipedia
from stemstudio_worker.ports import (
    ArtworkError,
    ArtworkImages,
    MetadataFindings,
    MetadataLookupError,
    MetadataQuery,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    COVER_WARNING_TEXT,
    METADATA_FAILED_TEXT,
    WIKIPEDIA_WARNING_TEXT,
    ArtistInfo,
    ArtworkInfo,
    MetadataResult,
    ReleaseGroupInfo,
    WikipediaSummary,
)

STAGES: Final = (
    "Searching MusicBrainz",
    "Fetching album cover",
    "Reading Wikidata",
    "Reading Wikipedia",
)
"""Stage texts of a metadata job, in order (data-model 2.3)."""

ARTIST_WARNING_TEXT: Final = "The artist details could not be loaded."
"""Warning when the MusicBrainz artist step failed (the job still completes)."""

WIKIDATA_WARNING_TEXT: Final = "Wikidata could not be reached."
"""Warning when the Wikidata step failed (the job still completes)."""

Clock = Callable[[], datetime]
"""Returns the current time (timezone-aware)."""

_MAX_NAME: Final = 500
_LOG = logging.getLogger(__name__)


def _utc_now() -> datetime:
    return datetime.now(UTC)


class OnlineMetadataLookup:
    """Looks songs up with the online services through one rate-limited client."""

    def __init__(
        self, client: OnlineClient, settings: OnlineSettings, clock: Clock = _utc_now
    ) -> None:
        """Create the lookup.

        Args:
            client: The process-wide online client (shared with the image normaliser).
            settings: ``WorkerSettings.online`` (switch and Wikipedia languages).
            clock: Source of ``ArtistInfo.fetched_at``.
        """
        self.client = client
        self._settings = settings
        self._clock = clock
        self._musicbrainz = MusicBrainz(client)
        self._covers = CoverArtArchive(client)
        self._wikidata = Wikidata(client)
        self._wikipedia = Wikipedia(client)

    def lookup(self, query: MetadataQuery, progress: ProgressCallback) -> MetadataFindings:
        """Run the metadata job steps (see ``ports.MetadataLookup.lookup``).

        Raises:
            MetadataLookupError: The recording search failed after retries.
        """
        if not self._settings.lookups_enabled:
            return MetadataFindings(MetadataResult(outcome="disabled"))
        progress(0.0, STAGES[0])
        found = self._find(query)
        if found is None:
            return MetadataFindings(MetadataResult(outcome="not-found"))
        return self._complete(found, progress)

    def _find(self, query: MetadataQuery) -> RecordingFound | None:
        """The recording; a failed search fails the job with the fixed text."""
        try:
            return self._musicbrainz.find(query)
        except OnlineRequestError as exc:
            _LOG.warning("Recording search failed: %s", exc)
            raise MetadataLookupError(METADATA_FAILED_TEXT) from None

    def _complete(self, found: RecordingFound, progress: ProgressCallback) -> MetadataFindings:
        """The remaining steps after the recording was found."""
        warnings: list[str] = []
        group = self._release_group(found.release_group)
        progress(0.25, STAGES[1])
        cover = self._cover(group, warnings)
        artist = self._artist(found.artist_mbid, progress, warnings)
        result = MetadataResult(
            outcome="found",
            recording=found.recording,
            release_group=group,
            artwork=cover[1] if cover else None,
            artist=artist,
            warnings=warnings,
        )
        return MetadataFindings(result, cover[0] if cover else None)

    def _release_group(self, group: ReleaseGroupInfo | None) -> ReleaseGroupInfo | None:
        """Refine the search's release group with its first release date (best effort)."""
        if group is None:
            return None
        try:
            return self._musicbrainz.release_group(group.mbid) or group
        except OnlineRequestError as exc:
            _LOG.info("Release group details failed: %s", exc)
            return group

    def _cover(
        self, group: ReleaseGroupInfo | None, warnings: list[str]
    ) -> tuple[ArtworkImages, ArtworkInfo] | None:
        """The Cover Art Archive front; a failure adds the cover warning."""
        if group is None:
            return None
        try:
            return self._covers.front(group.mbid)
        except (OnlineRequestError, ArtworkError) as exc:
            _LOG.info("Cover Art Archive step failed: %s", exc)
            warnings.append(COVER_WARNING_TEXT)
            return None

    def _artist(
        self, mbid: str | None, progress: ProgressCallback, warnings: list[str]
    ) -> ArtistInfo | None:
        """MusicBrainz artist facts completed with Wikidata and Wikipedia."""
        facts = self._artist_facts(mbid, warnings)
        progress(0.5, STAGES[2])
        extra = self._wikidata_facts(facts, warnings)
        progress(0.75, STAGES[3])
        summary = self._summary(extra, warnings)
        return None if facts is None else _artist_info(facts, extra, summary, self._clock())

    def _artist_facts(self, mbid: str | None, warnings: list[str]) -> ArtistFacts | None:
        """The MusicBrainz artist; a failure adds the artist warning."""
        if mbid is None:
            return None
        try:
            return self._musicbrainz.artist(mbid)
        except OnlineRequestError as exc:
            _LOG.info("Artist step failed: %s", exc)
            warnings.append(ARTIST_WARNING_TEXT)
            return None

    def _wikidata_facts(self, facts: ArtistFacts | None, warnings: list[str]) -> WikidataFacts:
        """The Wikidata facts of the artist; a failure adds the Wikidata warning."""
        if facts is None or facts.wikidata_id is None:
            return WikidataFacts()
        try:
            return self._wikidata.facts(facts.wikidata_id, self._settings.wikipedia_languages)
        except OnlineRequestError as exc:
            _LOG.info("Wikidata step failed: %s", exc)
            warnings.append(WIKIDATA_WARNING_TEXT)
            return WikidataFacts()

    def _summary(self, extra: WikidataFacts, warnings: list[str]) -> WikipediaSummary | None:
        """The Wikipedia summary; a failure adds the Wikipedia warning."""
        if not extra.sitelinks:
            return None
        try:
            return self._wikipedia.summary(extra.sitelinks, self._settings.wikipedia_languages)
        except OnlineRequestError as exc:
            _LOG.info("Wikipedia step failed: %s", exc)
            warnings.append(WIKIPEDIA_WARNING_TEXT)
            return None


def _artist_info(
    facts: ArtistFacts, extra: WikidataFacts, summary: WikipediaSummary | None, now: datetime
) -> ArtistInfo:
    """Merge MusicBrainz facts (preferred) with Wikidata facts into the contract record."""
    base = ArtistInfo(
        mbid=facts.mbid,
        name=facts.name[:_MAX_NAME],
        type=facts.type,
        country=facts.country,
        wikidata_id=facts.wikidata_id,
        summary=summary,
        fetched_at=now,
    )
    return base.model_copy(update=_merged(facts, extra))


def _merged(facts: ArtistFacts, extra: WikidataFacts) -> dict[str, object]:
    """Years and website from MusicBrainz, else Wikidata; genres; members unless a person."""
    return {
        "begin_year": facts.begin_year or extra.begin_year,
        "end_year": facts.end_year or extra.end_year,
        "genres": extra.genres,
        "members": [] if facts.type == "person" else extra.members,
        "website": facts.website or extra.website,
    }
