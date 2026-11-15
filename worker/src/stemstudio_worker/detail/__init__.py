"""Detail (second-stage) separation of the standard stems (stem-separation agent, feature 002).

Contract (binding for the parallel wave): the composition root calls
``create_detail_adapters(settings)``; the signature is fixed. Models, gating, folding,
re-assignment and checks follow ``specs/002-detail-separation/research.md`` (D-02..D-05).
``create_detail_pipeline(settings)`` builds the step pipeline the detail jobs run.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail.download import ModelDownloader
from stemstudio_worker.detail.fake import FakeModelProvider, FakeSource
from stemstudio_worker.detail.runtimes import RealModelProvider
from stemstudio_worker.detail.steps import DetailPipeline, RegistrySource
from stemstudio_worker.ports import DetailAdapters

__all__ = ["create_detail_adapters", "create_detail_pipeline"]


def create_detail_adapters(settings: WorkerSettings) -> DetailAdapters:
    """Build the detail adapters for ``settings.separator`` (real models or fakes).

    Nothing is downloaded or loaded here; models are fetched on first use into
    ``settings.model_dir``.

    Args:
        settings: Worker settings (separator kind, device, model directory, fake step delay).

    Returns:
        The detail adapters.
    """
    if settings.separator == "fake":
        return DetailAdapters(
            models=FakeModelProvider(FakeSource(settings.fake_detail_step_seconds))
        )
    downloader = ModelDownloader(settings.model_dir)
    return DetailAdapters(models=RealModelProvider(downloader, settings.device))


def create_detail_pipeline(settings: WorkerSettings) -> DetailPipeline:
    """Build the detail step pipeline: fake models per parent, or the ADR-010 registry models.

    Args:
        settings: Worker settings.

    Returns:
        The pipeline the detail runner executes.
    """
    if settings.separator == "fake":
        return DetailPipeline(FakeSource(settings.fake_detail_step_seconds))
    return DetailPipeline(RegistrySource(create_detail_adapters(settings).models))
