"""Artefact kinds of detail and rest stems from the 002 split metrics (data-model 4.4, A3)."""

import numpy as np

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.insights.thresholds import MODEL_RESIDUAL_DB, REST_SHARE
from stemstudio_worker.schemas import ArtefactKind, DetailParentInput, QualityInputKind


def rest_share(rest: Spectrogram, parent: Spectrogram) -> float:
    """Energy of a parent's ``rest`` stem as a share of the parent stem's energy (0..1)."""
    total = float(np.sum(parent.power, dtype=np.float64))
    if total <= 0:
        return 0.0
    return min(1.0, float(np.sum(rest.power, dtype=np.float64)) / total)


def detail_kinds(
    kind: QualityInputKind, parent: DetailParentInput | None, share: float | None
) -> list[ArtefactKind]:
    """``model-residual`` and ``rest-share`` for detail stems; ``rest-share`` for rest stems.

    Args:
        kind: Kind of the stem; standard stems never get these kinds.
        parent: The 002 metrics of the stem's parent, if the request has them.
        share: The parent's rest energy share (``rest_share``), if the parent has a rest stem.
    """
    kinds: list[ArtefactKind] = []
    residual = parent.model_residual_db if parent is not None else None
    if kind == "detail" and residual is not None and residual > MODEL_RESIDUAL_DB:
        kinds.append("model-residual")
    if kind != "standard" and share is not None and share > REST_SHARE:
        kinds.append("rest-share")
    return kinds
