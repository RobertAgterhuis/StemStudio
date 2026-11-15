"""Step plan and parent steps of a detail job (research.md D-02..D-04, data-model 2.3).

Planning reads every standard stem once: ``bass`` becomes "not supported", silent parents
"skipped"; both are checkpointed at once. The remaining parents form the steps (units of the
priority scheduler): drums, vocals and the instrument group. With a summing source the active
instrument parents run through one model call on their sum and the families are re-assigned to
each parent; a single active parent runs directly. Each step checkpoints its parents, so a retry
plans only what is still missing.
"""

from collections.abc import Callable, Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final, Protocol

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
from stemstudio_worker.detail.registry import SEPARATOR_VERSION
from stemstudio_worker.detail.split import ParentResult, SplitTarget, split_parent, trivial_outcome
from stemstudio_worker.ports import DetailAudio, DetailModel, DetailModelProvider
from stemstudio_worker.schemas import (
    DetailModelId,
    ParentOutcome,
    ParentOutcomeKind,
    ParentStemName,
    StemName,
)

STEP_WEIGHTS: Final[Mapping[str, float]] = {"drums": 0.20, "vocals": 0.05, "instruments": 0.74}
"""Share of the measured cost per step (research.md D-03); renormalised over the steps that run."""

FINALISE_WEIGHT: Final = 0.01
"""Share of the final step (moving the checkpoints into place, writing the result)."""

PARTITION: Final = CheckPolicy(model_residual=True, double_count=False)
"""Checks for models whose outputs partition the input (drums, vocals)."""

FAMILIES: Final = CheckPolicy(model_residual=False, double_count=True)
"""Checks for models with overlapping outputs (instrument families)."""

_MODEL_SHARE: Final = 0.9
_SINGLE_STEPS: Final[tuple[ParentStemName, ...]] = ("drums", "vocals")

StepProgress = Callable[[float], None]
"""Receives the completed fraction of a step; may raise ``DetailCancelledError``."""


@dataclass(frozen=True, slots=True)
class ModelChoice:
    """The model that splits a parent and how its outputs are treated.

    Attributes:
        model_id: Id recorded in the outcome.
        model: The loaded model.
        table: Folds the raw outputs into detail stems.
        policy: The plausibility checks that apply.
    """

    model_id: DetailModelId
    model: DetailModel
    table: FoldTable
    policy: CheckPolicy


class ModelSource(Protocol):
    """Chooses the model per parent (real registry models or fakes)."""

    @property
    def sum_instruments(self) -> bool:
        """Whether active instrument parents share one model call on their sum."""
        ...

    def choose(self, parent: ParentStemName) -> ModelChoice:
        """Return the (loaded) model for ``parent``; may download weights first."""
        ...


class RegistrySource:
    """The ADR-010 models from a :class:`DetailModelProvider` (instrument parents summed)."""

    sum_instruments = True

    def __init__(self, provider: DetailModelProvider) -> None:
        """Load models through ``provider`` on first use."""
        self._provider = provider

    def choose(self, parent: ParentStemName) -> ModelChoice:
        """Return MDX23C DrumSep, UVR KARA 2 or MVSep Mega 53 for ``parent``."""
        model_id, table, policy = _REGISTRY_CHOICE.get(parent, _INSTRUMENT_CHOICE)
        return ModelChoice(model_id, self._provider.get(model_id), table, policy)


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
    _work: Callable[[StepProgress], None]

    def run(self, progress: StepProgress) -> None:
        """Run the step, reporting its fraction to ``progress``."""
        self._work(progress)


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

    def __init__(self, source: ModelSource, version: str = SEPARATOR_VERSION) -> None:
        """Use ``source`` for the models; ``version`` stamps checkpoints and results."""
        self._source = source
        self._version = version

    @property
    def separator_version(self) -> str:
        """Version of the pipeline (models + thresholds)."""
        return self._version

    def plan(self, stems: Mapping[StemName, Path], partial: Path) -> list[DetailStep]:
        """Checkpoint the trivial outcomes and return the steps still to run.

        Args:
            stems: Standard stem name -> FLAC file, in standard display order.
            partial: The ``detail.partial`` directory (created when missing).
        """
        partial.mkdir(parents=True, exist_ok=True)
        clear_temporaries(partial)
        pending = [n for n in stems if read_checkpoint(partial, n, self._version) is None]
        active: list[ParentStemName] = []
        for name in pending:
            if self._gate(partial, name, stems[name]) and is_splittable(name):
                active.append(name)
        return self._steps(tuple(active), stems, partial)

    def outcomes(self, names: Sequence[StemName], partial: Path) -> list[ParentOutcome]:
        """Read the checkpointed outcomes of ``names`` in that order.

        Raises:
            LookupError: A parent has no checkpoint of this version.
        """
        found = [read_checkpoint(partial, name, self._version) for name in names]
        if any(outcome is None for outcome in found):
            msg = "A detail checkpoint is missing"
            raise LookupError(msg)
        return [outcome for outcome in found if outcome is not None]

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
        groups: list[tuple[tuple[ParentStemName, ...], float]] = [
            ((p,), STEP_WEIGHTS[p]) for p in _SINGLE_STEPS if p in active
        ]
        instruments = tuple(p for p in active if p in INSTRUMENT_PARENTS)
        if instruments:
            split = [instruments] if self._source.sum_instruments else [(p,) for p in instruments]
            groups += [(g, STEP_WEIGHTS["instruments"] / len(split)) for g in split]
        return [self._step(_Group(parents, stems, partial), weight) for parents, weight in groups]

    def _step(self, group: _Group, weight: float) -> DetailStep:
        return DetailStep(group.parents, weight, lambda progress: self._run(group, progress))

    def _run(self, group: _Group, progress: StepProgress) -> None:
        progress(0.0)
        audios = {p: read_stem(group.stems[p]) for p in group.parents}
        choice = self._source.choose(group.parents[0])
        mix = output_sum(list(audios.values())) if len(audios) > 1 else audios[group.parents[0]]
        outputs = choice.model.separate(mix, _ModelProgress(progress))
        target = SplitTarget(group.partial, self._version, choice.model_id, choice.policy)
        for result in _results(audios, outputs, choice.table):
            split_parent(target, result)
        progress(1.0)


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


def _residual(audio: DetailAudio, outputs: Iterable[DetailAudio]) -> float:
    return model_residual_db(audio, list(outputs))


def _skip_kind(name: StemName) -> ParentOutcomeKind:
    """Outcome of a parent that is not split: silent, or bass (no model)."""
    if is_splittable(name):
        return ParentOutcomeKind.SKIPPED_SILENT
    return ParentOutcomeKind.NOT_SUPPORTED
