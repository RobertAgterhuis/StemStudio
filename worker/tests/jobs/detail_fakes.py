"""A fake detail pipeline for the runner tests (writes tiny files as checkpoints)."""

from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

from stemstudio_worker.schemas import (
    DetailStemInfo,
    OutcomeMetrics,
    ParentOutcome,
    ParentOutcomeKind,
    ParentStemName,
    StemName,
)

METRICS = OutcomeMetrics(parent_level_dbfs=-20.0, active_share=0.9)


@dataclass
class FakeStep:
    """Writes ``<partial>/<parent>/{kick,rest}.flac``; may fail or report progress first."""

    parents: tuple[ParentStemName, ...]
    weight: float
    partial: Path
    pipeline: "FakePipeline"

    def run(self, progress: Callable[[float], None]) -> None:
        self.pipeline.runs.append(self.parents)
        for fraction in (0.0, 0.5, 1.0):
            progress(fraction)
            hook = self.pipeline.during.get(self.parents[0])
            if hook is not None and fraction == 0.5:
                hook()
        error = self.pipeline.errors.get(self.parents[0])
        if error is not None:
            raise error
        for parent in self.parents:
            folder = self.partial / parent
            folder.mkdir(parents=True, exist_ok=True)
            for name in ("kick", "rest"):
                (folder / f"{name}.flac").write_bytes(b"fLaC" + name.encode())


@dataclass
class FakePipeline:
    """Plans one step per entry of ``groups``; ``errors``/``during`` hook into a step by parent."""

    groups: list[tuple[ParentStemName, ...]] = field(
        default_factory=lambda: [("drums",), ("vocals",)]
    )
    weights: list[float] = field(default_factory=lambda: [0.2, 0.05])
    separator_version: str = "1"
    errors: dict[str, BaseException] = field(default_factory=dict)
    during: dict[str, Callable[[], None]] = field(default_factory=dict)
    plans: list[Mapping[StemName, Path]] = field(default_factory=list)
    runs: list[tuple[ParentStemName, ...]] = field(default_factory=list)

    def plan(self, stems: Mapping[StemName, Path], partial: Path) -> list[FakeStep]:
        self.plans.append(dict(stems))
        partial.mkdir(parents=True, exist_ok=True)
        return [
            FakeStep(g, w, partial, self) for g, w in zip(self.groups, self.weights, strict=True)
        ]

    def outcomes(self, names: Sequence[StemName], partial: Path) -> list[ParentOutcome]:
        return [_outcome(name, partial) for name in names]


def _outcome(name: StemName, partial: Path) -> ParentOutcome:
    folder = partial / name
    if not folder.is_dir():
        return ParentOutcome(parent=name, outcome=ParentOutcomeKind.NOT_SUPPORTED, model=None,
                             stems=[], metrics=METRICS)  # fmt: skip
    stems = [
        DetailStemInfo(name=n, path=f"{name}/{n}", size_bytes=(folder / f"{n}.flac").stat().st_size)
        for n in ("kick", "rest")
    ]
    return ParentOutcome(parent=name, outcome=ParentOutcomeKind.SPLIT, model="fake-detail",
                         stems=stems, metrics=METRICS)  # fmt: skip
