"""Tests of the instrument analysis job store (016 T033, DELTA 1, ADR-003).

Jobs persist as ``instrument-analysis-jobs/<id>.json`` with the validated request; after a restart
unfinished jobs are failed ("interrupted by restart", stage cleared) and their files dropped; a
delete removes record and files; terminal jobs are swept after 24 hours.
"""

import json
from datetime import timedelta
from pathlib import Path

from stemstudio_worker.jobs.instrument_analysis_store import (
    STORE_DIR,
    InstrumentAnalysisStore,
    StoredAnalysisJob,
)
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    ChordsJobRequest,
    DrumHitsJobRequest,
    DrumInputs,
    InstrumentAnalysisJob,
    RenderJobStatus,
    VocalPitchJobRequest,
)

from .conftest import T0

S = RenderJobStatus
TRACK = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"
CHORDS = ChordsJobRequest(kind="chords", inputs=[f"{TRACK}/stems/guitar.flac"], vocabulary="simple")
DRUMS = DrumHitsJobRequest(
    kind="drum-hits", drum_inputs=DrumInputs(kick=f"{TRACK}/detail/drums/kick.flac")
)
PITCH = VocalPitchJobRequest(
    kind="vocal-pitch", input=f"{TRACK}/stems/vocals.flac", source="vocals"
)


def _record(
    job_id: str, status: S = S.QUEUED, request=CHORDS, age: timedelta = timedelta()
) -> StoredAnalysisJob:  # type: ignore[no-untyped-def]
    job = InstrumentAnalysisJob(
        analysis_job_id=job_id,
        kind=request.kind,
        status=status,
        stage="analysing" if status is S.RUNNING else None,
        progress=0.5 if status is S.RUNNING else 0,
        created_at=T0 - age,
        updated_at=T0 - age,
    )
    return StoredAnalysisJob(job=job, request=request)


def _store(tmp_path: Path) -> InstrumentAnalysisStore:
    return InstrumentAnalysisStore.of(DataPaths(tmp_path))


def test_jobs_persist_as_json_files_in_their_own_directory(tmp_path: Path) -> None:
    store = _store(tmp_path)
    store.save(_record("a" * 32))

    path = tmp_path / STORE_DIR / f"{'a' * 32}.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["job"]["analysisJobId"] == "a" * 32
    assert data["request"]["kind"] == "chords"
    assert STORE_DIR == "instrument-analysis-jobs"


def test_every_request_kind_survives_a_reload(tmp_path: Path) -> None:
    store = _store(tmp_path)
    for job_id, request in (("a" * 32, CHORDS), ("b" * 32, DRUMS), ("c" * 32, PITCH)):
        store.save(_record(job_id, S.COMPLETED, request))

    reloaded = _store(tmp_path)
    reloaded.load_all()

    assert reloaded.get("a" * 32).request == CHORDS  # type: ignore[union-attr]
    assert reloaded.get("b" * 32).request == DRUMS  # type: ignore[union-attr]
    assert reloaded.get("c" * 32).request == PITCH  # type: ignore[union-attr]


def test_a_restart_fails_unfinished_jobs_and_drops_their_files(tmp_path: Path) -> None:
    store = _store(tmp_path)
    store.save(_record("a" * 32, S.RUNNING))
    store.save(_record("b" * 32, S.QUEUED))
    store.save(_record("c" * 32, S.COMPLETED))
    store.output_dir("a" * 32).mkdir(parents=True)

    loaded = InstrumentAnalysisStore.loaded(DataPaths(tmp_path), lambda: T0 + timedelta(seconds=5))

    running = loaded.get("a" * 32)
    assert running is not None
    assert (running.job.status, running.job.error, running.job.stage) == (
        S.FAILED,
        RESTART_INTERRUPTED_TEXT,
        None,
    )
    assert running.job.updated_at == T0 + timedelta(seconds=5)
    assert loaded.get("b" * 32).job.status is S.FAILED  # type: ignore[union-attr]
    assert loaded.get("c" * 32).job.status is S.COMPLETED  # type: ignore[union-attr]
    assert not store.output_dir("a" * 32).exists()


def test_delete_removes_record_and_files_and_is_idempotent(tmp_path: Path) -> None:
    store = _store(tmp_path)
    store.save(_record("a" * 32))
    store.output_dir("a" * 32).mkdir(parents=True)

    store.remove("a" * 32)
    store.remove("a" * 32)
    store.remove("../../etc")

    assert store.get("a" * 32) is None
    assert not (tmp_path / STORE_DIR / f"{'a' * 32}.json").exists()
    assert not store.output_dir("a" * 32).exists()


def test_terminal_jobs_older_than_24_hours_are_swept(tmp_path: Path) -> None:
    store = _store(tmp_path)
    store.save(_record("a" * 32, S.COMPLETED, age=timedelta(hours=25)))
    store.save(_record("b" * 32, S.FAILED, age=timedelta(hours=23)))
    store.save(_record("c" * 32, S.RUNNING, age=timedelta(hours=30)))

    swept = store.sweep(T0)

    assert swept == ["a" * 32]
    assert store.get("a" * 32) is None
    assert store.get("b" * 32) is not None and store.get("c" * 32) is not None


def test_an_unreadable_record_file_is_skipped(tmp_path: Path) -> None:
    folder = tmp_path / STORE_DIR
    folder.mkdir()
    (folder / f"{'d' * 32}.json").write_text("{not json", encoding="utf-8")

    store = InstrumentAnalysisStore.loaded(DataPaths(tmp_path), lambda: T0)

    assert store.all() == []


def test_a_record_whose_request_kind_differs_from_the_job_kind_is_skipped(tmp_path: Path) -> None:
    store = _store(tmp_path)
    record = _record("e" * 32, S.COMPLETED, CHORDS)
    data = json.loads(record.model_dump_json(by_alias=True))
    data["job"]["kind"] = "vocal-pitch"
    folder = tmp_path / STORE_DIR
    folder.mkdir()
    (folder / f"{'e' * 32}.json").write_text(json.dumps(data), encoding="utf-8")

    store.load_all()

    assert store.get("e" * 32) is None
