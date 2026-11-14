"""Source resolution, upload intake, download and ffmpeg normalisation (audio-ingest agent).

Contract (binding for the parallel wave): the composition root calls
``create_ingest_adapters(settings)``.
The audio-ingest agent replaces the placeholder body; the signature is fixed.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import IngestAdapters

__all__ = ["create_ingest_adapters"]


def create_ingest_adapters(settings: WorkerSettings) -> IngestAdapters:
    """Build the production ingest adapters.

    Args:
        settings: Worker settings (data directory, separator kind, device, limits).

    Returns:
        Resolver, upload intake, fetcher and normalizer.

    Raises:
        NotImplementedError: Placeholder until the audio-ingest agent implements it.
    """
    msg = "create_ingest_adapters is implemented by the audio-ingest agent"
    raise NotImplementedError(msg)
