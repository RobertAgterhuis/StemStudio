"""Tests of the practice job services without HTTP (014 T053/T077/T087 service parts)."""

from pathlib import Path

import pytest

from stemstudio_worker.config import RenderSettings, WorkerSettings
from stemstudio_worker.jobs import practice_service
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.practice_lane import PracticeLane, PracticeParts
from stemstudio_worker.jobs.practice_service import (
    NO_ANALYZER_TEXT,
    PracticeContext,
    PracticeServices,
    page_file,
    section_inputs,
)
from stemstudio_worker.jobs.runtime import utc_now
from stemstudio_worker.ports import InputPathError
from stemstudio_worker.render import create_library_reader
from stemstudio_worker.schemas import (
    DOCUMENT_FAILED_TEXT,
    SCORE_VIEW_FAILED_TEXT,
    ManualTempo,
    RenderJobStatus,
    ScoreViewJobRequest,
    SectionJobRequest,
    SetlistDocumentRequest,
)

from .practice_fakes import MUSICXML, section_body, write_library

S = RenderJobStatus


def _services(tmp_path: Path) -> PracticeServices:
    library = tmp_path / "library"
    write_library(library)
    settings = WorkerSettings(data_dir=tmp_path, render=RenderSettings(library_dir=library))
    reader = create_library_reader(settings)
    clock = utc_now
    lane = PracticeLane.loaded(DataPaths(tmp_path / "data"), clock)
    return PracticeServices.start(PracticeContext(lane, reader, clock), PracticeParts())


def _section() -> SectionJobRequest:
    return SectionJobRequest.model_validate(section_body())


def _view() -> ScoreViewJobRequest:
    grid = ManualTempo(bpm=120, beats_per_bar=4, first_downbeat_seconds=0)
    return ScoreViewJobRequest(musicxml=MUSICXML, grid=grid, duration_seconds=8)


def _document() -> SetlistDocumentRequest:
    return SetlistDocumentRequest.model_validate({
        "title": "T", "generatedAt": "2026-11-16T18:00:00Z",
        "options": {"includeLyrics": True, "includeSections": True, "includeNotes": True,
                    "pageBreakPerSong": True},
        "songs": [{"position": 1, "title": "S", "durationSeconds": 10}],
    })  # fmt: skip


def test_without_parts_every_kind_fails_readably(tmp_path: Path) -> None:
    services = _services(tmp_path)
    try:
        jobs = (services.sections.create(_section()), services.score_views.create(_view()),
                services.documents.create(_document()))  # fmt: skip
        assert services.lane.join(5)
        errors = [services.sections.get(jobs[0].job_id).error,
                  services.score_views.get(jobs[1].job_id).error,
                  services.documents.get(jobs[2].job_id).error]  # fmt: skip
    finally:
        services.lane.stop(5)

    assert errors == [NO_ANALYZER_TEXT, SCORE_VIEW_FAILED_TEXT, DOCUMENT_FAILED_TEXT]


def test_a_too_large_score_fails_the_job(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(practice_service, "MAX_MUSICXML_BYTES", 4)
    services = _services(tmp_path)
    try:
        job_id = services.score_views.create(_view()).job_id
        assert services.lane.join(5)
        job = services.score_views.get(job_id)
    finally:
        services.lane.stop(5)

    assert (job.status, job.error) == (S.FAILED, SCORE_VIEW_FAILED_TEXT)


def test_processing_an_unknown_or_finished_job_does_nothing(tmp_path: Path) -> None:
    services = _services(tmp_path)
    services.lane.stop(5)

    services.sections.process("0" * 32)

    assert services.sections.get("0" * 32) is None


def test_a_report_after_delete_cancels(tmp_path: Path) -> None:
    services = _services(tmp_path)
    services.lane.stop(5)
    job_id = services.sections.create(_section()).job_id
    report = services.sections._reporter(job_id)
    services.sections.delete(job_id)

    with pytest.raises(practice_service.PracticeCancelledError):
        report(0.5)


def test_section_inputs_name_every_channel() -> None:
    inputs = section_inputs(_section())

    assert inputs["other.woodwinds"].endswith("/detail/other/woodwinds.flac")
    assert len(inputs) == 7
    wrong = _section().model_copy(update={"stems": _section().stems.model_copy(
        update={"bass": _section().stems.drums})})  # fmt: skip
    with pytest.raises(InputPathError):
        section_inputs(wrong)


def test_page_files_are_numbered_with_three_digits() -> None:
    assert (page_file(1), page_file(200)) == ("page-001.svg", "page-200.svg")
