"""Normalised user pictures (``POST /v1/artwork``), kept for one hour (feature 005, FR-013).

Layout: ``artwork/<artworkId>/{cover,thumb}.jpg`` plus ``info.json`` with the artwork info and
the creation time. Nothing is attached to a job; the backend downloads both variants and keeps
its own copy.
"""

import re
import shutil
from datetime import datetime, timedelta
from pathlib import Path

from pydantic import ValidationError

from stemstudio_worker.jobs.artwork_files import write_artwork
from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import ArtworkImages
from stemstudio_worker.schemas import ArtworkInfo, ArtworkUpload, ContractModel

KEEP_FOR = timedelta(hours=1)
"""How long a normalised user picture is kept."""

_ID = re.compile(r"[0-9a-f]{32}")
_INFO = "info.json"


class _Entry(ContractModel):
    """Content of ``artwork/<id>/info.json``."""

    info: ArtworkInfo
    created_at: datetime


class UserArtworkStore:
    """Stores normalised user pictures and sweeps them after :data:`KEEP_FOR`."""

    def __init__(self, paths: DataPaths, clock: Clock) -> None:
        """Keep pictures below ``paths.user_artwork_root``; ``clock`` stamps creation times."""
        self._paths = paths
        self._clock = clock

    def add(self, images: ArtworkImages) -> ArtworkUpload:
        """Store ``images`` under a new id (source ``user``)."""
        upload = ArtworkUpload(artwork_id=new_id(), info=images.info("user"))
        folder = self._paths.user_artwork(upload.artwork_id)
        entry = _Entry(info=upload.info, created_at=self._clock())
        write_artwork(folder, images, {_INFO: entry.model_dump_json(by_alias=True)})
        return upload

    def info(self, artwork_id: str) -> ArtworkInfo | None:
        """The info of a stored picture; None when unknown, damaged or not a valid id."""
        entry = self._entry(artwork_id)
        return None if entry is None else entry.info

    def directory(self, artwork_id: str) -> Path:
        """Folder of a picture's two JPEGs."""
        return self._paths.user_artwork(artwork_id)

    def sweep(self) -> list[str]:
        """Remove pictures older than one hour (and damaged entries); returns their ids."""
        cutoff = self._clock() - KEEP_FOR
        expired = [i for i in self._ids() if _expired(self._entry(i), cutoff)]
        for artwork_id in expired:
            shutil.rmtree(self._paths.user_artwork(artwork_id), ignore_errors=True)
        return expired

    def _ids(self) -> list[str]:
        root = self._paths.user_artwork_root
        if not root.is_dir():
            return []
        return [p.name for p in root.iterdir() if p.is_dir() and _ID.fullmatch(p.name)]

    def _entry(self, artwork_id: str) -> _Entry | None:
        if not _ID.fullmatch(artwork_id):
            return None
        try:
            raw = (self._paths.user_artwork(artwork_id) / _INFO).read_text(encoding="utf-8")
            return _Entry.model_validate_json(raw)
        except (OSError, ValidationError):
            return None


def _expired(entry: _Entry | None, cutoff: datetime) -> bool:
    return entry is None or entry.created_at < cutoff
