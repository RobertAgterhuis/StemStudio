"""What the routes need, composed by ``main.py`` (the API never builds adapters itself)."""

from collections.abc import Callable
from dataclasses import dataclass, field

from stemstudio_generator.api.samples import CheckedSamples
from stemstudio_generator.api.uploads import UploadLimits
from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.jobs.files import JobFiles
from stemstudio_generator.jobs.scheduler import Scheduler
from stemstudio_generator.ports import Clock, StemMixer, VoiceSampleAnalyzer
from stemstudio_generator.schemas import DeviceInfo, ModelStatus


def _nothing() -> None:
    """Default lifecycle hook."""


@dataclass(slots=True)
class ApiContext:
    """Services of one generator or singer instance.

    Attributes:
        settings: The instance settings (engine, kinds).
        device: The start-up probe result.
        scheduler: The job queue.
        files: Job file layout.
        mixer: Checks and sums uploaded stems (restyle).
        models: Model files of the instance's engines (``/health``).
        clock: Time source of new jobs.
        new_id: New generation ids (32 hex characters).
        limits: Upload size limits.
        on_start: Called when the app starts (reconcile, start the queue thread).
        on_stop: Called when the app stops.
        analyzer: Voice-sample checks (singer service only, else None).
        samples: Checked samples kept for download (singer service only).
    """

    settings: GeneratorSettings
    device: DeviceInfo
    scheduler: Scheduler
    files: JobFiles
    mixer: StemMixer
    models: Callable[[], list[ModelStatus]]
    clock: Clock
    new_id: Callable[[], str]
    limits: UploadLimits = field(default_factory=UploadLimits)
    on_start: Callable[[], None] = _nothing
    on_stop: Callable[[], None] = _nothing
    analyzer: VoiceSampleAnalyzer | None = None
    samples: CheckedSamples | None = None
