"""Runs one generation job: GPU gate, model load, the kind's step, OOM retry, terminal state.

Per job (data-model 2.4): ``waitingForGpu`` until the gate admits a ``GpuNeed``;
``downloadingModel`` while missing weights are fetched (else "Loading model"); ``running``
while the kind's handler works; then ``completed``, ``failed`` or ``cancelled``. A GPU
out-of-memory error unloads the engine and retries the step **once** with the low-memory
needs (stage "Retrying with low memory"); a second OOM, or an OOM without a low-memory
profile, fails with the readable ``outOfMemory`` text (ADR-018 D4). Expected failures
(``GeneratorError``) keep their code and message; anything else is logged and reported as a
generic ``engineError`` (no internals in the message).
"""

import logging
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from typing import Final, Protocol

from stemstudio_generator.jobs.progress import JobOutcome, JobReporter
from stemstudio_generator.ports import (
    Cancelled,
    DeviceUse,
    GeneratorError,
    GpuGate,
    GpuNeed,
    GpuOutOfMemoryError,
    ProgressCallback,
)
from stemstudio_generator.schemas import (
    LOW_MEMORY_RETRY_STAGE,
    Generation,
    GenerationKind,
    ModelStatus,
)

__all__ = [
    "DOWNLOADING_STAGE",
    "ENGINE_ERROR_TEXT",
    "GENERATING_STAGE",
    "LOADING_STAGE",
    "WAITING_STAGE",
    "JobHandler",
    "JobOutcome",
    "JobRunner",
    "ModelHost",
]

WAITING_STAGE: Final = "Waiting for the GPU"
"""Stage text when a job leaves the queue."""

DOWNLOADING_STAGE: Final = "Downloading model"
"""Stage text while missing weights are downloaded."""

LOADING_STAGE: Final = "Loading model"
"""Stage text while the weights are loaded."""

GENERATING_STAGE: Final = "Generating"
"""Stage text when the kind's step starts."""

ENGINE_ERROR_TEXT: Final = (
    "The generation failed unexpectedly; please try again (details are in the generator log)."
)
"""Message of unexpected failures."""

_MAX_MESSAGE: Final = 500
_LOG: Final = logging.getLogger(__name__)


class ModelHost(Protocol):
    """The model side of an engine (``MusicEngine`` or ``VoiceConverter``)."""

    def required_models(self) -> list[ModelStatus]:
        """Model files with presence (missing ones are downloaded by ``load``)."""
        ...

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Download (first use) and load the models for ``use.need.profile``."""
        ...

    def unload(self) -> None:
        """Drop the models and empty the CUDA cache."""
        ...


class JobHandler(Protocol):
    """The kind-specific part of a job (restyle, song, voice conversion)."""

    @property
    def engine(self) -> ModelHost:
        """The engine the handler runs on."""
        ...

    def needs(self, job: Generation) -> Sequence[GpuNeed]:
        """GPU needs of the job in order of preference."""
        ...

    def execute(self, job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        """Run the step on the admitted device and return the takes."""
        ...


@dataclass(slots=True)
class _Run:
    handler: JobHandler
    reporter: JobReporter
    admitted: list[DeviceUse] = field(default_factory=list)


class JobRunner:
    """Processes jobs with one handler per served kind."""

    def __init__(self, gate: GpuGate, handlers: Mapping[GenerationKind, JobHandler]) -> None:
        """Run on ``gate`` with ``handlers`` per kind."""
        self._gate = gate
        self.handlers: dict[GenerationKind, JobHandler] = dict(handlers)

    @property
    def kinds(self) -> tuple[GenerationKind, ...]:
        """Kinds this runner serves."""
        return tuple(self.handlers)

    def run(self, reporter: JobReporter) -> Generation:
        """Process the reporter's job to a terminal state and return it."""
        try:
            run = _Run(self._handler(reporter.job), reporter)
            reporter.complete(self._attempts(run))
        except Cancelled:
            reporter.cancelled()
        except GeneratorError as error:
            _fail(reporter, error)
        except Exception:
            _LOG.exception("Generation %s failed", reporter.job.generation_id)
            _fail(reporter, GeneratorError(ENGINE_ERROR_TEXT))
        return reporter.job

    def idle(self) -> None:
        """The queue is empty: unload every engine once (frees the GPU for other programs)."""
        engines = {id(handler.engine): handler.engine for handler in self.handlers.values()}
        for engine in engines.values():
            engine.unload()

    def _handler(self, job: Generation) -> JobHandler:
        handler = self.handlers.get(job.kind)
        if handler is None:
            raise GeneratorError(f"This service does not run {job.kind} jobs.")
        return handler

    def _attempts(self, run: _Run) -> JobOutcome:
        run.reporter.wait(WAITING_STAGE)
        needs = tuple(run.handler.needs(run.reporter.job))
        try:
            return self._attempt(run, needs, GENERATING_STAGE)
        except Exception as error:
            low = self._low_memory_needs(run, needs, error)
        try:
            return self._attempt(run, low, LOW_MEMORY_RETRY_STAGE)
        except Exception as error:
            if not self._gate.is_out_of_memory(error):
                raise
            run.handler.engine.unload()
            raise GpuOutOfMemoryError from error

    def _low_memory_needs(
        self, run: _Run, needs: Sequence[GpuNeed], error: Exception
    ) -> tuple[GpuNeed, ...]:
        """After an OOM: unload and return the retry needs; re-raise anything else."""
        if not self._gate.is_out_of_memory(error):
            raise error
        run.handler.engine.unload()
        low = _retry_needs(needs, run.admitted[-1])
        if not low:
            raise GpuOutOfMemoryError from error
        return low

    def _attempt(self, run: _Run, needs: Sequence[GpuNeed], stage: str) -> JobOutcome:
        reporter = run.reporter
        with self._gate.hold(needs, reporter.wait) as use:
            run.admitted.append(use)
            reporter.admitted(use.device)
            _load(run.handler.engine, use, reporter)
            reporter.status("running", stage)
            return run.handler.execute(reporter.job, use, reporter.progress)


def _retry_needs(needs: Sequence[GpuNeed], failed: DeviceUse) -> tuple[GpuNeed, ...]:
    """Low-memory needs for the one retry; none when the failed attempt was low-memory already."""
    if failed.need.profile == "lowMemory":
        return ()
    return tuple(need for need in needs if need.profile == "lowMemory")


def _load(engine: ModelHost, use: DeviceUse, reporter: JobReporter) -> None:
    if all(model.present for model in engine.required_models()):
        reporter.status("running", LOADING_STAGE)
    else:
        reporter.status("downloadingModel", DOWNLOADING_STAGE)
    engine.load(use, reporter.progress)


def _fail(reporter: JobReporter, error: GeneratorError) -> None:
    try:
        reporter.fail(error.code, str(error)[:_MAX_MESSAGE])
    except Cancelled:
        reporter.cancelled()
