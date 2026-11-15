"""Ports (``typing.Protocol`` interfaces) between the generator's job runner and its adapters.

Owned by the generative agent (implementations) and faked in every unit test:

* ``MusicEngine``: ACE-Step 1.5 (``engines/acestep_engine.py``, lazy ``acestep`` import) and
  ``FakeMusicEngine`` (``engines/fake.py``): restyle (``cover``/``repaint``, 009) and song
  (``text2music``, 010).
* ``VoiceConverter`` and ``PitchTracker``: SoulX-Singer-SVC and RMVPE on the singer service
  (``engines/soulx_svc.py``, ``engines/rmvpe.py``) and their fakes (010).
* ``GpuGate``: the cross-container ``flock`` lease on the shared lock file, the NVML free-memory
  check that picks the first profile that fits (standard -> low-memory -> wait), the per-process
  memory cap and the OOM classification (``devices/``, ADR-018 D3/D4).
* ``DeviceProbe``: the start-up probe (same reason texts as the worker, ADR-012 D2).
* ``JobStore`` and ``Clock``: persisted job state and time (``jobs/``).

Dependency rule (enforced by tools/quality): ``api -> jobs -> ports <- {engines, devices, audio}``;
``engines``, ``devices`` and ``audio`` import only ``ports``, ``schemas`` and ``config``; only
``main.py`` composes. Adapters signal expected failures by raising a ``GeneratorError``
subclass whose message is shown to the user verbatim (readable, no secrets).
"""

from collections.abc import Callable, Sequence
from contextlib import AbstractContextManager
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Final, Literal, Protocol, runtime_checkable

import numpy as np
import numpy.typing as npt

from stemstudio_generator.schemas import (
    OUT_OF_MEMORY_TEXT,
    DeviceInfo,
    Generation,
    GenerationErrorCode,
    JobDevice,
    ModelStatus,
    RestyleParams,
    SongParams,
    SongProfile,
    VoiceConversionParams,
)

ProgressCallback = Callable[[float, str], None]
"""``(fraction 0..1 within the current step, stage text)``; raises ``Cancelled`` when the job
was cancelled, so engines stop between diffusion steps."""

WaitCallback = Callable[[str], None]
"""Called with a readable stage text while the gate waits for the lease or free memory."""

MemoryProfile = Literal["standard", "lowMemory"]
"""``standard`` everything on the GPU; ``lowMemory`` DiT/VAE/text encoder offloaded to the CPU."""


# ------------------------------------------------------------------ errors


class GeneratorError(Exception):
    """Expected failure of a job step; ``code`` and the message become ``Generation.error``."""

    code: GenerationErrorCode = "engineError"


class Cancelled(Exception):  # noqa: N818 - control-flow signal, not an error
    """Raised by a ``ProgressCallback`` when the job was cancelled."""


class GpuOutOfMemoryError(GeneratorError):
    """The GPU ran out of memory (CUDA OOM or ACE-Step's own VRAM guard)."""

    code: GenerationErrorCode = "outOfMemory"

    def __init__(self, message: str = OUT_OF_MEMORY_TEXT) -> None:
        """Create the error with the readable default message."""
        super().__init__(message)


class GpuBusyError(GeneratorError):
    """Not enough free GPU memory within the wait limit."""

    code: GenerationErrorCode = "gpuBusy"


class GpuRequiredError(GeneratorError):
    """The GPU is not usable (any longer)."""

    code: GenerationErrorCode = "gpuRequired"


class ModelDownloadError(GeneratorError):
    """Weights could not be downloaded or failed their pinned revision / checksum."""

    code: GenerationErrorCode = "modelDownloadFailed"


class InvalidInputError(GeneratorError):
    """Uploaded audio could not be read or does not fit the request."""

    code: GenerationErrorCode = "invalidInput"


class UnvoicedTargetError(GeneratorError):
    """The conversion target has less than 10 % voiced frames (1.1.0)."""

    code: GenerationErrorCode = "unvoicedTarget"


class PromptInvalidError(GeneratorError):
    """The voice sample (prompt) could not be used (1.1.0)."""

    code: GenerationErrorCode = "promptInvalid"


# ------------------------------------------------------------------ GPU needs and gate


