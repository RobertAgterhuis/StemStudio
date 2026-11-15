"""The running job subsystem: stores, reconciliation, scheduler, lanes and services.

Besides the priority scheduler it runs the light lanes (metadata, render), the lyrics jobs
(lookups on the network lane of the metadata jobs, alignments as scheduler units; feature 006)
and the sweep timer.
"""

import functools
from collections.abc import Callable
from dataclasses import dataclass, field
from datetime import UTC, datetime

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.jobs.artwork import ArtworkCapture
from stemstudio_worker.jobs.detail_ports import DetailPipelinePort
from stemstudio_worker.jobs.detail_runner import DetailRunner
from stemstudio_worker.jobs.detail_service import DetailContext, DetailService
from stemstudio_worker.jobs.detail_store import DetailJobStore
from stemstudio_worker.jobs.lyrics_defaults import default_lyrics_parts
from stemstudio_worker.jobs.lyrics_lane import LyricsQueues, LyricsService
from stemstudio_worker.jobs.lyrics_runner import LyricsContext, LyricsParts, LyricsRunner
from stemstudio_worker.jobs.lyrics_store import LyricsJobStore
from stemstudio_worker.jobs.metadata_lane import DisabledLookup, MetadataLane
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.jobs.reconcile import reconcile, reconcile_detail_jobs
from stemstudio_worker.jobs.render_service import NoLibrary, NoRenderer, RenderService
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.jobs.scheduler import PriorityScheduler, SchedulerHooks
from stemstudio_worker.jobs.service import JobService
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.jobs.sweeper import SWEEP_INTERVAL_SECONDS, Sweeper
from stemstudio_worker.jobs.user_artwork import UserArtworkStore
from stemstudio_worker.ports import (
    ImageNormalizer,
    LibraryReader,
    MetadataLookup,
    PipelineAdapters,
    RenderExecutor,
)
from stemstudio_worker.schemas import CPU_DEVICE, DeviceInfo

_STOP_TIMEOUT_SECONDS = 5.0


def utc_now() -> datetime:
    """Return the current time in UTC."""
    return datetime.now(UTC)


def _nothing() -> None:
    """Default idle hook: nothing to release."""


@dataclass(frozen=True, slots=True)
class DeviceHooks:
    """What the job subsystem knows about the processing device (feature 003).

    Attributes:
        info: The worker's device from the start-up probe (``/health``, planned job devices).
        on_idle: Called when the scheduler has nothing queued (releases GPU models).
    """

    info: DeviceInfo = CPU_DEVICE
    on_idle: Callable[[], None] = _nothing


@dataclass(frozen=True, slots=True)
class OnlineParts:
    """The adapters of feature 005 (built by the composition root from ``metadata``).

    Attributes:
        normalizer: Normalises source pictures ("Saving cover"); None: jobs get no picture.
        lookup: Looks songs up for metadata jobs; None: every metadata job is ``disabled``.
    """

    normalizer: ImageNormalizer | None = None
    lookup: MetadataLookup | None = None


@dataclass(frozen=True, slots=True)
class RenderParts:
    """The adapters of feature 007 (built by the composition root from ``render``).

    Attributes:
        executor: Renders library files into one audio file.
        library: Read-only library access; its ``status()`` is ``Health.library``.
    """

    executor: RenderExecutor = field(default_factory=NoRenderer)
    library: LibraryReader = field(default_factory=NoLibrary)


@dataclass(frozen=True, slots=True)
class LaneWiring:
    """What the lanes take from the runtime.

    Attributes:
        paths: Data directory layout.
        clock: Current UTC time.
        lyrics: Runs lyrics jobs (lookups on the network lane, alignments on the scheduler).
        align: Queues an alignment on the priority scheduler.
    """

    paths: DataPaths
    clock: Clock
    lyrics: LyricsRunner
    align: Callable[[str], None]


@dataclass(frozen=True, slots=True)
class Lanes:
    """The light lanes beside the separation scheduler, their stores and the sweep timer.

    Attributes:
        metadata: The metadata job lane (feature 005).
        artwork: Normalised user pictures, kept for one hour (feature 005).
        normalizer: The picture normaliser of ``POST /v1/artwork``; None when unavailable.
        render: The render job lane (feature 007).
        library: The library reader (``Health.library``).
        sweeper: Hourly removal of expired job files.
        lyrics: The lyrics jobs (feature 006).
    """

    metadata: MetadataLane
    artwork: UserArtworkStore
    normalizer: ImageNormalizer | None
    render: RenderService
    library: LibraryReader
    sweeper: Sweeper
    lyrics: LyricsService

    @classmethod
    def start(cls, parts: "WorkerParts", wiring: LaneWiring) -> "Lanes":
        """Start the lanes and the lyrics jobs (after their restart handling) and the sweeper."""
        online, render_parts = parts.online, parts.render
        paths, clock = wiring.paths, wiring.clock
        metadata = MetadataLane.start(paths, online.lookup or DisabledLookup(), clock)
        lyrics = LyricsService.start(wiring.lyrics, _lyrics_queues(metadata, wiring))
        render = RenderService.start(paths, render_parts.executor, clock)
        artwork = UserArtworkStore(paths, clock)
        sweeps = [metadata.sweep, artwork.sweep, render.sweep, lyrics.sweep]
        sweeper = Sweeper(SWEEP_INTERVAL_SECONDS, sweeps)
        sweeper.start()
        return cls(metadata, artwork, online.normalizer, render, render_parts.library, sweeper,
                   lyrics)  # fmt: skip

    def stop(self, timeout: float) -> None:
        """Stop the sweep timer and the lane threads."""
        self.sweeper.stop(timeout)
        self.metadata.stop(timeout)
        self.render.stop(timeout)


