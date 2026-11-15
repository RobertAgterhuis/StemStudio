"""Tests of the transcription job store (008 T028, data-model 2.9; rules as 007 render jobs).

``transcription-jobs/<id>.json`` holds job and request, ``transcription-jobs/<id>/`` the three
result files; a restart fails unfinished jobs ("interrupted by restart") and drops their files;
a delete removes everything (idempotent); terminal jobs are swept after 24 hours.
"""

import hashlib
import json
from datetime import timedelta
from pathlib import Path

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.transcription_store import (
    StoredTranscriptionJob,
    TranscriptionJobStore,
)
from stemstudio_worker.schemas import (
    RESTART_INTERRUPTED_TEXT,
    RenderJobStatus,
    ScoreFileRole,
    TranscriptionJob,
    TranscriptionJobRequest,
)

from .conftest import T0
from .fakes import FakeClock

S = RenderJobStatus
BASS = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/bass.flac"
REQUEST = TranscriptionJobRequest.model_validate({
    "profile": "bass", "input": {"path": BASS},
    "grid": {"kind": "manual", "bpm": 120, "beatsPerBar": 4, "firstDownbeatSeconds": 0.5},
    "titles": {"title": "Bass line"},
})  # fmt: skip
CONTENTS: dict[ScoreFileRole, bytes] = {
    "pdf": b"%PDF-1.7", "musicxml": b"<score-partwise/>", "midi": b"MThd",
}  # fmt: skip


def _seeded(root: Path, statuses: dict[str, S], age: timedelta = timedelta()) -> DataPaths:
    """Write job records (with result files) as an earlier worker run would have left them."""
    paths = DataPaths(root)
    store = TranscriptionJobStore(paths)
    for job_id, status in statuses.items():
        job = TranscriptionJob(transcription_job_id=job_id, status=status, stage="transcribing",
                               progress=0.4, created_at=T0 - age, updated_at=T0 - age)  # fmt: skip
        store.save(StoredTranscriptionJob(job=job, request=REQUEST))
        store.save_files(job_id, CONTENTS)
    return paths


def _loaded(paths: DataPaths) -> TranscriptionJobStore:
    store = TranscriptionJobStore(paths)
    store.load_all()
    return store


def test_a_job_and_its_three_files_are_persisted(tmp_path: Path) -> None:
    paths = DataPaths(tmp_path)
    store = TranscriptionJobStore(paths)
    job_id = "1" * 32

    files = store.save_files(job_id, CONTENTS)

    assert [(f.role, f.file_name, f.content_type, f.size_bytes) for f in files] == [
        ("pdf", "score.pdf", "application/pdf", 8),
        ("musicxml", "score.musicxml", "application/vnd.recordare.musicxml+xml", 17),
        ("midi", "score.mid", "audio/midi", 4),
    ]
    assert files[0].sha256 == hashlib.sha256(CONTENTS["pdf"]).hexdigest()
    assert store.file(job_id, "midi") == paths.transcription_jobs / job_id / "score.mid"
    assert store.file(job_id, "midi").read_bytes() == b"MThd"  # type: ignore[union-attr]
    assert store.file("2" * 32, "pdf") is None


def test_records_are_json_files_named_after_the_job(tmp_path: Path) -> None:
    paths = _seeded(tmp_path, {"1" * 32: S.COMPLETED})

    stored = json.loads((paths.transcription_jobs / f"{'1' * 32}.json").read_text())

    assert stored["job"]["transcriptionJobId"] == "1" * 32
    assert stored["request"]["profile"] == "bass"
    assert _loaded(paths).get("1" * 32) is not None


def test_a_restart_fails_unfinished_jobs_and_drops_their_files(tmp_path: Path) -> None:
    paths = _seeded(tmp_path, {"1" * 32: S.QUEUED, "2" * 32: S.RUNNING, "3" * 32: S.COMPLETED})
    store = _loaded(paths)

    interrupted = store.reconcile(FakeClock())

    assert sorted(interrupted) == ["1" * 32, "2" * 32]
    for job_id in ("1" * 32, "2" * 32):
        job = store.get(job_id).job  # type: ignore[union-attr]
        assert (job.status, job.error, job.stage) == (S.FAILED, RESTART_INTERRUPTED_TEXT, None)
        assert not store.output_dir(job_id).exists()
    assert store.file("3" * 32, "pdf") is not None
    assert _loaded(paths).get("1" * 32).job.status is S.FAILED  # type: ignore[union-attr]


def test_delete_removes_record_and_files_and_is_idempotent(tmp_path: Path) -> None:
    paths = _seeded(tmp_path, {"1" * 32: S.COMPLETED})
    store = _loaded(paths)

    store.remove("1" * 32)
    store.remove("1" * 32)
    store.remove("../escape")

    assert store.get("1" * 32) is None
    assert list(paths.transcription_jobs.iterdir()) == []


def test_terminal_jobs_older_than_24_hours_are_swept(tmp_path: Path) -> None:
    paths = _seeded(tmp_path, {"1" * 32: S.COMPLETED, "2" * 32: S.FAILED}, timedelta(hours=25))
    _seeded(tmp_path, {"3" * 32: S.CANCELLED, "4" * 32: S.RUNNING}, timedelta(hours=23))
    _seeded(tmp_path, {"5" * 32: S.RUNNING}, timedelta(hours=30))
    store = _loaded(paths)

    swept = store.sweep(T0)

    assert sorted(swept) == ["1" * 32, "2" * 32]
    assert sorted(r.job.transcription_job_id for r in store.all()) == ["3" * 32, "4" * 32, "5" * 32]
