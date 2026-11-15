"""Tests of the render service defaults for a runtime built without ``render`` (007 T033)."""

from pathlib import Path

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.render_service import NoLibrary, NoRenderer, RenderService
from stemstudio_worker.jobs.render_store import RenderJobStore
from stemstudio_worker.ports import RenderError
from stemstudio_worker.schemas import (
    LIBRARY_NOT_CHECKED,
    LIBRARY_UNAVAILABLE_TEXT,
    RenderInput,
    RenderJobRequest,
    RenderJobStatus,
    RenderOutput,
)

from .fakes import FakeClock

REQUEST = RenderJobRequest(
    inputs=[RenderInput(path="tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/bass.flac",
                        gain=0.5, pan=0)],
    master_gain=1, output=RenderOutput(format="wav"),
)  # fmt: skip


def test_no_library_is_never_readable() -> None:
    library = NoLibrary()

    assert library.status() == LIBRARY_NOT_CHECKED
    with pytest.raises(RenderError, match=LIBRARY_UNAVAILABLE_TEXT):
        library.resolve("tracks/x/stems/bass.flac")


def test_jobs_of_a_worker_without_renderer_fail_with_the_library_text(tmp_path: Path) -> None:
    service = RenderService.start(DataPaths(tmp_path), NoRenderer(), FakeClock())
    try:
        job_id = service.create(REQUEST).render_job_id
        assert service.join(5.0)
        job = service.get(job_id)
    finally:
        service.stop(5.0)

    assert job is not None
    assert (job.status, job.error) == (RenderJobStatus.FAILED, LIBRARY_UNAVAILABLE_TEXT)


def test_processing_an_unknown_or_finished_job_does_nothing(tmp_path: Path) -> None:
    service = RenderService(RenderJobStore(DataPaths(tmp_path)), NoRenderer(), FakeClock())

    service.process("0" * 32)

    assert service.get("0" * 32) is None
