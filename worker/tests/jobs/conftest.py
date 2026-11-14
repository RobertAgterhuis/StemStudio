"""Shared builders for the jobs tests (exposed as fixtures; tests have no package)."""

from collections.abc import Callable
from datetime import UTC, datetime

import pytest

from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.schemas import Job, JobState, SourceMetadata, SourceType

T0 = datetime(2026, 11, 14, 12, 0, tzinfo=UTC)

RecordFactory = Callable[..., StoredJob]


def build_job(
    job_id: str = "a" * 32,
    source_key: str = "youtube:dQw4w9WgXcQ",
    status: JobState = JobState.QUEUED,
) -> Job:
    """A valid job in ``status`` for ``source_key``."""
    return Job(
        job_id=job_id,
        source_key=source_key,
        source_type=SourceType(source_key.split(":", 1)[0]),
        status=status,
        stage="Queued",
        progress=0,
        metadata=SourceMetadata(title="Song", artist="Artist", source_url=None),
        created_at=T0,
        updated_at=T0,
    )


@pytest.fixture
def make_record() -> RecordFactory:
    """Factory of stored jobs; keyword arguments go to :func:`build_job`."""

    def factory(**kwargs: object) -> StoredJob:
        return StoredJob(job=build_job(**kwargs))  # type: ignore[arg-type]

    return factory
