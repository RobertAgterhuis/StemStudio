"""The ``restyle`` kind (009): stems -> summed source -> ACE-Step cover/repaint -> labelled takes.

``prepare`` sums the uploaded stems before the GPU is requested (CPU work never holds the lease);
``execute`` runs the engine on the admitted device and labels every take; ``cleanup`` deletes the
inputs and the engine's untagged files when the job ends, whatever the outcome.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final

from stemstudio_generator.jobs.files import JobFiles
from stemstudio_generator.jobs.progress import JobOutcome
from stemstudio_generator.ports import (
    RESTYLE_NEEDS,
    DeviceUse,
    EngineRestyle,
    EngineTake,
    GpuNeed,
    MixedSource,
    MusicEngine,
    ProgressCallback,
    StemMixer,
    TakeLabels,
    TakeWriter,
    WrittenFile,
)
from stemstudio_generator.schemas import (
    CLIPPED_WARNING,
    GeneratedTake,
    Generation,
    RestyleParams,
    TakeModel,
    VocalLabel,
)

SOURCE_NAME: Final = "source.wav"
"""Engine input file in the job's ``work/`` directory."""


@dataclass(frozen=True, slots=True)
class TakeIdentity:
    """Who made a take: the ``GeneratedTake.model`` and the ``GENERATOR`` Vorbis comment.

    Attributes:
        model: Model id and revision.
        generator: Label text, e.g. ``ACE-Step 1.5 (<revision>)``.
    """

    model: TakeModel
    generator: str


@dataclass(frozen=True, slots=True)
class TakeOutput:
    """Writes the labelled takes of a job.

    Attributes:
        writer: The FLAC writer.
        identity: Model and label of the takes.
        files: Job file layout.
    """

    writer: TakeWriter
    identity: TakeIdentity
    files: JobFiles

    def label(
        self, job: Generation, take: EngineTake, voice: VocalLabel | None = None
    ) -> GeneratedTake:
        """Write ``take`` as the job's labelled take and return its contract record."""
        labels = TakeLabels(self.identity.generator, job.generation_id, take.index, voice)
        written = self.writer.write(
            take.path, self.files.take(job.generation_id, take.index), labels
        )
        take.path.unlink(missing_ok=True)
        return generated_take(take, written, self.identity.model, voice)


def generated_take(
    take: EngineTake, written: WrittenFile, model: TakeModel, voice: VocalLabel | None
) -> GeneratedTake:
    """The contract record of a labelled take."""
    return GeneratedTake(
        index=take.index, seed=take.seed, duration_seconds=written.duration_seconds,
        sha256=written.sha256, bytes=written.bytes, peak_dbfs=min(written.peak_dbfs, 0.0),
        model=model, vocal=voice, profile=take.profile,
    )  # fmt: skip


def restyle_params(job: Generation) -> RestyleParams:
    """The job's restyle parameters (a restyle job always has them)."""
    if job.restyle is None:  # pragma: no cover - guaranteed by Generation's validator
        msg = "not a restyle job"
        raise ValueError(msg)
    return job.restyle


class RestyleHandler:
    """``JobHandler`` of the ``restyle`` kind."""

    def __init__(self, engine: MusicEngine, mixer: StemMixer, output: TakeOutput) -> None:
        """Restyle with ``engine``; ``mixer`` prepares the source, ``output`` labels the takes."""
        self._engine = engine
        self._mixer = mixer
        self._output = output
        self._sources: dict[str, MixedSource] = {}

    @property
    def engine(self) -> MusicEngine:
        """The music engine."""
        return self._engine

    def needs(self, job: Generation) -> Sequence[GpuNeed]:
        """Standard cover profile, then the low-memory one."""
        del job
        return RESTYLE_NEEDS

    def prepare(self, job: Generation) -> None:
        """Sum the stems (and cut the range for ``mode: range``) into ``work/source.wav``."""
        params = restyle_params(job)
        files = self._output.files
        cut = (
            (params.range_start_seconds, params.range_end_seconds)
            if params.mode == "range"
            else None
        )
        stems = sorted(files.inputs(job.generation_id).iterdir())
        dest = files.work(job.generation_id) / SOURCE_NAME
        self._sources[job.generation_id] = self._mixer.mix(stems, dest, cut)

    def execute(self, job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        """Run the engine on the prepared source and label the takes."""
        del use  # the runner loaded the engine for it
        source = self._sources[job.generation_id]
        request = EngineRestyle(
            source.path, restyle_params(job), self._output.files.work(job.generation_id)
        )
        takes = [
            self._output.label(job, take) for take in self._engine.restyle(request, on_progress)
        ]
        return JobOutcome(takes=takes, warnings=[CLIPPED_WARNING] if source.clipped else [])

    def cleanup(self, job: Generation) -> None:
        """Forget the source and delete the inputs and work files."""
        self._sources.pop(job.generation_id, None)
        self._output.files.clean(job.generation_id)
