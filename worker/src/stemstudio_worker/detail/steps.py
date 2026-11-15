"""Step plan and parent steps of a detail job (research.md D-02..D-04, data-model 2.3).

Planning reads every standard stem once: ``bass`` becomes "not supported", silent parents
"skipped"; both are checkpointed at once. The remaining parents form the steps (units of the
priority scheduler): drums, vocals and the instrument group. With a summing source the active
instrument parents run through one model call on their sum and the families are re-assigned to
each parent; a single active parent runs directly. Each step checkpoints its parents, so a retry
plans only what is still missing. With a ``DeviceRunner`` every step is one device step: the model
is loaded for the device the runner picks (GPU, or the CPU on fallback) and the step returns where
it ran (feature 003).
"""

from collections.abc import Callable, Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final, Protocol, TypeGuard

from stemstudio_worker.detail.audio_io import read_stem
from stemstudio_worker.detail.checkpoint import (
    clear_temporaries,
    commit,
    open_temporary,
    read_checkpoint,
)
from stemstudio_worker.detail.checks import CheckPolicy, model_residual_db
from stemstudio_worker.detail.folding import (
    DRUMSEP_TABLE,
    MEGA_TABLE,
    VOCAL_TABLE,
    FoldTable,
    fold,
    output_sum,
)
from stemstudio_worker.detail.gating import read_parent
from stemstudio_worker.detail.names import INSTRUMENT_PARENTS, is_splittable
from stemstudio_worker.detail.reassign import reassign_families
from stemstudio_worker.detail.registry import GPU_NEEDS, SEPARATOR_VERSION
from stemstudio_worker.detail.split import ParentResult, SplitTarget, split_parent, trivial_outcome
from stemstudio_worker.ports import DetailAudio, DetailModel, DeviceRunner, DeviceUse, GpuNeed
from stemstudio_worker.schemas import (
    DetailModelId,
    ParentOutcome,
    ParentOutcomeKind,
    ParentStemName,
    StemName,
)

STEP_WEIGHTS: Final[Mapping[str, float]] = {"drums": 0.20, "vocals": 0.05, "instruments": 0.74}
"""Share of the measured cost per step (research.md D-03); the job runner adds the final unit
(0.01) and renormalises over the steps that run."""

PARTITION: Final = CheckPolicy(model_residual=True, double_count=False)
"""Checks for models whose outputs partition the input (drums, vocals)."""

FAMILIES: Final = CheckPolicy(model_residual=False, double_count=True)
"""Checks for models with overlapping outputs (instrument families)."""

_MODEL_SHARE: Final = 0.9
_SINGLE_STEPS: Final[tuple[ParentStemName, ...]] = ("drums", "vocals")

_Weighted = tuple[tuple[ParentStemName, ...], float]

StepProgress = Callable[[float], None]
"""Receives the completed fraction of a step; may raise ``DetailCancelledError``."""

_RunOutput = tuple[dict[str, DetailAudio], str | None]
"""Model outputs plus the model's own CPU fallback reason (or None)."""


ModelLoader = Callable[[str], DetailModel]
"""Returns the model loaded on a torch device (``cuda``/``cpu``); may download weights first."""


@dataclass(frozen=True, slots=True)
class ModelChoice:
    """The model that splits a parent and how its outputs are treated.

    Attributes:
        model_id: Id recorded in the outcome.
        load: Loads the model on the device of the step.
        table: Folds the raw outputs into detail stems.
        policy: The plausibility checks that apply.
        need: Free GPU memory the model step needs.
    """

    model_id: DetailModelId
    load: ModelLoader
    table: FoldTable
    policy: CheckPolicy
    need: GpuNeed


class DeviceModelProvider(Protocol):
    """Loads registry models per device (``runtimes.RealModelProvider``)."""

    def load(self, model_id: DetailModelId, device: str) -> DetailModel:
        """Return ``model_id`` on ``device``, loading it on first use."""
        ...

    def release_gpu(self) -> None:
        """Drop the GPU instances."""
        ...


class ModelSource(Protocol):
    """Chooses the model per parent (real registry models or fakes)."""

    @property
    def sum_instruments(self) -> bool:
        """Whether active instrument parents share one model call on their sum."""
        ...

    def choose(self, parent: ParentStemName) -> ModelChoice:
        """Return the model choice for ``parent`` (nothing is loaded yet)."""
        ...

    def release_gpu(self) -> None:
        """Drop models held in GPU memory (the worker is idle)."""
        ...


