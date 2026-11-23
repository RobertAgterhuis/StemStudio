"""Generates chord-spelling-vectors.json 1.0.0 (architect, 016 Phase 0 T004; pure stdlib, as 007's render vectors).

Usage (repo root)::

    python -I specs/016-instrument-views/contracts/generate_chord_spelling_vectors.py [--check]

The rules are data-model.md 2.5 (spelling), 2.8 (key display, written keys) and 2.9 / research R-14 (capo shapes and
the capo advisor), with the Phase 0 decisions written into ``RULES``. Consumers: the backend Domain
(``tests/StemStudio.Domain.Tests/InstrumentViews/ChordSpellingTests.cs``, ``KeyFormatterTests.cs``,
``CapoAdvisorTests.cs``) and the engine (``src/audio-engine/tests/chords/chord-spelling.test.ts``). ``--check`` fails
when the file on disk differs from a fresh generation.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

OUT = Path(__file__).resolve().parent / "chord-spelling-vectors.json"
VERSION = "1.0.0"
SHARPS = ("C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B")
FLATS = ("C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B")
FLAT_MAJOR_TONICS = frozenset({5, 10, 3, 8, 1, 6})  # F Bb Eb Ab Db Gb (Gb, not F#, for tonic 6)
SUFFIX = {"maj": "", "min": "m", "dim": "dim", "aug": "aug", "min6": "m6", "maj6": "6", "min7": "m7",
          "minmaj7": "m(maj7)", "maj7": "maj7", "7": "7", "dim7": "dim7", "hdim7": "m7b5", "sus2": "sus2", "sus4": "sus4"}
QUALITIES = tuple(SUFFIX)
OPEN_SHAPES = {"maj": {0, 9, 7, 4, 2}, "min": {9, 4, 2}, "7": {0, 9, 7, 4, 2, 11}, "min7": {9, 4, 2},
               "maj7": {0, 9, 7, 4, 2, 5}, "sus2": {9, 2}, "sus4": {9, 2, 4}}
WRITTEN = {"concert": 0, "trumpet-bb": 2, "clarinet-bb": 2, "soprano-sax-bb": 2, "tenor-sax-bb": 14,
           "bass-clarinet-bb": 14, "alto-sax-eb": 9, "baritone-sax-eb": 21, "clarinet-eb": -3, "horn-f": 7,
           "english-horn-f": 7, "flute": 0, "oboe": 0, "violin": 0, "viola": 0, "cello": 0, "double-bass": 12,
           "trombone": 0, "bassoon": 0, "tuba": 0}
RULES = [
    "displayRoot = (root + transpose - capo) mod 12; the bass moves the same way (data-model 2.5).",
    "The spelling key is the key of the region the chord starts in (fallback the global key; no key = sharps), moved by"
    " (tonic + transpose - capo) mod 12.",
    "Flats when the moved key's relative major tonic is one of F Bb Eb Ab Db Gb (major tonic t, minor tonic t + 3);"
    " otherwise sharps. 6 accidentals: Gb major / Eb minor flats, F# major / D# minor are never produced.",
    "A sharps key spells every chromatic chord with sharps (C major: A#, not Bb) - the binding data-model rule.",
    "Slash bass: '/' + the moved bass spelled with the same key; a bass equal to the root is not written.",
    "No chord (root null) = 'N.C.' at every transpose and capo.",
    "Key display: '<Tonic> major|minor'; transpose t != 0 -> '<moved> (original <key>, +t|-t)'; confidence < 0.5 appends"
    " ' (uncertain)'; the written key adds the instrument's semitones (mod 12) to the concert tonic, spelled the same way.",
    "Capo shape: the chord lowered by the capo (displayRoot with capo). Open shapes by root pitch class and quality"
    " (research R-14 table; the bass is ignored); everything else is a barre shape.",
    "Capo advisor: for c in 0..7, open = open-shape time / non-N time; rank by open - 0.02 c, exact ties by the lower"
    " capo; top three; shapeKey = global key lowered by c (and moved by the transpose); fewOpenShapes when the best open"
    " < 0.5; open rounded to 4 decimals.",
    "Shortcut: transpose t in 1..7 -> capo t; t = -5 -> capo 7; otherwise none.",
]


def uses_flats(tonic: int, mode: str) -> bool:
    """Data-model 2.5 flat-key rule."""
    major = tonic if mode == "major" else (tonic + 3) % 12
    return major in FLAT_MAJOR_TONICS


def name(pc: int, flats: bool) -> str:
    """Note name of a pitch class."""
    return (FLATS if flats else SHARPS)[pc % 12]


def symbol(chord: dict, key: tuple[int, str] | None, transpose: int, capo: int) -> str:
    """Display symbol of a chord."""
    if chord["root"] is None:
        return "N.C."
    flats = key is not None and uses_flats((key[0] + transpose - capo) % 12, key[1])
    root = (chord["root"] + transpose - capo) % 12
    text = name(root, flats) + SUFFIX[chord["quality"]]
    bass = chord["bass"]
    if bass is not None and bass != chord["root"]:
        text += "/" + name((bass + transpose - capo) % 12, flats)
    return text


def key_name(tonic: int, mode: str) -> str:
    """'<Tonic> major|minor' spelled by the rule."""
    return f"{name(tonic, uses_flats(tonic, mode))} {mode}"


def key_display(tonic: int, mode: str, transpose: int, confidence: float) -> str:
    """Data-model 2.8 display."""
    original = key_name(tonic, mode)
    text = original if transpose == 0 else f"{key_name((tonic + transpose) % 12, mode)} (original {original}, {transpose:+d})"
    return text + (" (uncertain)" if confidence < 0.5 else "")


def case(root, quality, bass, key, transpose, capo):
    """One spelling vector."""
    chord = {"root": root, "quality": quality, "bass": bass}
    return {**chord, "keyTonic": key[0] if key else None, "keyMode": key[1] if key else None,
            "transpose": transpose, "capo": capo, "symbol": symbol(chord, key, transpose, capo)}


def spelling_cases() -> list[dict]:
    """At least 200 spelling cases (data-model 2.5)."""
    out = [case(r, q, None, (0, "major"), 0, 0) for r in range(12) for q in QUALITIES]
    out += [case(r, "maj", None, (5, "major"), 0, 0) for r in range(12)]
    out += [case(r, "min", None, (3, "minor"), 0, 0) for r in range(12)]
    out += [case(1, "maj", None, (1, "major"), 0, 0), case(6, "maj", None, (6, "major"), 0, 0),
            case(6, "maj", None, (6, "minor"), 0, 0), case(3, "min", None, (3, "minor"), 0, 0)]
    out += [case(9, "min7", 7, (0, "major"), t, 0) for t in range(-12, 13)]
    out += [case(0, "maj", 4, (0, "major"), 0, 0), case(0, "maj", 0, (0, "major"), 0, 0),
            case(2, "maj", 6, (2, "major"), 0, 0), case(1, "maj", 5, (1, "major"), 0, 0)]
    out += [case(2, "maj", None, (2, "major"), 0, 0), case(1, "maj", None, (1, "major"), 0, 0)]  # region keys
    out += [case(None, None, None, (0, "major"), t, c) for t, c in ((0, 0), (5, 0), (0, 3))]
    out += [case(0, "maj", None, None, 0, 0), case(10, "maj", None, None, 0, 0)]
    out += [case(3, "maj", None, (3, "major"), 0, c) for c in range(13)]
    return out


def key_cases() -> list[dict]:
    """Key displays incl. transposed, uncertain and written keys."""
    out = [{"tonic": t, "mode": m, "transpose": 0, "confidence": 0.9, "instrument": "concert",
            "display": key_display(t, m, 0, 0.9), "writtenKey": None} for m in ("major", "minor") for t in range(12)]
    out += [{"tonic": 0, "mode": "major", "transpose": t, "confidence": 0.9, "instrument": "concert",
             "display": key_display(0, "major", t, 0.9), "writtenKey": None} for t in (-12, -5, -1, 1, 2, 3, 6, 12)]
    out.append({"tonic": 9, "mode": "minor", "transpose": 0, "confidence": 0.4, "instrument": "concert",
                "display": key_display(9, "minor", 0, 0.4), "writtenKey": None})
    for tonic, mode in ((0, "major"), (3, "major"), (2, "minor")):
        for instrument, semis in WRITTEN.items():
            if instrument == "concert":
                continue
            written = (tonic + semis) % 12
            out.append({"tonic": tonic, "mode": mode, "transpose": 0, "confidence": 0.9, "instrument": instrument,
                        "display": key_display(tonic, mode, 0, 0.9), "writtenKey": key_name(written, mode)})
    return out


def capo_cases() -> list[dict]:
    """Every root x quality x capo 0..12 at transpose 0 in C major, plus transpose samples."""
    out = [{"root": r, "quality": q, "bass": None, "transpose": 0, "capo": c,
            "shape": symbol({"root": r, "quality": q, "bass": None}, (0, "major"), 0, c)}
           for r in range(12) for q in QUALITIES for c in range(13)]
    out += [{"root": 0, "quality": "maj", "bass": 7, "transpose": t, "capo": c,
             "shape": symbol({"root": 0, "quality": "maj", "bass": 7}, (0, "major"), t, c)}
            for t in range(-12, 13, 3) for c in (0, 2, 5)]
    return out


def is_open(root: int, quality: str, transpose: int, capo: int) -> bool:
    """R-14 open-shape table on the shape's pitch class."""
    return (root + transpose - capo) % 12 in OPEN_SHAPES.get(quality, set())


