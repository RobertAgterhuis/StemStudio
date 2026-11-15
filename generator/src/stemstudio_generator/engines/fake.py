"""Deterministic fake engines of CI and E2E (``STEMSTUDIO_GENERATOR=fake``, 009 data-model 1).

``FakeMusicEngine`` writes seeded noise plus a tone of the requested length (FLAC 48 kHz
stereo 24-bit, before the label tags), one take per seed, in fake steps of
``STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS``. ``STEMSTUDIO_FAKE_GENERATOR_MODE`` selects the
behaviour:

* ``ready``: normal.
* ``no-gpu``: ``load`` raises ``GpuRequiredError`` (the fake probe reports no GPU).
* ``oom``: generating with the ``standard`` profile raises ``GpuOutOfMemoryError``; after the
  low-memory reload it succeeds (the one retry of ADR-018 D4).
* ``oom-twice``: every attempt raises ``GpuOutOfMemoryError``.
* ``busy``: the engine is normal; ``fake_free_memory_mib`` reports less free memory than any
  ``GpuNeed``, so the gate waits and fails ``gpuBusy``.
* ``slow``: every fake step lasts ``SLOW_FACTOR`` times longer (cancel tests).
* ``unvoiced-sample``: the engines are normal; ``FakePitchTracker`` (voice-sample check) hears
  no voice.

``FakeVoiceConverter`` returns the target's voice at 24 kHz mono (``unvoicedTarget`` when the
target is noise) and ``FakePitchTracker`` (``fake_pitch.py``) stands in for RMVPE (010).
"""

import time
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np
import soundfile as sf

from stemstudio_generator.config import (
    TARGET_MIN_VOICED_RATIO,
    FakeGeneratorMode,
    GeneratorSettings,
)
from stemstudio_generator.engines.fake_pitch import FakePitchTracker, read_mono
from stemstudio_generator.engines.profiles import (
    ACESTEP_MODELS,
    SOULX_MODELS,
    load_profile,
    model_statuses,
)
from stemstudio_generator.ports import (
    ConversionInput,
    DeviceUse,
    EngineRestyle,
    EngineSong,
    EngineTake,
    GeneratorError,
    GpuOutOfMemoryError,
    GpuRequiredError,
    MemoryProfile,
    ProgressCallback,
    PromptInvalidError,
    Signal,
    UnvoicedTargetError,
    voiced_ratio,
)
from stemstudio_generator.schemas import (
    PLANNING_STAGE,
    TAKE_SAMPLE_RATE,
    UNVOICED_TARGET_TEXT,
    ModelStatus,
    SongProfile,
)

SLOW_FACTOR: Final = 10.0
"""Step-length multiplier of mode ``slow``."""

TICKS_PER_STEP: Final = 4
"""Progress callbacks per fake step (so cancel is seen within a quarter step)."""

FAKE_TOTAL_MEMORY_MIB: Final = 16_303
"""Total memory of the fake GPU (an RTX 5070 Ti, as the contract examples)."""

FAKE_FREE_MEMORY_MIB: Final = 14_960
"""Free memory of the fake GPU: every standard ``GpuNeed`` fits."""

FAKE_BUSY_FREE_MEMORY_MIB: Final = 4_096
"""Free memory in mode ``busy``: below the smallest ``GpuNeed`` (``SVC_FP16`` 6,144 MiB)."""

SVC_SAMPLE_RATE: Final = 24_000
"""Sample rate of the SoulX-Singer-SVC output (and of ``VoiceConverter.convert``)."""

LOADING_STAGE: Final = "Loading model"
CONVERTING_STAGE: Final = "Converting voice"
NOT_LOADED_TEXT: Final = "The engine model is not loaded."
NO_GPU_TEXT: Final = "No usable NVIDIA GPU (fake generator mode no-gpu)."

Sleep = Callable[[float], None]


def fake_free_memory_mib(mode: FakeGeneratorMode) -> int:
    """NVML free memory the fake GPU reports in ``mode`` (for the fake gate of ``devices``)."""
    return FAKE_BUSY_FREE_MEMORY_MIB if mode == "busy" else FAKE_FREE_MEMORY_MIB


