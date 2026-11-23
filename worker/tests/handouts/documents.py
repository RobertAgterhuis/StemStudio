"""Handout requests and notation fixtures for the renderer tests (built in the test, consumer side).

Requests start from the ``BassHandoutRequest`` example of the worker contract 1.11.0; notation
PDFs are small pypdf files with one line of text per page (``NOTATION <name> <page>``).
"""

import copy
import io
import zlib
from functools import cache
from pathlib import Path
from typing import Any

import pypdf
import yaml
from pypdf.generic import DecodedStreamObject, DictionaryObject, NameObject, StreamObject

from stemstudio_worker.schemas import HandoutJobRequest

from .support import jpeg

SPEC = (Path(__file__).resolve().parents[3] / "specs" / "020-insights-handouts" / "contracts"
        / "worker-api.openapi.yaml")  # fmt: skip
A4 = (595.28, 841.89)
LETTER = (612.0, 792.0)
ARTWORK = "tracks/7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f/artwork/auto/cover.jpg"
TAB = "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/tab.pdf"
SCORE = "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf"
LYRICS = {
    "chordPlacement": "word", "source": "LRCLIB",
    "lines": [
        {"text": "Dvořák wrote Łódź songs", "section": 0,
         "chords": [{"symbol": "Am", "charIndex": 0}, {"symbol": "F#m7", "charIndex": 13}]},
        {"text": "", "section": 1,
         "chords": [{"symbol": "C", "charIndex": 0}, {"symbol": "G", "charIndex": 4}]},
    ],
}  # fmt: skip


@cache
def _example() -> dict[str, Any]:
    document = yaml.safe_load(SPEC.read_text(encoding="utf-8"))
    value: dict[str, Any] = document["components"]["examples"]["BassHandoutRequest"]["value"]
    return value


def body(**song: Any) -> dict[str, Any]:
    """The example body with ``song`` fields changed (camelCase keys)."""
    data = copy.deepcopy(_example())
    data["document"]["songs"][0].update(song)
    return data


def request(paper: str = "a4", **song: Any) -> HandoutJobRequest:
    """A validated request of the example with ``song`` changes."""
    data = body(**song)
    data["paper"] = paper
    return HandoutJobRequest.model_validate(data)


def full_song() -> dict[str, Any]:
    """Song changes that include every ingredient: lyrics with chords, progress, notes."""
    include = ["facts", "form-chart", "chord-chart", "chords-over-lyrics", "notation",
               "practice-notes", "practice-progress"]  # fmt: skip
    return {
        "include": include,
        "lyrics": copy.deepcopy(LYRICS),
        "practiceNotes": "Keep the eighths even.\n<b>not bold</b> & <script>x</script>",
    }


def notation_pdf(path: Path, pages: int = 1, size: tuple[float, float] = A4) -> Path:
    """A notation PDF with ``NOTATION <stem> <n>`` on page ``n`` (parents created)."""
    writer = pypdf.PdfWriter()
    for number in range(1, pages + 1):
        page = writer.add_blank_page(*size)
        font = DictionaryObject({NameObject("/Type"): NameObject("/Font"),
                                 NameObject("/Subtype"): NameObject("/Type1"),
                                 NameObject("/BaseFont"): NameObject("/Helvetica")})  # fmt: skip
        fonts = DictionaryObject({NameObject("/F1"): writer._add_object(font)})
        page[NameObject("/Resources")] = DictionaryObject({NameObject("/Font"): fonts})
        stream = DecodedStreamObject()
        stream.set_data(f"BT /F1 24 Tf 72 700 Td (NOTATION {path.stem} {number}) Tj ET".encode())
        page[NameObject("/Contents")] = writer._add_object(stream)
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    writer.write(buffer)
    path.write_bytes(buffer.getvalue())
    return path.resolve()


def token_pdf(path: Path, pages: int, tokens: int) -> Path:
    """A small A4 PDF whose every page has a compressed content stream of about ``tokens`` tokens.

    The stream repeats ``0 0 m`` (three tokens); it compresses to a few KiB however long it is.
    """
    writer = pypdf.PdfWriter()
    blob = zlib.compress(b"0 0 m\n" * (tokens // 3), 9)
    for _ in range(pages):
        page = writer.add_blank_page(*A4)
        stream = StreamObject()
        stream._data = blob  # encoded, as a hostile file carries it
        stream[NameObject("/Filter")] = NameObject("/FlateDecode")
        page[NameObject("/Contents")] = writer._add_object(stream)
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    writer.write(buffer)
    path.write_bytes(buffer.getvalue())
    return path.resolve()


def library_files(root: Path, tab_pages: int = 2, score_size: tuple[float, float] = A4
                  ) -> dict[str, Path]:  # fmt: skip
    """Artwork and the two notation PDFs of the example below ``root`` (library path -> real)."""
    return {
        ARTWORK: jpeg(root / ARTWORK, (64, 64)),
        TAB: notation_pdf(root / TAB, tab_pages),
        SCORE: notation_pdf(root / SCORE, 1, score_size),
    }


def page_texts(pdf: bytes) -> list[str]:
    """The extracted text of every page."""
    return [page.extract_text() for page in pypdf.PdfReader(io.BytesIO(pdf)).pages]
