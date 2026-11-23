"""Gate G1 harness (T039, research.md R-04 V2): our indices against BSS Eval SIR and SAR.

Per track: separate the mixture into the six standard stems (production: ``htdemucs_6s``), run
the signal analyzer, score the estimates against the four MUSDB18-HQ sources (guitar and piano
are summed into ``other``), then compute per stem type the Spearman correlation of our bleed
index with -SIR and of our artefact index with -SAR. Gate: rho >= 0.5 (bleed) and >= 0.4
(artefacts) -> ``passed``, else ``failed``; fewer than ``MIN_TRACKS`` tracks -> ``not-measured``.

The separator and the scorer are injected: the real run (``test_g1_validation.py``, marked
integration) uses Demucs and ``museval`` 0.4.1 in a throw-away environment through
``g1_museval.py``; the default suite runs a synthetic two-track stand-in end to end. Audio and
scores are written only into the given work folder, never into the repository.
"""

import json
import subprocess
from collections.abc import Callable, Iterable, Mapping
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.stats import spearmanr

from stemstudio_worker.insights.analyzer import SignalQualityAnalyzer
from stemstudio_worker.ports import QualityInputs, StemAudio
from stemstudio_worker.schemas import STANDARD_STEMS, QualityResult

SOURCES = ("vocals", "drums", "bass", "other")
"""MUSDB18-HQ sources = the stem types G1 can validate."""

GATES = {"bleed": 0.5, "artefacts": 0.4}
MIN_TRACKS = 10

Audio = np.ndarray
Separator = Callable[[Audio, int], dict[str, Audio]]
"""(stereo mixture, rate) -> the six standard stems."""
Scorer = Callable[[Mapping[str, Audio], Mapping[str, Audio], int], dict[str, tuple[float, float]]]
"""(estimates, references, rate) -> per source (SIR, SAR) in dB."""


@dataclass(frozen=True, slots=True)
class Track:
    """One MUSDB-style track: the mixture and its four sources (stereo float arrays)."""

    name: str
    mixture: Audio
    sources: dict[str, Audio]
    rate: int


@dataclass(slots=True)
class G1Report:
    """Per stem type and dimension: the paired values, rho and the gate outcome."""

    pairs: dict[tuple[str, str], list[tuple[float, float]]] = field(default_factory=dict)

    def add(self, source: str, dimension: str, ours: float, negated_reference: float) -> None:
        self.pairs.setdefault((source, dimension), []).append((ours, negated_reference))

    def rho(self, source: str, dimension: str) -> float:
        values = self.pairs.get((source, dimension), [])
        if len(values) < 2:
            return float("nan")
        ours, reference = zip(*values, strict=True)
        if len(set(ours)) < 2 or len(set(reference)) < 2:
            return float("nan")  # no ranking possible (e.g. every artefact index 0)
        return float(spearmanr(ours, reference).statistic)

    def status(self, source: str, dimension: str) -> str:
        if len(self.pairs.get((source, dimension), [])) < MIN_TRACKS:
            return "not-measured"
        rho = self.rho(source, dimension)
        return "passed" if np.isfinite(rho) and rho >= GATES[dimension] else "failed"

    def markdown(self) -> str:
        rows = [
            "| Stem type | Dimension | Tracks | Spearman rho | Gate | Status |",
            "|---|---|---|---|---|---|",
        ]
        for source in SOURCES:
            for dimension, gate in GATES.items():
                count = len(self.pairs.get((source, dimension), []))
                rho = self.rho(source, dimension)
                status = self.status(source, dimension)
                rows.append(
                    f"| {source} | {dimension} | {count} | {rho:.2f} | >= {gate} | {status} |"
                )
        return "\n".join(rows)


def _write(folder: Path, stems: Mapping[str, Audio], rate: int) -> dict[str, Path]:
    folder.mkdir(parents=True, exist_ok=True)
    paths = {}
    for name, audio in stems.items():
        paths[name] = folder / f"{name}.flac"
        sf.write(paths[name], np.clip(audio, -1.0, 1.0), rate, format="FLAC", subtype="PCM_24")
    return paths


def _analyse(paths: Mapping[str, Path]) -> QualityResult:
    stems = tuple(StemAudio(name, "standard", paths[name]) for name in STANDARD_STEMS)
    return SignalQualityAnalyzer().analyze(
        QualityInputs(stems=stems, detail_parents=()), lambda _: None
    )


