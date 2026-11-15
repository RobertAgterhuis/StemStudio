"""SC-002 on the real models: note F1 on qa's synthetic fixtures (008 T040, T051).

Deselected by default (``-m integration``); downloads the basic-pitch ONNX model (230 KB) and the
piano checkpoint (172 MB) into ``STEMSTUDIO_MODEL_DIR/transcription`` on first use. The fixtures
come from ``uv run --project worker python tests/fixtures/generate_score_fixtures.py`` (qa 008
T011; ground truth in ``tests/fixtures/score-manifest.json``). Raw notes are cleaned and quantised
by the beat-analysis notation code (profile range, monophonic reduction, grid snap) before the
note onset F-measure (onset within 50 ms, same pitch) is taken.
"""

import json
import os
import time
from fractions import Fraction
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.config import load_settings
from stemstudio_worker.devices import create_device_runner
from stemstudio_worker.notation.cleaning import clean_notes
from stemstudio_worker.notation.profiles import get_profile
from stemstudio_worker.notation.timebase import TimeBase
from stemstudio_worker.ports import NoteEvent
from stemstudio_worker.schemas import BeatGridTempo
from stemstudio_worker.transcription.basic_pitch import BasicPitchTranscriber
from stemstudio_worker.transcription.models import ModelStore
from stemstudio_worker.transcription.piano import PianoTranscriber, TorchRuntime

FIXTURES = Path(__file__).resolve().parents[3] / "tests" / "fixtures"


def _fixture(name: str) -> tuple[Path, dict[str, Any], float]:
    manifest = json.loads((FIXTURES / "score-manifest.json").read_text(encoding="utf-8"))
    audio = FIXTURES / "audio" / name
    if not audio.is_file():
        pytest.skip("generate the fixtures with tests/fixtures/generate_score_fixtures.py")
    return audio, manifest["fixtures"][name], manifest["onsetToleranceSeconds"]


def _grid(facts: dict[str, Any]) -> BeatGridTempo:
    grid = {**facts["grid"], "kind": "beat-grid"}
    return BeatGridTempo.model_validate(grid)


def _cleaned(notes: tuple[NoteEvent, ...], facts: dict[str, Any]) -> list[tuple[float, int]]:
    """Clean and quantise like the notator, then map the snapped onsets back to seconds."""
    grid = _grid(facts)
    base = TimeBase.from_grid(grid, facts["durationSeconds"])
    placed = clean_notes(notes, get_profile(facts["profile"]), base.quantiser("sixteenth")).notes
    period, first = 60 / grid.bpm, grid.downbeats[0]
    return [(first + float(Fraction(n.onset)) * period, n.pitch) for n in placed]


def _f1(
    reference: list[tuple[float, int]], estimate: list[tuple[float, int]], tolerance: float
) -> float:
    unused = sorted(estimate)
    matched = 0
    for start, pitch in sorted(reference):
        hit = next((e for e in unused if e[1] == pitch and abs(e[0] - start) <= tolerance), None)
        if hit is not None:
            unused.remove(hit)
            matched += 1
    if not matched:
        return 0.0
    precision, recall = matched / len(estimate), matched / len(reference)
    return 2 * precision * recall / (precision + recall)


def _store() -> ModelStore:
    return ModelStore(load_settings(os.environ).model_dir)


def _measure(transcriber: Any, name: str) -> None:
    audio, facts, tolerance = _fixture(name)
    started = time.perf_counter()
    result = transcriber.transcribe(audio, None, lambda fraction: None)
    seconds = time.perf_counter() - started
    reference = [(n["start"], n["pitch"]) for n in facts["notes"]]
    score = _f1(reference, _cleaned(result.notes, facts), tolerance)
    per_second = seconds / facts["durationSeconds"]
    print(f"{name}: F1 {score:.3f} ({len(result.notes)} raw notes), {per_second:.3f} s per audio s")  # noqa: T201
    assert score >= facts["targetF1"]


@pytest.mark.integration
def test_basic_pitch_reaches_f1_090_on_the_single_voice_bass_line() -> None:
    _measure(BasicPitchTranscriber(_store(), threads=8), "score_bass_line_120bpm.wav")


@pytest.mark.integration
def test_the_piano_crnn_reaches_f1_085_on_the_polyphonic_texture() -> None:
    settings = load_settings({**os.environ, "STEMSTUDIO_SEPARATOR": "demucs"})
    transcriber = PianoTranscriber(_store(), create_device_runner(settings), TorchRuntime(8))
    _measure(transcriber, "score_piano_texture_20s.wav")
