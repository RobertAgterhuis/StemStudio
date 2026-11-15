"""The sung alignment test clip and its word-start annotation (qa, feature 006, T012).

Run from the repository root with the worker environment (numpy, scipy, soundfile)::

    uv run --project worker python tests/fixtures/generate_lyrics_fixtures.py

Writes ``tests/fixtures/audio/align_clip_sung_29s.wav`` (git-ignored, 44.1 kHz stereo PCM16, an
isolated lead vocal: one synthetic voice, no accompaniment) and the committed annotation
``tests/fixtures/lyrics/align-clip.json``: per line and word the exact start and end in
milliseconds. The song (words, melody) is self-written by qa and, with the audio, dedicated to
the public domain (CC0 1.0); the voice is the formant synthesiser in ``sung_voice.py``. Because
the clip is rendered from a score, the annotation is exact (a word starts at the first sample of
its first phone) instead of hand-marked. Reruns are byte-identical (fixed seed).

Used by the SC-003 integration test ``worker/tests/alignment/test_align_clip.py`` (stem-separation
006:T042): align the plain text of the annotation and count word starts within 300 ms.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]

from generate_fixtures import FIXTURE_DIR, SAMPLE_RATE
from sung_voice import PHONES, Segment, expand, synthesise

ANNOTATION_VERSION = 1
CLIP_NAME = "align_clip_sung_29s.wav"
BPM = 100
BEAT_S = 60.0 / BPM
LEAD_IN_S = 1.0
TAIL_S = 1.0
PEAK = 0.7
PCM_MAX = 32_767
WORD_GAP_S = 0.03
"""Silence at the end of every word, so words do not run into each other."""
LANGUAGE = "en"
TOLERANCE_MS = 300
PROVENANCE = "self-written lyrics and melody; formant synthesiser tests/fixtures/sung_voice.py"
MIN_WITHIN_SHARE = 0.8


@dataclass(frozen=True)
class Syllable:
    """One sung syllable: ARPAbet phones, length in beats, pitch (MIDI)."""

    phones: str
    beats: float
    midi: int


@dataclass(frozen=True)
class Word:
    """A word of the lyrics and its syllables."""

    text: str
    syllables: tuple[Syllable, ...]


@dataclass(frozen=True)
class Line:
    """A lyric line: beats of rest before it, then its words."""

    rest_beats: float
    words: tuple[Word, ...]

    @property
    def text(self) -> str:
        """The line as plain text."""
        return " ".join(word.text for word in self.words)


def _w(text: str, *syllables: tuple[str, float, int]) -> Word:
    return Word(text, tuple(Syllable(*s) for s in syllables))


SONG: tuple[Line, ...] = (
    Line(0, (
        _w("Morning", ("M AO R", 1, 62), ("N IH NG", 1, 64)), _w("light", ("L AY T", 2, 66)),
        _w("is", ("IH Z", 0.5, 64)), _w("on", ("AA N", 0.5, 62)), _w("the", ("DH AH", 1, 61)),
        _w("water", ("W AO", 1, 62), ("D ER", 2, 57)),
    )),
    Line(1, (
        _w("every", ("EH V", 1, 62), ("R IY", 1, 64)), _w("boat", ("B OW T", 2, 66)),
        _w("is", ("IH Z", 1, 67)), _w("rolling", ("R OW", 1, 66), ("L IH NG", 1, 64)),
        _w("home", ("HH OW M", 3, 62)),
    )),
    Line(4, (
        _w("I", ("AY", 1, 69)), _w("will", ("W IH L", 1, 67)), _w("sing", ("S IH NG", 2, 66)),
        _w("a", ("AX", 1, 64)), _w("little", ("L IH", 1, 66), ("T AX L", 1, 64)),
        _w("song", ("S AO NG", 3, 62)),
    )),
    Line(1, (
        _w("waiting", ("W EY", 1, 62), ("T IH NG", 1, 64)), _w("for", ("F AO R", 1, 66)),
        _w("the", ("DH AX", 1, 64)), _w("summer", ("S AH", 1, 62), ("M ER", 1, 61)),
        _w("rain", ("R EY N", 4, 62)),
    )),
)  # fmt: skip
"""Self-written lyrics and melody (D major, 100 BPM, one syllable per note; CC0)."""


@dataclass(frozen=True)
class Placed:
    """A word placed in time with its phone segments."""

    word: str
    start_s: float
    end_s: float
    segments: tuple[Segment, ...]


class SongError(ValueError):
    """Raised when a syllable is too short for its consonants."""


def syllable_segments(syllable: Syllable, start_s: float, end_s: float) -> list[Segment]:
    """Phone segments of one syllable: consonants at their own length, the vowel fills the rest.

    Raises:
        SongError: When the consonants leave no room for the vowel.
    """
    phones = expand(syllable.phones)
    fixed = sum(PHONES[p].seconds for p in phones)
    vowel_s = (end_s - start_s) - fixed
    if vowel_s <= 0:
        raise SongError(f"syllable {syllable.phones!r} is shorter than its consonants")
    segments, at = [], start_s
    for phone in phones:
        length = PHONES[phone].seconds or vowel_s
        segments.append(Segment(phone, at, at + length, syllable.midi))
        at += length
    return segments


def place_word(word: Word, start_s: float) -> Placed:
    """Lays out a word from ``start_s``; the last syllable keeps ``WORD_GAP_S`` of silence."""
    segments: list[Segment] = []
    at = start_s
    for index, syllable in enumerate(word.syllables):
        length = syllable.beats * BEAT_S
        gap = WORD_GAP_S if index == len(word.syllables) - 1 else 0.0
        segments.extend(syllable_segments(syllable, at, at + length - gap))
        at += length
    return Placed(word.text, start_s, segments[-1].end_s, tuple(segments))


def place_song(song: Sequence[Line] = SONG) -> list[list[Placed]]:
    """Every word of ``song`` placed in time, per line, after ``LEAD_IN_S``."""
    lines: list[list[Placed]] = []
    at = LEAD_IN_S
    for line in song:
        at += line.rest_beats * BEAT_S
        placed = []
        for word in line.words:
            placed.append(place_word(word, at))
            at += sum(s.beats for s in word.syllables) * BEAT_S
        lines.append(placed)
    return lines


def duration_s(lines: Sequence[Sequence[Placed]]) -> float:
    """Clip length: the last word's end plus ``TAIL_S``."""
    return round(lines[-1][-1].end_s + TAIL_S, 3)


