"""Worker configuration from environment variables (no secrets are needed or read)."""

import os
import re
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, cast

SeparatorKind = Literal["demucs", "fake"]
DeviceKind = Literal["cpu", "cuda", "auto"]
FakeDeviceKind = Literal["cpu", "gpu", "gpu-oom"]
FakeOnlineMode = Literal["found", "not-found", "failed", "off"]

_SEPARATORS: tuple[SeparatorKind, ...] = ("demucs", "fake")
_DEVICES: tuple[DeviceKind, ...] = ("cpu", "cuda", "auto")
_FAKE_DEVICES: tuple[FakeDeviceKind, ...] = ("cpu", "gpu", "gpu-oom")
_FAKE_ONLINE: tuple[FakeOnlineMode, ...] = ("found", "not-found", "failed", "off")
_ON_OFF: tuple[str, ...] = ("on", "off")
_LANGUAGE = re.compile(r"[a-z]{2,3}")
_MAX_LANGUAGES = 5


@dataclass(frozen=True, slots=True)
class OnlineSettings:
    """Online lookups of feature 005 (data-model.md 1).

    Attributes:
        lookups_enabled: False when ``STEMSTUDIO_ONLINE_LOOKUPS=off``: no request to MusicBrainz,
            Cover Art Archive, Wikidata or Wikipedia; metadata jobs complete as ``disabled``.
        contact_url: Raw ``STEMSTUDIO_CONTACT_URL`` (unset -> None). Validated by the
            User-Agent builder (an invalid value is replaced by the generic token and logged
            without the value); never an e-mail address.
        wikipedia_languages: Wiki language codes in the order they are tried
            (``STEMSTUDIO_WIKIPEDIA_LANGUAGES``, comma list, 1..5 codes of 2-3 letters).
        fake_mode: Behaviour of the fake metadata provider and fake source artwork
            (``STEMSTUDIO_FAKE_ONLINE``); only used with ``separator=fake``.
    """

    lookups_enabled: bool = True
    contact_url: str | None = None
    wikipedia_languages: tuple[str, ...] = ("en",)
    fake_mode: FakeOnlineMode = "found"


@dataclass(frozen=True, slots=True)
class RenderSettings:
    """Render jobs of feature 007 (data-model.md 1).

    Attributes:
        library_dir: Read-only root of the API data directory (``STEMSTUDIO_LIBRARY_DIR``);
            None when unset, in which case render jobs fail with the "library is not available"
            text and ``/health`` reports ``library.readable = false``.
        block_seconds: Block length of the streaming renderer
            (``STEMSTUDIO_RENDER_BLOCK_SECONDS``, > 0; tests use small values).
    """

    library_dir: Path | None = None
    block_seconds: float = 10.0


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
        online: Online lookup settings (feature 005).
        render: Render job settings (feature 007).
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
    online: OnlineSettings = OnlineSettings()
    render: RenderSettings = RenderSettings()


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
        online=_online_settings(env),
        render=_render_settings(env),
    )


def _online_settings(env: Mapping[str, str]) -> OnlineSettings:
    """Read the feature 005 variables (``STEMSTUDIO_ONLINE_LOOKUPS`` ... ``FAKE_ONLINE``)."""
    contact = env.get("STEMSTUDIO_CONTACT_URL", "").strip()
    return OnlineSettings(
        lookups_enabled=_choice(env, "STEMSTUDIO_ONLINE_LOOKUPS", _ON_OFF) == "on",
        contact_url=contact or None,
        wikipedia_languages=_languages(env.get("STEMSTUDIO_WIKIPEDIA_LANGUAGES", "en")),
        fake_mode=cast(FakeOnlineMode, _choice(env, "STEMSTUDIO_FAKE_ONLINE", _FAKE_ONLINE)),
    )


def _render_settings(env: Mapping[str, str]) -> RenderSettings:
    """Read the feature 007 variables (``STEMSTUDIO_LIBRARY_DIR``, ``..._RENDER_BLOCK_SECONDS``)."""
    library = env.get("STEMSTUDIO_LIBRARY_DIR", "").strip()
    block = _seconds(env, "STEMSTUDIO_RENDER_BLOCK_SECONDS", 10.0)
    if block == 0:
        msg = "STEMSTUDIO_RENDER_BLOCK_SECONDS must be greater than 0"
        raise ValueError(msg)
    return RenderSettings(library_dir=Path(library) if library else None, block_seconds=block)


def _languages(raw: str) -> tuple[str, ...]:
    """Parse a comma list of 1..5 wiki language codes (2-3 lower-case letters)."""
    codes = tuple(code.strip().lower() for code in raw.split(",") if code.strip())
    if not 0 < len(codes) <= _MAX_LANGUAGES or not all(map(_LANGUAGE.fullmatch, codes)):
        msg = f"STEMSTUDIO_WIKIPEDIA_LANGUAGES must list 1-5 language codes; got {raw!r}"
        raise ValueError(msg)
    return codes


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
