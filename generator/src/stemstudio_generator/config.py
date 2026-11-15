"""Generator configuration from environment variables (no secrets are needed or read).

Variables of 009 data-model.md 1 and 010 data-model.md 1. The same code base runs as the
``generator`` service (engine ``acestep``: kinds ``restyle`` and ``song``) and as the ``singer``
service (engine ``soulx``: kind ``voiceConversion`` and the voice-sample checks); CI and E2E use
engine ``fake`` with a ``STEMSTUDIO_FAKE_GENERATOR_MODE``.
"""

import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Final, Literal, cast

from stemstudio_generator.schemas import GenerationKind, GeneratorEngine

FakeGeneratorMode = Literal[
    "ready", "no-gpu", "oom", "oom-twice", "busy", "slow", "unvoiced-sample"
]
"""Behaviour of the fake engine (CI/E2E): ``ready`` normal; ``no-gpu`` not ready; ``oom`` the
first attempt runs out of GPU memory and the low-memory retry succeeds; ``oom-twice`` both fail;
``busy`` the fake NVML never reports enough free memory; ``slow`` long steps; ``unvoiced-sample``
every voice-sample check fails as unvoiced (1.1.0)."""

_ENGINES: Final[tuple[GeneratorEngine, ...]] = ("acestep", "soulx", "fake")
_FAKE_MODES: Final[tuple[FakeGeneratorMode, ...]] = (
    "ready", "no-gpu", "oom", "oom-twice", "busy", "slow", "unvoiced-sample",
)  # fmt: skip
_KINDS: Final[tuple[GenerationKind, ...]] = ("restyle", "song", "voiceConversion")
_DEFAULT_KINDS: Final[dict[GeneratorEngine, tuple[GenerationKind, ...]]] = {
    "acestep": ("restyle", "song"),
    "soulx": ("voiceConversion",),
    "fake": ("restyle", "song"),
}

MAX_STEM_BYTES: Final = 300 * 1024 * 1024
"""Largest ``stem``, ``target``, ``prompt`` or ``accompaniment`` part (300 MiB)."""

MAX_SAMPLE_BYTES: Final = 20 * 1024 * 1024
"""Largest voice-sample upload (20 MiB, 1.1.0)."""

SAMPLE_MIN_SECONDS: Final = 10.0
"""Shortest usable voice sample (seconds)."""

SAMPLE_MAX_SECONDS: Final = 60.0
"""Longest usable voice sample (seconds)."""

SAMPLE_MIN_VOICED_RATIO: Final = 0.5
"""Voiced share a voice sample needs (RMVPE F0 > 0)."""

TARGET_MIN_VOICED_RATIO: Final = 0.1
"""Voiced share below which a conversion target fails with ``unvoicedTarget``."""

CHECKED_SAMPLE_KEEP_SECONDS: Final = 600.0
"""How long an ``ok`` sample stays downloadable (10 minutes)."""

JOB_RETENTION_SECONDS: Final = 24 * 3600.0
"""Terminal jobs not deleted by the backend are removed after 24 h."""

MEMORY_RECHECK_SECONDS: Final = 10.0
"""Interval of the free-memory re-check while ``waitingForGpu`` (ADR-018 D4)."""

MEMORY_CAP_MARGIN_MIB: Final = 256
"""Head-room kept below the free memory when the per-process memory fraction is set."""


@dataclass(frozen=True, slots=True)
class GeneratorSettings:
    """Runtime settings of one generator or singer instance.

    Attributes:
        engine: ``acestep``, ``soulx`` or ``fake`` (``STEMSTUDIO_GENERATOR``, default ``acestep``).
        kinds: Job kinds served (``STEMSTUDIO_GENERATOR_KINDS``, comma list; default by engine:
            ``acestep``/``fake`` -> ``restyle,song``, ``soulx`` -> ``voiceConversion``).
        fake_mode: Fake engine behaviour (``STEMSTUDIO_FAKE_GENERATOR_MODE``); fake engine only.
        fake_step_seconds: Seconds per fake step (``STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS``).
        data_dir: Job JSON files and scratch (``STEMSTUDIO_DATA_DIR``).
        model_dir: Model cache root (``STEMSTUDIO_MODEL_DIR``; ``/models/acestep``,
            ``/models/soulx``, ``/models/hf``), never inside the repository.
        gpu_lease_file: Shared cross-container lock file (``STEMSTUDIO_GPU_LEASE_FILE``;
            blank -> no file lease, tests and the fake stack only).
        gpu_memory_wait_seconds: Longest wait for enough free GPU memory
            (``STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS``, default 600) before ``gpuBusy``.
        svc_max_target_seconds: Longest vocal stem accepted for conversion
            (``STEMSTUDIO_SVC_MAX_TARGET_SECONDS``, default 480).
    """

    engine: GeneratorEngine = "acestep"
    kinds: tuple[GenerationKind, ...] = ("restyle", "song")
    fake_mode: FakeGeneratorMode = "ready"
    fake_step_seconds: float = 1.0
    data_dir: Path = Path("/data")
    model_dir: Path = Path("/models")
    gpu_lease_file: Path | None = Path("/gpu-lease/gpu.lock")
    gpu_memory_wait_seconds: float = 600.0
    svc_max_target_seconds: float = 480.0

    @property
    def voice_sample_check(self) -> bool:
        """True when this instance serves the voice-sample endpoints (singer service)."""
        return "voiceConversion" in self.kinds


