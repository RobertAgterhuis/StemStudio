"""Demucs and fake separators (stem-separation agent).

Contract (binding for the parallel wave): the composition root calls
``create_separator(settings)``.
The stem-separation agent replaces the placeholder body; the signature is fixed.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import Separator

__all__ = ["create_separator"]


def create_separator(settings: WorkerSettings) -> Separator:
    """Build the separator selected by ``settings.separator`` (``demucs`` or ``fake``).

    Args:
        settings: Worker settings (data directory, separator kind, device, limits).

    Returns:
        The separator.

    Raises:
        NotImplementedError: Placeholder until the stem-separation agent implements it.
    """
    msg = "create_separator is implemented by the stem-separation agent"
    raise NotImplementedError(msg)
