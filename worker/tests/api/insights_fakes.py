"""Consumer-driven fakes of the ``insights`` ports (data-model 2.6, contract 1.11.0 examples).

``FakeQualityAnalyzer`` returns the fixed ratings of the fake analyzer of data-model 2.6 (the
beat-analysis lane builds the real one); ``FakeHandoutRenderer`` returns a small PDF with the
outline of the ``CompletedHandoutJob`` example. Both can be held at a gate and made to fail.
"""

import threading
from collections.abc import Mapping, Sequence
from pathlib import Path

from stemstudio_worker.ports import (
    InsightsProgress,
    QualityInputs,
    RenderedHandout,
)
from stemstudio_worker.schemas import (
    ArtefactEstimate,
    BleedEstimate,
    HandoutJobRequest,
    HandoutWarning,
    QualityResult,
    StemQuality,
)

WAIT = 5.0
TRACK = "7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f"
STEMS = ("vocals", "drums", "bass", "guitar", "piano", "other")
PDF = b"%PDF-1.7 fake handout"
OUTLINE = ("Example Song", "Form", "Chord chart", "Notation - Bass - tablature",
           "Notation - Bass - score", "Practice notes", "About this handout")  # fmt: skip

_RATINGS: dict[str, tuple[str, str, str, str | None, str, list[str]]] = {
    # channel: rating, reason, bleed level, main source, artefact level, kinds (data-model 2.6)
    "vocals": ("fair", "bleed", "medium", "drums", "low", []),
    "drums": ("good", "clean", "low", None, "low", []),
    "bass": ("good", "clean", "low", None, "low", []),
    "guitar": ("poor", "artefacts", "low", None, "high", ["flicker"]),
    "piano": ("not-present", "not-present", "high", "guitar", "low", []),
    "other": ("fair", "bleed", "medium", "guitar", "low", []),
    "detail": ("good", "clean", "low", None, "low", []),
    "rest": ("fair", "artefacts", "low", None, "medium", ["rest-share"]),
}


def fake_quality(channel: str, kind: str) -> StemQuality:
    """The fixed estimate of ``channel`` (data-model 2.6)."""
    key = channel if kind == "standard" else kind
    rating, reason, bleed, source, artefacts, kinds = _RATINGS[key]
    return StemQuality.model_validate({
        "channel": channel, "rating": rating, "ratingReason": reason, "activeSeconds": 120.0,
        "activeShare": 0.02 if rating == "not-present" else 0.7, "levelDb": -12.0,
        "bleed": BleedEstimate.model_validate({"level": bleed, "index": 0.2, "mainSource": source,
                                               "coOccurrence": False, "reliability": "indicative"}),
        "artefacts": ArtefactEstimate.model_validate({"level": artefacts, "index": 0.3,
                                                      "kinds": kinds, "reliability": "indicative"}),
    })  # fmt: skip


class _Gated:
    """Shared gate, progress script and failure injection of the fakes."""

    def __init__(self, fractions: Sequence[float]) -> None:
        self.gate, self.entered = threading.Event(), threading.Event()
        self.gate.set()
        self.fractions = list(fractions)
        self.error: Exception | None = None

    def _run(self, progress: InsightsProgress) -> None:
        for fraction in self.fractions:
            progress(fraction)
            self.entered.set()
            assert self.gate.wait(WAIT)
            self.gate.set()
        if self.error is not None:
            raise self.error


class FakeQualityAnalyzer(_Gated):
    """``QualityAnalyzer`` with the fixed ratings of the fake analyzer (data-model 2.6)."""

    version = "fake-1"

    def __init__(self) -> None:
        super().__init__([0.1, 0.5, 0.95])
        self.calls: list[QualityInputs] = []

    def analyze(self, inputs: QualityInputs, progress: InsightsProgress) -> QualityResult:
        self.calls.append(inputs)
        self._run(progress)
        stems = [fake_quality(stem.channel, stem.kind) for stem in inputs.stems]
        return QualityResult(analyzer_version=self.version, duration_seconds=0.5, stems=stems)


class FakeHandoutRenderer(_Gated):
    """``HandoutRenderer`` returning ``PDF`` with the example outline (contract 1.11.0)."""

    def __init__(self) -> None:
        super().__init__([0.1, 0.7, 0.95])
        self.calls: list[tuple[HandoutJobRequest, Mapping[str, Path]]] = []
        self.warnings: tuple[HandoutWarning, ...] = ()

    def render(self, request: HandoutJobRequest, files: Mapping[str, Path],
               progress: InsightsProgress) -> RenderedHandout:  # fmt: skip
        self.calls.append((request, dict(files)))
        self._run(progress)
        return RenderedHandout(pdf=PDF, pages=5, outline=OUTLINE, warnings=self.warnings)


def write_track(root: Path) -> None:
    """The six stems and a drums split (kick, rest) of the example track below ``root``."""
    for name in STEMS:
        _touch(root / f"tracks/{TRACK}/stems/{name}.flac")
    for name in ("kick", "rest"):
        _touch(root / f"tracks/{TRACK}/detail/drums/{name}.flac")


def _touch(path: Path, data: bytes = b"fLaC") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