@dataclass(frozen=True, slots=True)
class GpuNeed:
    """Free GPU memory a model step needs: measured NVML peak minus idle plus 1 GiB margin.

    Attributes:
        name: Constant name, for logs and stage texts.
        memory_mib: Required free memory in MiB.
        profile: Memory profile the engine must use when this need is chosen.
    """

    name: str
    memory_mib: int
    profile: MemoryProfile = "standard"


COVER_TURBO: Final = GpuNeed("COVER_TURBO", 13_926)
"""Restyle, standard profile, worst case full 4:22 song (009 research S-03: 12.6 GB + 1 GiB)."""

COVER_TURBO_LOWMEM: Final = GpuNeed("COVER_TURBO_LOWMEM", 8_602, "lowMemory")
"""Restyle with CPU offload (7.4 GB + 1 GiB); about 4x slower."""

SONG_PLANNED: Final = GpuNeed("SONG_PLANNED", 12_288)
"""Song with the 1.7B LM planner (pt backend), LM loaded **directly in bf16** (Phase 0 re-measure:
11,832 MiB, rounded up). Loading it with ACE-Step's default loader (fp32 then cast) peaks at
about 15.4 GB; the engine adapter must pass ``torch_dtype=bfloat16`` at load time."""

SONG_DIRECT: Final = GpuNeed("SONG_DIRECT", 9_830)
"""Song with the DiT alone (``thinking=False``), 9.6 GB (010 research S-04)."""

SONG_DIRECT_LOWMEM: Final = GpuNeed("SONG_DIRECT_LOWMEM", 7_578, "lowMemory")
"""Song with the DiT alone and CPU offload, 7.4 GB."""

SVC_FP16: Final = GpuNeed("SVC_FP16", 6_144)
"""SoulX-Singer-SVC in fp16 with RMVPE and whisper-base, 6.0 GB (010 research S-05)."""

RESTYLE_NEEDS: Final[tuple[GpuNeed, ...]] = (COVER_TURBO, COVER_TURBO_LOWMEM)
"""Restyle profiles in order of preference (the gate picks the first that fits)."""

SONG_NEEDS: Final[tuple[GpuNeed, ...]] = (SONG_PLANNED, SONG_DIRECT, SONG_DIRECT_LOWMEM)
"""Song profiles in order of preference; ``SONG_PLANNED`` only when ``thinking`` is requested."""

CONVERSION_NEEDS: Final[tuple[GpuNeed, ...]] = (SVC_FP16,)
"""Voice-conversion profile."""


@dataclass(frozen=True, slots=True)
class DeviceUse:
    """What the gate granted for one step.

    Attributes:
        need: The chosen need (its ``profile`` tells the engine how to load).
        device: Device record for ``Generation.device``.
        free_memory_mib: NVML free memory when the step was admitted.
    """

    need: GpuNeed
    device: JobDevice
    free_memory_mib: int


@runtime_checkable
class GpuGate(Protocol):
    """One GPU step at a time across containers, with the free-memory policy (ADR-018 D3/D4)."""

    def hold(
        self, needs: Sequence[GpuNeed], on_wait: WaitCallback
    ) -> AbstractContextManager[DeviceUse]:
        """Take the lease and admit the first need that fits; release on exit (also on errors).

        Raises:
            GpuBusyError: No need fitted within ``gpu_memory_wait_seconds``.
            GpuRequiredError: The GPU is not usable.
        """
        ...

    def is_out_of_memory(self, error: BaseException) -> bool:
        """True for a CUDA OOM or ACE-Step's "Insufficient free VRAM" guard."""
        ...


@runtime_checkable
class DeviceProbe(Protocol):
    """Start-up GPU probe; never raises (no GPU -> ``kind == "cpu"`` with the reason)."""

    def probe(self) -> DeviceInfo:
        """Probe CUDA, the architecture list, a test kernel and NVML once."""
        ...


# ------------------------------------------------------------------ engines


@dataclass(frozen=True, slots=True)
class EngineRestyle:
    """Input of ``MusicEngine.restyle``.

    Attributes:
        source: Summed source (WAV): the range for ``range``, the whole mix for ``inPlace``.
        params: The request parameters (caption, strength, seeds, mode, range).
        out_dir: Directory for the take files.
    """

    source: Path
    params: RestyleParams
    out_dir: Path


