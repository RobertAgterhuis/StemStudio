"""A :class:`JsonStore` of job records with the restart and sweep queries the light lanes share.

Records hold a wire ``job`` with a state (``is_terminal``), ``created_at`` and ``updated_at``
(metadata jobs of 005, lyrics jobs of 006, transcription jobs of 008).
"""

from datetime import datetime
from typing import Protocol, cast

from pydantic import BaseModel

from stemstudio_worker.jobs.json_store import JsonStore


class _State(Protocol):
    @property
    def is_terminal(self) -> bool: ...


class _Job(Protocol):
    @property
    def status(self) -> _State: ...

    @property
    def created_at(self) -> datetime: ...

    @property
    def updated_at(self) -> datetime: ...


class _Record(Protocol):
    @property
    def job(self) -> _Job: ...


class JobRecordStore[R: BaseModel](JsonStore[R]):
    """Adds :meth:`unfinished` and :meth:`expired` to a store of job records."""

    def unfinished(self) -> list[R]:
        """Every queued or running job, oldest first (run again after a restart)."""
        pending = [r for r in self.all() if not _job(r).status.is_terminal]
        return sorted(pending, key=lambda r: _job(r).created_at)

    def expired(self, cutoff: datetime) -> list[str]:
        """Ids of terminal jobs last updated before ``cutoff``."""
        return [
            self._key(r)
            for r in self.all()
            if _job(r).status.is_terminal and _job(r).updated_at < cutoff
        ]


def _job(record: BaseModel) -> _Job:
    """The wire job of a record (every record type of this store has one)."""
    return cast(_Record, record).job
