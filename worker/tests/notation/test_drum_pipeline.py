"""Drum detail stems -> onsets -> two-voice score (008 T071; FR-009, SC-003).

Generated click stems of a rock groove at 120 BPM go through the real drum transcriber and the
real notator; every hit must land on its sixteenth.
"""

from pathlib import Path

import numpy as np
import soundfile as sf
from music21 import converter, stream

from stemstudio_worker.analysis import LibrosaDrumTranscriber
from stemstudio_worker.notation import create_notator
from stemstudio_worker.ports import NotationSettings
from stemstudio_worker.schemas import BeatGridTempo, DrumPiece, ScoreTitles

BARS = 2
GROOVE: dict[DrumPiece, list[float]] = {
    "kick": [0, 2, 2.5],
    "snare": [1, 3],
    "hihat": [q / 2 for q in range(8)],
}
SOUND: dict[DrumPiece, tuple[float, float]] = {
    "kick": (70.0, 0.04),
    "snare": (900.0, 0.02),
    "hihat": (7_000.0, 0.004),
}
FIRST_DOWNBEAT = 0.5
SR = 44_100


def burst(signal: np.ndarray, at: float, piece: DrumPiece) -> None:
    """Add one decaying sine burst of ``piece`` at ``at`` seconds."""
    frequency, decay = SOUND[piece]
    t = np.arange(int(decay * 8 * SR)) / SR
    start = round(at * SR)
    signal[start : start + len(t)] += 0.9 * np.sin(2 * np.pi * frequency * t) * np.exp(-t / decay)


def stem(folder: Path, piece: DrumPiece) -> Path:
    signal = np.zeros(int((FIRST_DOWNBEAT + BARS * 2 + 1) * SR))
    for bar in range(BARS):
        for quarter in GROOVE[piece]:
            burst(signal, FIRST_DOWNBEAT + (bar * 4 + quarter) * 0.5, piece)
    path = folder / f"{piece}.wav"
    sf.write(path, signal, SR, subtype="PCM_16")
    return path


SETTINGS = NotationSettings(
    profile="drums",
    grid=BeatGridTempo(
        bpm=120,
        beats_per_bar=4,
        beats=[FIRST_DOWNBEAT + i * 0.5 for i in range(12)],
        downbeats=[FIRST_DOWNBEAT],
    ),
    resolution="sixteenth",
    key_override=None,
    titles=ScoreTitles(title="Drums"),
    duration_seconds=5.5,
)


def test_a_rock_groove_is_notated_on_its_sixteenths(tmp_path: Path) -> None:
    inputs = {piece: stem(tmp_path, piece) for piece in GROOVE}
    hits = LibrosaDrumTranscriber().transcribe(inputs, None, lambda fraction: None)
    document = create_notator().notate_drums(hits, SETTINGS)
    assert (document.bars, document.notes, document.warnings) == (BARS, 26, ())
    score = converter.parse(document.musicxml, format="musicxml")
    hands, feet = next(iter(score.recurse().getElementsByClass(stream.Measure))).voices
    assert [n.offset for n in feet.notes] == GROOVE["kick"]
    assert [n.offset for n in hands.notes] == GROOVE["hihat"]
