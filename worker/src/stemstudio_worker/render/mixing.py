"""Mixing law of render jobs: gain, Web Audio stereo panner, sum and master gain.

Binding text: ``specs/007-export-library/data-model.md`` section 3; the pan matrices are checked
against ``contracts/render-vectors.json``.
"""

import math
from collections.abc import Sequence

import numpy as np
import numpy.typing as npt

from stemstudio_worker.schemas import RenderInput

Block = npt.NDArray[np.float32]
"""One block of stereo frames, shape ``(frames, 2)``."""


def pan_matrix(pan: float) -> npt.NDArray[np.float64]:
    """Return the 2x2 matrix of the StereoPannerNode for a stereo input.

    ``L' = m[0,0]·L + m[0,1]·R`` and ``R' = m[1,0]·L + m[1,1]·R``. Pan 0 uses the ``pan > 0``
    form (``θ = 0``), which equals the ``pan <= 0`` form mathematically and gives the exact
    identity in floating point, so a centred input stays bit-identical.

    Args:
        pan: -1 (left) .. 1 (right).

    Returns:
        A new float64 matrix.
    """
    if pan < 0:
        theta = (pan + 1) * math.pi / 2
        return np.array([[1.0, math.cos(theta)], [0.0, math.sin(theta)]])
    theta = pan * math.pi / 2
    return np.array([[math.cos(theta), 0.0], [math.sin(theta), 1.0]])


def mix_block(blocks: Sequence[Block], inputs: Sequence[RenderInput], master_gain: float) -> Block:
    """Mix one block of every input: ``sum(pan(x_i · gain_i, pan_i)) · master_gain``.

    Args:
        blocks: One float32 ``(frames, 2)`` block per input (at least one), all of the same
            length.
        inputs: The inputs, in the order of ``blocks``.
        master_gain: Gain applied to the sum.

    Returns:
        A new float32 ``(frames, 2)`` block.

    Raises:
        ValueError: ``blocks`` is empty or differs in length from ``inputs``.
    """
    if not blocks or len(blocks) != len(inputs):
        msg = "mix_block needs one block per input"
        raise ValueError(msg)
    acc = np.zeros((blocks[0].shape[0], 2), dtype=np.float32)
    for block, source in zip(blocks, inputs, strict=True):
        matrix = pan_matrix(source.pan).T.astype(np.float32)
        acc += (block * np.float32(source.gain)) @ matrix
    result: Block = acc * np.float32(master_gain)
    return result
