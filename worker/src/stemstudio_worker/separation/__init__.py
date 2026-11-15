"""Demucs and fake separators (stem-separation agent).

The composition root calls ``create_separator(settings, devices)`` (feature 003: the device runner
built by ``devices.create_device_runner`` is shared with the detail models).
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import DeviceRunner, Separator
from stemstudio_worker.separation.demucs_runtime import default_runtime
from stemstudio_worker.separation.demucs_separator import HTDEMUCS_NEED_MIB, DemucsSeparator
from stemstudio_worker.separation.fake import FakeSeparator

__all__ = ["HTDEMUCS_NEED_MIB", "create_separator"]


def create_separator(settings: WorkerSettings, devices: DeviceRunner) -> Separator:
    """Build the separator selected by ``settings.separator`` (``demucs`` or ``fake``).

    Nothing is loaded here: the Demucs model is loaded (and downloaded on first use) by the
    first separation.

    Args:
        settings: Worker settings (separator kind, primary and fallback model).
        devices: Runs each separation on the worker's device.

    Returns:
        A :class:`FakeSeparator` for ``fake``, else a :class:`DemucsSeparator`.
    """
    if settings.separator == "fake":
        return FakeSeparator(devices)
    models = (settings.primary_model, settings.fallback_model)
    return DemucsSeparator(models, default_runtime(), devices)
