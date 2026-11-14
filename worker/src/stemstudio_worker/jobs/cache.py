"""Result cache: ``cache/by-audio/<sha>/{result.json, stems/}`` and ``cache/by-source/*.json``."""

import shutil
from datetime import datetime
from pathlib import Path
from typing import Annotated

from pydantic import Field, ValidationError

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import ContractModel, Job, JobResult, StemInfo, StemName

_RESULT = "result.json"


class _SourcePointer(ContractModel):
    """Content of ``cache/by-source/<key>.json``."""

    audio_sha256: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]


def stem_url(job_id: str, stem: StemName) -> str:
    """Relative URL of a stem of a job: ``/v1/jobs/{jobId}/stems/{name}``."""
    return f"/v1/jobs/{job_id}/stems/{stem}"


def rebind(canonical: JobResult, job: Job, completed_at: datetime) -> JobResult:
    """Derive the result of ``job`` from a cached result (own id, key, metadata and stem URLs)."""
    stems = [s.model_copy(update={"path": stem_url(job.job_id, s.name)}) for s in canonical.stems]
    return canonical.model_copy(
        update={"job_id": job.job_id, "source_key": job.source_key, "stems": stems,
                "metadata": job.metadata or canonical.metadata, "completed_at": completed_at},
    )  # fmt: skip


def stem_infos(job_id: str, stems: dict[StemName, Path]) -> list[StemInfo]:
    """Describe stem files for a result (URL and size)."""
    return [
        StemInfo(name=name, path=stem_url(job_id, name), size_bytes=path.stat().st_size)
        for name, path in stems.items()
    ]


class ResultCache:
    """Stores finished separations so identical sources or audio are never separated twice."""

    def __init__(self, paths: DataPaths) -> None:
        """Use the cache directories below ``paths.root``."""
        self._paths = paths

    def store(self, result: JobResult, stems: dict[StemName, Path]) -> None:
        """Move ``stems`` and write ``result.json`` into ``by-audio/<sha>/`` (rename last)."""
        final = self._paths.by_audio(result.audio_sha256)
        partial = final.with_name(final.name + ".partial")
        shutil.rmtree(partial, ignore_errors=True)
        (partial / "stems").mkdir(parents=True)
        for name, source in stems.items():
            shutil.move(source, partial / "stems" / f"{name}.flac")
        (partial / _RESULT).write_text(result.model_dump_json(by_alias=True), encoding="utf-8")
        shutil.rmtree(final, ignore_errors=True)
        partial.replace(final)

    def lookup_source(self, source_key: str) -> JobResult | None:
        """Return the cached result for a source key, or None (also for a damaged cache)."""
        pointer = _read_model(_SourcePointer, self._paths.by_source(source_key))
        return None if pointer is None else self.lookup_audio(pointer.audio_sha256)

    def lookup_audio(self, audio_sha256: str) -> JobResult | None:
        """Return the cached result for normalised audio when it and all its stems exist."""
        result = _read_model(JobResult, self._paths.by_audio(audio_sha256) / _RESULT)
        if result is None or not self._stems_exist(result):
            return None
        return result

    def link_source(self, source_key: str, audio_sha256: str) -> None:
        """Point ``source_key`` at the cached audio entry (atomic replace)."""
        target = self._paths.by_source(source_key)
        target.parent.mkdir(parents=True, exist_ok=True)
        temp = target.with_name(target.name + ".tmp")
        pointer = _SourcePointer(audio_sha256=audio_sha256)
        temp.write_text(pointer.model_dump_json(by_alias=True), encoding="utf-8")
        temp.replace(target)

    def _stems_exist(self, result: JobResult) -> bool:
        return all(
            self._paths.stem_file(result.audio_sha256, s.name).is_file() for s in result.stems
        )


def _read_model[M: ContractModel](model: type[M], path: Path) -> M | None:
    """Parse ``path`` as ``model``; None when missing or invalid."""
    try:
        return model.model_validate_json(path.read_text(encoding="utf-8"))
    except (OSError, ValidationError):
        return None
