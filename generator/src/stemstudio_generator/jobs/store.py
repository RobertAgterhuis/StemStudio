"""Persisted generation jobs: one JSON file per job under ``<data_dir>/jobs`` (``JobStore``).

Each job also owns a working directory ``<data_dir>/jobs/<id>/`` for its uploaded inputs and
its takes; deleting the job removes both. Files are written to a temporary name and renamed,
so a crash never leaves a half-written job behind.
"""

import contextlib
import logging
import shutil
from pathlib import Path
from typing import Final

from pydantic import ValidationError

from stemstudio_generator.schemas import Generation

_LOG: Final = logging.getLogger(__name__)
_SUFFIX: Final = ".json"


class JsonJobStore:
    """``JobStore`` over JSON files (camelCase, the wire shape of ``Generation``)."""

    def __init__(self, data_dir: Path) -> None:
        """Store jobs under ``data_dir/jobs`` (created on first write)."""
        self._root = data_dir / "jobs"

    def save(self, job: Generation) -> None:
        """Create or replace the job file atomically."""
        self._root.mkdir(parents=True, exist_ok=True)
        target = self._file(job.generation_id)
        temporary = target.with_suffix(".tmp")
        temporary.write_text(job.model_dump_json(by_alias=True), "utf-8")
        temporary.replace(target)

    def get(self, generation_id: str) -> Generation | None:
        """The job, or None when unknown."""
        return _read(self._file(generation_id))

    def list_all(self) -> list[Generation]:
        """Every readable job, oldest first."""
        if not self._root.is_dir():
            return []
        jobs = (_read(path) for path in self._root.glob(f"*{_SUFFIX}"))
        return sorted((job for job in jobs if job is not None), key=lambda job: job.created_at)

    def delete(self, generation_id: str) -> None:
        """Remove the job file and its directory (no error when unknown)."""
        with contextlib.suppress(FileNotFoundError):
            self._file(generation_id).unlink()
        shutil.rmtree(self._root / generation_id, ignore_errors=True)

    def job_dir(self, generation_id: str) -> Path:
        """The job's working directory (created if missing)."""
        path = self._root / generation_id
        path.mkdir(parents=True, exist_ok=True)
        return path

    def _file(self, generation_id: str) -> Path:
        return self._root / f"{generation_id}{_SUFFIX}"


def _read(path: Path) -> Generation | None:
    try:
        return Generation.model_validate_json(path.read_bytes())
    except FileNotFoundError:
        return None
    except (OSError, ValidationError):
        _LOG.warning("Skipping unreadable job file %s", path.name)
        return None
