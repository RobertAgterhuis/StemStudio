"""Composition root of the generator and singer services (the only module that wires adapters).

``compose(settings)`` builds, for ``STEMSTUDIO_GENERATOR``:

* ``fake``: the fake engines, the fake probe and the real gate over a fake NVML (CI, E2E);
* ``acestep``: the ACE-Step 1.5 engine (lazy import), the torch/NVML probe and the file-lease
  gate with the torch memory cap (``generator`` service);
* ``soulx``: the SoulX-Singer-SVC converter (``singer`` service, 010).

At start-up interrupted jobs are reconciled, queued jobs resumed and the queue thread started.
"""

import logging
import time
import uuid
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Final

import uvicorn
from fastapi import FastAPI

from stemstudio_generator.api.app import create_app
from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.audio.flac import FlacWriter
from stemstudio_generator.audio.pipeline import AudioStemMixer
from stemstudio_generator.config import GeneratorSettings, load_settings
from stemstudio_generator.devices.fake import FakeDeviceProbe, fake_gate
from stemstudio_generator.devices.gate import GatePolicy, GpuAccess, LeaseGpuGate
from stemstudio_generator.devices.lease import open_lease
from stemstudio_generator.devices.memory import MemoryCap, NoMemoryCap, load_torch_cap
from stemstudio_generator.devices.nvml import GpuMemory, PynvmlReader
from stemstudio_generator.devices.probe import GpuProbe, load_torch_probe
from stemstudio_generator.engines.acestep_engine import AceStepEngine
from stemstudio_generator.engines.fake import (
    FAKE_TOTAL_MEMORY_MIB,
    FakeMusicEngine,
    fake_free_memory_mib,
)
from stemstudio_generator.engines.profiles import MUSIC_GENERATOR_LABEL, MUSIC_TAKE_MODEL
from stemstudio_generator.jobs.clock import SystemClock
from stemstudio_generator.jobs.files import JobFiles
from stemstudio_generator.jobs.reconcile import reconcile
from stemstudio_generator.jobs.restyle import RestyleHandler, TakeIdentity, TakeOutput
from stemstudio_generator.jobs.runner import JobHandler, JobRunner
from stemstudio_generator.jobs.scheduler import Scheduler
from stemstudio_generator.jobs.store import JsonJobStore
from stemstudio_generator.ports import Clock, DeviceProbe, GpuGate, MusicEngine, TakeWriter
from stemstudio_generator.schemas import DeviceInfo, GenerationKind, ModelStatus

PORT: Final = 8010
"""Port of the service inside the compose network (no host port)."""

_LOG: Final = logging.getLogger(__name__)


def _new_id() -> str:
    return uuid.uuid4().hex


@dataclass(frozen=True, slots=True)
class Wiring:
    """Replaceable infrastructure (tests swap these).

    Attributes:
        clock: Time source of jobs, the gate and the scheduler.
        writer: Take writer (default: ffmpeg ``FlacWriter``).
        new_id: Generation id factory.
        start_worker: Start the queue thread at start-up.
        sleep: Sleep of the fake engines.
    """

    clock: Clock = field(default_factory=SystemClock)
    writer: TakeWriter | None = None
    new_id: Callable[[], str] = _new_id
    start_worker: bool = True
    sleep: Callable[[float], None] = time.sleep


@dataclass(frozen=True, slots=True)
class Composition:
    """The composed instance.

    Attributes:
        app: The FastAPI app.
        scheduler: The job queue.
        store: The job store.
        files: Job file layout.
        gate: The GPU gate.
    """

    app: FastAPI
    scheduler: Scheduler
    store: JsonJobStore
    files: JobFiles
    gate: GpuGate


@dataclass(frozen=True, slots=True)
class _Devices:
    info: DeviceInfo
    gate: GpuGate


def compose(settings: GeneratorSettings, wiring: Wiring | None = None) -> Composition:
    """Build the instance for ``settings``."""
    wiring = wiring or Wiring()
    store = JsonJobStore(settings.data_dir)
    files = JobFiles(store)
    devices = _devices(settings, wiring.clock)
    handlers = _handlers(settings, wiring, files)
    scheduler = Scheduler(store, JobRunner(devices.gate, handlers), wiring.clock)
    context = ApiContext(
        settings=settings, device=devices.info, scheduler=scheduler, files=files,
        mixer=AudioStemMixer(), models=lambda: _models(handlers), clock=wiring.clock,
        new_id=wiring.new_id, on_start=lambda: _start(scheduler, store, wiring),
        on_stop=scheduler.stop,
    )  # fmt: skip
    return Composition(create_app(context), scheduler, store, files, devices.gate)


def create() -> FastAPI:
    """The app for the environment's settings (uvicorn factory)."""
    return compose(load_settings()).app


def run() -> None:
    """Console entry point: serve on ``0.0.0.0:8010`` (internal compose network only)."""
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")
    uvicorn.run(create(), host="0.0.0.0", port=PORT)  # noqa: S104 - container-internal service


def _start(scheduler: Scheduler, store: JsonJobStore, wiring: Wiring) -> None:
    scheduler.resume(reconcile(store, wiring.clock))
    if wiring.start_worker:
        scheduler.start()


def _devices(settings: GeneratorSettings, clock: Clock) -> _Devices:
    if settings.engine == "fake":
        memory = GpuMemory(FAKE_TOTAL_MEMORY_MIB, fake_free_memory_mib(settings.fake_mode))
        probe: DeviceProbe = FakeDeviceProbe(settings.fake_mode, memory)
        gate = fake_gate(memory, clock, settings.gpu_memory_wait_seconds, settings.gpu_lease_file)
        return _Devices(probe.probe(), gate)
    return _gpu_devices(settings, clock)


def _gpu_devices(settings: GeneratorSettings, clock: Clock) -> _Devices:
    info = GpuProbe(load_torch_probe, PynvmlReader).probe()
    _LOG.info("GPU probe: %s", info.model_dump_json())
    cap: MemoryCap = load_torch_cap() if info.kind == "gpu" else NoMemoryCap()
    access = GpuAccess(
        lease=open_lease(settings.gpu_lease_file, clock), nvml=PynvmlReader(), cap=cap,
        name=info.name,
    )  # fmt: skip
    policy = GatePolicy(wait_seconds=settings.gpu_memory_wait_seconds)
    return _Devices(info, LeaseGpuGate(access, clock, policy))


def _handlers(
    settings: GeneratorSettings, wiring: Wiring, files: JobFiles
) -> dict[GenerationKind, JobHandler]:
    writer = wiring.writer or FlacWriter()
    handlers: dict[GenerationKind, JobHandler] = {}
    if "restyle" in settings.kinds:
        output = TakeOutput(writer, TakeIdentity(MUSIC_TAKE_MODEL, MUSIC_GENERATOR_LABEL), files)
        handlers["restyle"] = RestyleHandler(
            _music_engine(settings, wiring), AudioStemMixer(), output
        )
    return handlers


def _music_engine(settings: GeneratorSettings, wiring: Wiring) -> MusicEngine:
    if settings.engine == "fake":
        return FakeMusicEngine(settings, wiring.sleep)
    return AceStepEngine(settings)


def _models(handlers: dict[GenerationKind, JobHandler]) -> list[ModelStatus]:
    return [model for handler in handlers.values() for model in handler.engine.required_models()]
