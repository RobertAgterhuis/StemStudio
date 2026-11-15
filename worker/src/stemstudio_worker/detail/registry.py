"""Registry of the detail models (research.md D-07, ADR-009 model weights exception).

URLs are constants (no user input reaches a download); weights are downloaded at first use into
``STEMSTUDIO_MODEL_DIR/detail/<id>/`` and verified with SHA-256, never committed.
"""

from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final

from stemstudio_worker.ports import GpuNeed
from stemstudio_worker.schemas import DetailModelId

SEPARATOR_VERSION: Final = "1"
"""Version of the detail pipeline (models + thresholds); bump it to invalidate cached results."""


@dataclass(frozen=True, slots=True)
class ModelSpec:
    """One downloadable detail model.

    Attributes:
        model_id: Registry id recorded in the parent outcomes.
        file_name: File name below ``<model_dir>/detail/<model_id>/``.
        url: HTTPS download URL.
        size_bytes: Expected size (informational).
        sha256: Lower-case hex SHA-256 of the file.
        licence: Licence status (ADR-009).
    """

    model_id: DetailModelId
    file_name: str
    url: str
    size_bytes: int
    sha256: str
    licence: str


MODELS: Final[tuple[ModelSpec, ...]] = (
    ModelSpec(
        model_id="mdx23c-drumsep-6s",
        file_name="MDX23C-DrumSep-aufr33-jarredou.ckpt",
        url="https://github.com/nomadkaraoke/python-audio-separator/releases/download/"
        "model-configs/MDX23C-DrumSep-aufr33-jarredou.ckpt",
        size_bytes=437_652_699,
        sha256="d2a4aa53eb584d21eead358a4e66d1882ad182911be018f052b5da73be9096d0",
        licence="None stated upstream; allowed under ADR-009 (local, non-commercial)",
    ),
    ModelSpec(
        model_id="uvr-mdxnet-kara-2",
        file_name="UVR_MDXNET_KARA_2.onnx",
        url="https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/"
        "UVR_MDXNET_KARA_2.onnx",
        size_bytes=52_786_726,
        sha256="bf32e15105a09c0f7dddd2b67346146334d6f3ecb399ed7638eba2ab07cbf5f4",
        licence="MIT per the UVR README; credit Ultimate Vocal Remover (UVR) and its developers",
    ),
    ModelSpec(
        model_id="mvsep-mega-53",
        file_name="mvsep_mega_model_bs_roformer_53_stems_v1.ckpt",
        url="https://github.com/ZFTurbo/Music-Source-Separation-Training/releases/download/"
        "v1.0.21/mvsep_mega_model_bs_roformer_53_stems_v1.ckpt",
        size_bytes=1_368_919_887,
        sha256="c62820893bbf86d4e734f966bd142d9157cfc8bb8e79e9d8f9ea553f3ff3519f",
        licence="None stated; allowed under ADR-009 (local, non-commercial)",
    ),
)
"""The three detail models, in pipeline order (drums, vocals, instruments)."""

_BY_ID: Final = {spec.model_id: spec for spec in MODELS}

GPU_NEEDS: Final[Mapping[DetailModelId, GpuNeed]] = {
    "mdx23c-drumsep-6s": GpuNeed("MDX23C DrumSep", 4_400),
    "uvr-mdxnet-kara-2": GpuNeed("UVR MDX-Net KARA 2", 3_600),
    "mvsep-mega-53": GpuNeed("Mega 53-stem", 10_600),
}
"""Free GPU memory per model step (research.md G-04: measured allocated peak, or the NVML peak
for onnxruntime, rounded up to 100 MiB, plus 1,024 MiB margin; ADR-012 D4)."""


def spec_of(model_id: DetailModelId) -> ModelSpec:
    """Return the spec of ``model_id``.

    Raises:
        KeyError: ``model_id`` is not a downloadable model (e.g. ``fake-detail``).
    """
    return _BY_ID[model_id]
