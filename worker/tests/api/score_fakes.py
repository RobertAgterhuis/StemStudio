"""Fakes and builders for the transcription route tests (notator, engraver, library files)."""

import copy
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf

from stemstudio_worker.ports import (
    DrumHit,
    EngravedScore,
    NotationSettings,
    NoteEvent,
    ScoreDocument,
)
from stemstudio_worker.schemas import KeyInfo, ScoreWarning

from .contract import spec

WAIT = 5.0
TRACK = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"
BASS = f"{TRACK}/stems/bass.flac"
PDF = b"%PDF-1.7 fake score"
KEY = KeyInfo(tonic="C", mode="major", confidence=0.9, source="estimated")


def request_body(name: str = "BassTranscriptionRequest", **changes: Any) -> dict[str, Any]:
    """A contract example request with ``changes`` applied."""
    body: dict[str, Any] = copy.deepcopy(spec()["components"]["examples"][name]["value"])
    body.update(changes)
    return body


def write_stem(library: Path, path: str = BASS, seconds: float = 4.5) -> Path:
    """Write a quiet stereo FLAC at ``path`` below ``library``."""
    target = library / path
    target.parent.mkdir(parents=True, exist_ok=True)
    tone = 0.1 * np.sin(np.arange(int(seconds * 44_100)) * 2 * np.pi * 110 / 44_100)
    sf.write(target, np.stack([tone, tone], axis=1), 44_100, format="FLAC", subtype="PCM_16")
    return target


@dataclass
class FakeNotator:
    """Returns a fixed score document; ``on_call`` runs first (e.g. to read the job state)."""

    error: Exception | None = None
    warnings: tuple[ScoreWarning, ...] = ()
    on_call: Callable[[], None] = lambda: None
    calls: list[tuple[int, NotationSettings]] = field(default_factory=list)

    def _document(self, count: int, settings: NotationSettings) -> ScoreDocument:
        self.calls.append((count, settings))
        self.on_call()
        if self.error is not None:
            raise self.error
        return ScoreDocument(musicxml=b"<score-partwise/>", midi=b"MThd", key=KEY, bpm=120,
                             time_signature="4/4", bars=3, notes=count,
                             warnings=self.warnings)  # fmt: skip

    def notate(self, notes: Sequence[NoteEvent], settings: NotationSettings) -> ScoreDocument:
        return self._document(len(notes), settings)

    def notate_drums(self, hits: Sequence[DrumHit], settings: NotationSettings) -> ScoreDocument:
        document = self._document(len(hits), settings)
        return ScoreDocument(document.musicxml, document.midi, None, document.bpm,
                             document.time_signature, document.bars, document.notes)  # fmt: skip


@dataclass
class FakeEngraver:
    """Returns a two-page fake PDF; records titles and subjects."""

    error: Exception | None = None
    on_call: Callable[[], None] = lambda: None
    calls: list[tuple[str, str | None]] = field(default_factory=list)

    def engrave(self, musicxml: bytes, title: str, subject: str | None) -> EngravedScore:
        self.calls.append((title, subject))
        self.on_call()
        if self.error is not None:
            raise self.error
        return EngravedScore(pdf=PDF, pages=2)


def blank_pdf_page(svg: str) -> bytes:
    """One empty A4 PDF page (stands in for CairoSVG, which needs libcairo)."""
    import io  # noqa: PLC0415

    from pypdf import PdfWriter  # noqa: PLC0415

    writer = PdfWriter()
    writer.add_blank_page(width=595, height=842)
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()
