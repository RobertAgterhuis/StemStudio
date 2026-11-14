"""The single integration test: real Demucs on a short synthetic clip (T060).

Run with ``uv run pytest -m integration``. Weights download on first use (set ``HF_HOME`` and
``TORCH_HOME`` to a persistent cache). When the model cannot be loaded the test is skipped
locally but FAILS when ``CI=true``, so CI can never pass without exercising the model (K5).
"""

import os
import time
from pathlib import Path

import pytest
import soundfile as sf

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import SeparationError, SeparationResult
from stemstudio_worker.separation import create_separator

from .conftest import RATE, synth_mix

EXPECTED_STEMS = {
    "htdemucs_6s": ["vocals", "drums", "bass", "guitar", "piano", "other"],
    "htdemucs": ["vocals", "drums", "bass", "other"],
}


@pytest.mark.integration
@pytest.mark.timeout(1800)
def test_real_demucs_separates_synthetic_clip_into_aligned_stems(tmp_path: Path) -> None:
    wav = tmp_path / "clip.wav"
    sf.write(wav, synth_mix(6.0), RATE, subtype="PCM_16")
    (tmp_path / "stems").mkdir()
    fractions: list[float] = []

    started = time.monotonic()
    result = _separate_or_fail(wav, tmp_path / "stems", fractions)
    print(f"model={result.model} elapsed={time.monotonic() - started:.1f}s")  # noqa: T201

    assert list(result.stems) == EXPECTED_STEMS[result.model]
    for path in result.stems.values():
        _assert_aligned_flac(path, sf.info(wav).frames)
    assert fractions == sorted(fractions)
    assert fractions[-1] == 1.0


def _separate_or_fail(wav: Path, out_dir: Path, fractions: list[float]) -> SeparationResult:
    """Run the real separator; a missing model skips locally and fails when CI=true."""
    separator = create_separator(WorkerSettings(data_dir=out_dir, separator="demucs"))
    try:
        return separator.separate(wav, out_dir, lambda f, s: fractions.append(f))
    except SeparationError as exc:
        if os.environ.get("CI", "").lower() == "true":
            pytest.fail(f"Demucs model unavailable in CI: {exc}")
        pytest.skip(f"Demucs model unavailable (offline?): {exc}")


def _assert_aligned_flac(path: Path, frames: int) -> None:
    info = sf.info(path)
    assert (info.format, info.subtype) == ("FLAC", "PCM_16")
    assert (info.samplerate, info.channels, info.frames) == (RATE, 2, frames)
