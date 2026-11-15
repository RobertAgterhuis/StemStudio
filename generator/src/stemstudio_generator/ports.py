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


# ------------------------------------------------------------------ pinned code and weights


@dataclass(frozen=True, slots=True)
class WeightsPin:
    """A Hugging Face model repository pinned by revision (ADR-009 rule 1, ADR-018 D5).

    Attributes:
        repo: Repository id.
        revision: Commit of the repository.
        files: Files used from it, with their SHA-256 where measured (empty string: recorded by
            the CTO from the cache, 010 tasks T061).
    """

    repo: str
    revision: str
    files: tuple[tuple[str, str], ...] = ()


ACESTEP_CODE_COMMIT: Final = "ca1e85fe9430179831e6bc6be790c332190a3866"
"""ACE-Step 1.5 source commit (MIT), the ``acestep`` extra's git pin."""

SOULX_CODE_COMMIT: Final = "81aeb3ae772c70093c3de74dc23c92d983801ae4"
"""SoulX-Singer source commit (Apache-2.0), fetched by the singer image build."""

ACESTEP_WEIGHTS: Final = WeightsPin(
    "ACE-Step/Ace-Step1.5",
    "19671f406d603126926c1b7e2adc169acbcade22",
    (
        (
            "acestep-v15-turbo/model.safetensors",
            "3f6e0797fad420a39bd33979eb6e840e30989e34a3794e843d23b60ec6e422d7",
        ),
        (
            "acestep-5Hz-lm-1.7B/model.safetensors",
            "f161689da73e5ecefa28ff780d51c2d92a00f056d021d7933c779ed5c6cd7db8",
        ),
    ),
)
"""ACE-Step 1.5 weights bundle (MIT): turbo DiT, VAE, Qwen3-Embedding-0.6B, 5Hz LM 1.7B."""

SOULX_SVC_WEIGHTS: Final = WeightsPin(
    "Soul-AILab/SoulX-Singer",
    "40493ad90286056c7a9095035164434a79daa8c9",
    (("model-svc.pt", "38e72d9e15be6eef3cc937daaf81421a7b5b546816786530bb9aa346cee5d43c"),),
)
"""SoulX-Singer-SVC checkpoint (Apache-2.0, 2.79 GB)."""

RMVPE_WEIGHTS: Final = WeightsPin(
    "Soul-AILab/SoulX-Singer-Preprocess",
    "83dc50289d22a81b1e9998f5b9e111aef7c1fdcd",
    (("rmvpe/rmvpe.pt", "6d62215f4306e3ca278246188607209f09af3dc77ed4232efdd069798c4ec193"),),
)
"""RMVPE F0 extractor (Apache-2.0, 0.18 GB)."""

WHISPER_BASE_WEIGHTS: Final = WeightsPin(
    "openai/whisper-base", "e37978b90ca9030d5170a5c07aadb050351a65bb"
)
"""whisper-base content encoder of SoulX-SVC (Apache-2.0); must be loaded by this revision."""

ACESTEP_CHECKPOINT_DIR: Final = "acestep/checkpoints"
"""Bundle directory under ``model_dir`` (ACE-Step ``checkpoint_dir``; has REVISION, SHA256SUMS)."""

SOULX_SVC_FILE: Final = "soulx/model-svc.pt"
"""SoulX-Singer-SVC checkpoint under ``model_dir`` (``soulx/REVISION`` + ``soulx/SHA256SUMS``)."""

RMVPE_FILE: Final = "soulx/rmvpe/rmvpe.pt"
"""RMVPE checkpoint under ``model_dir``."""

WHISPER_BASE_DIR: Final = "soulx/whisper-base"
"""whisper-base snapshot under ``model_dir`` (REVISION, SHA256SUMS); loaded from this path."""

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
