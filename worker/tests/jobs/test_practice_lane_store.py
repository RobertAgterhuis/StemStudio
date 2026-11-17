"""Tests of the ``practice`` lane and its three job stores (014 T030, data-model 2.8, FR-001)."""

import json
import threading
from collections.abc import Callable
from datetime import timedelta
from pathlib import Path

import pytest

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.practice_lane import PracticeLane, PracticeParts
from stemstudio_worker.jobs.practice_store import (
    PracticeStores,
    StampedScoreViewJob,
    StampedSectionJob,
    StampedSetlistDocumentJob,
    StoredScoreViewJob,
    StoredSectionJob,
    StoredSetlistDocumentJob,
    wire_job,
)
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    BeatGridTempo,
    LaneHealth,
    RenderJobStatus,
    ScoreViewJob,
    ScoreViewJobRequest,
    SectionJob,
    SectionJobRequest,
    SetlistDocumentOptions,
    SetlistDocumentRequest,
    SetlistSongContent,
    StemPaths,
)

from .conftest import T0
from .fakes import FakeClock

S = RenderJobStatus
_TRACK = "3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b"
_ASSET = "9a8b7c6d-1111-4222-8333-444455556666"
SECTION_REQUEST = SectionJobRequest(
    stems=StemPaths(**{name: f"tracks/{_TRACK}/stems/{name}.flac"
                       for name in ("vocals", "drums", "bass", "other")}),
    grid=None, duration_seconds=60,
)  # fmt: skip
SCORE_REQUEST = ScoreViewJobRequest(
    musicxml=f"assets/{_ASSET}/score.musicxml",
    grid=BeatGridTempo(bpm=120, beats_per_bar=4, beats=[0.5, 1.0, 1.5, 2.0], downbeats=[0.5]),
    duration_seconds=10,
)  # fmt: skip
DOCUMENT_REQUEST = SetlistDocumentRequest(
    title="Band night", generated_at=T0,
    options=SetlistDocumentOptions(include_lyrics=True, include_sections=True, include_notes=True,
                                   page_break_per_song=True),
    songs=[SetlistSongContent(position=1, title="Song", duration_seconds=180)],
)  # fmt: skip


def _section(job_id: str, status: S, age: timedelta = timedelta()) -> StoredSectionJob:
    stamp = T0 - age
    job = StampedSectionJob(job_id=job_id, status=status, progress=0.4, created_at=stamp,
                            updated_at=stamp)  # fmt: skip
    return StoredSectionJob(job=job, request=SECTION_REQUEST)


def _score(job_id: str, status: S, age: timedelta = timedelta()) -> StoredScoreViewJob:
    stamp = T0 - age
    job = StampedScoreViewJob(job_id=job_id, status=status, progress=0.4, created_at=stamp,
                              updated_at=stamp)  # fmt: skip
    return StoredScoreViewJob(job=job, request=SCORE_REQUEST)


def _document(job_id: str, status: S, age: timedelta = timedelta()) -> StoredSetlistDocumentJob:
    stamp = T0 - age
    job = StampedSetlistDocumentJob(job_id=job_id, status=status, progress=0.4,
                                    created_at=stamp, updated_at=stamp)  # fmt: skip
    return StoredSetlistDocumentJob(job=job, request=DOCUMENT_REQUEST)


def _saved(tmp_path: Path, *records: object) -> PracticeStores:
    """Save ``records`` (with one output file each) and return freshly loaded stores."""
    stores = PracticeStores.of(DataPaths(tmp_path))
    for record in records:
        store = stores.owner(record)
        store.save(record)
        job_id = record.job.job_id  # type: ignore[attr-defined]
        store.output_dir(job_id).mkdir(parents=True, exist_ok=True)
        (store.output_dir(job_id) / "page-001.svg").write_text("<svg/>")
    reloaded = PracticeStores.of(DataPaths(tmp_path))
    reloaded.load_all()
    return reloaded


def test_the_three_kinds_persist_in_their_own_folders(tmp_path: Path) -> None:
    stores = _saved(tmp_path, _section("1" * 32, S.QUEUED), _score("2" * 32, S.COMPLETED),
                    _document("3" * 32, S.COMPLETED))  # fmt: skip

    section = json.loads((tmp_path / "section-jobs" / f"{'1' * 32}.json").read_text())
    assert section["job"]["jobId"] == "1" * 32
    assert section["request"]["durationSeconds"] == 60
    assert (tmp_path / "score-view-jobs" / ("2" * 32) / "page-001.svg").is_file()
    assert (tmp_path / "setlist-document-jobs" / f"{'3' * 32}.json").is_file()
    assert stores.sections.get("1" * 32) == _section("1" * 32, S.QUEUED)
    assert stores.score_views.get("2" * 32) == _score("2" * 32, S.COMPLETED)
    assert stores.documents.get("3" * 32) == _document("3" * 32, S.COMPLETED)


