"""Thread-safe record store: in-memory index backed by one JSON file per record (atomic replace)."""

import logging
import re
import threading
import uuid
from collections.abc import Callable
from pathlib import Path

from pydantic import BaseModel, ValidationError

_LOG = logging.getLogger(__name__)
_ID = re.compile(r"[0-9a-f]{32}")


def new_id() -> str:
    """Return a fresh record id: ``uuid4().hex`` (32 lower-case hex characters)."""
    return uuid.uuid4().hex


class JsonStore[R: BaseModel]:
    """Persists records of type ``R`` as ``<directory>/<id>.json``."""

    def __init__(self, directory: Path, model: type[R], key: Callable[[R], str]) -> None:
        """Create a store over ``directory`` for ``model`` records identified by ``key``."""
        self._dir = directory
        self._model = model
        self._key = key
        self._lock = threading.RLock()
        self._records: dict[str, R] = {}

    def load_all(self) -> None:
        """Read every record file into memory; unreadable files are logged and skipped."""
        self._dir.mkdir(parents=True, exist_ok=True)
        for path in sorted(self._dir.glob("*.json")):
            record = self._read(path)
            if record is not None:
                with self._lock:
                    self._records[self._key(record)] = record

    def save(self, record: R) -> None:
        """Write ``record`` to a temporary file, then atomically replace its file."""
        self._dir.mkdir(parents=True, exist_ok=True)
        target = self._dir / f"{self._key(record)}.json"
        temp = target.with_name(target.name + ".tmp")
        with self._lock:
            temp.write_text(record.model_dump_json(by_alias=True), encoding="utf-8")
            temp.replace(target)
            self._records[self._key(record)] = record

    def get(self, record_id: str) -> R | None:
        """Return the record with ``record_id``, or None when unknown or not a valid id."""
        if not _ID.fullmatch(record_id):
            return None
        with self._lock:
            return self._records.get(record_id)

    def all(self) -> list[R]:
        """Return a snapshot of every known record."""
        with self._lock:
            return list(self._records.values())

    def _read(self, path: Path) -> R | None:
        try:
            return self._model.model_validate_json(path.read_text(encoding="utf-8"))
        except (OSError, ValidationError) as exc:
            _LOG.warning("Skipping unreadable record file %s: %s", path.name, exc)
            return None
