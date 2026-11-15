"""Detail (second-stage) separation of the standard stems (stem-separation agent, feature 002).

Contract (binding for the parallel wave): the composition root calls
``create_detail_adapters(settings)``; the signature is fixed. Models, gating, folding,
re-assignment and checks follow ``specs/002-detail-separation/research.md`` (D-02..D-05).
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import DetailAdapters

__all__ = ["create_detail_adapters"]


def create_detail_adapters(settings: WorkerSettings) -> DetailAdapters:
    """Build the detail adapters for ``settings.separator`` (real models or fakes).

    Nothing is downloaded or loaded here; models are fetched on first use into
    ``settings.model_dir``.

    Args:
        settings: Worker settings (separator kind, device, model directory, fake step delay).

    Returns:
        The detail adapters.
    """
    raise NotImplementedError("Implemented by the stem-separation agent (tasks T043).")