def _lyrics_queues(metadata: MetadataLane, wiring: LaneWiring) -> LyricsQueues:
    """Lookups on the metadata jobs' network lane, alignments on the priority scheduler."""
    lookup = functools.partial(metadata.lane.submit_call, wiring.lyrics.lookup)
    return LyricsQueues(lookup=lookup, align=wiring.align)


@dataclass(frozen=True, slots=True)
class WorkerParts:
    """What the job subsystem runs: adapters, detail pipeline, device hooks, online parts.

    ``lyrics`` defaults to stand-ins (lookups ``disabled``, alignments failing, CPU only).
    """

    adapters: PipelineAdapters
    pipeline: DetailPipelinePort
    devices: DeviceHooks = DeviceHooks()
    online: OnlineParts = OnlineParts()
    render: RenderParts = RenderParts()
    lyrics: LyricsParts = field(default_factory=default_lyrics_parts)


def _load(paths: DataPaths, clock: Clock) -> DetailContext:
    """Load both stores and fail the jobs a restart interrupted."""
    store, details = JobStore(paths.jobs), DetailJobStore(paths.detail_jobs)
    store.load_all()
    details.load_all()
    reconcile(store, paths, clock)
    reconcile_detail_jobs(details, paths, clock)
    return DetailContext(store, details, paths, clock)


@dataclass(frozen=True, slots=True)
class WorkerRuntime:
    """Everything the HTTP layer needs while the worker runs.

    Attributes:
        settings: Worker settings.
        adapters: The pipeline adapters (used directly for resolving URLs and accepting uploads).
        paths: Data directory layout.
        store: The job store.
        details: The detail job store.
        scheduler: The priority scheduler with its worker thread.
        service: Creates (or reuses) jobs.
        detail_service: Creates, reads and cancels detail jobs.
        lanes: The metadata lane and the sweep timer.
        device: The worker's device from the start-up probe.
    """

    settings: WorkerSettings
    adapters: PipelineAdapters
    paths: DataPaths
    store: JobStore
    details: DetailJobStore
    scheduler: PriorityScheduler
    service: JobService
    detail_service: DetailService
    lanes: Lanes
    device: DeviceInfo = CPU_DEVICE

    @classmethod
    def start(
        cls, settings: WorkerSettings, adapters: PipelineAdapters, pipeline: DetailPipelinePort
    ) -> "WorkerRuntime":
        """Start on a CPU worker (no device hooks); see :meth:`start_with`."""
        return cls.start_with(settings, WorkerParts(adapters, pipeline))

    @classmethod
    def start_with(cls, settings: WorkerSettings, parts: WorkerParts) -> "WorkerRuntime":
        """Load the jobs, reconcile interrupted ones and start the worker thread."""
        paths = DataPaths(settings.data_dir)
        context = _load(paths, utc_now)
        lyrics = _lyrics_runner(context, parts.lyrics)
        scheduler = _scheduler(context, parts, lyrics.align)
        store, clock = context.jobs, context.clock
        service = JobService(store, scheduler.submit, clock)
        detail_service = DetailService(context, scheduler.submit_detail)
        scheduler.start()
        lanes = Lanes.start(parts, LaneWiring(paths, clock, lyrics, scheduler.submit_align))
        return cls(settings, parts.adapters, paths, store, context.details, scheduler, service,
                   detail_service, lanes, parts.devices.info)  # fmt: skip

    def stop(self) -> None:
        """Stop the worker thread; a job still running is reconciled at the next start."""
        self.lanes.stop(_STOP_TIMEOUT_SECONDS)
        self.scheduler.stop(_STOP_TIMEOUT_SECONDS)


def _lyrics_runner(context: DetailContext, parts: LyricsParts) -> LyricsRunner:
    """Load the lyrics jobs and build their runner."""
    store = LyricsJobStore(context.paths)
    store.load_all()
    return LyricsRunner(LyricsContext(store, context.jobs, context.paths, context.clock), parts)


def _scheduler(
    context: DetailContext, parts: WorkerParts, align: Callable[[str], None]
) -> PriorityScheduler:
    """The scheduler over the standard, align and detail runners of ``context``."""
    job_context = JobContext(context.jobs, context.paths, context.clock, parts.devices.info)
    normalizer = parts.online.normalizer
    capture = None if normalizer is None else ArtworkCapture(normalizer)
    runner = JobRunner(job_context, parts.adapters, capture)
    details = DetailRunner(context, parts.pipeline)
    hooks = SchedulerHooks(on_yield=details.waiting, on_idle=parts.devices.on_idle, align=align)
    return PriorityScheduler(runner.process, details.step, hooks)
