"""Time-synced lyrics from LRCLIB (feature 006, ADR-015; audio-ingest).

``lrc`` and ``lyricsfile`` parse the two LRCLIB timing formats, ``interpolate`` spreads line
timing over words, ``normalize`` holds the shared limits and document building, ``select``
picks a record, ``lrclib`` talks to the service through the 005 online client, ``lookup``
implements the ``LyricsLookup`` port and ``fake`` its deterministic stand-in.

Lyrics are copyrighted: no module here logs lyric text (ADR-015 D6, FR-010).
"""
