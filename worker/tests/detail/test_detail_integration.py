"""The detail integration test: the three real detail models on a 10 s synthetic band (T050).

Run with ``uv run pytest -m integration tests/detail``. Weights (~1.86 GB) are downloaded on first
use into ``STEMSTUDIO_MODEL_DIR`` (default ``worker/data/models``, git-ignored) and verified with
SHA-256. Offline, the test is skipped locally but FAILS when ``CI=true``. This test also covers
the vendored MSST code, which is excluded from the unit-coverage gate (plan.md Complexity
Tracking).
"""

import os
import time
from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail import create_detail_pipeline
from stemstudio_worker.devices.runner import CpuRunner
from stemstudio_worker.detail.audio_io import read_stem
from stemstudio_worker.detail.gating import read_parent, relative_db, sub_stem_audible
from stemstudio_worker.detail.names import REST
from stemstudio_worker.ports import DetailAudio, DetailModelUnavailableError
from stemstudio_worker.schemas import DetailModelId, ParentOutcome, ParentOutcomeKind, StemName

from .models import write_stems

RATE = 44_100
SECONDS = 10.0
K = ParentOutcomeKind
EXPECTED_MODELS: dict[str, DetailModelId] = {
    "drums": "mdx23c-drumsep-6s", "vocals": "uvr-mdxnet-kara-2",
    "guitar": "mvsep-mega-53", "other": "mvsep-mega-53",
}  # fmt: skip
SPLIT_CANDIDATES: tuple[StemName, ...] = ("drums", "vocals", "guitar", "other")


def _model_dir() -> Path:
    default = Path(__file__).resolve().parents[2] / "data" / "models"
    return Path(os.environ.get("STEMSTUDIO_MODEL_DIR", str(default)))


def _t() -> np.ndarray:
    return np.arange(int(SECONDS * RATE)) / RATE


def _stereo(mono: np.ndarray, peak: float) -> DetailAudio:
    mono = mono / max(float(np.max(np.abs(mono))), 1e-9) * peak
    return np.stack([mono, mono]).astype(np.float32)


def _hits(period: float, offset: float, decay: float) -> np.ndarray:
    t = _t()
    since = (t - offset) % period
    return np.exp(-since / decay) * (t >= offset)


def _drums(rng: np.random.Generator) -> DetailAudio:
    t = _t()
    kick = np.sin(2 * np.pi * (50 + 60 * _hits(0.5, 0.0, 0.03)) * t) * _hits(0.5, 0.0, 0.12)
    snare = (rng.normal(size=t.size) * 0.6 + np.sin(2 * np.pi * 190 * t)) * _hits(1.0, 0.5, 0.08)
    hihat = np.diff(rng.normal(size=t.size + 1)) * _hits(0.25, 0.125, 0.02)
    return _stereo(kick + 0.8 * snare + 0.4 * hihat, 0.6)


def _vocals() -> DetailAudio:
    t = _t()
    pitch = 220 * (1 + 0.01 * np.sin(2 * np.pi * 5 * t))
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    voice = sum(np.sin(k * phase) / k for k in range(1, 8))
    phrases = (np.sin(2 * np.pi * 0.25 * t) > -0.6).astype(float)
    return _stereo(voice * phrases, 0.5)


def _saw(freq: float) -> np.ndarray:
    return 2 * ((_t() * freq) % 1.0) - 1


def _band() -> dict[StemName, DetailAudio]:
    rng = np.random.default_rng(7)
    t = _t()
    swell = np.minimum(1.0, t / 2.0)
    return {
        "vocals": _vocals(),
        "drums": _drums(rng),
        "bass": _stereo(np.sin(2 * np.pi * 55 * t), 0.5),
        "guitar": _stereo(_saw(110) + _saw(165) + _saw(220), 0.4),
        "piano": np.zeros((2, t.size), dtype=np.float32),
        "other": _stereo((np.sin(2 * np.pi * 440 * t) + np.sin(2 * np.pi * 660 * t)) * swell, 0.4),
    }


def _assert_split(outcome: ParentOutcome, parent: DetailAudio, folder: Path) -> None:
    assert outcome.stems[-1].name == REST
    written = [read_stem(folder / f"{s.name}.flac") for s in outcome.stems]
    assert all(w.shape == parent.shape for w in written)
    assert relative_db(parent - np.sum(written, axis=0, dtype=np.float32), parent) <= -30
    assert outcome.metrics.reconstruction_error_db is not None
    assert outcome.metrics.reconstruction_error_db <= -30
    reading = read_parent(parent)
    assert all(sub_stem_audible(w, parent, reading) for w in written[:-1])


def _run(stems: dict[StemName, Path], partial: Path) -> dict[StemName, ParentOutcome]:
    """Plan and run every step with the real models; skip offline (fail in CI)."""
    settings = WorkerSettings(data_dir=partial.parent, separator="demucs", model_dir=_model_dir())
    pipeline = create_detail_pipeline(settings, CpuRunner())
    started = time.monotonic()
    timings: dict[str, float] = {}
    try:
        for step in pipeline.plan(stems, partial):
            step_started = time.monotonic()
            step.run(lambda fraction: None)
            timings["+".join(step.parents)] = round(time.monotonic() - step_started, 1)
    except DetailModelUnavailableError as exc:
        if os.environ.get("CI", "").lower() == "true":
            pytest.fail(f"Detail models unavailable in CI: {exc}")
        pytest.skip(f"Detail models unavailable (offline?): {exc}")
    print(f"detail wall time {time.monotonic() - started:.1f}s, per step {timings}")  # noqa: T201
    return {o.parent: o for o in pipeline.outcomes(tuple(stems), partial)}


def _assert_parent(outcome: ParentOutcome, parent: DetailAudio, folder: Path) -> None:
    print(outcome.parent, outcome.outcome.value, [s.name for s in outcome.stems],  # noqa: T201
          outcome.metrics)  # fmt: skip
    assert outcome.model == EXPECTED_MODELS[outcome.parent]
    if outcome.outcome is K.SPLIT:
        _assert_split(outcome, parent, folder)
    else:
        assert outcome.stems == []


@pytest.mark.integration
@pytest.mark.timeout(3600)
def test_real_detail_models_split_a_synthetic_band(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", _band())
    outcomes = _run(stems, tmp_path / "partial")

    assert outcomes["bass"].outcome is K.NOT_SUPPORTED
    assert outcomes["piano"].outcome is K.SKIPPED_SILENT
    for parent in SPLIT_CANDIDATES:
        _assert_parent(outcomes[parent], read_stem(stems[parent]), tmp_path / "partial" / parent)
    assert any(o.outcome is K.SPLIT for o in outcomes.values())
