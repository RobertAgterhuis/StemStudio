"""``LyricsLookup`` implementations and their factory (ADR-015 D1, data-model.md 2.3).

``LrclibLookup`` asks LRCLIB at most three times (get with album, get, search; the gets need an
artist), stops at the first request that yields a usable record and turns it into one
``TimedLyrics`` document: word sync from the ``lyricsfile`` (falling back to the LRC lines when
the file cannot be read), line sync interpolated from LRC, or plain text. Lookups switched off
(``STEMSTUDIO_ONLINE_LOOKUPS=off``) complete as ``disabled`` without any request. Lyric text is
never logged.
"""

from collections.abc import Callable

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.lyrics.fake import FakeLyricsLookup
from stemstudio_worker.lyrics.interpolate import lrc_document
from stemstudio_worker.lyrics.lrclib import Getter, LrclibClient, LrclibRecord
from stemstudio_worker.lyrics.lyricsfile import parse_lyricsfile
from stemstudio_worker.lyrics.normalize import (
    DocumentMeta,
    instrumental_document,
    normalize_newlines,
    plain_document,
)
from stemstudio_worker.lyrics.select import Choice, select_record
from stemstudio_worker.ports import (
    LyricsLookup,
    LyricsQuery,
    LyricsUnusableError,
    ProgressCallback,
)
from stemstudio_worker.schemas import (
    LYRICS_STAGE_SEARCHING,
    MAX_LYRICS_CHARS,
    LyricsResult,
    TimedLyrics,
)

_Request = Callable[[], list[LrclibRecord]]


class DisabledLyricsLookup:
    """Lookup used while online lookups are off: always ``disabled``, never a request."""

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:  # noqa: ARG002 (port signature)
        """Return outcome ``disabled``."""
        return LyricsResult(outcome="disabled")


class LrclibLookup:
    """Finds lyrics on LRCLIB (``LyricsLookup`` port)."""

    def __init__(self, client: LrclibClient) -> None:
        """Create the lookup over one LRCLIB client."""
        self._client = client

    def lookup(self, query: LyricsQuery, progress: ProgressCallback) -> LyricsResult:
        """Search LRCLIB and build the result (see ``ports.LyricsLookup``).

        Raises:
            LyricsUnavailableError: LRCLIB could not be reached.
            LyricsUnusableError: The chosen record could not be read.
        """
        progress(0.0, LYRICS_STAGE_SEARCHING)
        result = result_for(self._find(query))
        progress(1.0, LYRICS_STAGE_SEARCHING)
        return result

    def _find(self, query: LyricsQuery) -> Choice | None:
        """The first usable record of the request sequence."""
        for request in self._requests(query):
            choice = select_record(request(), query)
            if choice is not None:
                return choice
        return None

    def _requests(self, query: LyricsQuery) -> list[_Request]:
        """Get with album (album and artist known), get (artist known), search."""
        client = self._client
        requests: list[_Request] = []
        if query.artist and query.album:
            requests.append(lambda: _listed(client.get(query, with_album=True)))
        if query.artist:
            requests.append(lambda: _listed(client.get(query, with_album=False)))
        requests.append(lambda: client.search(query))
        return requests


def _listed(record: LrclibRecord | None) -> list[LrclibRecord]:
    """A get answer as a (possibly empty) record list."""
    return [] if record is None else [record]


def result_for(choice: Choice | None) -> LyricsResult:
    """The lookup result for a chosen record (``not-found`` for None).

    Raises:
        LyricsUnusableError: The record's lyrics could not be read.
    """
    if choice is None:
        return LyricsResult(outcome="not-found")
    meta = _meta(choice.record)
    if choice.kind == "instrumental":
        return LyricsResult(outcome="instrumental", lyrics=instrumental_document(meta))
    return LyricsResult(outcome="found", lyrics=_document(choice, meta))


def _meta(record: LrclibRecord) -> DocumentMeta:
    """Source fields of a record; its plain text when present and within the limit."""
    duration = record.duration if record.duration > 0 else None
    plain = normalize_newlines(record.plain_lyrics or "")
    text = plain if 0 < len(plain) <= MAX_LYRICS_CHARS else None
    return DocumentMeta("lrclib", str(record.id), duration, None, text)


def _document(choice: Choice, meta: DocumentMeta) -> TimedLyrics:
    """Parse the part of the record that ``choice.kind`` names."""
    record = choice.record
    if choice.kind == "word":
        return _word_document(record, meta)
    if choice.kind == "line":
        return lrc_document(record.synced_lyrics or "", meta)
    return plain_document(record.plain_lyrics or "", meta)


def _word_document(record: LrclibRecord, meta: DocumentMeta) -> TimedLyrics:
    """The ``lyricsfile`` document, or the LRC document when the file cannot be read."""
    try:
        return parse_lyricsfile(record.lyricsfile or "", meta)
    except LyricsUnusableError:
        if not record.synced_lyrics:
            raise
        return lrc_document(record.synced_lyrics, meta)


def create_lyrics_lookup(settings: WorkerSettings, getter: Getter) -> LyricsLookup:
    """Build the lookup of the composition root.

    Args:
        settings: Worker settings (online switch, separator, ``lyrics.fake_mode``).
        getter: The process-wide 005 ``OnlineClient``.

    Returns:
        ``DisabledLyricsLookup`` when lookups are off, ``FakeLyricsLookup`` on the fake stack,
        else ``LrclibLookup``.
    """
    if not settings.online.lookups_enabled:
        return DisabledLyricsLookup()
    if settings.separator == "fake":
        return FakeLyricsLookup(settings.lyrics.fake_mode)
    return LrclibLookup(LrclibClient(getter))
