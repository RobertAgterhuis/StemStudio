"""Worker configuration from environment variables (no secrets are needed or read)."""

import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, cast

SeparatorKind = Literal["demucs", "fake"]
DeviceKind = Literal["cpu", "cuda", "auto"]

_SEPARATORS: tuple[SeparatorKind, ...] = ("demucs", "fake")
_DEVICES: tuple[DeviceKind, ...] = ("cpu", "cuda", "auto")


@dataclass(frozen=True, slots=True)
class WorkerSettings:
    """Runtime settings of the worker.

    Attributes:
        data_dir: Root of jobs, cache and temporary files (``STEMSTUDIO_DATA_DIR``).
        separator: ``demucs`` (real model) or ``fake`` (copies the mix into stems; for CI/E2E)
            (``STEMSTUDIO_SEPARATOR``).
        device: Torch device preference (``STEMSTUDIO_DEVICE``); ``auto`` picks CUDA if present.
        max_duration_seconds: Longest accepted audio (15 minutes).
        max_upload_bytes: Largest accepted upload (200 MiB).
        primary_model: Demucs model tried first.
        fallback_model: Demucs model used when the primary cannot be loaded.
    """

    data_dir: Path
    separator: SeparatorKind = "demucs"
    device: DeviceKind = "cpu"
    max_duration_seconds: float = 900.0
    max_upload_bytes: int = 200 * 1024 * 1024
    primary_model: str = "htdemucs_6s"
    fallback_model: str = "htdemucs"


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
    )


def _choice(env: Mapping[str, str], name: str, allowed: tuple[str, ...]) -> str:
    """Read ``name`` (case-insensitive value) and check it against ``allowed``; first is default."""
    value = env.get(name, allowed[0]).strip().lower()
    if value not in allowed:
        msg = f"{name} must be one of {', '.join(allowed)}; got {value!r}"
        raise ValueError(msg)
    return value
