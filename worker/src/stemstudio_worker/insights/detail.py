"""Detail stem artefact kinds (data-model 4.4, A3). Stub for the red commit of T032."""

from stemstudio_worker.insights.representation import Spectrogram
from stemstudio_worker.schemas import ArtefactKind, DetailParentInput, QualityInputKind


def rest_share(rest: Spectrogram, parent: Spectrogram) -> float:
    """Stub."""
    raise NotImplementedError


def detail_kinds(
    kind: QualityInputKind, parent: DetailParentInput | None, share: float | None
) -> list[ArtefactKind]:
    """Stub."""
    raise NotImplementedError
