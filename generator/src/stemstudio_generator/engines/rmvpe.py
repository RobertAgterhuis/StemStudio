"""RMVPE behind ``PitchTracker`` for the voice-sample checks (010 US3, research S-05).

The checks run on the CPU without the GPU lease, so profiles can be created while a GPU job
runs. The model is loaded on the first check and kept; one check at a time.
"""

import threading
from collections.abc import Callable
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt

from stemstudio_generator.config import GeneratorSettings
from stemstudio_generator.engines.soulx_api import SoulXApi, import_soulx
from stemstudio_generator.ports import RMVPE_FILE

_THRESHOLD = 0.03
"""RMVPE voicing threshold (SoulX's default)."""


class RmvpePitchTracker:
    """``PitchTracker``: RMVPE on the CPU, F0 per 10 ms frame (16 kHz, hop 160)."""

    def __init__(
        self, settings: GeneratorSettings, api: Callable[[], SoulXApi] = import_soulx
    ) -> None:
        """Use ``<model_dir>/soulx/rmvpe/rmvpe.pt``; ``api`` imports SoulX on the first check."""
        self._model_path = settings.model_dir / RMVPE_FILE
        self._import = api
        self._lock = threading.Lock()
        self._api: SoulXApi | None = None
        self._model: Any = None

    def f0(self, audio: Path) -> npt.NDArray[np.float32]:
        """F0 in Hz per 10 ms frame; 0 marks an unvoiced frame."""
        with self._lock:
            api, model = self._ready()
            samples = api.load_16k(str(audio))
            return np.asarray(model.infer_from_audio(samples, thred=_THRESHOLD), dtype=np.float32)

    def _ready(self) -> tuple[SoulXApi, Any]:
        if self._api is None:
            self._api = self._import()
        if self._model is None:
            self._model = self._api.rmvpe(str(self._model_path), is_half=False, device="cpu")
        return self._api, self._model
