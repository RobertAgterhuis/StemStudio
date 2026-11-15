"""Re-assignment of instrument families from one sum run to the parents (research.md, ADR-010).

The Mega 53 model runs once on the sum of the active instrument parents. Each family is shared
out with STFT power masks: ``share_p = iSTFT(STFT(family) x |P_p|^2 / sum_q |P_q|^2)``
(Hann, n_fft 4096, hop 1024, per channel). The last parent receives ``family - sum(others)``,
so the shares always add up to the family exactly.
"""

from collections.abc import Mapping
from typing import Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.ports import DetailAudio
from stemstudio_worker.schemas import DetailStemName, ParentStemName

N_FFT: Final = 4096
"""STFT frame length (samples)."""

HOP: Final = 1024
"""STFT hop (samples)."""

_TINY_POWER: Final = 1e-20
_WINDOW: Final = (0.5 - 0.5 * np.cos(2 * np.pi * np.arange(N_FFT) / N_FFT)).astype(np.float32)

Spectrum = npt.NDArray[np.complex64]
Signal = npt.NDArray[np.float32]


def _padded_length(length: int) -> int:
    body = length + N_FFT
    frames = max(1, -(-(body - N_FFT) // HOP) + 1)
    return (frames - 1) * HOP + N_FFT


def _stft(signal: Signal) -> Spectrum:
    padded = np.zeros(_padded_length(signal.size), dtype=np.float32)
    padded[N_FFT // 2 : N_FFT // 2 + signal.size] = signal
    frames = np.lib.stride_tricks.sliding_window_view(padded, N_FFT)[::HOP] * _WINDOW
    return np.fft.rfft(frames, axis=1).astype(np.complex64)


def _istft(spectrum: Spectrum, length: int) -> Signal:
    frames = np.fft.irfft(spectrum, n=N_FFT, axis=1).astype(np.float32) * _WINDOW
    out = np.zeros(_padded_length(length), dtype=np.float32)
    weight = np.zeros_like(out)
    for index, frame in enumerate(frames):
        out[index * HOP : index * HOP + N_FFT] += frame
        weight[index * HOP : index * HOP + N_FFT] += _WINDOW * _WINDOW
    out /= np.where(weight > _TINY_POWER, weight, 1.0)
    return out[N_FFT // 2 : N_FFT // 2 + length]


def _channel_shares(family: Signal, parents: list[Signal]) -> list[Signal]:
    """Shares of one channel of ``family`` for each parent channel (last one = remainder)."""
    powers = [np.abs(_stft(parent)) ** 2 for parent in parents]
    total = np.sum(powers, axis=0)
    silent = total <= _TINY_POWER
    spectrum = _stft(family)
    shares: list[Signal] = []
    for power in powers[:-1]:
        mask = np.where(silent, 1.0 / len(parents), power / np.where(silent, 1.0, total))
        shares.append(_istft((spectrum * mask).astype(np.complex64), family.size))
    remainder = family - np.sum(shares, axis=0, dtype=np.float32) if shares else family
    shares.append(remainder.astype(np.float32))
    return shares


def reassign(
    family: DetailAudio, parents: Mapping[ParentStemName, DetailAudio]
) -> dict[ParentStemName, DetailAudio]:
    """Share ``family`` (from the sum run) out to ``parents`` following their power.

    Args:
        family: Family audio ``(channels, frames)`` of the sum of ``parents``.
        parents: Parent name -> parent audio of the same shape (at least one).

    Returns:
        Parent name -> share of the family; the shares sum to ``family``. A single parent
        receives ``family`` unchanged.
    """
    names = list(parents)
    if len(names) == 1:
        return {names[0]: family}
    per_channel = [
        _channel_shares(family[ch], [parents[name][ch] for name in names])
        for ch in range(family.shape[0])
    ]
    return {name: np.stack([shares[i] for shares in per_channel]) for i, name in enumerate(names)}


def reassign_families(
    families: Mapping[DetailStemName, DetailAudio], parents: Mapping[ParentStemName, DetailAudio]
) -> dict[ParentStemName, dict[DetailStemName, DetailAudio]]:
    """Apply :func:`reassign` to every family; returns parent -> family -> share."""
    result: dict[ParentStemName, dict[DetailStemName, DetailAudio]] = {p: {} for p in parents}
    for family_name, audio in families.items():
        for parent, share in reassign(audio, parents).items():
            result[parent][family_name] = share
    return result
