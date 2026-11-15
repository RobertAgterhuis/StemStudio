"""Detail (second-stage) separation of the standard stems (stem-separation agent, feature 002).

``create_detail_adapters(settings)`` builds the model provider (002 contract).
``create_detail_pipeline(settings, devices)`` builds the step pipeline the detail jobs run; every
parent step runs through the shared ``DeviceRunner`` (feature 003). Models, gating, folding,
re-assignment and checks follow ``specs/002-detail-separation/research.md`` (D-02..D-05).
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail.download import ModelDownloader
from stemstudio_worker.detail.fake import FakeModelProvider, FakeSource
from stemstudio_worker.detail.runtimes import RealModelProvider
from stemstudio_worker.detail.steps import DetailPipeline, RegistrySource
from stemstudio_worker.ports import DetailAdapters, DeviceRunner

__all__ = ["create_detail_adapters", "create_detail_pipeline"]


def create_detail_adapters(settings: WorkerSettings) -> DetailAdapters:
    """Build the detail adapters for ``settings.separator`` (real models or fakes).

    Nothing is downloaded or loaded here; models are fetched on first use into
    ``settings.model_dir``.

    Args:
        settings: Worker settings (separator kind, model directory, fake step delay).

    Returns:
        The detail adapters.
    """
    if settings.separator == "fake":
        return DetailAdapters(
            models=FakeModelProvider(FakeSource(settings.fake_detail_step_seconds))
        )
    return DetailAdapters(models=_real_provider(settings))


def _real_provider(settings: WorkerSettings) -> RealModelProvider:
    return RealModelProvider(ModelDownloader(settings.model_dir))


def create_detail_pipeline(settings: WorkerSettings, devices: DeviceRunner) -> DetailPipeline:
    """Build the detail step pipeline: fake models per parent, or the ADR-010 registry models.

    Args:
        settings: Worker settings.
        devices: Runs every parent step on the worker's device.

    Returns:
        The pipeline the detail runner executes.
    """
    if settings.separator == "fake":
        return DetailPipeline(FakeSource(settings.fake_detail_step_seconds), devices)
    return DetailPipeline(RegistrySource(_real_provider(settings)), devices)
