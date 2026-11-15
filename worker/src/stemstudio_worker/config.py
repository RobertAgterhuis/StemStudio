"""Worker configuration from environment variables (no secrets are needed or read)."""

import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, cast

SeparatorKind = Literal["demucs", "fake"]
DeviceKind = Literal["cpu", "cuda", "auto"]
FakeDeviceKind = Literal["cpu", "gpu", "gpu-oom"]

_SEPARATORS: tuple[SeparatorKind, ...] = ("demucs", "fake")
_DEVICES: tuple[DeviceKind, ...] = ("cpu", "cuda", "auto")
_FAKE_DEVICES: tuple[FakeDeviceKind, ...] = ("cpu", "gpu", "gpu-oom")


@dataclass(frozen=True, slots=True)
class WorkerSettings:
    """Runtime settings of the worker.

    Attributes:
        data_dir: Root of jobs, cache and temporary files (``STEMSTUDIO_DATA_DIR``).
        separator: ``demucs`` (real model) or ``fake`` (copies the mix into stems; for CI/E2E)
            (``STEMSTUDIO_SEPARATOR``).
        device: Torch device preference (``STEMSTUDIO_DEVICE``). ``cuda`` and ``auto`` both use
            the GPU when the start-up probe finds a usable one and otherwise run on the CPU with
            a reason in ``/health`` (feature 003, FR-003); ``cpu`` never touches CUDA.
        max_duration_seconds: Longest accepted audio (15 minutes).
        max_upload_bytes: Largest accepted upload (200 MiB).
        primary_model: Demucs model tried first.
        fallback_model: Demucs model used when the primary cannot be loaded.
        model_dir: Root of downloaded detail model weights (``STEMSTUDIO_MODEL_DIR``); never
            inside the repository (constitution IV).
        fake_detail_step_seconds: Delay per parent step of the fake detail models
            (``STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS``), so E2E tests can observe progress.
        fake_device: Device the fake separator pretends to use (``STEMSTUDIO_FAKE_DEVICE``):
            ``cpu`` (default), ``gpu`` (a fake GPU) or ``gpu-oom`` (a fake GPU whose first model
            step of every job runs out of memory and is retried on the CPU); only with
            ``separator=fake``, for E2E tests of the device display (feature 003).
    """

    data_dir: Path
    separator: SeparatorKind = "demucs"
    device: DeviceKind = "cpu"
    max_duration_seconds: float = 900.0
    max_upload_bytes: int = 200 * 1024 * 1024
    primary_model: str = "htdemucs_6s"
    fallback_model: str = "htdemucs"
    model_dir: Path = Path("data/models")
    fake_detail_step_seconds: float = 1.0
    fake_device: FakeDeviceKind = "cpu"


def load_settings(environ: Mapping[str, str] | None = None) -> WorkerSettings:
    """Build settings from environment variables.

    Args:
        environ: Variables to read; defaults to ``os.environ``.

    Returns:
        The validated settings.

    Raises:
        ValueError: A variable has a value outside its allowed set.
    """
    env = os.environ if environ is None else environ
    return WorkerSettings(
        data_dir=Path(env.get("STEMSTUDIO_DATA_DIR", "data/worker")),
        separator=cast(SeparatorKind, _choice(env, "STEMSTUDIO_SEPARATOR", _SEPARATORS)),
        device=cast(DeviceKind, _choice(env, "STEMSTUDIO_DEVICE", _DEVICES)),
        model_dir=Path(env.get("STEMSTUDIO_MODEL_DIR", "data/models")),
        fake_detail_step_seconds=_seconds(env, "STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS", 1.0),
        fake_device=cast(FakeDeviceKind, _choice(env, "STEMSTUDIO_FAKE_DEVICE", _FAKE_DEVICES)),
    )


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
