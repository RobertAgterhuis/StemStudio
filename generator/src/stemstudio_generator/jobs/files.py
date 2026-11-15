"""File layout of one job under ``<data_dir>/jobs/<id>/``.

``inputs/`` holds the uploaded parts (deleted when the job ends), ``work/`` the engine input and
the engine's untagged output (deleted when the job ends), ``takes/`` the labelled takes that
``GET .../takes/{index}`` serves until the job is deleted.
"""

import shutil
from pathlib import Path

from stemstudio_generator.jobs.store import JsonJobStore


class JobFiles:
    """Paths of the job directories (created on demand)."""

    def __init__(self, store: JsonJobStore) -> None:
        """Lay out files under the store's job directories."""
        self._store = store

    def inputs(self, generation_id: str) -> Path:
        """Uploaded parts."""
        return self._dir(generation_id, "inputs")

    def work(self, generation_id: str) -> Path:
        """Engine input and untagged engine output."""
        return self._dir(generation_id, "work")

    def takes(self, generation_id: str) -> Path:
        """Labelled takes."""
        return self._dir(generation_id, "takes")

    def take(self, generation_id: str, index: int) -> Path:
        """The labelled take ``index``."""
        return self.takes(generation_id) / f"take-{index}.flac"

    def vocal(self, generation_id: str, index: int) -> Path:
        """The labelled vocal stem of take ``index`` (voice conversions, 1.1.0)."""
        return self.takes(generation_id) / f"take-{index}-vocal.flac"

    def clean(self, generation_id: str) -> None:
        """Remove ``inputs/`` and ``work/`` (the takes stay)."""
        root = self._store.job_dir(generation_id)
        for name in ("inputs", "work"):
            shutil.rmtree(root / name, ignore_errors=True)

    def remove(self, generation_id: str) -> None:
        """Remove the job file and every file of the job (a refused request)."""
        self._store.delete(generation_id)

    def _dir(self, generation_id: str, name: str) -> Path:
        path = self._store.job_dir(generation_id) / name
        path.mkdir(parents=True, exist_ok=True)
        return path
