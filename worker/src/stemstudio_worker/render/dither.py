"""Quantisation of float samples to integer PCM with optional TPDF dither (data-model.md 3).

The integer scale is ``2^(bits-1)``, the inverse of how libsndfile reads PCM as float
(``int / 2^(bits-1)``), so a 16-bit stem read as float32 and quantised without dither gives back
exactly its samples (SC-004). Values are rounded and clamped to the integer range.
"""

import hashlib

import numpy as np
import numpy.typing as npt

from stemstudio_worker.render.mixing import Block

IntBlock = npt.NDArray[np.int32]
"""One block of quantised stereo frames, shape ``(frames, 2)``."""


def full_scale(bits: int) -> int:
    """Return ``2^(bits-1)``, the integer value of 1.0 at ``bits`` bits."""
    return int(2 ** (bits - 1))


def dither_seed(key: str) -> int:
    """Derive a reproducible 64-bit dither seed from a key (the render job id)."""
    return int.from_bytes(hashlib.sha256(key.encode("utf-8")).digest()[:8], "big")


class TpdfDither:
    """Triangular (TPDF) dither of +/- 1 LSB from a seeded random generator."""

    def __init__(self, seed: int) -> None:
        """Create the generator; the same seed gives the same noise sequence."""
        self._rng = np.random.default_rng(seed)

    def noise(self, shape: tuple[int, ...]) -> npt.NDArray[np.float64]:
        """Return noise in LSB: the difference of two uniform values, in ``(-1, 1)``."""
        noise: npt.NDArray[np.float64] = self._rng.random(shape) - self._rng.random(shape)
        return noise


def quantize(block: Block, bits: int, dither: TpdfDither | None) -> IntBlock:
    """Quantise a float block to ``bits``-bit integers (held in int32).

    Args:
        block: Float samples, 1.0 = full scale.
        bits: 16 or 24.
        dither: TPDF dither to add before rounding, or None for plain rounding.

    Returns:
        ``clamp(round(block · 2^(bits-1) [+ dither]))``.
    """
    scale = full_scale(bits)
    scaled = block.astype(np.float64) * scale
    if dither is not None:
        scaled += dither.noise(scaled.shape)
    ints: IntBlock = np.clip(np.round(scaled), -scale, scale - 1).astype(np.int32)
    return ints
