"""The detail pipeline as seen by the job runner (implemented by ``detail.steps``).

``jobs`` never imports ``detail`` (plan.md dependency direction); the composition root passes a
pipeline that satisfies these protocols structurally.
"""

from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Protocol

from stemstudio_worker.ports import DeviceUse
from stemstudio_worker.schemas import ParentOutcome, ParentStemName, StemName

StepProgress = Callable[[float], None]
"""Receives the completed fraction of a step; may raise ``DetailCancelledError``."""


class DetailStepUnit(Protocol):
    """One unit of a detail job: splits its parents and checkpoints them."""

    @property
    def parents(self) -> tuple[ParentStemName, ...]:
        """The parents split by this step."""
        ...

    @property
    def weight(self) -> float:
        """Share of the job's cost (renormalised by the runner)."""
        ...

    def run(self, progress: StepProgress) -> DeviceUse | None:
        """Run the step, reporting its fraction to ``progress``; returns where it ran."""
        ...


class DetailPipelinePort(Protocol):
    """Plans the steps of one audio and reads their checkpointed outcomes."""

    @property
    def separator_version(self) -> str:
        """Version of the pipeline; a cached result of another version is recomputed."""
        ...

    def plan(self, stems: Mapping[StemName, Path], partial: Path) -> Sequence[DetailStepUnit]:
        """Checkpoint trivial outcomes and return the steps still to run."""
        ...

    def outcomes(self, names: Sequence[StemName], partial: Path) -> list[ParentOutcome]:
        """Return the checkpointed outcomes of ``names`` in that order."""
        ...