class RegistrySource:
    """The ADR-010 models from a :class:`DetailModelProvider` (instrument parents summed)."""

    sum_instruments = True

    def __init__(self, provider: DeviceModelProvider) -> None:
        """Load models through ``provider`` on first use."""
        self._provider = provider

    def choose(self, parent: ParentStemName) -> ModelChoice:
        """Return MDX23C DrumSep, UVR KARA 2 or MVSep Mega 53 for ``parent``."""
        model_id, table, policy = _REGISTRY_CHOICE.get(parent, _INSTRUMENT_CHOICE)
        load = lambda device: self._provider.load(model_id, device)  # noqa: E731 - adapter
        return ModelChoice(model_id, load, table, policy, GPU_NEEDS[model_id])

    def release_gpu(self) -> None:
        """Drop the provider's GPU instances."""
        self._provider.release_gpu()


_INSTRUMENT_CHOICE: Final[tuple[DetailModelId, FoldTable, CheckPolicy]] = (
    "mvsep-mega-53", MEGA_TABLE, FAMILIES,
)  # fmt: skip
_REGISTRY_CHOICE: Final[dict[str, tuple[DetailModelId, FoldTable, CheckPolicy]]] = {
    "drums": ("mdx23c-drumsep-6s", DRUMSEP_TABLE, PARTITION),
    "vocals": ("uvr-mdxnet-kara-2", VOCAL_TABLE, PARTITION),
}


@dataclass(frozen=True, slots=True)
class DetailStep:
    """One scheduler unit: splits ``parents`` and checkpoints them.

    Attributes:
        parents: The parents of the step (one, or the instrument group).
        weight: Share of the job's cost (before renormalisation).
    """

    parents: tuple[ParentStemName, ...]
    weight: float
    _work: Callable[[StepProgress], DeviceUse | None]

    def run(self, progress: StepProgress) -> DeviceUse | None:
        """Run the step, reporting its fraction to ``progress``.

        Returns:
            Where the model ran, or None for a pipeline without a device runner.
        """
        return self._work(progress)


class _ModelProgress:
    """Port progress callback mapping model chunks onto the first 90 % of a step."""

    def __init__(self, progress: StepProgress) -> None:
        self._progress = progress

    def __call__(self, fraction: float, stage: str) -> None:  # noqa: ARG002 - port signature
        self._progress(_MODEL_SHARE * min(max(fraction, 0.0), 1.0))


@dataclass(frozen=True, slots=True)
class _Group:
    parents: tuple[ParentStemName, ...]
    stems: Mapping[StemName, Path]
    partial: Path


