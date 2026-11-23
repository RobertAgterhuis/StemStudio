"""SC-001 timing (T103, integration): 5-minute song, 6 + 12 detail stems, <= 30 s on CPU."""

import time
from pathlib import Path

import pytest

from stemstudio_worker.insights.analyzer import SignalQualityAnalyzer
from stemstudio_worker.ports import QualityInputs, StemAudio
from stemstudio_worker.schemas import DetailParentInput

from .synthetic import FAMILIES, arranged_song, write_stem

SECONDS = 300.0
BUDGET_SECONDS = 30.0
DETAIL = {
    "drums": ("kick", "snare", "toms", "hihat", "cymbals", "rest"),
    "vocals": ("lead", "backing", "rest"),
    "other": ("strings", "keys", "rest"),
}


@pytest.mark.integration
def test_five_minute_song_with_eighteen_stems_within_30_seconds(tmp_path: Path) -> None:
    song = arranged_song(seconds=SECONDS)
    stems = [
        StemAudio(name, "standard", write_stem(tmp_path / f"{name}.flac", song[name]))
        for name in FAMILIES
    ]
    for parent, children in DETAIL.items():
        share = 1.0 / len(children)
        for child in children:
            kind = "rest" if child == "rest" else "detail"
            path = write_stem(tmp_path / f"{parent}.{child}.flac", share * song[parent])
            stems.append(StemAudio(f"{parent}.{child}", kind, path))
    parents = tuple(
        DetailParentInput(parent=p, model_residual_db=-25.0, reconstruction_error_db=-170.0)
        for p in DETAIL
    )
    inputs = QualityInputs(stems=tuple(stems), detail_parents=parents)
    assert len(stems) == 18

    started = time.perf_counter()
    result = SignalQualityAnalyzer().analyze(inputs, lambda _: None)
    elapsed = time.perf_counter() - started

    print(f"SC-001: {len(stems)} stems of {SECONDS:.0f} s analysed in {elapsed:.1f} s")  # noqa: T201
    assert len(result.stems) == 18
    assert elapsed <= BUDGET_SECONDS
