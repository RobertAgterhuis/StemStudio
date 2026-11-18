"""Foote novelty on the self-similarity matrix (data-model 2.2 step 4, frozen by spike T010)."""

from typing import Final

import numpy as np
import numpy.typing as npt

KERNEL_HALF: Final = 6
"""Half size of the checkerboard kernel in frames (bars); research R-10."""

MAX_WINDOW: Final = 3
"""``pre_max = post_max``: a peak is the maximum of the frames up to 3 before and after it."""
AVG_WINDOW: Final = 2
"""``pre_avg = post_avg``: a peak is at least the mean of the frames up to 2 around it."""
DELTA: Final = 0.0
WAIT: Final = 3
"""Frames to skip after a peak (peaks are at least 4 frames apart)."""
_EPSILON: Final = 1e-6

Vector = npt.NDArray[np.float64]


def checkerboard(half: int) -> Vector:
    """A ``2 half`` square checkerboard kernel tapered by a Gaussian (zero sum)."""
    axis = np.arange(-half, half) + 0.5
    sign = np.sign(axis)
    taper = np.exp(-((axis / (half / 2)) ** 2) / 2)
    kernel = np.outer(sign * taper, sign * taper)
    return np.asarray(kernel - kernel.mean())


def novelty(ssm: Vector, half: int = KERNEL_HALF) -> Vector:
    """Novelty per frame (a change starting at that frame), clipped at 0 and scaled to 0..1."""
    kernel = checkerboard(half)
    padded = np.pad(ssm, half, mode="edge")
    curve = np.array([np.sum(padded[i:i + 2 * half, i:i + 2 * half] * kernel)
                      for i in range(len(ssm))])  # fmt: skip
    curve = np.clip(curve, 0, None)
    peak = float(curve.max(initial=0.0))
    return np.asarray(curve / peak) if peak > 0 else curve


def peaks(curve: Vector) -> list[int]:
    """Peak frames of ``curve`` (the librosa ``peak_pick`` rule with the frozen windows).

    Frames with no novelty are never peaks (``peak_pick`` would count a flat zero stretch and
    let it block a real peak through ``wait``); frame 0 is the song start, not a boundary.
    """
    found: list[int] = []
    for i in range(1, len(curve)):
        if _is_peak(curve, i) and (not found or i - found[-1] > WAIT):
            found.append(i)
    return found


def _is_peak(curve: Vector, i: int) -> bool:
    local = curve[max(0, i - MAX_WINDOW) : i + MAX_WINDOW + 1]
    around = curve[max(0, i - AVG_WINDOW) : i + AVG_WINDOW + 1]
    value = float(curve[i])
    return (
        value > _EPSILON and value >= float(local.max()) and value >= float(around.mean()) + DELTA
    )
