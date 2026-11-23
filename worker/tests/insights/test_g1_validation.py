"""Gate G1 (T039): the synthetic two-track stand-in end to end, and the real run (integration).

Real run (local only, never in CI, data never committed)::

    STEMSTUDIO_MUSDB_DIR=<MUSDB18-HQ>/test \
    STEMSTUDIO_MUSEVAL_PYTHON=<throw-away env with museval==0.4.1>/python \
    STEMSTUDIO_G1_OUT=<scratch folder outside the repository> \
    uv run pytest tests/insights/test_g1_validation.py -m integration -s

It prints the results table; the reliability table in ``insights/thresholds.py`` is then set by
hand from it (a changed table bumps ``ANALYZER_VERSION``).
"""

import os
from pathlib import Path

import numpy as np
import pytest

from . import g1
from .synthetic import SR, arranged_song, inject_bleed


def _stand_in_tracks() -> list[g1.Track]:
    tracks = []
    for seed in (1, 2):
        song = arranged_song(seed=seed, seconds=15.0)
        sources = {"vocals": song["vocals"], "drums": song["drums"], "bass": song["bass"],
                   "other": song["guitar"] + song["piano"] + song["other"]}  # fmt: skip
        stereo = {name: np.stack([audio, audio], axis=1) for name, audio in sources.items()}
        tracks.append(g1.Track(f"stand-in-{seed}", sum(stereo.values()), stereo, SR))
    return tracks


def _stand_in_separator(seed_alpha: dict[int, float]) -> g1.Separator:
    """Returns the true sources with a known amount of drums bleed in the vocals."""
    song_by_mix: dict[int, dict[str, np.ndarray]] = {}

    def separate(mixture: np.ndarray, _rate: int) -> dict[str, np.ndarray]:
        seed = 1 if not song_by_mix else 2
        song = arranged_song(seed=seed, seconds=15.0)
        song_by_mix[seed] = song
        stems = dict(song)
        stems["vocals"] = inject_bleed(song["vocals"], song["drums"], seed_alpha[seed])
        assert mixture.shape[0] == len(song["vocals"])
        return {name: np.stack([audio, audio], axis=1) for name, audio in stems.items()}

    return separate


def test_stand_in_runs_end_to_end(tmp_path: Path) -> None:
    report = g1.run_g1(
        _stand_in_tracks(), _stand_in_separator({1: 0.03, 2: 0.3}), g1.bss_lite, tmp_path
    )
    table = report.markdown()
    for source in g1.SOURCES:
        for dimension in g1.GATES:
            assert len(report.pairs[(source, dimension)]) == 2
            assert report.status(source, dimension) == "not-measured"  # 2 < MIN_TRACKS
            assert f"| {source} | {dimension} | 2 |" in table
    # More drums bleed in the vocals of track 2: a higher index and a lower SIR.
    assert report.rho("vocals", "bleed") == pytest.approx(1.0)
    assert all(path.is_relative_to(tmp_path) for path in tmp_path.rglob("*"))


def test_gate_outcomes() -> None:
    report = g1.G1Report()
    for index in range(g1.MIN_TRACKS):
        report.add("vocals", "bleed", index, index)
        report.add("drums", "bleed", index, -index)
    assert report.status("vocals", "bleed") == "passed"
    assert report.status("drums", "bleed") == "failed"
    assert np.isnan(report.rho("bass", "bleed"))
    assert report.status("bass", "artefacts") == "not-measured"


def test_bss_lite_separates_interference_from_artefacts() -> None:
    rng = np.random.default_rng(0)
    a, b = rng.standard_normal((2, SR, 1))
    clean = g1.bss_lite({"a": a, "b": b}, {"a": a, "b": b}, SR)
    leaky = g1.bss_lite({"a": a + 0.3 * b, "b": b}, {"a": a, "b": b}, SR)
    noisy = g1.bss_lite({"a": a + 0.3 * rng.standard_normal((SR, 1)), "b": b}, {"a": a, "b": b}, SR)
    assert leaky["a"][0] == pytest.approx(10.5, abs=0.3)  # SIR of 0.3 amplitude bleed
    assert noisy["a"][0] > 40.0
    assert noisy["a"][1] == pytest.approx(10.5, abs=0.3)  # SAR of 0.3 amplitude noise
    assert clean["a"][1] > 40.0


@pytest.mark.integration
def test_g1_on_musdb18_hq(tmp_path: Path) -> None:  # pragma: no cover - needs the dataset
    root = os.environ.get("STEMSTUDIO_MUSDB_DIR")
    python = os.environ.get("STEMSTUDIO_MUSEVAL_PYTHON")
    if not root or not python:
        pytest.skip("pending dataset: set STEMSTUDIO_MUSDB_DIR and STEMSTUDIO_MUSEVAL_PYTHON")
    from demucs.api import Separator  # noqa: PLC0415 - heavy, only for the real run

    model = Separator(model="htdemucs_6s")
    workdir = Path(os.environ.get("STEMSTUDIO_G1_OUT", tmp_path))

    def separate(mixture: np.ndarray, rate: int) -> dict[str, np.ndarray]:
        import torch  # noqa: PLC0415

        _, stems = model.separate_tensor(torch.from_numpy(mixture.T.copy()), rate)
        return {name: stems[name].numpy().T for name in stems}

    report = g1.run_g1(
        g1.read_musdb_test_split(Path(root)),
        separate,
        g1.museval_scorer(Path(python), workdir),
        workdir,
    )
    print(report.markdown())  # noqa: T201
