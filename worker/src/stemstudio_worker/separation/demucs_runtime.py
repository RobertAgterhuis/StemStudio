"""Real Demucs runtime: ``demucs.pretrained.get_model`` + ``demucs.apply.apply_model`` on torch.

torch and demucs are imported lazily so that ``STEMSTUDIO_SEPARATOR=fake`` never loads them.
Weights are downloaded on first use (Hugging Face Hub, ``HF_HOME``; fallback
``dl.fbaipublicfiles.com`` via ``torch.hub``, ``TORCH_HOME``).
"""

from collections.abc import Callable, Mapping
from typing import Any, cast

import numpy as np

from stemstudio_worker.separation.demucs_separator import Audio, DemucsModel, DemucsRuntime

_MIN_STD = 1e-8
_OVERLAP = 0.25


def default_runtime() -> DemucsRuntime:
    """Return the runtime backed by the installed demucs and torch packages."""
    return DemucsRuntime(load=load_pretrained, apply=apply_pretrained)


def load_pretrained(name: str) -> DemucsModel:
    """Load a pretrained model (or bag of models) by name, downloading weights if needed."""
    from demucs.pretrained import get_model  # noqa: PLC0415 - heavy, optional import

    model: DemucsModel = get_model(name)
    return model


def apply_pretrained(
    model: DemucsModel, mix: Audio, device: str, on_fraction: Callable[[float], None]
) -> Audio:
    """Run ``model`` on a ``(channels, frames)`` mix and return ``(sources, channels, frames)``.

    The mix is normalised like the Demucs CLI (zero mean, unit deviation of the mono reference)
    and the stems are scaled back afterwards.
    """
    import torch  # noqa: PLC0415 - heavy, optional import
    from demucs.apply import apply_model  # noqa: PLC0415

    wav = torch.from_numpy(mix)
    reference = wav.mean(0)
    mean, std = reference.mean(), reference.std().clamp_min(_MIN_STD)
    tracker = _SegmentTracker(on_fraction, frames=mix.shape[-1])
    with torch.no_grad():
        normalised = ((wav - mean) / std)[None]
        out = apply_model(
            cast(Any, model), normalised, device=device, overlap=_OVERLAP, callback=tracker
        )
    return np.asarray((out[0] * std + mean).cpu().numpy(), dtype=np.float32)


class _SegmentTracker:
    """Turns ``apply_model`` segment callbacks into a 0..1 fraction of the whole separation."""

    def __init__(self, on_fraction: Callable[[float], None], frames: int) -> None:
        self._on_fraction = on_fraction
        self._frames = max(frames, 1)

    def __call__(self, event: Mapping[str, Any]) -> None:
        if event.get("state") != "end":
            return
        models = max(int(event.get("models", 1)), 1)
        within = min(int(event.get("segment_offset", 0)) / self._frames, 1.0)
        self._on_fraction((int(event.get("model_idx_in_bag", 0)) + within) / models)
