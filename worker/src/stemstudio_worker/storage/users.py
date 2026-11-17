"""Which jobs use which cache entry, read from the job files (feature 013, data-model 7).

Standard jobs (``jobs/*.json``) point at the entry of their ``audioSha256``; detail jobs
(``detail-jobs/*.json``) at their own ``audioSha256``; lyrics alignments (``lyrics-jobs/*.json``)
at the entry of their standard job. A non-terminal job of any of them puts its entry in use.
"""

from collections.abc import Iterator
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

from pydantic import BaseModel, ValidationError

from stemstudio_worker.jobs.detail_store import StoredDetailJob
from stemstudio_worker.jobs.lyrics_store import StoredLyricsJob
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import JobState


@dataclass(slots=True)
class CacheUsers:
    """Users of the cache entries, keyed by ``audioSha256``.

    Attributes:
        job_ids: Completed or active standard jobs per entry (evicted ones excluded), oldest first.
        in_use: Entries a queued or running job uses.
        from_library: Entries a rehydration created.
        last_used: Latest update time of a job that used the entry.
        jobs: Every standard job id with whether it is terminal.
    """

    job_ids: dict[str, list[str]] = field(default_factory=dict)
    in_use: set[str] = field(default_factory=set)
    from_library: set[str] = field(default_factory=set)
    last_used: dict[str, datetime] = field(default_factory=dict)
    jobs: dict[str, bool] = field(default_factory=dict)

    def touch(self, sha: str, when: datetime, *, active: bool) -> None:
        """Record a use of ``sha`` at ``when`` (``active``: by a non-terminal job)."""
        if active:
            self.in_use.add(sha)
        previous = self.last_used.get(sha)
        if previous is None or when > previous:
            self.last_used[sha] = when

    def add_standard(self, record: StoredJob) -> None:
        """Attribute a standard job to its entry (failed and evicted jobs are not attributed)."""
        job, sha = record.job, record.audio_sha256
        self.jobs[job.job_id] = job.status.is_terminal
        if sha is None or record.cache_evicted or job.status is JobState.FAILED:
            return
        self.job_ids.setdefault(sha, []).append(job.job_id)
        self.touch(sha, job.updated_at, active=not job.status.is_terminal)
        if record.from_library:
            self.from_library.add(sha)


def read_cache_users(paths: DataPaths) -> CacheUsers:
    """Read the standard, detail and lyrics job files below ``paths`` (unreadable ones skipped)."""
    users = CacheUsers()
    standard = sorted(_records(paths.jobs, StoredJob), key=lambda r: r.job.created_at)
    for record in standard:
        users.add_standard(record)
    for detail in _records(paths.detail_jobs, StoredDetailJob):
        users.touch(detail.job.audio_sha256, detail.job.updated_at,
                    active=not detail.job.status.is_terminal)  # fmt: skip
    _add_alignments(users, paths, {r.job.job_id: r.audio_sha256 for r in standard})
    return users


def _add_alignments(users: CacheUsers, paths: DataPaths, shas: dict[str, str | None]) -> None:
    """Active lyrics alignments put the entry of their standard job in use."""
    for lyrics in _records(paths.lyrics_jobs, StoredLyricsJob):
        sha = _alignment_entry(lyrics, shas)
        if sha is not None:
            users.touch(sha, lyrics.job.updated_at, active=True)


def _alignment_entry(lyrics: StoredLyricsJob, shas: dict[str, str | None]) -> str | None:
    """The entry an active alignment reads, else None (lookups, terminal jobs, unknown jobs)."""
    if lyrics.job.status.is_terminal or lyrics.request.mode != "align":
        return None
    return shas.get(lyrics.request.standard_job_id or "")


def _records[M: BaseModel](directory: Path, model: type[M]) -> Iterator[M]:
    """The valid ``model`` records of ``directory/*.json``."""
    for path in sorted(directory.glob("*.json")):
        try:
            yield model.model_validate_json(path.read_text(encoding="utf-8"))
        except (OSError, ValidationError):
            continue
