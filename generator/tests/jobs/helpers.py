"""Test doubles for the job tests: jobs, a recording gate, engine and handler."""

from collections.abc import Callable, Iterator, Sequence
from contextlib import contextmanager
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any

from stemstudio_generator.devices.oom import is_gpu_oom
from stemstudio_generator.jobs.runner import JobOutcome
from stemstudio_generator.ports import (
    RESTYLE_NEEDS,
    DeviceUse,
    GpuNeed,
    ProgressCallback,
    WaitCallback,
)
from stemstudio_generator.schemas import (
    GeneratedTake,
    Generation,
    GenerationKind,
    JobDevice,
    ModelStatus,
    RestyleParams,
    TakeModel,
)

from ..fakes import START

RESTYLE = RestyleParams(
    caption="jazz trio, brushed drums",
    audio_cover_strength=0.5,
    seeds=[7],
    mode="range",
    range_start_seconds=0.0,
    range_end_seconds=30.0,
)

TAKE = GeneratedTake(
    index=1,
    seed=7,
    duration_seconds=30.0,
    sha256="a" * 64,
    bytes=1024,
    peak_dbfs=-1.0,
    model=TakeModel(id="acestep-v15-turbo", revision="19671f4"),
)

GPU = JobDevice(kind="gpu", name="Fake GPU")


def make_job(
    number: int = 1, created_at: datetime = START, kind: GenerationKind = "restyle"
) -> Generation:
    """A queued restyle job with a predictable id."""
    return Generation(
        generation_id=f"{number:032x}",
        kind=kind,
        status="queued",
        progress=0,
        stage="Queued",
        restyle=RESTYLE,
        created_at=created_at,
        updated_at=created_at,
    )


@dataclass
class RecordingGate:
    """``GpuGate`` that admits ``needs[0]`` (or ``force``), optionally reporting waits first."""

    waits: list[str] = field(default_factory=list)
    events: list[str] = field(default_factory=list)
    requested: list[tuple[str, ...]] = field(default_factory=list)
    error: BaseException | None = None

    @contextmanager
    def hold(self, needs: Sequence[GpuNeed], on_wait: WaitCallback) -> Iterator[DeviceUse]:
        self.requested.append(tuple(need.name for need in needs))
        for stage in self.waits:
            on_wait(stage)
        if self.error is not None:
            raise self.error
        self.events.append("acquire")
        try:
            yield DeviceUse(need=needs[0], device=GPU, free_memory_mib=14_800)
        finally:
            self.events.append("release")

    def is_out_of_memory(self, error: BaseException) -> bool:
        return is_gpu_oom(error)


@dataclass
class FakeEngine:
    """``ModelHost`` recording loads and unloads."""

    present: bool = True
    loads: list[str] = field(default_factory=list)
    unloads: int = 0
    load_progress: list[tuple[float, str]] = field(default_factory=list)

    def required_models(self) -> list[ModelStatus]:
        return [
            ModelStatus(
                id="acestep-v15-turbo", present=self.present, bytes=4_700_000_000, revision="r"
            )
        ]

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        self.loads.append(use.need.profile)
        for fraction, stage in self.load_progress:
            on_progress(fraction, stage)
        self.present = True

    def unload(self) -> None:
        self.unloads += 1


Step = Callable[[Generation, DeviceUse, ProgressCallback], JobOutcome]


def _complete(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
    on_progress(0.5, "Generating take 1 of 1")
    return JobOutcome(takes=[TAKE])


@dataclass
class FakeHandler:
    """``JobHandler`` whose steps are scripted per attempt (the last one repeats)."""

    engine: FakeEngine = field(default_factory=FakeEngine)
    steps: list[Step] = field(default_factory=lambda: [_complete])
    uses: list[DeviceUse] = field(default_factory=list)
    job_needs: tuple[GpuNeed, ...] = RESTYLE_NEEDS

    def needs(self, job: Generation) -> Sequence[GpuNeed]:
        return self.job_needs

    def execute(self, job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        self.uses.append(use)
        step = self.steps[0] if len(self.steps) == 1 else self.steps.pop(0)
        return step(job, use, on_progress)


def raising(error: BaseException) -> Step:
    """A step that raises ``error``."""

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        raise error

    return step


def calling(action: Callable[[ProgressCallback], Any]) -> Step:
    """A step that runs ``action(on_progress)`` and then completes."""

    def step(job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        action(on_progress)
        return JobOutcome(takes=[TAKE])

    return step
