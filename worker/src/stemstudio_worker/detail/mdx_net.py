"""UVR MDX-Net KARA 2 (ONNX) with its STFT wrapper (research.md D-02, ADR-010).

Written after python-audio-separator ``mdx_separator.py`` (MIT) and Ultimate Vocal Remover:
n_fft 5120, hop 1024, 2048 frequency bins, 256 frames per chunk, compensation 1.065 (UVR model
hash ``1d64a6d2c30f709b8c9b4ce1366d96ee``). The model's primary output on a vocals stem is the
backing vocals; ``lead = input - backing``, so the split is exact by construction.
Credit: Ultimate Vocal Remover (UVR) and its developers.
"""

from collections.abc import Callable
from typing import TYPE_CHECKING, Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.detail.demix import STAGE, inference_error
from stemstudio_worker.ports import DetailAudio, ProgressCallback

if TYPE_CHECKING:
    import torch

N_FFT: Final = 5120
HOP: Final = 1024
DIM_F: Final = 2048
DIM_T: Final = 256
COMPENSATE: Final = 1.065
CHUNK: Final = HOP * (DIM_T - 1)
TRIM: Final = N_FFT // 2
GENERATED: Final = CHUNK - 2 * TRIM

Session = Callable[[npt.NDArray[np.float32]], npt.NDArray[np.float32]]
"""Runs the ONNX model: spectrum ``(1, 4, 2048, 256)`` -> spectrum of the same shape."""


def _spectrum(wave: "torch.Tensor", window: "torch.Tensor") -> "torch.Tensor":
    """``(2, CHUNK)`` -> ``(1, 4, DIM_F, DIM_T)`` (real/imag per channel, low bins only)."""
    import torch  # noqa: PLC0415 - torch loads only for real models

    spec = torch.stft(wave, n_fft=N_FFT, hop_length=HOP, window=window, center=True,
                      return_complex=True)  # fmt: skip
    spec = torch.view_as_real(spec).permute(0, 3, 1, 2).reshape(1, 4, -1, DIM_T)
    return spec[:, :, :DIM_F]


def _wave(spec: "torch.Tensor", window: "torch.Tensor") -> "torch.Tensor":
    """Inverse of :func:`_spectrum` (missing high bins are zero) -> ``(2, CHUNK)``."""
    import torch  # noqa: PLC0415 - torch loads only for real models

    bins = N_FFT // 2 + 1
    full = torch.cat([spec, torch.zeros(1, 4, bins - DIM_F, DIM_T)], dim=2)
    full = full.reshape(2, 2, bins, DIM_T).permute(0, 2, 3, 1).contiguous()
    return torch.istft(torch.view_as_complex(full), n_fft=N_FFT, hop_length=HOP, window=window,
                       center=True)  # fmt: skip


class Kara2Model:
    """``DetailModel`` for UVR MDX-Net KARA 2: outputs ``backing`` and ``lead``."""

    def __init__(self, session: Session) -> None:
        """Run inference through ``session``."""
        self._session = session

    def separate(self, audio: DetailAudio, progress: ProgressCallback) -> dict[str, DetailAudio]:
        """Split a vocals stem into backing (model output x 1.065) and lead (the remainder)."""
        try:
            backing = (self._backing(audio, progress) * COMPENSATE).astype(np.float32)
        except RuntimeError as exc:
            raise inference_error(exc) from exc
        return {"backing": backing, "lead": (audio - backing).astype(np.float32)}

    def _backing(self, audio: DetailAudio, progress: ProgressCallback) -> npt.NDArray[np.float32]:
        import torch  # noqa: PLC0415 - torch loads only for real models

        frames = audio.shape[1]
        pad = GENERATED - frames % GENERATED
        padded = np.pad(audio.astype(np.float32), ((0, 0), (TRIM, pad + TRIM)))
        window = torch.hann_window(N_FFT, periodic=True)
        starts = range(0, frames + pad, GENERATED)
        parts: list[npt.NDArray[np.float32]] = []
        for index, start in enumerate(starts):
            wave = torch.from_numpy(padded[:, start : start + CHUNK])
            spec = self._session(_spectrum(wave, window).numpy())
            parts.append(_wave(torch.from_numpy(spec), window).numpy()[:, TRIM:-TRIM])
            progress((index + 1) / len(starts), STAGE)
        return np.concatenate(parts, axis=1)[:, :frames]