@dataclass(frozen=True, slots=True)
class EngineSong:
    """Input of ``MusicEngine.song`` (1.1.0).

    Attributes:
        params: The request parameters (caption, lyrics, language, tempo, duration, seeds).
        planned: Run the language-model planner (``thinking=True``, need ``SONG_PLANNED``).
        out_dir: Directory for the take files.
    """

    params: SongParams
    planned: bool
    out_dir: Path


@dataclass(frozen=True, slots=True)
class EngineTake:
    """One take written by an engine (FLAC 48 kHz stereo, before the label tags are added).

    Attributes:
        index: Take number from 1.
        seed: Seed used.
        path: The audio file.
        duration_seconds: Length.
        profile: Song profile that ran (songs only).
    """

    index: int
    seed: int
    path: Path
    duration_seconds: float
    profile: SongProfile | None = None


@runtime_checkable
class MusicEngine(Protocol):
    """ACE-Step 1.5 behind the generator (restyle 009, song 010)."""

    def required_models(self) -> list[ModelStatus]:
        """Model files of this engine with presence and download size (for ``/health``)."""
        ...

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Download (first use) and load the models for ``use.need.profile``."""
        ...

    def restyle(self, request: EngineRestyle, on_progress: ProgressCallback) -> list[EngineTake]:
        """Run ``cover`` (range) or ``repaint`` (in place); one take per seed."""
        ...

    def song(self, request: EngineSong, on_progress: ProgressCallback) -> list[EngineTake]:
        """Run ``text2music``; one take per seed (1.1.0)."""
        ...

    def unload(self) -> None:
        """Drop the handlers and empty the CUDA cache (the queue is empty)."""
        ...


@dataclass(frozen=True, slots=True)
class ConversionInput:
    """Input of ``VoiceConverter.convert`` (1.1.0).

    Attributes:
        target: The vocal stem to convert.
        prompt: The voice-profile sample (10-60 s).
        params: Pitch shift, steps, cfg, gain, profile name.
    """

    target: Path
    prompt: Path
    params: VoiceConversionParams


Signal = npt.NDArray[np.float32]
"""Mono or (frames, channels) float32 audio."""


@runtime_checkable
class VoiceConverter(Protocol):
    """SoulX-Singer-SVC on the singer service (1.1.0)."""

    def required_models(self) -> list[ModelStatus]:
        """Model files (SVC checkpoint, RMVPE, whisper-base) with presence and size."""
        ...

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Download (first use) and load the SVC model in fp16 on CUDA."""
        ...

    def convert(self, request: ConversionInput, on_progress: ProgressCallback) -> Signal:
        """Convert the target's timbre to the prompt's; 24 kHz mono float32 of the target length.

        Raises:
            UnvoicedTargetError: The target is less than 10 % voiced.
            PromptInvalidError: The prompt cannot be used.
        """
        ...

    def unload(self) -> None:
        """Drop the model and empty the CUDA cache."""
        ...


@runtime_checkable
class PitchTracker(Protocol):
    """RMVPE F0 extraction (CPU for sample checks, CUDA under the gate for conversions)."""

    def f0(self, audio: Path) -> npt.NDArray[np.float32]:
        """F0 per 10 ms frame in Hz; 0 marks an unvoiced frame."""
        ...


# ------------------------------------------------------------------ jobs and time


@runtime_checkable
class JobStore(Protocol):
    """Persisted generation jobs (one JSON file per job under ``data_dir/jobs``)."""

    def save(self, job: Generation) -> None:
        """Create or replace the job file atomically."""
        ...

    def get(self, generation_id: str) -> Generation | None:
        """The job, or None when unknown."""
        ...

    def list_all(self) -> list[Generation]:
        """Every stored job, oldest first."""
        ...

    def delete(self, generation_id: str) -> None:
        """Remove the job file and its directory (no error when unknown)."""
        ...


@runtime_checkable
class Clock(Protocol):
    """Wall clock and sleeping (faked in tests)."""

    def now(self) -> datetime:
        """Current UTC time."""
        ...

    def sleep(self, seconds: float) -> None:
        """Block for ``seconds``."""
        ...


def voiced_ratio(f0: npt.NDArray[np.float32]) -> float:
    """Share of voiced frames (F0 > 0); 0.0 for an empty curve."""
    return float(np.count_nonzero(f0 > 0) / f0.size) if f0.size else 0.0