def fake_take_signal(seed: int, frames: int, *, voice: bool = False) -> Signal:
    """Seeded stereo noise plus a seed-dependent tone (and a vibrato 'voice' when ``voice``)."""
    rng = np.random.default_rng(seed)
    t = np.arange(frames, dtype=np.float64) / TAKE_SAMPLE_RATE
    music = 0.3 * np.sin(2 * np.pi * (110.0 + seed % 330) * t)
    if voice:
        music += 0.2 * np.sin(2 * np.pi * 330.0 * t + 3.0 * np.sin(2 * np.pi * 5.0 * t))
    noise = rng.uniform(-0.05, 0.05, (frames, 2))
    return (music[:, None] + noise).astype(np.float32)


@dataclass(frozen=True, slots=True)
class TakePlan:
    """The takes a fake call writes.

    Attributes:
        seeds: One take per seed.
        frames: Length of each take at 48 kHz.
        out_dir: Directory of the take files.
        voice: Add the fake 'voice' (songs with vocals).
        profile: Song profile the takes report.
    """

    seeds: Sequence[int]
    frames: int
    out_dir: Path
    voice: bool = False
    profile: SongProfile | None = None

    def write(self, index: int, seed: int) -> EngineTake:
        """Write take ``index`` as ``take-<index>.flac`` (48 kHz stereo 24-bit)."""
        path = self.out_dir / f"take-{index}.flac"
        signal = fake_take_signal(seed, self.frames, voice=self.voice)
        sf.write(path, signal, TAKE_SAMPLE_RATE, subtype="PCM_24")
        return EngineTake(index, seed, path, self.frames / TAKE_SAMPLE_RATE, self.profile)


class FakeSteps:
    """Fake model steps: load state, progress ticks with sleeps, GPU behaviour of the mode."""

    def __init__(self, settings: GeneratorSettings, sleep: Sleep) -> None:
        """Create the steps for ``settings.fake_mode`` and ``settings.fake_step_seconds``."""
        self.mode = settings.fake_mode
        slow = SLOW_FACTOR if self.mode == "slow" else 1.0
        self._tick_seconds = settings.fake_step_seconds * slow / TICKS_PER_STEP
        self._sleep = sleep
        self.use: DeviceUse | None = None

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Pretend to load the model for ``use.need``; mode ``no-gpu`` raises."""
        if self.mode == "no-gpu":
            raise GpuRequiredError(NO_GPU_TEXT)
        on_progress(0.0, LOADING_STAGE)
        self.use = use
        on_progress(1.0, LOADING_STAGE)

    def loaded(self) -> DeviceUse:
        """The grant of the last ``load``.

        Raises:
            GeneratorError: Nothing is loaded.
        """
        if self.use is None:
            raise GeneratorError(NOT_LOADED_TEXT)
        return self.use

    def check_memory(self, *, retry: bool) -> None:
        """Raise ``GpuOutOfMemoryError`` in mode ``oom-twice``, and in ``oom`` unless ``retry``."""
        if self.mode == "oom-twice" or (self.mode == "oom" and not retry):
            raise GpuOutOfMemoryError

    def run(self, stage: str, part: tuple[int, int], on_progress: ProgressCallback) -> None:
        """One fake step: ``TICKS_PER_STEP`` sleeps; progress of part ``part[0]`` of ``part[1]``."""
        index, parts = part
        for tick in range(1, TICKS_PER_STEP + 1):
            self._sleep(self._tick_seconds)
            on_progress((index * TICKS_PER_STEP + tick) / (parts * TICKS_PER_STEP), stage)


class FakeMusicEngine:
    """``MusicEngine`` fake: deterministic takes, no model, no GPU."""

    def __init__(self, settings: GeneratorSettings, sleep: Sleep = time.sleep) -> None:
        """Create the fake for ``settings.fake_mode``; ``sleep`` is replaced in tests."""
        self._steps = FakeSteps(settings, sleep)

    @property
    def loaded_profile(self) -> MemoryProfile | None:
        """Memory profile of the last ``load`` (None when unloaded)."""
        return None if self._steps.use is None else self._steps.use.need.profile

    def required_models(self) -> list[ModelStatus]:
        """The ACE-Step bundle, always present."""
        return model_statuses(ACESTEP_MODELS, lambda _: True)

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Pretend to load; ``no-gpu`` raises ``GpuRequiredError``."""
        self._steps.load(use, on_progress)

    def restyle(self, request: EngineRestyle, on_progress: ProgressCallback) -> list[EngineTake]:
        """One take per seed with the length of the summed source."""
        self._attempt()
        frames = round(sf.info(request.source).duration * TAKE_SAMPLE_RATE)
        return self._takes(TakePlan(request.params.seeds, frames, request.out_dir), on_progress)

    def song(self, request: EngineSong, on_progress: ProgressCallback) -> list[EngineTake]:
        """One take per seed of ``durationSeconds``; planned only when the LM was loaded."""
        use = self._attempt()
        planned = request.planned and load_profile(use.need).language_model
        if planned:
            on_progress(0.0, PLANNING_STAGE)
        params = request.params
        frames = round(params.duration_seconds * TAKE_SAMPLE_RATE)
        plan = TakePlan(params.seeds, frames, request.out_dir, voice=not params.instrumental,
                        profile="planned" if planned else "direct")  # fmt: skip
        return self._takes(plan, on_progress)

    def unload(self) -> None:
        """Forget the loaded profile."""
        self._steps.use = None

    def _attempt(self) -> DeviceUse:
        """Check the load and the OOM mode (the low-memory profile is the retry)."""
        use = self._steps.loaded()
        self._steps.check_memory(retry=use.need.profile == "lowMemory")
        return use

    def _takes(self, plan: TakePlan, on_progress: ProgressCallback) -> list[EngineTake]:
        """Run one fake step per seed, then write the takes."""
        count = len(plan.seeds)
        for part in range(count):
            self._steps.run(f"Generating take {part + 1} of {count}", (part, count), on_progress)
        return [plan.write(index, seed) for index, seed in enumerate(plan.seeds, start=1)]


