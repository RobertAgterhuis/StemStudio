"""Frames and frame features of the ``stems-novelty`` analyser (data-model 2.2 steps 1-3).

Frames are bars of a reliable grid (the lead-in before the first downbeat is one frame), beats
grouped by ``beatsPerBar`` for an unreliable grid, or fixed 2 s without beats. Per frame: chroma
CQT (12), MFCC (20, normalised per coefficient over the song), mix RMS, vocal activity ratio and
the energy share of every standard stem; ``stack_memory(n_steps=2)`` and L2-normalised columns.
"""

from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from typing import Final

import librosa
import numpy as np
import numpy.typing as npt
import soundfile as sf

from stemstudio_worker.ports import StemFiles, StructureError
from stemstudio_worker.schemas import BeatGrid, SectionSnap

SR: Final = 22_050
"""Analysis sample rate (every stem is loaded mono at this rate)."""

HOP: Final = 512
"""Hop of the short-time features (about 23 ms)."""

STANDARD: Final = ("vocals", "drums", "bass", "guitar", "piano", "other")
"""Standard stems in a fixed order (``guitar``/``piano`` absent for 4-stem tracks)."""

FALLBACK_FRAME_SECONDS: Final = 2.0
_EDGE: Final = 0.05
_VOCAL_LEVEL: Final = 0.1
_VOCAL_RATIO_ACTIVE: Final = 0.3
_READ_ERROR: Final = "The stems could not be read."

Vector = npt.NDArray[np.float64]


@dataclass(frozen=True, slots=True)
class FrameGrid:
    """Frame boundaries in seconds (first 0, last the duration) and what they snap to."""

    bounds: tuple[float, ...]
    snap: SectionSnap

    @property
    def count(self) -> int:
        """Number of frames."""
        return len(self.bounds) - 1


@dataclass(frozen=True, slots=True)
class FrameFeatures:
    """Features of every frame.

    Attributes:
        matrix: ``(rows, frames)`` stacked, L2-normalised feature columns.
        mix_rms: RMS of the mix per frame.
        vocal_ratio: Share of the frame's hops whose vocals RMS exceeds 0.1 x its p95.
        shares: Energy share per standard stem and frame (sums to 1 per frame).
        energy: Energy per channel (standard and detail) and frame.
        vocals_p95: 95th percentile of the vocals RMS (``no-vocals`` check).
        mix_p95: 95th percentile of the mix RMS.
    """

    matrix: Vector
    mix_rms: Vector
    vocal_ratio: Vector
    shares: dict[str, Vector]
    energy: dict[str, Vector]
    vocals_p95: float
    mix_p95: float


def frame_grid(grid: BeatGrid | None, duration: float) -> FrameGrid:
    """The frames of a song of ``duration`` seconds for ``grid`` (data-model 2.2 step 2)."""
    starts = None if grid is None else _grid_starts(grid)
    if starts is not None:
        return _framed(*starts, duration)
    steps = np.arange(FALLBACK_FRAME_SECONDS, duration, FALLBACK_FRAME_SECONDS)
    return _framed([float(s) for s in steps], "time", duration)


def _grid_starts(grid: BeatGrid) -> tuple[Sequence[float], SectionSnap] | None:
    """Bar starts of a reliable grid, else beats grouped by the bar length, else None."""
    if grid.reliable and grid.downbeats:
        return grid.downbeats, "downbeat"
    if grid.beats:
        return grid.beats[:: grid.beats_per_bar], "beat"
    return None


def _framed(starts: Sequence[float], snap: SectionSnap, duration: float) -> FrameGrid:
    inner = sorted(t for t in starts if _EDGE < t < duration - _EDGE)
    return FrameGrid(bounds=(0.0, *inner, float(duration)), snap=snap)


def load_stems(files: StemFiles, duration: float) -> dict[str, Vector]:
    """Every file mono at ``SR``, cut or padded to ``duration``.

    Raises:
        StructureError: A file cannot be decoded.
    """
    length = round(duration * SR)
    return {channel: _fit(_read(path), length) for channel, path in files.items()}


def _read(path: object) -> Vector:
    try:
        data, rate = sf.read(path, dtype="float32", always_2d=True)
    except (OSError, RuntimeError, sf.LibsndfileError) as exc:
        raise StructureError(_READ_ERROR) from exc
    mono = data.mean(axis=1)
    if rate != SR:
        mono = librosa.resample(mono, orig_sr=rate, target_sr=SR)
    return np.asarray(mono, dtype=np.float64)


def _fit(signal: Vector, length: int) -> Vector:
    if len(signal) >= length:
        return signal[:length]
    return np.pad(signal, (0, length - len(signal)))


