"""Per-parent checkpoints ``detail.partial/<parent>/{outcome.json, <name>.flac}`` (data-model 2.3).

A parent is written into ``<parent>.tmp/`` and renamed into place when complete, so a checkpoint
is either whole or absent. Checkpoints of another ``separatorVersion`` are ignored.
"""

import shutil
from pathlib import Path

from pydantic import ValidationError

from stemstudio_worker.schemas import ContractModel, ParentOutcome, StemName

_OUTCOME = "outcome.json"
_TMP = ".tmp"


class Checkpoint(ContractModel):
    """Content of ``outcome.json``: the outcome and the pipeline version that produced it."""

    version: str
    outcome: ParentOutcome


def clear_temporaries(partial: Path) -> None:
    """Remove unfinished ``<parent>.tmp`` directories (left by a crash or a cancel)."""
    for leftover in partial.glob(f"*{_TMP}"):
        shutil.rmtree(leftover, ignore_errors=True)


def read_checkpoint(partial: Path, parent: StemName, version: str) -> ParentOutcome | None:
    """Return the checkpointed outcome of ``parent`` for ``version``, or None."""
    try:
        text = (partial / parent / _OUTCOME).read_text(encoding="utf-8")
        checkpoint = Checkpoint.model_validate_json(text)
    except (OSError, ValidationError):
        return None
    return checkpoint.outcome if checkpoint.version == version else None


def open_temporary(partial: Path, parent: StemName) -> Path:
    """Create an empty ``<parent>.tmp`` directory to write a checkpoint into."""
    temporary = partial / f"{parent}{_TMP}"
    shutil.rmtree(temporary, ignore_errors=True)
    temporary.mkdir(parents=True)
    return temporary


def commit(temporary: Path, outcome: ParentOutcome, version: str) -> None:
    """Write ``outcome.json`` into ``temporary`` and rename it to ``<parent>/`` (replacing)."""
    checkpoint = Checkpoint(version=version, outcome=outcome)
    (temporary / _OUTCOME).write_text(checkpoint.model_dump_json(by_alias=True), encoding="utf-8")
    final = temporary.with_name(outcome.parent)
    shutil.rmtree(final, ignore_errors=True)
    temporary.replace(final)
