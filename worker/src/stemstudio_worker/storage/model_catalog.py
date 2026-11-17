"""Known model ids per ``STEMSTUDIO_MODEL_DIR`` folder (``ModelCacheUsage.models``, feature 013).

A model counts as present when its marker (a file or folder below the top-level folder) exists.
The markers mirror where each component downloads its weights: Demucs through Hugging Face
(``hf/``, ``HF_HOME``) or ``torch.hub`` (``torch/``, ``TORCH_HOME``), the detail models
(``detail/<id>/<file>``, ``detail.registry``), Whisper (``whisper/<name>.pt``) and the
transcription weights (``transcription/<id>/``). Folders of other services (ACE-Step, SoulX) are
measured but list no models.
"""

from dataclasses import dataclass
from pathlib import Path
from typing import Final

from stemstudio_worker.ports import BASIC_PITCH_WEIGHTS, PIANO_WEIGHTS, WHISPER_TURBO_WEIGHTS


@dataclass(frozen=True, slots=True)
class CatalogEntry:
    """One known model and where it shows in the model cache.

    Attributes:
        folder: Top-level folder below ``STEMSTUDIO_MODEL_DIR``.
        model_id: Reported id.
        marker: Relative path below ``folder`` whose existence means the model is cached.
    """

    folder: str
    model_id: str
    marker: str


_HF_DEMUCS = "hub/models--adefossez--"
_TORCH_DEMUCS = "hub/checkpoints/"

CATALOG: Final[tuple[CatalogEntry, ...]] = (
    CatalogEntry("hf", "htdemucs_6s", f"{_HF_DEMUCS}HTDemucs-6s"),
    CatalogEntry("hf", "htdemucs", f"{_HF_DEMUCS}HTDemucs"),
    CatalogEntry("torch", "htdemucs_6s", f"{_TORCH_DEMUCS}5c90dfd2-34c22ccb.th"),
    CatalogEntry("torch", "htdemucs", f"{_TORCH_DEMUCS}955717e8-8726e21a.th"),
    CatalogEntry(
        "detail", "mdx23c-drumsep-6s", "mdx23c-drumsep-6s/MDX23C-DrumSep-aufr33-jarredou.ckpt"
    ),
    CatalogEntry("detail", "uvr-mdxnet-kara-2", "uvr-mdxnet-kara-2/UVR_MDXNET_KARA_2.onnx"),
    CatalogEntry(
        "detail", "mvsep-mega-53", "mvsep-mega-53/mvsep_mega_model_bs_roformer_53_stems_v1.ckpt"
    ),
    CatalogEntry("whisper", "whisper-large-v3-turbo", f"{WHISPER_TURBO_WEIGHTS.model_id}.pt"),
    CatalogEntry("whisper", "whisper-small", "small.pt"),
    CatalogEntry("transcription", BASIC_PITCH_WEIGHTS.model_id, BASIC_PITCH_WEIGHTS.model_id),
    CatalogEntry("transcription", PIANO_WEIGHTS.model_id, PIANO_WEIGHTS.model_id),
)
"""Every model the worker downloads, in report order per folder."""


def models_in(folder: Path) -> list[str]:
    """Ids of the catalogue models whose marker exists below the top-level ``folder``."""
    return [e.model_id for e in CATALOG if e.folder == folder.name and (folder / e.marker).exists()]
