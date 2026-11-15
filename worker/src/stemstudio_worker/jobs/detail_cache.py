"""Detail result cache ``cache/by-audio/<sha>/detail/{result.json, <parent>/<name>.flac}``.

The finished ``detail.partial`` directory (checkpoints of every parent plus ``result.json``) is
renamed to ``detail/`` in one step, so a cached result is complete or absent (SC-007).
"""

import shutil
from datetime import datetime

from pydantic import ValidationError

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import DetailResult, DetailStemInfo, ParentOutcome

_RESULT = "result.json"


def detail_stem_url(detail_id: str, parent: str, name: str) -> str:
    """Relative URL of a detail stem: ``/v1/detail-jobs/{id}/stems/{parent}/{name}``."""
    return f"/v1/detail-jobs/{detail_id}/stems/{parent}/{name}"


def bind_outcome(outcome: ParentOutcome, detail_id: str) -> ParentOutcome:
    """Point every stem of ``outcome`` at the URLs of ``detail_id``."""
    stems = [_bind_stem(s, detail_id, outcome.parent) for s in outcome.stems]
    return outcome.model_copy(update={"stems": stems})


def _bind_stem(stem: DetailStemInfo, detail_id: str, parent: str) -> DetailStemInfo:
    return stem.model_copy(update={"path": detail_stem_url(detail_id, parent, stem.name)})


def rebind(cached: DetailResult, detail_id: str, completed_at: datetime) -> DetailResult:
    """Derive the result of ``detail_id`` from a cached result (own id, URLs and time)."""
    parents = [bind_outcome(p, detail_id) for p in cached.parents]
    return cached.model_copy(
        update={"detail_job_id": detail_id, "parents": parents, "completed_at": completed_at}
    )


class DetailCache:
    """Stores finished detail results per audio."""

    def __init__(self, paths: DataPaths) -> None:
        """Use the cache directories below ``paths.root``."""
        self._paths = paths

    def lookup(self, audio_sha256: str, version: str) -> DetailResult | None:
        """Return the cached result of ``version`` when it and all its stems exist."""
        try:
            text = (self._paths.detail_final(audio_sha256) / _RESULT).read_text(encoding="utf-8")
            result = DetailResult.model_validate_json(text)
        except (OSError, ValidationError):
            return None
        if result.separator_version != version or not self._stems_exist(result):
            return None
        return result

    def store(self, result: DetailResult) -> None:
        """Write ``result.json`` into ``detail.partial`` and rename it to ``detail/``."""
        partial = self._paths.detail_partial(result.audio_sha256)
        final = self._paths.detail_final(result.audio_sha256)
        (partial / _RESULT).write_text(result.model_dump_json(by_alias=True), encoding="utf-8")
        shutil.rmtree(final, ignore_errors=True)
        partial.replace(final)

    def _stems_exist(self, result: DetailResult) -> bool:
        return all(
            self._paths.detail_stem(result.audio_sha256, p.parent, s.name).is_file()
            for p in result.parents
            for s in p.stems
        )
