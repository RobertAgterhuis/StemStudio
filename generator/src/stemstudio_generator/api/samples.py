"""Checked voice samples kept for one download within 10 minutes (010 US3, FR-007).

Only ``ok`` samples are kept; the file is removed after its first download, after the keep time
or when the service restarts (files nobody registered are purged).
"""

import threading
from datetime import datetime, timedelta
from pathlib import Path

from stemstudio_generator.config import CHECKED_SAMPLE_KEEP_SECONDS
from stemstudio_generator.ports import Clock


class CheckedSamples:
    """Normalised sample files by check id with their expiry."""

    def __init__(self, directory: Path, clock: Clock) -> None:
        """Keep samples in ``directory`` (created on demand)."""
        self._directory = directory
        self._clock = clock
        self._expiry: dict[str, datetime] = {}
        self._lock = threading.Lock()

    def path_for(self, check_id: str) -> Path:
        """Where the normalised sample of ``check_id`` is written."""
        self._directory.mkdir(parents=True, exist_ok=True)
        return self._directory / f"{check_id}.flac"

    def keep(self, check_id: str) -> None:
        """Keep the sample for ``CHECKED_SAMPLE_KEEP_SECONDS``."""
        with self._lock:
            self._expiry[check_id] = self._clock.now() + timedelta(
                seconds=CHECKED_SAMPLE_KEEP_SECONDS
            )

    def take(self, check_id: str) -> Path | None:
        """The sample file for its one download (None when unknown or expired)."""
        self.purge()
        with self._lock:
            if self._expiry.pop(check_id, None) is None:
                return None
        path = self.path_for(check_id)
        return path if path.is_file() else None

    def purge(self) -> None:
        """Delete expired and unregistered sample files."""
        now = self._clock.now()
        with self._lock:
            for check_id in [key for key, until in self._expiry.items() if until < now]:
                del self._expiry[check_id]
            live = set(self._expiry)
        if self._directory.is_dir():
            for path in self._directory.glob("*.flac"):
                if path.stem not in live:
                    path.unlink(missing_ok=True)
