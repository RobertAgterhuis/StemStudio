"""Segment clusters (data-model 2.2 step 7): average-linkage cosine clustering, threshold 0.25."""

import string
from typing import Final

import numpy as np
import numpy.typing as npt
from sklearn.cluster import AgglomerativeClustering

DISTANCE_THRESHOLD: Final = 0.25
"""Cosine distance below which segments join one cluster (spike T010, research R-10)."""

SINGLE_MARGIN: Final = 0.5
"""Cluster margin when there is no other cluster to compare with."""

Matrix = npt.NDArray[np.float64]


def cluster_letters(means: Matrix) -> list[str]:
    """Letters ``A``.. per segment (rows of ``means``) in order of first appearance; ``Z`` caps."""
    if len(means) < 2:  # noqa: PLR2004 - clustering needs two samples
        return ["A"] * len(means)
    model = AgglomerativeClustering(n_clusters=None, distance_threshold=DISTANCE_THRESHOLD,
                                    metric="cosine", linkage="average")  # fmt: skip
    raw = model.fit_predict(means)
    order: dict[int, str] = {}
    for label in raw:
        order.setdefault(int(label), string.ascii_uppercase[min(len(order), 25)])
    return [order[int(label)] for label in raw]


def margins(means: Matrix, letters: list[str]) -> list[float]:
    """Cosine distance (0..1) of each segment to the nearest centroid of another cluster."""
    centroids = {letter: means[[k for k, x in enumerate(letters) if x == letter]].mean(axis=0)
                 for letter in dict.fromkeys(letters)}  # fmt: skip
    return [_margin(row, letter, centroids) for row, letter in zip(means, letters, strict=True)]


def _margin(row: npt.NDArray[np.float64], own: str, centroids: dict[str, Matrix]) -> float:
    others = [_distance(row, centre) for letter, centre in centroids.items() if letter != own]
    return min(1.0, *others) if others else SINGLE_MARGIN


def _distance(a: npt.NDArray[np.float64], b: npt.NDArray[np.float64]) -> float:
    norm = float(np.linalg.norm(a) * np.linalg.norm(b))
    return 1.0 - float(a @ b) / norm if norm > 0 else 1.0
