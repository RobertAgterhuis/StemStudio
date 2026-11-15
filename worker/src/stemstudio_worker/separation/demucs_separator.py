"""Demucs separator: primary model with fallback, progress reporting and FLAC output.

The model runtime (loading weights, running inference) is injected as :class:`DemucsRuntime`, so
this module never imports torch or demucs; ``demucs_runtime.default_runtime()`` provides the real
one and unit tests pass a fake. The separation runs as one model step of the injected
``DeviceRunner`` (feature 003): GPU or CPU, with the single CPU retry on GPU out-of-memory.
"""

import logging
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Final, Protocol, cast

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import (
    DeviceRunner,
    DeviceUse,
    GpuNeed,
    ProgressCallback,
    SeparationError,
    SeparationResult,
    merge_device,
)
from stemstudio_worker.schemas import ModelName, StemName
from stemstudio_worker.separation.flac_writer import SIX_STEMS, STAGE, read_audio, write_flac

_LOG = logging.getLogger(__name__)

HTDEMUCS_NEED_MIB: Final = 1_700
"""Free GPU memory a Demucs separation needs (research.md G-04: htdemucs_6s 0.57 GiB allocated,
rounded up, plus 1,024 MiB margin); also used for the smaller ``htdemucs`` fallback."""

Audio = npt.NDArray[np.float32]
"""Float audio; a mix is ``(channels, frames)``, a separation ``(sources, channels, frames)``."""


class DemucsModel(Protocol):
    """The parts of a loaded Demucs model (or bag of models) the separator relies on."""

    sources: list[str]
    samplerate: int


ModelApplier = Callable[[DemucsModel, Audio, str, Callable[[float], None]], Audio]
"""``(model, mix, device, on_fraction) -> stems``; ``on_fraction`` receives 0..1 progress."""


@dataclass(frozen=True, slots=True)
class DemucsRuntime:
    """Model runtime the separator depends on.

    Attributes:
        load: Loads (and downloads on first use) a pretrained model by name.
        apply: Runs the model on a mix on a torch device (``cuda`` or ``cpu``).
    """

    load: Callable[[str], DemucsModel]
    apply: ModelApplier


class DemucsSeparator:
    """Implements the ``Separator`` port with Demucs; the model is loaded once and reused."""

    def __init__(
        self, models: tuple[str, str], runtime: DemucsRuntime, devices: DeviceRunner
    ) -> None:
        """Create a separator.

        Args:
            models: Primary and fallback model names, e.g. ``("htdemucs_6s", "htdemucs")``.
            runtime: The model runtime.
            devices: Runs the separation on the worker's device (GPU policy of ADR-012).
        """
        self._models = models
        self._runtime = runtime
        self._devices = devices
        self._loaded: tuple[ModelName, DemucsModel] | None = None

    def separate(
        self, wav_path: Path, out_dir: Path, progress: ProgressCallback
    ) -> SeparationResult:
        """Separate ``wav_path`` into ``out_dir/<name>.flac`` (see ``ports.Separator``).

        Returns:
            The model, the stems and where the separation ran.

        Raises:
            SeparationError: Neither model could be loaded, or inference failed.
        """
        name, model = self._model()
        report = _MonotonicProgress(progress)
        report(0.0)
        mix = np.ascontiguousarray(read_audio(wav_path).T, dtype=np.float32)
        separated, use = self._apply(_Loaded(name, model), mix, report)
        stems = _write_stems(model.sources, separated, out_dir)
        report(1.0)
        return SeparationResult(model=name, stems=stems, device=merge_device(None, use))

    def _model(self) -> tuple[ModelName, DemucsModel]:
        if self._loaded is None:
            self._loaded = self._load_first_available()
        return self._loaded

    def _load_first_available(self) -> tuple[ModelName, DemucsModel]:
        for name in self._models:
            try:
                return cast(ModelName, name), self._runtime.load(name)
            except Exception:
                _LOG.warning("Could not load Demucs model %s", name, exc_info=True)
        msg = (
            f"The separation models {' and '.join(self._models)} could not be loaded. "
            "They are downloaded on first use; check the network connection."
        )
        raise SeparationError(msg)

    def _apply(
        self, loaded: "_Loaded", mix: Audio, report: Callable[[float], None]
    ) -> tuple[Audio, DeviceUse]:
        """Run the model as one device step; errors reach the runner raw (OOM -> CPU retry)."""
        need = GpuNeed(loaded.name, HTDEMUCS_NEED_MIB)
        try:
            return self._devices.run(
                lambda device: self._runtime.apply(loaded.model, mix, device, report), need
            )
        except Exception as exc:
            _LOG.exception("Demucs inference failed")
            raise SeparationError("Stem separation failed.") from exc


@dataclass(frozen=True, slots=True)
class _Loaded:
    name: ModelName
    model: DemucsModel


class _MonotonicProgress:
    """Forwards progress clamped to 0..1 and never lower than before."""

    def __init__(self, progress: ProgressCallback) -> None:
        self._progress = progress
        self._best = 0.0

    def __call__(self, fraction: float) -> None:
        self._best = max(self._best, min(fraction, 1.0))
        self._progress(self._best, STAGE)


def _write_stems(sources: list[str], separated: Audio, out_dir: Path) -> dict[StemName, Path]:
    """Write each separated source as FLAC, in display order; reject unknown source names."""
    by_name = dict(zip(sources, separated, strict=True))
    if not set(by_name) <= set(SIX_STEMS):
        raise SeparationError("The separation model produced unknown stems.")
    stems: dict[StemName, Path] = {}
    for name in (n for n in SIX_STEMS if n in by_name):
        stems[name] = out_dir / f"{name}.flac"
        write_flac(stems[name], by_name[name].T)
    return stems