def _our_indices(
    result: QualityResult, stems: Mapping[str, Audio]
) -> dict[str, tuple[float, float]]:
    """(bleed, artefact) index per MUSDB source; ``other`` = energy-weighted guitar/piano/other."""
    by_channel = {stem.channel: stem for stem in result.stems}
    out = {
        name: (by_channel[name].bleed.index, by_channel[name].artefacts.index)
        for name in SOURCES[:3]
    }
    parts = ("guitar", "piano", "other")
    weights = np.array([float(np.sum(stems[name] ** 2)) for name in parts]) + 1e-12
    bleed = np.array([by_channel[name].bleed.index for name in parts])
    artefacts = np.array([by_channel[name].artefacts.index for name in parts])
    out["other"] = (
        float(np.average(bleed, weights=weights)),
        float(np.average(artefacts, weights=weights)),
    )
    return out


def run_g1(tracks: Iterable[Track], separate: Separator, score: Scorer, workdir: Path) -> G1Report:
    """Run the G1 protocol over ``tracks``; audio goes only into ``workdir``."""
    report = G1Report()
    for track in tracks:
        stems = separate(track.mixture, track.rate)
        result = _analyse(_write(workdir / track.name, stems, track.rate))
        estimates = {name: stems[name] for name in SOURCES[:3]}
        estimates["other"] = stems["guitar"] + stems["piano"] + stems["other"]
        scores = score(estimates, track.sources, track.rate)
        for source, (bleed, artefacts) in _our_indices(result, stems).items():
            sir, sar = scores[source]
            report.add(source, "bleed", bleed, -sir)
            report.add(source, "artefacts", artefacts, -sar)
    return report


def bss_lite(
    estimates: Mapping[str, Audio], references: Mapping[str, Audio], _rate: int
) -> dict[str, tuple[float, float]]:
    """Stand-in scorer: BSS Eval decomposition with 0-tap projections (stand-in runs only)."""
    names = list(references)
    basis = np.stack([references[name].mean(axis=-1) for name in names])
    gram = basis @ basis.T + 1e-9 * np.eye(len(names))
    scores = {}
    for index, name in enumerate(names):
        estimate = estimates[name].mean(axis=-1)
        weights = np.linalg.solve(gram, basis @ estimate)
        target = weights[index] * basis[index]
        interference = weights @ basis - target
        artefact = estimate - target - interference
        sir = 10 * np.log10(np.sum(target**2) / (np.sum(interference**2) + 1e-12) + 1e-12)
        sar = 10 * np.log10(
            np.sum((target + interference) ** 2) / (np.sum(artefact**2) + 1e-12) + 1e-12
        )
        scores[name] = (float(sir), float(sar))
    return scores


def museval_scorer(python: Path, workdir: Path) -> Scorer:
    """Scorer that runs ``g1_museval.py`` with ``museval`` 0.4.1 in a throw-away environment."""
    script = Path(__file__).with_name("g1_museval.py")

    def score(
        estimates: Mapping[str, Audio], references: Mapping[str, Audio], rate: int
    ) -> dict[str, tuple[float, float]]:
        folder = workdir / "museval"
        est, ref = _write(folder / "est", estimates, rate), _write(folder / "ref", references, rate)
        command = [
            str(python),
            "-I",
            str(script),
            str(ref["vocals"].parent),
            str(est["vocals"].parent),
        ]
        # Trusted: our own script and a python the owner names; no shell.
        output = subprocess.run(command, check=True, capture_output=True, text=True)  # noqa: S603
        return {name: (values[0], values[1]) for name, values in json.loads(output.stdout).items()}

    return score


def read_musdb_test_split(root: Path) -> Iterable[Track]:
    """Tracks of a MUSDB18-HQ ``test`` folder (``<track>/{mixture,<source>}.wav``)."""
    for folder in sorted(path for path in root.iterdir() if path.is_dir()):
        mixture, rate = sf.read(folder / "mixture.wav", dtype="float32", always_2d=True)
        sources = {
            name: sf.read(folder / f"{name}.wav", dtype="float32", always_2d=True)[0]
            for name in SOURCES
        }
        yield Track(folder.name, mixture, sources, int(rate))
