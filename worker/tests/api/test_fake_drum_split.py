"""The fake stack's drum split feeds the fake drum trainer a hi-hat (016 R-11, mid-wave fix).

Research R-11 and ``e2e/tests/instrument-drums.spec.ts`` expect the fake groove: kick on 1 and
3, snare on 2 and 4 and a hi-hat on every eighth. The fake drum transcriber only plays the pieces
it is given, so the fake detail split of the drums must deliver a ``hihat`` stem.
"""

from pathlib import Path

import numpy as np
import soundfile as sf

from stemstudio_worker.detail.fake import FakeSource
from stemstudio_worker.schemas import DrumPiece
from stemstudio_worker.views_fake import FakeDrumHitsTranscriber

SAMPLE_RATE = 44_100
SECONDS = 4.0


def _split_drum_files(tmp_path: Path) -> dict[DrumPiece, Path]:
    mix = np.full((2, int(SAMPLE_RATE * SECONDS)), 0.2, dtype=np.float32)
    model = FakeSource(0.0, lambda _s: None).choose("drums").load("cpu")
    outputs = model.separate(mix, lambda _f, _s: None)
    files: dict[DrumPiece, Path] = {}
    for name, audio in outputs.items():
        path = tmp_path / f"{name}.wav"
        sf.write(path, audio.T, SAMPLE_RATE)
        files[name] = path  # type: ignore[index]  # the fake drum outputs are drum pieces
    return files


def test_the_fake_drum_split_delivers_a_hihat_stem(tmp_path: Path) -> None:
    assert set(_split_drum_files(tmp_path)) == {"kick", "snare", "toms", "hihat"}


def test_the_fake_split_and_trainer_play_hihats_on_every_eighth(tmp_path: Path) -> None:
    hits = FakeDrumHitsTranscriber().transcribe(_split_drum_files(tmp_path), None, lambda _f: None)
    times: dict[str, list[float]] = {}
    for hit in hits:
        times.setdefault(hit.piece, []).append(hit.time)

    assert times["hihat"] == [0.5 + 0.25 * i for i in range(14)]
    assert times["kick"] == [0.5, 1.5, 2.5, 3.5]
    assert times["snare"] == [1.0, 2.0, 3.0]