def test_wire_job_drops_the_internal_time_stamp() -> None:
    record = _section("4" * 32, S.RUNNING)

    job = wire_job(record.job, SectionJob)

    assert type(job) is SectionJob
    assert "updatedAt" not in job.model_dump(by_alias=True)
    assert (job.job_id, job.status, job.progress) == ("4" * 32, S.RUNNING, 0.4)
    assert type(wire_job(_score("5" * 32, S.QUEUED).job, ScoreViewJob)) is ScoreViewJob


@pytest.mark.parametrize("status", [S.QUEUED, S.RUNNING])
def test_restart_fails_unfinished_jobs_of_every_kind(tmp_path: Path, status: S) -> None:
    stores = _saved(tmp_path, _section("1" * 32, status), _score("2" * 32, status),
                    _document("3" * 32, status))  # fmt: skip

    assert stores.reconcile(FakeClock()) == ["1" * 32, "2" * 32, "3" * 32]

    for store, job_id in ((stores.sections, "1"), (stores.score_views, "2"),
                          (stores.documents, "3")):  # fmt: skip
        record = store.get(job_id * 32)
        assert record is not None
        assert (record.job.status, record.job.error) == (S.FAILED, RESTART_INTERRUPTED_TEXT)
        assert record.job.updated_at > T0
        assert not store.output_dir(job_id * 32).exists()


def test_restart_leaves_finished_jobs_alone(tmp_path: Path) -> None:
    stores = _saved(tmp_path, _score("2" * 32, S.COMPLETED), _document("3" * 32, S.FAILED))

    assert stores.reconcile(FakeClock()) == []
    assert stores.score_views.output_dir("2" * 32).is_dir()


def test_delete_removes_the_record_and_the_files(tmp_path: Path) -> None:
    stores = _saved(tmp_path, _score("2" * 32, S.COMPLETED))

    stores.score_views.remove("2" * 32)
    stores.score_views.remove("2" * 32)

    assert stores.score_views.get("2" * 32) is None
    assert not (tmp_path / "score-view-jobs" / f"{'2' * 32}.json").exists()
    assert not stores.score_views.output_dir("2" * 32).exists()


def test_sweep_removes_terminal_jobs_older_than_24_hours_in_every_store(tmp_path: Path) -> None:
    old, fresh = timedelta(hours=25), timedelta(hours=23)
    stores = _saved(
        tmp_path, _section("1" * 32, S.COMPLETED, old), _score("2" * 32, S.FAILED, old),
        _document("3" * 32, S.CANCELLED, old), _document("4" * 32, S.COMPLETED, fresh),
        _section("5" * 32, S.RUNNING, timedelta(days=3)),
    )  # fmt: skip
    lane = PracticeLane(stores, lambda: T0)

    assert sorted(lane.sweep()) == ["1" * 32, "2" * 32, "3" * 32]
    assert stores.documents.get("4" * 32) is not None
    assert stores.sections.get("5" * 32) is not None


class _Kinds:
    """Process functions of three job kinds that record their order and any overlap."""

    def __init__(self) -> None:
        self.gate, self.started = threading.Event(), threading.Event()
        self.order: list[str] = []
        self.overlap: list[str] = []
        self._running = threading.Lock()

    def of(self, name: str) -> Callable[[str], None]:
        def process(job_id: str) -> None:
            if not self._running.acquire(blocking=False):
                self.overlap.append(job_id)
                return
            try:
                self.started.set()
                self.gate.wait(5)
                self.order.append(f"{name}:{job_id}")
            finally:
                self._running.release()

        return process


def test_the_lane_runs_one_job_at_a_time_fifo_across_the_kinds(tmp_path: Path) -> None:
    lane, kinds = PracticeLane(PracticeStores.of(DataPaths(tmp_path)), FakeClock()), _Kinds()
    lane.start()
    try:
        for name, job_id in (("section", "a"), ("score-view", "b"), ("setlist", "c"),
                             ("section", "d")):  # fmt: skip
            lane.submit(kinds.of(name), job_id)
        assert kinds.started.wait(5)
        health = lane.health()
        kinds.gate.set()
        assert lane.join(5)
    finally:
        lane.stop(5)

    assert kinds.order == ["section:a", "score-view:b", "setlist:c", "section:d"]
    assert kinds.overlap == []
    assert (health.running, health.queued) == (True, 3)
    assert lane.health() == LaneHealth(queued=0, running=False)


def test_an_idle_lane_reports_nothing_queued(tmp_path: Path) -> None:
    lane = PracticeLane(PracticeStores.of(DataPaths(tmp_path)), FakeClock())

    assert (lane.health().queued, lane.health().running) == (0, False)


def test_parts_without_an_analyser_report_the_default_structure() -> None:
    health = PracticeParts().structure_health()

    assert (health.analyzer, health.version) == ("stems-novelty", "1.0.0")