class FakeVoiceConverter:
    """``VoiceConverter`` fake: the target's voice, resampled to 24 kHz mono and scaled.

    Mode ``oom`` fails the first attempt for each target and lets the retry succeed (SoulX has
    no low-memory profile); ``oom-twice`` fails every attempt.
    """

    def __init__(self, settings: GeneratorSettings, sleep: Sleep = time.sleep) -> None:
        """Create the fake for ``settings.fake_mode``; ``sleep`` is replaced in tests."""
        self._steps = FakeSteps(settings, sleep)
        self._tracker = FakePitchTracker()
        self._attempted: set[Path] = set()

    def required_models(self) -> list[ModelStatus]:
        """The singer models, always present."""
        return model_statuses(SOULX_MODELS, lambda _: True)

    def load(self, use: DeviceUse, on_progress: ProgressCallback) -> None:
        """Pretend to load; ``no-gpu`` raises ``GpuRequiredError``."""
        self._steps.load(use, on_progress)

    def convert(self, request: ConversionInput, on_progress: ProgressCallback) -> Signal:
        """Deterministic 'conversion' of the target (24 kHz mono float32, the target length).

        Raises:
            InvalidInputError: The target is not readable.
            PromptInvalidError: The prompt is not readable.
            UnvoicedTargetError: The target is less than 10 % voiced.
        """
        self._steps.loaded()
        retry = request.target in self._attempted
        self._attempted.add(request.target)
        self._steps.check_memory(retry=retry)
        read_mono(request.prompt, PromptInvalidError)
        target, rate = read_mono(request.target)
        if voiced_ratio(self._tracker.f0(request.target)) < TARGET_MIN_VOICED_RATIO:
            raise UnvoicedTargetError(UNVOICED_TARGET_TEXT)
        self._steps.run(CONVERTING_STAGE, (0, 1), on_progress)
        resampled = _resample_linear(target, rate, SVC_SAMPLE_RATE)
        converted: Signal = np.clip(0.8 * resampled, -1.0, 1.0)
        return converted.astype(np.float32, copy=False)

    def unload(self) -> None:
        """Forget the loaded model."""
        self._steps.use = None


def _resample_linear(mono: Signal, rate: int, target_rate: int) -> Signal:
    """Linear-interpolation resample (good enough for a fake) to ``round(n * target / rate)``."""
    count = round(mono.size * target_rate / rate)
    positions = np.arange(count, dtype=np.float64) * rate / target_rate
    return np.interp(positions, np.arange(mono.size), mono).astype(np.float32)