def advise(progression: list[tuple[int, str, int]], key: tuple[int, str], transpose: int) -> dict:
    """Research R-14 ranking."""
    total = sum(d for _, _, d in progression)
    ranked = []
    for capo in range(8):
        open_time = sum(d for r, q, d in progression if is_open(r, q, transpose, capo))
        share = round(open_time / total, 4)
        ranked.append((share - 0.02 * capo, -capo, capo, share))
    ranked.sort(reverse=True)
    top = ranked[:3]
    shortcut = transpose if 1 <= transpose <= 7 else 7 if transpose == -5 else None
    return {
        "shortcut": None if shortcut is None else {"capo": shortcut, "reason": "original-shapes"},
        "suggestions": [{"capo": c, "shapeKey": key_name((key[0] + transpose - c) % 12, key[1]), "openShare": s,
                         "fewOpenShapes": top[0][3] < 0.5} for _, _, c, s in top],
    }


FIXTURES = {
    "E-flat major (Eb Bb Cm Ab)": ((3, "major"), [(3, "maj", 4000), (10, "maj", 4000), (0, "min", 4000), (8, "maj", 4000)]),
    "A-flat major (Ab Db Eb Fm)": ((8, "major"), [(8, "maj", 4000), (1, "maj", 4000), (3, "maj", 4000), (5, "min", 4000)]),
    "B-flat major (Bb Eb F Gm)": ((10, "major"), [(10, "maj", 4000), (3, "maj", 4000), (5, "maj", 4000), (7, "min", 4000)]),
    "F-sharp major (F# B C# D#m)": ((6, "major"), [(6, "maj", 4000), (11, "maj", 4000), (1, "maj", 4000), (3, "min", 4000)]),
}


def capo_fixtures() -> list[dict]:
    """Four progressions with expected advice at transposes 0, +3 and -5."""
    out = []
    for label, (key, progression) in FIXTURES.items():
        for transpose in (0, 3, -5):
            out.append({"name": label, "keyTonic": key[0], "keyMode": key[1], "transpose": transpose,
                        "segments": [{"root": r, "quality": q, "durationMs": d} for r, q, d in progression],
                        **advise(progression, key, transpose)})
    return out


def build() -> str:
    """The JSON text."""
    data = {"version": VERSION, "rules": RULES, "cases": spelling_cases(), "keys": key_cases(),
            "capo": capo_cases(), "capoFixtures": capo_fixtures()}
    return json.dumps(data, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    """Entry point."""
    text = build()
    if "--check" in argv:
        fresh = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print(f"{OUT.name}: {'up to date' if fresh else 'NOT up to date (run without --check)'}")
        return 0 if fresh else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