def _ms(seconds: float) -> int:
    return round(seconds * 1000)


def _line_entry(text: str, words: Sequence[Placed]) -> dict[str, Any]:
    entries = [{"text": w.word, "startMs": _ms(w.start_s), "endMs": _ms(w.end_s)} for w in words]
    return {
        "text": text,
        "startMs": entries[0]["startMs"],
        "endMs": entries[-1]["endMs"],
        "words": entries,
    }


def annotation(lines: Sequence[Sequence[Placed]], song: Sequence[Line] = SONG) -> dict[str, Any]:
    """The committed annotation: plain text, lines and words with exact times (ms)."""
    return {
        "version": ANNOTATION_VERSION,
        "file": CLIP_NAME,
        "licence": "CC0-1.0",
        "provenance": PROVENANCE,
        "language": LANGUAGE,
        "bpm": BPM,
        "sampleRate": SAMPLE_RATE,
        "durationSeconds": duration_s(lines),
        "plainText": "\n".join(line.text for line in song),
        "sc003": {"toleranceMs": TOLERANCE_MS, "minWithinShare": MIN_WITHIN_SHARE},
        "lines": [_line_entry(line.text, placed) for line, placed in zip(song, lines, strict=True)],
    }


def render_clip(lines: Sequence[Sequence[Placed]]) -> np.ndarray[Any, np.dtype[np.int16]]:
    """Stereo 16-bit PCM of the sung clip, peak ``PEAK``."""
    segments = [s for line in lines for word in line for s in word.segments]
    mono = PEAK * synthesise(segments, duration_s(lines))
    pcm = np.round(mono * PCM_MAX).astype(np.int16)
    return np.column_stack((pcm, pcm))


def generate(out_dir: Path, annotation_path: Path) -> dict[str, Any]:
    """Writes the clip into ``out_dir`` and the annotation to ``annotation_path``."""
    lines = place_song()
    out_dir.mkdir(parents=True, exist_ok=True)
    sf.write(
        str(out_dir / CLIP_NAME), render_clip(lines), SAMPLE_RATE, subtype="PCM_16", format="WAV"
    )
    facts = annotation(lines)
    annotation_path.parent.mkdir(parents=True, exist_ok=True)
    annotation_path.write_text(
        json.dumps(facts, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n"
    )
    return facts


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Command line: ``--out-dir`` (default ``audio/``) and ``--annotation``."""
    parser = argparse.ArgumentParser(description="Generate the sung alignment test clip.")
    parser.add_argument("--out-dir", type=Path, default=FIXTURE_DIR / "audio")
    parser.add_argument(
        "--annotation", type=Path, default=FIXTURE_DIR / "lyrics" / "align-clip.json"
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Entry point; returns the process exit code."""
    args = parse_args(argv)
    facts = generate(args.out_dir, args.annotation)
    words = sum(len(line["words"]) for line in facts["lines"])
    sys.stdout.write(f"wrote {CLIP_NAME} ({words} words) to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
