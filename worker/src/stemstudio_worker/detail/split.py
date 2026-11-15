"""Outcome of one parent from its model outputs: gate, checks, FLACs, checkpoint (D-04 order)."""

from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from pathlib import Path

from stemstudio_worker.detail.audio_io import Pcm, read_stem, write_stem
from stemstudio_worker.detail.checkpoint import commit, open_temporary
from stemstudio_worker.detail.checks import (
    CheckPolicy,
    double_count_db,
    pre_write_outcome,
    reconstruction_db,
    reconstruction_ok,
    rest_of,
    to_pcm,
)
from stemstudio_worker.detail.gating import SILENCE_DB, ParentReading, read_parent, sub_stem_audible
from stemstudio_worker.detail.names import REST, display_order
from stemstudio_worker.ports import DetailAudio
from stemstudio_worker.schemas import (
    DetailModelId,
    DetailStemInfo,
    DetailStemName,
    OutcomeMetrics,
    ParentOutcome,
    ParentOutcomeKind,
    ParentStemName,
    StemName,
)

Families = Mapping[DetailStemName, DetailAudio]


@dataclass(frozen=True, slots=True)
class SplitTarget:
    """Where and how a parent's checkpoint is written.

    Attributes:
        partial: The ``detail.partial`` directory.
        version: The pipeline version stamped into the checkpoint.
        model_id: The model that produced the outputs.
        policy: The plausibility checks that apply to the model.
    """

    partial: Path
    version: str
    model_id: DetailModelId
    policy: CheckPolicy


@dataclass(frozen=True, slots=True)
class _Written:
    kind: ParentOutcomeKind
    reconstruction_db: float | None = None
    stems: tuple[DetailStemInfo, ...] = ()


def metrics(
    reading: ParentReading, residual_db: float | None = None, recon_db: float | None = None
) -> OutcomeMetrics:
    """Build the outcome metrics (rounded to 0.01)."""
    return OutcomeMetrics(
        parent_level_dbfs=round(reading.level_dbfs, 2),
        active_share=round(reading.active_share, 4),
        model_residual_db=None if residual_db is None else round(residual_db, 2),
        reconstruction_error_db=None if recon_db is None else round(recon_db, 2),
    )


def trivial_outcome(
    parent: StemName, kind: ParentOutcomeKind, reading: ParentReading
) -> ParentOutcome:
    """Outcome without a model run (``not-supported``, ``skipped-silent``)."""
    return ParentOutcome(parent=parent, outcome=kind, model=None, stems=[],
                         metrics=metrics(reading))  # fmt: skip


@dataclass(frozen=True, slots=True)
class ParentResult:
    """Model result for one parent.

    Attributes:
        parent: The parent stem.
        audio: The parent audio.
        families: The parent's (share of the) folded model outputs.
        residual_db: Parent minus the sum of every model output (or share), dB rel. parent.
    """

    parent: ParentStemName
    audio: DetailAudio
    families: Families
    residual_db: float


def split_parent(target: SplitTarget, result: ParentResult) -> None:
    """Gate the families of a parent, run the checks, write the FLACs and the checkpoint."""
    audio = result.audio
    reading = read_parent(audio)
    kept = {n: a for n, a in result.families.items() if sub_stem_audible(a, audio, reading)}
    double = double_count_db(audio, list(kept.values())) if kept else SILENCE_DB
    verdict = pre_write_outcome(len(kept), result.residual_db, double, target.policy)
    temporary = open_temporary(target.partial, result.parent)
    written = _Written(verdict) if verdict else _write_split(temporary, result.parent, audio, kept)
    outcome = ParentOutcome(
        parent=result.parent, outcome=written.kind, model=target.model_id,
        stems=list(written.stems),
        metrics=metrics(reading, result.residual_db, written.reconstruction_db),
    )  # fmt: skip
    commit(temporary, outcome, target.version)


def _write_split(
    temporary: Path, parent: ParentStemName, audio: DetailAudio, kept: Families
) -> _Written:
    files = _write_files(temporary, parent, audio, kept)
    error = reconstruction_db(audio, [read_stem(path) for path in files.values()])
    if not reconstruction_ok(error):
        _delete(files.values())
        return _Written(ParentOutcomeKind.NOT_RELIABLE, error)
    return _Written(ParentOutcomeKind.SPLIT, error, _infos(parent, files))


def _write_files(
    temporary: Path, parent: ParentStemName, audio: DetailAudio, kept: Families
) -> dict[DetailStemName, Path]:
    """Write the kept stems and the rest (16-bit) in display order; returns name -> file."""
    pcm: dict[DetailStemName, Pcm] = {name: to_pcm(data) for name, data in kept.items()}
    pcm[REST] = to_pcm(rest_of(audio, list(pcm.values())))
    files = {name: temporary / f"{name}.flac" for name in display_order(parent, set(pcm))}
    for name, path in files.items():
        write_stem(path, pcm[name])
    return files


def _delete(paths: Iterable[Path]) -> None:
    for path in paths:
        path.unlink()


def _infos(
    parent: ParentStemName, files: Mapping[DetailStemName, Path]
) -> tuple[DetailStemInfo, ...]:
    return tuple(
        DetailStemInfo(name=name, path=f"{parent}/{name}", size_bytes=path.stat().st_size)
        for name, path in files.items()
    )