def load_settings(environ: Mapping[str, str] | None = None) -> GeneratorSettings:
    """Build settings from environment variables.

    Args:
        environ: Variables to read; defaults to ``os.environ``.

    Returns:
        The validated settings.

    Raises:
        ValueError: A variable has a value outside its allowed set.
    """
    env = os.environ if environ is None else environ
    engine = cast(GeneratorEngine, _choice(env, "STEMSTUDIO_GENERATOR", _ENGINES))
    return GeneratorSettings(
        engine=engine,
        kinds=_kinds(env.get("STEMSTUDIO_GENERATOR_KINDS"), engine),
        fake_mode=_fake_mode(env),
        fake_step_seconds=_seconds(env, "STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS", 1.0),
        data_dir=Path(env.get("STEMSTUDIO_DATA_DIR", "/data")),
        model_dir=Path(env.get("STEMSTUDIO_MODEL_DIR", "/models")),
        gpu_lease_file=_optional_path(env, "STEMSTUDIO_GPU_LEASE_FILE", "/gpu-lease/gpu.lock"),
        gpu_memory_wait_seconds=_seconds(env, "STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS", 600.0),
        svc_max_target_seconds=_seconds(env, "STEMSTUDIO_SVC_MAX_TARGET_SECONDS", 480.0),
    )


def _fake_mode(env: Mapping[str, str]) -> FakeGeneratorMode:
    """Read ``STEMSTUDIO_FAKE_GENERATOR_MODE`` (default ``ready``)."""
    return cast(FakeGeneratorMode, _choice(env, "STEMSTUDIO_FAKE_GENERATOR_MODE", _FAKE_MODES))


def _kinds(raw: str | None, engine: GeneratorEngine) -> tuple[GenerationKind, ...]:
    """Parse the comma list of job kinds; unset -> the engine's default."""
    if raw is None or not raw.strip():
        return _DEFAULT_KINDS[engine]
    kinds = _unique_items(raw)
    if not kinds or not set(kinds) <= set(_KINDS):
        msg = f"STEMSTUDIO_GENERATOR_KINDS must list kinds of {', '.join(_KINDS)}; got {raw!r}"
        raise ValueError(msg)
    return cast(tuple[GenerationKind, ...], kinds)


def _unique_items(raw: str) -> tuple[str, ...]:
    """Non-blank comma-separated items, trimmed, first occurrence kept."""
    return tuple(dict.fromkeys(item.strip() for item in raw.split(",") if item.strip()))


def _choice(env: Mapping[str, str], name: str, allowed: tuple[str, ...]) -> str:
    """Read ``name`` (case-insensitive value) and check it against ``allowed``; first is default."""
    value = env.get(name, allowed[0]).strip().lower()
    if value not in allowed:
        msg = f"{name} must be one of {', '.join(allowed)}; got {value!r}"
        raise ValueError(msg)
    return value


def _seconds(env: Mapping[str, str], name: str, default: float) -> float:
    """Read ``name`` as a non-negative number of seconds; ``default`` when unset."""
    raw = env.get(name)
    if raw is None:
        return default
    try:
        value = float(raw)
    except ValueError:
        value = -1.0
    if value < 0:
        msg = f"{name} must be a non-negative number of seconds; got {raw!r}"
        raise ValueError(msg)
    return value


def _optional_path(env: Mapping[str, str], name: str, default: str) -> Path | None:
    """Read ``name`` as a path; unset -> ``default``; blank -> None (no file lease)."""
    raw = env.get(name, default).strip()
    return Path(raw) if raw else None