class DetailPipeline:
    """Plans and runs the detail steps of one audio's standard stems."""

    def __init__(
        self,
        source: ModelSource,
        devices: DeviceRunner | None = None,
        version: str = SEPARATOR_VERSION,
    ) -> None:
        """Create the pipeline.

        Args:
            source: Chooses the model per parent.
            devices: Runs each model step on the worker's device; without one the models run
                on the CPU and the steps record no device.
            version: Stamps checkpoints and results.
        """
        self._source = source
        self._devices = devices
        self._version = version

    @property
    def separator_version(self) -> str:
        """Version of the pipeline (models + thresholds)."""
        return self._version

    def release_gpu(self) -> None:
        """Drop the models held in GPU memory (called when the worker is idle, ADR-012 D6)."""
        self._source.release_gpu()

    def plan(self, stems: Mapping[StemName, Path], partial: Path) -> list[DetailStep]:
        """Checkpoint the trivial outcomes and return the steps still to run.

        Args:
            stems: Standard stem name -> FLAC file, in standard display order.
            partial: The ``detail.partial`` directory (created when missing).
        """
        partial.mkdir(parents=True, exist_ok=True)
        clear_temporaries(partial)
        active = tuple(n for n in stems if self._needs_model(n, stems[n], partial))
        return self._steps(active, stems, partial)

    def outcomes(self, names: Sequence[StemName], partial: Path) -> list[ParentOutcome]:
        """Read the checkpointed outcomes of ``names`` in that order.

        Raises:
            LookupError: A parent has no checkpoint of this version.
        """
        outcomes: list[ParentOutcome] = []
        for name in names:
            outcome = read_checkpoint(partial, name, self._version)
            if outcome is None:
                msg = f"The detail checkpoint of {name} is missing"
                raise LookupError(msg)
            outcomes.append(outcome)
        return outcomes

    def _needs_model(self, name: StemName, path: Path, partial: Path) -> TypeGuard[ParentStemName]:
        """True when ``name`` has no checkpoint yet and passes the gate (see :meth:`_gate`)."""
        if read_checkpoint(partial, name, self._version) is not None:
            return False
        return self._gate(partial, name, path)

    def _gate(self, partial: Path, name: StemName, path: Path) -> bool:
        """Checkpoint ``name`` when it is not split; return True when it must be split."""
        reading = read_parent(read_stem(path))
        if is_splittable(name) and reading.active:
            return True
        outcome = trivial_outcome(name, _skip_kind(name), reading)
        commit(open_temporary(partial, name), outcome, self._version)
        return False

    def _steps(
        self, active: tuple[ParentStemName, ...], stems: Mapping[StemName, Path], partial: Path
    ) -> list[DetailStep]:
        groups = _single_groups(active) + self._instrument_groups(active)
        return [self._step(_Group(parents, stems, partial), weight) for parents, weight in groups]

    def _instrument_groups(self, active: tuple[ParentStemName, ...]) -> list[_Weighted]:
        """One summed group, or one group per parent when the source does not sum."""
        instruments = _instruments(active)
        if not instruments:
            return []
        split = [instruments] if self._source.sum_instruments else [(p,) for p in instruments]
        return [(group, STEP_WEIGHTS["instruments"] / len(split)) for group in split]

    def _step(self, group: _Group, weight: float) -> DetailStep:
        return DetailStep(group.parents, weight, lambda progress: self._run(group, progress))

    def _run(self, group: _Group, progress: StepProgress) -> DeviceUse | None:
        progress(0.0)
        audios = {p: read_stem(group.stems[p]) for p in group.parents}
        choice = self._source.choose(group.parents[0])
        mix = output_sum(list(audios.values())) if len(audios) > 1 else audios[group.parents[0]]
        model_run = _ModelRun(choice, mix, _ModelProgress(progress))
        (outputs, reason), use = self._on_device(model_run, choice.need)
        target = SplitTarget(group.partial, self._version, choice.model_id, choice.policy)
        for result in _results(audios, outputs, choice.table):
            split_parent(target, result)
        progress(1.0)
        return _with_fallback(use, reason)

    def _on_device(self, run: "_ModelRun", need: GpuNeed) -> tuple[_RunOutput, DeviceUse | None]:
        if self._devices is None:
            return run("cpu"), None
        return self._devices.run(run, need)


@dataclass(frozen=True, slots=True)
class _ModelRun:
    """One model call on a device; also returns the model's own CPU fallback reason, if any."""

    choice: ModelChoice
    mix: DetailAudio
    progress: _ModelProgress

    def __call__(self, device: str) -> _RunOutput:
        model = self.choice.load(device)
        reason: str | None = getattr(model, "fallback_reason", None)
        return model.separate(self.mix, self.progress), reason


def _with_fallback(use: DeviceUse | None, reason: str | None) -> DeviceUse | None:
    """The model ran on the CPU although the step was given the GPU: record the fallback."""
    if use is None or reason is None:
        return use
    return DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=reason)


def _results(
    audios: Mapping[ParentStemName, DetailAudio],
    outputs: Mapping[str, DetailAudio],
    table: FoldTable,
) -> list[ParentResult]:
    """Per-parent model results; a group shares the folded families by power masks."""
    families = fold(outputs, table)
    if len(audios) == 1:
        ((parent, audio),) = audios.items()
        return [ParentResult(parent, audio, families, _residual(audio, outputs.values()))]
    shares = reassign_families(families, audios)
    return [
        ParentResult(p, a, shares[p], _residual(a, shares[p].values())) for p, a in audios.items()
    ]


def _instruments(active: tuple[ParentStemName, ...]) -> tuple[ParentStemName, ...]:
    return tuple(p for p in active if p in INSTRUMENT_PARENTS)


def _single_groups(active: tuple[ParentStemName, ...]) -> list[_Weighted]:
    return [((p,), STEP_WEIGHTS[p]) for p in _SINGLE_STEPS if p in active]


def _residual(audio: DetailAudio, outputs: Iterable[DetailAudio]) -> float:
    return model_residual_db(audio, list(outputs))


def _skip_kind(name: StemName) -> ParentOutcomeKind:
    """Outcome of a parent that is not split: silent, or bass (no model)."""
    if is_splittable(name):
        return ParentOutcomeKind.SKIPPED_SILENT
    return ParentOutcomeKind.NOT_SUPPORTED
