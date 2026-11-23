"""Size limits of a handout job request (020 security fixes; 422 ``ValidationFailed``).

The schema bounds each field; these bound the whole job, so a request at every field's maximum
(50 songs of 1000 lyric lines of 500 characters, 64 sections of 999 bars) cannot keep the
single-threaded ``insights`` lane busy for long. A request at the limits renders in about 40 s
on the reference CPU (SC-004 budget of a setlist: 90 s). The backend mirrors these numbers.

- ``MAX_HANDOUT_BODY_BYTES``: the JSON body (``api.body_limit``; non-ASCII text escaped as
  a JSON unicode escape takes six bytes a character).
- ``MAX_HANDOUT_TEXT_CHARS``: every string of the document together.
- ``MAX_HANDOUT_CELLS``: sections, bars and the chords in them, legend chords, lyric lines and
  their chords, setlist entries.
- ``MAX_HANDOUT_NOTATION_BYTES``: the distinct notation PDFs together (checked when the job is
  created; the renderer enforces it again when it runs).
"""

from collections.abc import Iterable
from typing import Any, Final

from stemstudio_worker.schemas import HandoutDocument, HandoutJobRequest, HandoutSong

MAX_HANDOUT_BODY_BYTES: Final = 8 * 1024 * 1024
"""Largest ``POST /v1/handout-jobs`` body."""

MAX_HANDOUT_TEXT_CHARS: Final = 500_000
"""Most characters of text of one handout document."""

MAX_HANDOUT_CELLS: Final = 30_000
"""Most layout cells of one handout document (see :func:`layout_cells`)."""

MAX_HANDOUT_NOTATION_BYTES: Final = 128 * 1024 * 1024
"""Largest total size of the distinct notation PDFs of one handout."""

_FIELD: Final = ("body", "document")


class HandoutLimitError(ValueError):
    """A handout request over a job limit; :meth:`errors` has the pydantic error shape."""

    def __init__(self, messages: list[str]) -> None:
        """Refuse the request with one readable message per limit passed."""
        super().__init__("; ".join(messages))
        self.messages = messages

    def errors(self) -> list[dict[str, Any]]:
        """The messages as pydantic errors of the ``document`` field (422 ``errors`` extension)."""
        return [{"loc": _FIELD, "type": "value_error", "msg": text} for text in self.messages]


def text_chars(document: HandoutDocument) -> int:
    """Characters of every string of ``document``."""
    return _chars(document.model_dump())


def _chars(value: object) -> int:
    if isinstance(value, str):
        return len(value)
    if isinstance(value, dict):
        return sum(_chars(item) for item in value.values())
    if isinstance(value, list):
        return sum(_chars(item) for item in value)
    return 0


def layout_cells(document: HandoutDocument) -> int:
    """Layout cells of ``document``: what the form, chord chart, lyrics and running order draw."""
    entries = len(document.setlist.entries) if document.setlist is not None else 0
    return entries + sum(_song_cells(song) for song in document.songs)


def _song_cells(song: HandoutSong) -> int:
    bars = [bar for section in song.sections for bar in section.chord_bars or ()]
    lines = song.lyrics.lines if song.lyrics is not None else ()
    chords = sum(len(bar) for bar in bars) + sum(len(line.chords) for line in lines)
    return len(song.sections) + len(bars) + len(song.chord_symbols) + len(lines) + chords


def check_request_limits(request: HandoutJobRequest) -> None:
    """Require the text and the layout cells of ``request`` to be within the job limits.

    Raises:
        HandoutLimitError: Over ``MAX_HANDOUT_TEXT_CHARS`` or ``MAX_HANDOUT_CELLS``.
    """
    chars, cells = text_chars(request.document), layout_cells(request.document)
    messages = []
    if chars > MAX_HANDOUT_TEXT_CHARS:
        messages.append(
            f"The handout has more than {MAX_HANDOUT_TEXT_CHARS} characters of text ({chars})."
        )
    if cells > MAX_HANDOUT_CELLS:
        messages.append(f"The handout has more than {MAX_HANDOUT_CELLS} sections, bars, chords "
                        f"and lyric lines ({cells}).")  # fmt: skip
    if messages:
        raise HandoutLimitError(messages)


def check_notation_bytes(total: int) -> None:
    """Require the distinct notation files of a job to be ``MAX_HANDOUT_NOTATION_BYTES`` at most.

    Raises:
        HandoutLimitError: ``total`` is over the limit.
    """
    if total > MAX_HANDOUT_NOTATION_BYTES:
        mib = MAX_HANDOUT_NOTATION_BYTES // (1024 * 1024)
        raise HandoutLimitError(
            [f"The notation files of the handout are larger than {mib} MiB together."]
        )


def notation_paths(request: HandoutJobRequest) -> Iterable[str]:
    """The distinct notation PDF paths of ``request`` in order of first use."""
    paths = (item.pdf_path for song in request.document.songs for item in song.notation)
    return dict.fromkeys(paths)