def frame_features(audio: Mapping[str, Vector], frames: FrameGrid) -> FrameFeatures:
    """Compute the frame features of ``audio`` (channel -> mono signal at ``SR``)."""
    standard = [name for name in STANDARD if name in audio]
    mix = np.sum([audio[name] for name in standard], axis=0)
    hops = _HopFrames.of(frames, len(mix))
    rms, energy = _energies(audio, hops)
    mix_rms_hops = _rms(mix)
    shares = _shares({name: energy[name] for name in standard})
    vocals = rms.get("vocals", np.zeros(1))
    vocals_p95 = _p95(vocals)
    vocal_ratio = _vocal_ratio(vocals, vocals_p95, hops)
    mix_rms = np.sqrt(hops.mean(mix_rms_hops**2))
    rows = [*_tonal(mix, hops), _scaled(mix_rms), vocal_ratio, *shares.values()]
    return FrameFeatures(_stacked(np.vstack(rows)), mix_rms, vocal_ratio, shares, energy,
                         vocals_p95, _p95(mix_rms_hops))  # fmt: skip


def _energies(
    audio: Mapping[str, Vector], hops: "_HopFrames"
) -> tuple[dict[str, Vector], dict[str, Vector]]:
    """Short-time RMS per channel and its energy per frame."""
    rms = {name: _rms(signal) for name, signal in audio.items()}
    return rms, {name: hops.total(values**2) for name, values in rms.items()}


def vocal_activity(features: FrameFeatures) -> npt.NDArray[np.bool_]:
    """Frames with active vocals: vocal ratio above 0.3 (data-model 2.2 step 5)."""
    return np.asarray(features.vocal_ratio > _VOCAL_RATIO_ACTIVE)


@dataclass(frozen=True, slots=True)
class _HopFrames:
    """The frame index of every hop and the hop count per frame."""

    index: npt.NDArray[np.intp]
    counts: Vector

    @classmethod
    def of(cls, frames: FrameGrid, samples: int) -> "_HopFrames":
        times = librosa.frames_to_time(np.arange(1 + samples // HOP), sr=SR, hop_length=HOP)
        index = np.clip(np.searchsorted(frames.bounds, times, side="right") - 1, 0,
                        frames.count - 1)  # fmt: skip
        counts = np.bincount(index, minlength=frames.count).astype(np.float64)
        return cls(index, np.maximum(counts, 1.0))

    def total(self, values: Vector) -> Vector:
        n = min(len(values), len(self.index))
        return np.bincount(self.index[:n], weights=values[:n], minlength=len(self.counts))

    def mean(self, values: Vector) -> Vector:
        return self.total(values) / self.counts


def _rms(signal: Vector) -> Vector:
    return np.asarray(librosa.feature.rms(y=signal, frame_length=2048, hop_length=HOP)[0])


def _p95(values: Vector) -> float:
    return float(np.percentile(values, 95)) if len(values) else 0.0


def _shares(energy: Mapping[str, Vector]) -> dict[str, Vector]:
    total = np.sum(list(energy.values()), axis=0)
    silent = total <= 0
    equal = 1.0 / len(energy)
    return {name: np.where(silent, equal, values / np.where(silent, 1.0, total))
            for name, values in energy.items()}  # fmt: skip


def _vocal_ratio(vocals: Vector, p95: float, hops: _HopFrames) -> Vector:
    if p95 <= 0:
        return np.zeros(len(hops.counts))
    return hops.mean((vocals > _VOCAL_LEVEL * p95).astype(np.float64))


def _tonal(mix: Vector, hops: _HopFrames) -> list[Vector]:
    """Chroma CQT (12 rows) and per-coefficient normalised MFCC (20 rows) per frame."""
    chroma = librosa.feature.chroma_cqt(y=mix, sr=SR, hop_length=HOP, tuning=0.0)
    mfcc = librosa.feature.mfcc(y=mix, sr=SR, n_mfcc=20, hop_length=HOP)
    chroma_rows = [hops.mean(row) for row in chroma]
    mfcc_rows = [_standardised(hops.mean(row)) for row in mfcc]
    return [*chroma_rows, *mfcc_rows]


def _standardised(row: Vector) -> Vector:
    spread = float(np.std(row))
    return (row - np.mean(row)) / spread if spread > 0 else np.zeros_like(row)


def _scaled(row: Vector) -> Vector:
    peak = float(np.max(row, initial=0.0))
    return row / peak if peak > 0 else row


def _stacked(rows: Vector) -> Vector:
    stacked = librosa.feature.stack_memory(rows, n_steps=2, mode="edge")
    norms = np.linalg.norm(stacked, axis=0)
    return np.asarray(stacked / np.where(norms > 0, norms, 1.0))
