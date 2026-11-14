"""Demucs and fake separators (stem-separation agent).

Contract (binding for the parallel wave): the composition root calls
``create_separator(settings)``; the signature is fixed.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import Separator
from stemstudio_worker.separation.demucs_runtime import default_runtime
from stemstudio_worker.separation.demucs_separator import DemucsSeparator
from stemstudio_worker.separation.fake import FakeSeparator

__all__ = ["create_separator"]


def create_separator(settings: WorkerSettings) -> Separator:
    """Build the separator selected by ``settings.separator`` (``demucs`` or ``fake``).

    Nothing is loaded here: the Demucs model is loaded (and downloaded on first use) by the
    first separation.

    Args:
        settings: Worker settings (separator kind, device, primary and fallback model).

    Returns:
        A :class:`FakeSeparator` for ``fake``, else a :class:`DemucsSeparator`.
    """
    if settings.separator == "fake":
        return FakeSeparator()
    models = (settings.primary_model, settings.fallback_model)
    return DemucsSeparator(models, settings.device, default_runtime())
