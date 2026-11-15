"""Real detail models: downloads, loading and the model provider (ADR-010).

``torch``, ``onnxruntime`` and the vendored MSST networks are imported only when a model is
loaded, so fake mode and the unit tests never load them. Checkpoints are loaded with
``weights_only=True`` (no pickled code is executed).
"""

import threading
from collections.abc import Callable, Mapping
from pathlib import Path
from types import ModuleType
from typing import TYPE_CHECKING, Any, Final

from stemstudio_worker.detail.demix import DemixPlan, MsstModel, Network, OutputSelection
from stemstudio_worker.detail.download import ModelDownloader
from stemstudio_worker.detail.folding import MEGA_OUTPUTS
from stemstudio_worker.detail.mdx_net import Kara2Model
from stemstudio_worker.detail.registry import spec_of
from stemstudio_worker.ports import DetailModel
from stemstudio_worker.schemas import DetailModelId

if TYPE_CHECKING:
    import torch

Loader = Callable[[Path, str], DetailModel]
"""Builds a model from its downloaded weights on a torch device (``cpu``/``cuda``)."""

MDX23C_PLAN: Final = DemixPlan(chunk_size=130_560, overlap=2)
"""MDX23C DrumSep: 3 s chunks, overlap 2 (same result as the config's 4 at half the cost)."""

MEGA_PLAN: Final = DemixPlan(chunk_size=882_000, overlap=2)
"""Mega 53-stem BS-RoFormer: 20 s chunks (config ``inference.chunk_size``), overlap 2."""

_STATE_KEYS: Final = ("state", "state_dict", "model_state_dict")


def resolve_device(preference: str) -> str:
    """Return ``cuda`` or ``cpu`` for the ``STEMSTUDIO_DEVICE`` preference."""
    import torch  # noqa: PLC0415 - torch loads only for real models

    if preference == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    return preference


def _vendor() -> ModuleType:
    from stemstudio_worker.detail.vendor.msst import configs  # noqa: PLC0415 - lazy import

    return configs


def load_network(build: Callable[[], "torch.nn.Module"], path: Path, device: str) -> Network:
    """Build a network, load its checkpoint safely and return a CPU-in/CPU-out callable."""
    import torch  # noqa: PLC0415 - torch loads only for real models

    state: Any = torch.load(path, map_location="cpu", weights_only=True)
    for key in _STATE_KEYS:
        state = state.get(key, state)
    model = build()
    model.load_state_dict(state)
    model.to(device).eval()

    def network(batch: "torch.Tensor") -> "torch.Tensor":
        return model(batch.to(device)).to("cpu", torch.float32)  # type: ignore[no-any-return]

    return network


def load_mdx23c(path: Path, device: str) -> DetailModel:
    """MDX23C DrumSep 6-stem: kick, snare, toms, hh, ride, crash."""
    vendor = _vendor()
    names = tuple(vendor.DRUMSEP_INSTRUMENTS)
    network = load_network(vendor.build_mdx23c, path, device)
    return MsstModel(network, MDX23C_PLAN, OutputSelection(names, names))


def load_mega53(path: Path, device: str) -> DetailModel:
    """MVSep Mega 53-stem BS-RoFormer, accumulating only the eight family outputs."""
    vendor = _vendor()
    network = load_network(vendor.build_mega53, path, device)
    selection = OutputSelection(tuple(vendor.MEGA_INSTRUMENTS), MEGA_OUTPUTS)
    return MsstModel(network, MEGA_PLAN, selection)


def load_kara2(path: Path, device: str) -> DetailModel:  # noqa: ARG001 - ONNX runs on the CPU
    """UVR MDX-Net KARA 2 through onnxruntime (CPU execution provider)."""
    import onnxruntime  # noqa: PLC0415 - loaded only for the real vocals model

    session = onnxruntime.InferenceSession(str(path), providers=["CPUExecutionProvider"])
    name = session.get_inputs()[0].name
    return Kara2Model(lambda spec: session.run(None, {name: spec})[0])


LOADERS: Final[Mapping[DetailModelId, Loader]] = {
    "mdx23c-drumsep-6s": load_mdx23c,
    "uvr-mdxnet-kara-2": load_kara2,
    "mvsep-mega-53": load_mega53,
}


class RealModelProvider:
    """``DetailModelProvider`` that downloads, verifies and loads each model once."""

    def __init__(
        self,
        downloader: ModelDownloader,
        device: str,
        loaders: Mapping[DetailModelId, Loader] = LOADERS,
    ) -> None:
        """Load models with ``loaders`` from the files of ``downloader``.

        Args:
            downloader: Makes the verified weight files available.
            device: ``STEMSTUDIO_DEVICE`` preference (``cpu``, ``cuda``, ``auto``), resolved
                when the first model loads.
            loaders: Model id -> loader.
        """
        self._downloader = downloader
        self._device = device
        self.loaders = loaders
        self._models: dict[DetailModelId, DetailModel] = {}
        self._lock = threading.Lock()

    def get(self, model_id: DetailModelId) -> DetailModel:
        """Return ``model_id``, downloading and loading it on first use.

        Raises:
            DetailModelUnavailableError: The download failed or was corrupt.
        """
        with self._lock:
            if model_id not in self._models:
                path = self._downloader.ensure(spec_of(model_id))
                device = resolve_device(self._device)
                self._models[model_id] = self.loaders[model_id](path, device)
            return self._models[model_id]
